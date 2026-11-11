// SPDX-License-Identifier: AGPL-3.0-only
// تدفق تسجيل الدخول مقتبس من Apps2Samsung (MIT, Patrick Stel): https://github.com/Jellyfin2Samsung/Samsung-Jellyfin-Installer

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace GameTranslator.TvInstall.Samsung;

/// <summary>البيانات العائدة من تسجيل الدخول بحساب Samsung؛ وهي لازمة لطلب الشهادة.</summary>
public sealed record SamsungAccount(string AccessToken, string UserId, string Email);

/// <summary>
/// تسجيل الدخول بحساب Samsung. تُفتح الصفحة في المتصفح؛ وصفحة SignInGate لدى Samsung ترسل الرمز المميز بطلب POST
/// إلى مستمع صغير على هذا الكمبيوتر. إضافة الشهادات في Tizen Studio تستخدم الطريقة نفسها.
/// </summary>
public static class SamsungLogin
{
    private const string SignInGate = "https://account.samsung.com/accounts/be1dce529476c1a6d407c4c7578c31bd/signInGate";
    private const string ClientId = "v285zxnl3h";
    private const string State = "accountcheckdogeneratedstatetext";
    public const int CallbackPort = 4794;
    private const string CallbackPath = "/signin/callback";

    public static string AuthorizeUrl =>
        $"{SignInGate}?locale=&clientId={ClientId}" +
        $"&redirect_uri={Uri.EscapeDataString($"http://localhost:{CallbackPort}{CallbackPath}")}" +
        $"&state={State}&tokenType=TOKEN";

    /// <summary>يفتح المستمع، ويفتح الصفحة عبر <paramref name="openBrowser"/>، ثم ينتظر تسجيل الدخول.</summary>
    public static async Task<SamsungAccount> SignInAsync(Action<string> openBrowser, TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        // قد يحلّ المتصفح localhost إلى ::1؛ فاستمع على المكدسين معًا.
        var listener = new TcpListener(IPAddress.IPv6Any, CallbackPort);
        listener.Server.DualMode = true;
        try { listener.Start(); }
        catch (SocketException error) { throw new SamsungInstallException(SamsungInstallError.LoginFailed, $"port {CallbackPort}: {error.Message}"); }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            openBrowser(AuthorizeUrl);
            while (true)
            {
                using var client = await listener.AcceptTcpClientAsync(limit.Token);
                await using var stream = client.GetStream();
                var (method, path, body) = await ReadRequestAsync(stream, limit.Token);
                var isCallback = method == "POST" && path.StartsWith(CallbackPath, StringComparison.Ordinal);
                var account = isCallback ? ParseCallback(body) : null;
                await RespondAsync(stream, account is not null ? 200 : isCallback ? 400 : 404, account is not null, limit.Token);
                if (account is not null) return account;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SamsungInstallException(SamsungInstallError.LoginFailed, "انتهت المهلة");
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>الجسم: <c>code=&lt;JSON&gt;&amp;state=…</c>. يحتوي JSON على access_token و userId و inputEmailID.</summary>
    internal static SamsungAccount? ParseCallback(string body)
    {
        string? state = null, code = null;
        foreach (var part in body.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) continue;
            if (pair[0] == "state") state = WebUtility.UrlDecode(pair[1]);
            if (pair[0] == "code") code = Uri.UnescapeDataString(pair[1]);
        }
        if (state != State || string.IsNullOrWhiteSpace(code)) return null;
        try
        {
            using var json = JsonDocument.Parse(code);
            var root = json.RootElement;
            string? Text(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            return Text("access_token") is { Length: > 0 } token && Text("userId") is { Length: > 0 } user
                ? new SamsungAccount(token, user, Text("inputEmailID") ?? "")
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<(string Method, string Path, string Body)> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[16384];
        var received = new MemoryStream();
        int headerEnd;
        while ((headerEnd = IndexOfHeaderEnd(received)) < 0)
        {
            var n = await stream.ReadAsync(buffer, cancellationToken);
            if (n <= 0 || received.Length > 1024 * 1024) return ("", "", "");
            received.Write(buffer, 0, n);
        }
        var bytes = received.ToArray();
        var head = Encoding.ASCII.GetString(bytes, 0, headerEnd);
        var lines = head.Split("\r\n");
        var request = lines[0].Split(' ');
        var length = 0;
        foreach (var line in lines)
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) int.TryParse(line[15..].Trim(), out length);
        var body = new MemoryStream();
        body.Write(bytes, headerEnd + 4, bytes.Length - headerEnd - 4);
        while (body.Length < length)
        {
            var n = await stream.ReadAsync(buffer, cancellationToken);
            if (n <= 0) break;
            body.Write(buffer, 0, n);
        }
        return (request.ElementAtOrDefault(0) ?? "", request.ElementAtOrDefault(1) ?? "", Encoding.UTF8.GetString(body.ToArray()));
    }

    private static int IndexOfHeaderEnd(MemoryStream stream)
    {
        var bytes = stream.GetBuffer();
        for (var i = 0; i + 3 < stream.Length; i++)
            if (bytes[i] == '\r' && bytes[i + 1] == '\n' && bytes[i + 2] == '\r' && bytes[i + 3] == '\n') return i;
        return -1;
    }

    private static async Task RespondAsync(NetworkStream stream, int status, bool signedIn, CancellationToken cancellationToken)
    {
        var page = signedIn
            ? "<!doctype html><meta charset=utf-8><title>GameTranslator</title><body style=\"font-family:system-ui;padding:3em\">" +
              "<h2>تم تسجيل الدخول / Signed in</h2><p>يمكنك إغلاق هذه الصفحة والعودة إلى GameTranslator.<br>You can close this tab and return to GameTranslator.</p>"
            : "Not found";
        var reason = status switch { 200 => "OK", 400 => "Bad Request", _ => "Not Found" };
        var body = Encoding.UTF8.GetBytes(page);
        var header = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {reason}\r\nContent-Type: text/html; charset=utf-8\r\n" +
                                             $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(body, cancellationToken);
    }
}
