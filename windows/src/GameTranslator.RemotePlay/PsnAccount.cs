// SPDX-License-Identifier: AGPL-3.0-only
// يحصل عبر تسجيل الدخول إلى PSN على معرّف الحساب اللازم لتسجيل Remote Play.
// التدفق وبيانات العميل مأخوذة من chiaki-ng (gui/include/psnaccountid.h, gui/src/psnaccountid.cpp).

using System.Buffers.Binary;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GameTranslator.RemotePlay;

public sealed class PsnAccountException : Exception
{
    /// <summary>رمز HTTP إن أعاد الخادم حالة خطأ؛ و null إن تعذر فهم الرد.</summary>
    public int? StatusCode { get; }

    public PsnAccountException(int? statusCode = null)
        : base(statusCode is { } code ? $"PSN request failed (HTTP {code})" : "Unexpected PSN response") => StatusCode = statusCode;
}

public static class PsnAccount
{
    internal const string ClientId = "ba495a24-818c-472b-b12d-ff231c1b5745";
    internal const string ClientSecret = "mvaiZkRsAsI1IBkY";
    internal const string TokenUrl = "https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/token";
    internal const string Scope = "psn:clientapp referenceDataService:countryConfig.read pushNotification:webSocket.desktop.connect sessionManager:remotePlaySession.system.update";

    public const string RedirectPrefix = "https://remoteplay.dl.playstation.net/remoteplay/redirect";

    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>صفحة تسجيل الدخول لدى Sony التي تُفتح في متصفح التطبيق الداخلي (WebView2).</summary>
    public static Uri AuthorizeUrl { get; } = new("https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/authorize?" + Query(
    [
        ("service_entity", "urn:service-entity:psn"),
        ("response_type", "code"),
        ("client_id", ClientId),
        ("redirect_uri", RedirectPrefix),
        ("scope", Scope),
        ("request_locale", "en_US"),
        ("ui", "pr"),
        ("service_logo", "ps"),
        ("layout_type", "popup"),
        ("smcid", "remoteplay"),
        ("prompt", "always"),
        ("PlatformPrivacyWs1", "minimal"),
    ]));

    /// <summary>يستخرج الرمز ذا الاستخدام الواحد من عنوان إعادة التوجيه الذي ينتقل إليه المتصفح بعد تسجيل الدخول.</summary>
    public static string? CodeFromRedirect(Uri url)
    {
        if (!url.IsAbsoluteUri || !url.AbsoluteUri.StartsWith(RedirectPrefix, StringComparison.Ordinal)) return null;
        foreach (var pair in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var name = Uri.UnescapeDataString(separator < 0 ? pair : pair[..separator]);
            if (name == "code") return separator < 0 ? "" : Uri.UnescapeDataString(pair[(separator + 1)..]);
        }
        return null;
    }

    /// <summary>يحوّل الرمز إلى token ثم الـ token إلى معرّف الحساب (8 بايتات). لا يُحفظ الـ token.</summary>
    public static Task<byte[]> AccountIdAsync(string code, CancellationToken ct = default) => AccountIdAsync(code, SharedClient, ct);

    internal static async Task<byte[]> AccountIdAsync(string code, HttpClient client, CancellationToken ct)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
        tokenRequest.Headers.Authorization = BasicAuth;
        tokenRequest.Content = new StringContent(Query(
        [
            ("grant_type", "authorization_code"),
            ("code", code),
            ("scope", Scope),
            ("redirect_uri", RedirectPrefix),
        ]), Encoding.UTF8);
        tokenRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        using var token = await Json(client, tokenRequest, ct).ConfigureAwait(false);
        if (!token.RootElement.TryGetProperty("access_token", out var accessToken) || accessToken.ValueKind != JsonValueKind.String)
            throw new PsnAccountException();

        using var infoRequest = new HttpRequestMessage(HttpMethod.Get, TokenUrl + "/" + Uri.EscapeDataString(accessToken.GetString()!));
        infoRequest.Headers.Authorization = BasicAuth;
        using var info = await Json(client, infoRequest, ct).ConfigureAwait(false);
        if (!info.RootElement.TryGetProperty("user_id", out var userId)) throw new PsnAccountException();
        var text = userId.ValueKind switch
        {
            JsonValueKind.String => userId.GetString(),
            JsonValueKind.Number => userId.GetRawText(),
            _ => null,
        };
        return (text is null ? null : AccountIdBytes(text)) ?? throw new PsnAccountException();
    }

    /// <summary>يحوّل <c>user_id</c> العشري إلى مصفوفة من 8 بايتات بترتيب little-endian.</summary>
    internal static byte[]? AccountIdBytes(string userId)
    {
        if (!ulong.TryParse(userId, NumberStyles.None, CultureInfo.InvariantCulture, out var value)) return null;
        var bytes = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        return bytes;
    }

    private static AuthenticationHeaderValue BasicAuth =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}")));

    private static async Task<JsonDocument> Json(HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new PsnAccountException((int)response.StatusCode);
        var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        try
        {
            var document = await JsonDocument.ParseAsync(body, cancellationToken: ct).ConfigureAwait(false);
            if (document.RootElement.ValueKind == JsonValueKind.Object) return document;
            document.Dispose();
        }
        catch (JsonException)
        {
        }
        throw new PsnAccountException();
    }

    /// <summary>يرمّز الاستعلام: المحارف المسموح بها فيه (ومنها ":" و "/") تبقى كما هي.</summary>
    internal static string Query(IEnumerable<(string Name, string Value)> items) =>
        string.Join("&", items.Select(item => Encode(item.Name) + "=" + Encode(item.Value)));

    private static string Encode(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            var c = (char)b;
            if (char.IsAsciiLetterOrDigit(c) || "-._~:/?@!$'()*,;".Contains(c)) result.Append(c);
            else result.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }
        return result.ToString();
    }
}
