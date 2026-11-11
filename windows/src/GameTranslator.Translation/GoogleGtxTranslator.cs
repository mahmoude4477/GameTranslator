// SPDX-License-Identifier: AGPL-3.0-only

using System.Net.Http;
using System.Text;
using System.Text.Json;
using GameTranslator.Core;

namespace GameTranslator.Translation;

/// <summary>
/// محرك ترجمة Google GTX المجاني الذي لا يحتاج إلى أي إعداد.
/// لا يتطلب مفتاح API؛ ويترجم مباشرة عبر الإنترنت إلى اللغة الهدف (العربية افتراضيًا).
/// </summary>
public sealed class GoogleGtxTranslator : ITextTranslator
{
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly string _source;
    private readonly string _target;

    public GoogleGtxTranslator(string source = "en", string target = "ar")
    {
        _source = string.IsNullOrWhiteSpace(source) ? "en" : source.ToLowerInvariant();
        _target = string.IsNullOrWhiteSpace(target) ? "ar" : target.ToLowerInvariant();
    }

    public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var trimmed = text.Trim();

        try
        {
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={_source}&tl={_target}&dt=t&q={Uri.EscapeDataString(trimmed)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            using var resp = await SharedClient.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode) return trimmed;

            var json = await resp.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var sentences = doc.RootElement[0];
                if (sentences.ValueKind == JsonValueKind.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var s in sentences.EnumerateArray())
                    {
                        if (s.ValueKind == JsonValueKind.Array && s.GetArrayLength() > 0)
                        {
                            var part = s[0].GetString();
                            if (!string.IsNullOrEmpty(part)) sb.Append(part);
                        }
                    }
                    var result = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(result)) return System.Net.WebUtility.HtmlDecode(result);
                }
            }
        }
        catch
        {
            // عند خطأ الشبكة يبقى النص الأصلي
        }

        return trimmed;
    }
}
