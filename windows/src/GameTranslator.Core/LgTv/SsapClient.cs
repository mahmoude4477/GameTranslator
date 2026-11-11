// SPDX-License-Identifier: AGPL-3.0-only

using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace GameTranslator.Core.LgTv;

/// <summary>كل ما يستطيع تشغيل تطبيق على التلفزيون ومعرفة التطبيق الظاهر في المقدمة.</summary>
public interface ITvControl : IDisposable
{
    Task LaunchAsync(string appId, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default);
    Task<string> ForegroundAppIdAsync(CancellationToken cancellationToken = default);
}

public sealed class SsapException(string reason) : Exception($"SSAP: {reason}")
{
    public string Reason { get; } = reason;
}

/// <summary>
/// يخاطب تلفزيون LG عبر SSAP (WebSocket). في أول اتصال يُطلب الإذن على شاشة التلفزيون؛
/// وإذا حُفظ client-key العائد تتم الاتصالات التالية بلا سؤال.
/// </summary>
public sealed class SsapClient : ITvControl
{
    private readonly IReadOnlyList<Uri> endpoints;
    private readonly TimeSpan approvalTimeout;
    private readonly SemaphoreSlim requestLock = new(1, 1);
    private string? clientKey;
    private ClientWebSocket? socket;
    private int nextId = 1;

    /// <summary>يُجرَّب أولًا الاتصال المشفَّر (3001) ثم غير المشفَّر (3000)؛ إصدارات webOS القديمة تفتح 3000 فقط.</summary>
    public SsapClient(LgTv tv, string? clientKey)
        : this([new Uri($"wss://{tv.Host}:3001"), new Uri($"ws://{tv.Host}:3000")], clientKey) { }

    public SsapClient(IReadOnlyList<Uri> endpoints, string? clientKey, TimeSpan? approvalTimeout = null)
    {
        this.endpoints = endpoints;
        this.clientKey = clientKey;
        this.approvalTimeout = approvalTimeout ?? TimeSpan.FromSeconds(60);
    }

    /// <summary>يقترن ويعيد client-key. ينتظر حتى يوافق المستخدم على التلفزيون (60 ثانية على الأكثر).</summary>
    public async Task<string> ConnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (var url in endpoints)
        {
            var candidate = new ClientWebSocket();
            // شهادة التلفزيون على المنفذ 3001 موقَّعة ذاتيًا؛ تُقبل لأن هذا الاتصال يذهب إلى عنوان التلفزيون فقط.
            candidate.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            try
            {
                using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    connectTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                    await candidate.ConnectAsync(url, connectTimeout.Token);
                }
                await SendText(candidate, SsapMessages.Register(clientKey), cancellationToken);
                var key = await WaitForRegistration(candidate, cancellationToken);
                socket = candidate;
                clientKey = key;
                return key;
            }
            catch (SsapException)
            {
                candidate.Abort();
                candidate.Dispose();
                throw;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                candidate.Abort();
                candidate.Dispose();
            }
        }
        throw new SsapException("cannot connect");
    }

    public async Task LaunchAsync(string appId, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken = default)
    {
        var parameterObject = new JsonObject();
        foreach (var (key, value) in parameters) parameterObject[key] = value;
        var response = await Request("ssap://system.launcher/launch", new JsonObject { ["id"] = appId, ["params"] = parameterObject }, cancellationToken);
        if (response["returnValue"]?.GetValue<bool>() == false) throw new SsapException((string?)response["errorText"] ?? "launch");
    }

    public async Task<string> ForegroundAppIdAsync(CancellationToken cancellationToken = default)
    {
        var response = await Request("ssap://com.webos.applicationManager/getForegroundAppInfo", null, cancellationToken);
        return (string?)response["appId"] ?? "";
    }

    public void Dispose()
    {
        socket?.Abort();
        socket?.Dispose();
        socket = null;
    }

    // --- الآلية الداخلية ---

    /// <summary>ينتظر حتى يُجاب عن سؤال الإذن على التلفزيون؛ ويتوقف إذا انتهت المهلة.</summary>
    private async Task<string> WaitForRegistration(ClientWebSocket candidate, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(approvalTimeout);
        try
        {
            while (true)
            {
                var message = await Receive(candidate, timeout.Token);
                switch ((string?)message["type"])
                {
                    case "registered":
                        return (string?)message["payload"]?["client-key"] ?? throw new SsapException("bad response");
                    case "error":
                        throw new SsapException((string?)message["error"] ?? "register");
                    default:
                        continue; // "response": عرض التلفزيون السؤال والموافقة في الانتظار
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SsapException("timeout");
        }
    }

    private async Task<JsonNode> Request(string uri, JsonNode? payload, CancellationToken cancellationToken)
    {
        var socket = this.socket ?? throw new SsapException("cannot connect");
        await requestLock.WaitAsync(cancellationToken);
        try
        {
            var id = $"req_{nextId++}";
            await SendText(socket, SsapMessages.Request(id, uri, payload), cancellationToken);
            while (true)
            {
                var message = await Receive(socket, cancellationToken);
                if ((string?)message["id"] != id) continue;
                if ((string?)message["type"] == "error") throw new SsapException((string?)message["error"] ?? uri);
                return message["payload"] ?? new JsonObject();
            }
        }
        finally
        {
            requestLock.Release();
        }
    }

    /// <summary>تلفزيون LG يرفض الإطار الثنائي (binary)؛ فتُرسل الرسائل في إطار نصي.</summary>
    private static Task SendText(ClientWebSocket socket, string text, CancellationToken cancellationToken) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, cancellationToken);

    private static async Task<JsonNode> Receive(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16384];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) throw new SsapException("closed");
            message.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        return JsonNode.Parse(message.ToArray()) ?? throw new SsapException("bad response");
    }
}
