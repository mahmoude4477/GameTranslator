// SPDX-License-Identifier: AGPL-3.0-only

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameTranslator.Core.LgTv;

/// <summary>رسائل WebSocket الخاص بالتحكم عن بُعد في Samsung. لا توثّقه Samsung؛ والحقول مأخوذة من مصادر المجتمع.</summary>
public static class SamsungMessages
{
    public static Uri Url(string host, string? token)
    {
        var name = Convert.ToBase64String(Encoding.UTF8.GetBytes("GameTranslator"));
        var text = $"wss://{host}:8002/api/v2/channels/samsung.remote.control?name={name}";
        if (!string.IsNullOrEmpty(token)) text += $"&token={token}";
        return new Uri(text);
    }

    /// <summary>يستقبل تطبيق التلفزيون المعاملات داخل <c>PAYLOAD</c> (انظر tv/tizen/platform.js).</summary>
    public static string Launch(string appId, IReadOnlyDictionary<string, string> parameters) => new JsonObject
    {
        ["method"] = "ms.channel.emit",
        ["params"] = new JsonObject
        {
            ["event"] = "ed.apps.launch",
            ["to"] = "host",
            ["data"] = new JsonObject
            {
                ["appId"] = appId,
                ["action_type"] = "DEEP_LINK",
                ["metaTag"] = JsonSerializer.Serialize(parameters),
            },
        },
    }.ToJsonString();

    /// <summary>نتيجة الاتصال: الرمز (token) إن وُوفق عليه (فارغ إن لم يعطه التلفزيون)، وnull إن كانت رسالة أخرى. وخطأ إن رُفض.</summary>
    public static string? ConnectResult(string text)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(text); } catch (JsonException) { return null; }
        return node?["event"]?.GetValue<string>() switch
        {
            "ms.channel.connect" => node["data"]?["token"]?.GetValue<string>() ?? "",
            "ms.channel.unauthorized" => throw new SsapException("rejected"),
            _ => null,
        };
    }

    /// <summary>اسم التلفزيون في رد <c>http://TV:8001/api/v2/</c>.</summary>
    public static string? DeviceName(string json)
    {
        try { return JsonNode.Parse(json)?["device"]?["name"]?.GetValue<string>()?.Replace("&quot;", "\""); }
        catch (JsonException) { return null; }
    }
}

/// <summary>يخاطب تلفزيون Samsung عبر WebSocket الخاص بالتحكم عن بُعد. في أول اتصال يطلب التلفزيون الإذن.</summary>
public sealed class SamsungClient(Uri url, TimeSpan? approvalTimeout = null) : ITvControl
{
    public const string AppId = "GameTranslatorT.GameTranslator";

    private ClientWebSocket? socket;

    public SamsungClient(LgTv tv, string? token) : this(SamsungMessages.Url(tv.Host, token)) { }

    /// <summary>يتصل ويعيد الرمز (token). ينتظر حتى يوافق المستخدم على التلفزيون.</summary>
    public async Task<string> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var candidate = new ClientWebSocket();
        // شهادة التلفزيون على المنفذ 8002 موقَّعة ذاتيًا؛ وهذا الاتصال يذهب إلى عنوان التلفزيون فقط.
        candidate.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(approvalTimeout ?? TimeSpan.FromSeconds(60));
        try
        {
            await candidate.ConnectAsync(url, timeout.Token);
            var buffer = new byte[8192];
            while (true)
            {
                var text = new StringBuilder();
                WebSocketReceiveResult result;
                do
                {
                    result = await candidate.ReceiveAsync(buffer, timeout.Token);
                    if (result.MessageType == WebSocketMessageType.Close) throw new SsapException("cannot connect");
                    text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);
                if (SamsungMessages.ConnectResult(text.ToString()) is { } token)
                {
                    socket = candidate;
                    return token;
                }
            }
        }
        catch
        {
            candidate.Dispose();
            throw;
        }
    }

    public async Task LaunchAsync(string appId, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        if (socket is null) throw new SsapException("cannot connect");
        var data = Encoding.UTF8.GetBytes(SamsungMessages.Launch(appId, parameters));
        await socket.SendAsync(data, WebSocketMessageType.Text, true, cancellationToken);
    }

    /// <summary>Samsung لا يفصح عن التطبيق الظاهر في المقدمة؛ يُفحص هل الاتصال قائم ويُعاد نص فارغ.</summary>
    public Task<string> ForegroundAppIdAsync(CancellationToken cancellationToken = default) =>
        socket is { State: WebSocketState.Open } ? Task.FromResult("") : throw new SsapException("cannot connect");

    public void Dispose()
    {
        socket?.Abort();
        socket?.Dispose();
        socket = null;
    }
}

/// <summary>يعثر على أجهزة تلفزيون Samsung في الشبكة المحلية عبر SSDP.</summary>
public static class SamsungDiscovery
{
    internal const string SearchTarget = "urn:samsung.com:device:RemoteControlReceiver:1";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    public static async Task<IReadOnlyList<LgTv>> SearchAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var hosts = await LgDiscovery.CollectResponses(SearchTarget, timeout ?? TimeSpan.FromSeconds(3), cancellationToken);
        var tvs = new List<LgTv>();
        foreach (var host in hosts.Distinct().Order(StringComparer.Ordinal))
        {
            string? name = null;
            try { name = SamsungMessages.DeviceName(await Http.GetStringAsync($"http://{host}:8001/api/v2/", cancellationToken)); }
            catch (Exception) { /* إذا تعذّرت قراءة الاسم يُستخدم اسم عام */ }
            tvs.Add(new LgTv(name ?? "Samsung TV", host, TvBrand.Samsung));
        }
        if (tvs.Count > 0) return tvs;

        // إذا لم يصل رد SSDP (بعض أجهزة المودم والتلفزيون لا تمرّر البث المتعدد) تُجرَّب عناوين الشبكة واحدًا واحدًا:
        // تلفزيون Samsung يقدّم على المنفذ 8001 واجهة تعطي اسمه.
        foreach (var host in await LocalNetwork.HostsWithOpenPortAsync(8001, TimeSpan.FromMilliseconds(700), cancellationToken))
        {
            try
            {
                if (SamsungMessages.DeviceName(await Http.GetStringAsync($"http://{host}:8001/api/v2/", cancellationToken)) is { } name)
                    tvs.Add(new LgTv(name, host, TvBrand.Samsung));
            }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException) { }
        }
        return tvs;
    }
}
