// SPDX-License-Identifier: AGPL-3.0-only

using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;

namespace GameTranslator.Core.LgTv;

/// <summary>TV'de uygulama başlatabilen ve önplandaki uygulamayı söyleyebilen her şey.</summary>
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
/// LG TV ile SSAP (WebSocket) üzerinden konuşur. İlk bağlantıda TV ekranında izin sorulur;
/// dönen client-key saklanırsa sonraki bağlantılar sorusuz olur.
/// </summary>
public sealed class SsapClient : ITvControl
{
    private readonly IReadOnlyList<Uri> endpoints;
    private readonly TimeSpan approvalTimeout;
    private readonly SemaphoreSlim requestLock = new(1, 1);
    private string? clientKey;
    private ClientWebSocket? socket;
    private int nextId = 1;

    /// <summary>Önce şifreli (3001), olmazsa düz (3000) bağlantı denenir; eski webOS'lar yalnız 3000'i açar.</summary>
    public SsapClient(LgTv tv, string? clientKey)
        : this([new Uri($"wss://{tv.Host}:3001"), new Uri($"ws://{tv.Host}:3000")], clientKey) { }

    public SsapClient(IReadOnlyList<Uri> endpoints, string? clientKey, TimeSpan? approvalTimeout = null)
    {
        this.endpoints = endpoints;
        this.clientKey = clientKey;
        this.approvalTimeout = approvalTimeout ?? TimeSpan.FromSeconds(60);
    }

    /// <summary>Eşleşir ve client-key'i döner. Kullanıcı TV'de onay verene kadar bekler (en çok 60 sn).</summary>
    public async Task<string> ConnectAsync(CancellationToken cancellationToken = default)
    {
        foreach (var url in endpoints)
        {
            var candidate = new ClientWebSocket();
            // TV'nin 3001 portundaki sertifikası kendinden imzalıdır; bu bağlantı yalnız TV'nin adresine gittiği için kabul edilir.
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

    // --- İç işleyiş ---

    /// <summary>TV'deki izin sorusu yanıtlanana kadar bekler; süre dolarsa vazgeçer.</summary>
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
                        continue; // "response": TV soruyu gösterdi, onay bekleniyor
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

    /// <summary>LG TV ikili çerçeveyi reddeder; mesajlar metin çerçevesiyle gider.</summary>
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
