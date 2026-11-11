// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameTranslator.Core;

/// <summary>موضع الترجمة على التلفزيون بالنسبة إلى الترجمة النصية الإنجليزية في اللعبة.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SubtitlePlacement>))]
public enum SubtitlePlacement
{
    /// <summary>فوق الترجمة النصية الإنجليزية مع تغطيتها.</summary>
    Cover,
    /// <summary>أعلى الترجمة النصية الإنجليزية مباشرة.</summary>
    Above,
    /// <summary>أسفل الترجمة النصية الإنجليزية مباشرة.</summary>
    Below,
    /// <summary>على ارتفاع ثابت في الشاشة (<see cref="SubtitleStyle.BottomPercent"/>).</summary>
    Fixed,
}

/// <summary>مظهر الترجمة النصية على التلفزيون. الحقول غير الموجودة في إعدادات حفظها إصدار قديم تأخذ القيمة الافتراضية.</summary>
/// <param name="FontSize">36 (صغير)، 46 (متوسط)، 58 (كبير)، 72 (كبير جدًا)</param>
/// <param name="BottomPercent">في الموضع الثابت: بُعد الترجمة النصية عن أسفل الشاشة، بالنسبة المئوية.</param>
/// <param name="Background">دُكنة الخلفية السوداء خلف النص، بالنسبة المئوية (0: بلا خلفية).</param>
public sealed record SubtitleStyle(
    int FontSize = 46,
    int BottomPercent = 7,
    SubtitlePlacement Placement = SubtitlePlacement.Cover,
    int Background = 72,
    bool Bold = false,
    string Color = "#FFFFFF");

/// <summary>الطرف الذي يوصل الترجمات النصية إلى التلفزيون (فعليًا <see cref="SubtitleServer"/>).</summary>
public interface ISubtitleSender
{
    /// <param name="box">موضع الترجمة النصية الإنجليزية للعبة على الشاشة (نسبةً إلى الحواف)؛ يضع التلفزيون الترجمة وفقًا له.</param>
    void SendSubtitle(string text, int durationMs = 15000, TextBox? box = null);
    void Clear();
}

/// <summary>
/// خادم Server-Sent Events المحلي الذي يتصل به تطبيق التلفزيون.
/// يبث في تدفق <c>GET /events</c> الأحداث <c>subtitle</c> و<c>clear</c> و<c>style</c>.
/// </summary>
public sealed class SubtitleServer(int port = 8787) : ISubtitleSender, IDisposable
{
    private sealed class Client(TcpClient tcp, bool viewer = false)
    {
        public TcpClient Tcp { get; } = tcp;
        /// <summary>مشاهد ليس تلفزيونًا مثل طبقة OBS: يستقبل الأحداث لكنه لا يدخل في عدد «التلفزيون متصل».</summary>
        public bool Viewer { get; } = viewer;
        public Stream Stream { get; } = tcp.GetStream();
        /// <summary>حتى لا تتداخل كتابتان على الاتصال نفسه.</summary>
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        // تُرسل الحروف العربية كما هي لا على شكل تهريبات \uXXXX.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly object gate = new();
    private readonly List<Client> clients = [];
    private TcpListener? listener;
    private Timer? pingTimer;
    private SubtitleStyle? lastStyle;

    /// <summary>يُستدعى عند تغيّر عدد أجهزة التلفزيون المتصلة (في مجمّع الخيوط).</summary>
    public event Action<int>? ClientsChanged;

    public int ConnectedClients
    {
        get { lock (gate) return TvCount(); }
    }

    private int TvCount() => clients.Count(c => !c.Viewer);

    /// <summary>طبقة الترجمة النصية التي تُضاف في OBS كـ«مصدر متصفح» (<c>GET /obs</c>).</summary>
    public static byte[] ObsOverlay { get; } = LoadObsOverlay();

    private static byte[] LoadObsOverlay()
    {
        using var stream = typeof(SubtitleServer).Assembly.GetManifestResourceStream("obs-overlay.html");
        if (stream is null) return [];
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>يشغّل الخادم ويعيد المنفذ الذي يستمع عليه (إذا كان <c>port: 0</c> فالمنفذ المختار عشوائيًا).</summary>
    public Task<int> StartAsync()
    {
        var listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        this.listener = listener;
        _ = AcceptLoop(listener);
        pingTimer = new Timer(_ => Broadcast(": ping\n\n"), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        return Task.FromResult(((IPEndPoint)listener.LocalEndpoint).Port);
    }

    public void Stop()
    {
        pingTimer?.Dispose();
        pingTimer = null;
        listener?.Stop();
        listener = null;
        List<Client> closing;
        lock (gate)
        {
            closing = [.. clients];
            clients.Clear();
        }
        foreach (var client in closing) client.Tcp.Dispose();
    }

    public void Dispose() => Stop();

    public void SendSubtitle(string text, int durationMs = 15000, TextBox? box = null) => SendSubtitle(text, durationMs, box, false);

    /// <param name="paragraphs">إذا كان true يُعرض النص سطرًا سطرًا محاذيًا لبداية السطر (نصوص بعناوين وبنود مثل لوحة المهام).</param>
    public void SendSubtitle(string text, int durationMs, TextBox? box, bool paragraphs)
    {
        var json = new SortedDictionary<string, object>
        {
            ["at"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["duration"] = durationMs,
            ["text"] = Directional(text),
        };
        if (paragraphs) json["paragraphs"] = true;
        if (box is { } b) json["box"] = BoxJson(b);
        Broadcast(Frame("subtitle", json));
    }

    /// <summary>
    /// يحيط السطر المكتوب من اليمين إلى اليسار (العربية، العبرية) بعلامات الاتجاه: فتبقى علامات الترقيم واسم المتحدث
    /// في الطرف الصحيح حتى في تطبيقات التلفزيون القديمة التي لا تضبط الاتجاه بنفسها. يُحاط كل سطر على حدة.
    /// </summary>
    public static string Directional(string text)
    {
        if (!text.Any(c => c is (>= '\u0590' and <= '\u08FF') or (>= '\uFB1D' and <= '\uFDFF') or (>= '\uFE70' and <= '\uFEFC')))
            return text;
        return string.Join('\n', text.Split('\n').Select(line => "\u202B" + line + "\u202C"));
    }

    /// <summary>يُقرَّب إلى أربع منازل (decimal: ليُكتب في JSON على شكل 0.8).</summary>
    /// القسمة تحذف الأصفار الأخيرة (0.8750 → 0.875).
    private static decimal Round(double value) =>
        Math.Round((decimal)Math.Clamp(value, 0, 1), 4) / 1.000000000000000000000000000000000m;

    public void Clear() => Broadcast(Frame("clear", new SortedDictionary<string, object>()));

    /// <summary>
    /// نتائج «ترجِم الشاشة»: يحمل الحدث <c>screen</c> كل ترجمة مع موضع النص (<c>box</c>) وارتفاع السطر المعتاد
    /// (<c>line</c>، نسبةً إلى الشاشة) ومع <c>paragraphs</c> إن كانت متعددة الأجزاء. يضعها التلفزيون مكان السابقة.
    /// </summary>
    public void SendScreen(IReadOnlyList<ScreenTranslation.Item> items, int durationMs)
    {
        var list = items.Select(item =>
        {
            var json = new SortedDictionary<string, object>
            {
                ["box"] = BoxJson(item.Box),
                ["line"] = Round(item.LineHeight),
                ["text"] = Directional(item.Translation),
            };
            if (item.Paragraphs) json["paragraphs"] = true;
            return json;
        }).ToList();
        Broadcast(Frame("screen", new SortedDictionary<string, object> { ["duration"] = durationMs, ["items"] = list }));
    }

    /// <summary>يزيل ترجمات «ترجِم الشاشة» من التلفزيون (لا يمسّ الترجمة النصية).</summary>
    public void ClearScreen() => Broadcast(Frame("screen-clear", new SortedDictionary<string, object>()));

    private static SortedDictionary<string, object> BoxJson(TextBox b) => new()
    {
        ["bottom"] = Round(b.Bottom), ["left"] = Round(b.Left), ["right"] = Round(b.Right), ["top"] = Round(b.Top),
    };

    public void SendStyle(SubtitleStyle style)
    {
        lock (gate) lastStyle = style;
        Broadcast(StyleFrame(style));
    }

    // --- الاتصالات ---

    private async Task AcceptLoop(TcpListener listener)
    {
        while (true)
        {
            TcpClient tcp;
            try { tcp = await listener.AcceptTcpClientAsync(); }
            catch { return; }
            _ = Serve(tcp);
        }
    }

    private async Task Serve(TcpClient tcp)
    {
        try
        {
            EnableKeepAlive(tcp.Client);
            var stream = tcp.GetStream();
            var requestLine = await ReadRequestLine(stream);
            var parts = requestLine?.Split(' ') ?? [];
            var method = parts.Length > 0 ? parts[0] : "";
            var target = parts.Length > 1 ? parts[1] : "";
            var path = target.Split('?')[0];

            if (method == "OPTIONS") await RespondAndClose(tcp, "204 No Content");
            else if (method == "GET" && path == "/events") await OpenStream(tcp, target.Contains("viewer=obs", StringComparison.Ordinal));
            else if (method == "GET" && path == "/obs") await RespondAndClose(tcp, ObsOverlay, "text/html; charset=utf-8");
            // يتعرّف تطبيق التلفزيون على الكمبيوتر في الشبكة بهذا الرد.
            else if (method == "GET" && path == "/hello") await RespondAndClose(tcp, "200 OK", "{\"app\":\"gametranslator\"}");
            else await RespondAndClose(tcp, "404 Not Found");
        }
        catch
        {
            tcp.Dispose();
        }
    }

    /// <summary>يقرأ حتى نهاية الترويسات (سطر فارغ) ويعيد السطر الأول. يُغلق الطلب الذي يتجاوز 64 KB.</summary>
    private static async Task<string?> ReadRequestLine(Stream stream)
    {
        var buffer = new byte[8192];
        using var head = new MemoryStream();
        while (head.Length < 65536)
        {
            var count = await stream.ReadAsync(buffer);
            if (count == 0) return null;
            head.Write(buffer, 0, count);
            var text = Encoding.ASCII.GetString(head.GetBuffer(), 0, (int)head.Length);
            var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (end >= 0) return text[..text.IndexOf("\r\n", StringComparison.Ordinal)];
        }
        return null;
    }

    private static async Task RespondAndClose(TcpClient tcp, byte[] data, string type)
    {
        var head = $"HTTP/1.1 200 OK\r\nAccess-Control-Allow-Origin: *\r\nContent-Type: {type}\r\nCache-Control: no-cache\r\n" +
                   $"Content-Length: {data.Length}\r\nConnection: close\r\n\r\n";
        var stream = tcp.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
        await stream.WriteAsync(data);
        tcp.Dispose();
    }

    private static async Task RespondAndClose(TcpClient tcp, string status, string body = "")
    {
        var head = $"HTTP/1.1 {status}\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
        await tcp.GetStream().WriteAsync(Encoding.ASCII.GetBytes(head));
        tcp.Dispose();
    }

    private async Task OpenStream(TcpClient tcp, bool viewer = false)
    {
        var head = new StringBuilder()
            .Append("HTTP/1.1 200 OK\r\n")
            .Append("Content-Type: text/event-stream; charset=utf-8\r\n")
            .Append("Cache-Control: no-cache\r\n")
            .Append("Connection: keep-alive\r\n")
            .Append("Access-Control-Allow-Origin: *\r\n\r\n")
            .Append("retry: 2000\n\n");
        var client = new Client(tcp, viewer);
        int count;
        lock (gate)
        {
            if (lastStyle is { } style) head.Append(StyleFrame(style));
            clients.Add(client);
            count = TvCount();
        }
        await Write(client, head.ToString());
        ClientsChanged?.Invoke(count);
        await WatchForClose(client);
    }

    /// <summary>يزيل العميل من القائمة عندما يغلق الاتصال.</summary>
    private async Task WatchForClose(Client client)
    {
        var buffer = new byte[1024];
        try
        {
            while (await client.Stream.ReadAsync(buffer) > 0) { }
        }
        catch { }
        Remove(client);
    }

    private void Remove(Client client)
    {
        int count;
        lock (gate)
        {
            if (!clients.Remove(client)) return;
            count = TvCount();
        }
        client.Tcp.Dispose();
        ClientsChanged?.Invoke(count);
    }

    /// <summary>الاتصال الذي يفشل الإرسال إليه يُعدّ منقطعًا ويُزال من القائمة.</summary>
    private void Broadcast(string frame)
    {
        List<Client> targets;
        lock (gate) targets = [.. clients];
        foreach (var client in targets)
            _ = Write(client, frame).ContinueWith(t => Remove(client), TaskContinuationOptions.OnlyOnFaulted);
    }

    private static async Task Write(Client client, string text)
    {
        await client.WriteLock.WaitAsync();
        try
        {
            await client.Stream.WriteAsync(Encoding.UTF8.GetBytes(text));
            await client.Stream.FlushAsync();
        }
        finally
        {
            client.WriteLock.Release();
        }
    }

    /// <summary>أجهزة التلفزيون التي تختفي بصمت (فصل الكهرباء، انقطاع Wi-Fi) تُكتشف بفحص بقاء اتصال TCP.</summary>
    private static void EnableKeepAlive(Socket socket)
    {
        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 10);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
        }
        catch (SocketException) { }
    }

    private static string StyleFrame(SubtitleStyle style) =>
        Frame("style", new SortedDictionary<string, object>
        {
            ["background"] = Math.Clamp(style.Background, 0, 100),
            ["bold"] = style.Bold,
            ["bottomPercent"] = style.BottomPercent,
            ["fontSize"] = style.FontSize,
            ["placement"] = style.Placement.ToString().ToLowerInvariant(),
            ["color"] = style.Color,
        });

    private static string Frame(string name, SortedDictionary<string, object> json) =>
        $"event: {name}\ndata: {JsonSerializer.Serialize(json, JsonOptions)}\n\n";
}
