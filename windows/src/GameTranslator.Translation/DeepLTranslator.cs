// SPDX-License-Identifier: AGPL-3.0-only

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using GameTranslator.Core;
using GameTranslator.Core.Dubbing;

namespace GameTranslator.Translation;

/// <summary>
/// İsteğe bağlı çevrimiçi çeviri: kullanıcının kendi DeepL anahtarıyla. Altyazı cümle cümle, konuşmacı adları
/// çevrilmeden gönderilir; önceki altyazı bağlam olarak eklenir (ücretlendirilmez, "o"nun kim olduğunu bilir).
/// DeepL'e ulaşılamazsa ya da kota biterse o satır cihaz üstü modelle çevrilir; oyun hiç altyazısız kalmaz.
/// </summary>
public sealed class DeepLTranslator : ITextTranslator
{
    private const int CacheLimit = 4096;
    /// <summary>Hata sonrası bu süre DeepL denenmez; her satırda bağlantı zaman aşımını beklemeyelim.</summary>
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(1);

    private static readonly Lazy<HttpClient> SharedClient = new(() => new HttpClient { Timeout = TimeSpan.FromSeconds(4) });

    private readonly string _key;
    private readonly string _target;
    private readonly ITextTranslator? _fallback;
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IReadOnlyList<SpokenLine>> _speech = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _names = new(StringComparer.Ordinal);
    private string? _previous;
    private DateTimeOffset _pausedUntil = DateTimeOffset.MinValue;

    /// <param name="target">Çevirinin dili ("tr", "ar"); DeepL'e büyük harfle gider.</param>
    public DeepLTranslator(string key, ITextTranslator? fallback, string target, HttpClient? http = null)
    {
        _key = key.Trim();
        _target = target.Trim().ToUpperInvariant();
        _fallback = fallback;
        _http = http ?? SharedClient.Value;
    }

    /// <summary>Son DeepL hatası; çeviri düzgün gidiyorsa null.</summary>
    public OnlineKeyState? LastError { get; private set; }

    /// <summary>Ücretsiz anahtarlar ":fx" ile biter ve ayrı adrese gider.</summary>
    internal static Uri BaseUri(string key) =>
        new(key.Trim().EndsWith(":fx", StringComparison.Ordinal) ? "https://api-free.deepl.com/v2/" : "https://api.deepl.com/v2/");

    public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_cache.TryGetValue(text, out var cached)) return cached;
        if (DateTimeOffset.UtcNow < _pausedUntil) return await FallbackAsync(text, cancellationToken).ConfigureAwait(false);

        var pieces = SubtitleSegmenter.Split(text);
        foreach (var name in pieces.Select(SubtitleSegmenter.LabelName).OfType<string>())
            foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 2))
                _names[word.ToLowerInvariant()] = 0;
        var names = _names.Keys.ToHashSet(StringComparer.Ordinal);
        var sources = pieces.Where(p => p.Translate).Select(p => SubtitleSegmenter.NormalizeCase(p.Text, names)).ToList();

        string[] translated;
        try
        {
            translated = sources.Count == 0 ? [] : await RequestAsync(sources, cancellationToken).ConfigureAwait(false);
            LastError = null;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or DeepLException
                                      && !cancellationToken.IsCancellationRequested)
        {
            LastError = error is DeepLException deepL ? deepL.State : OnlineKeyState.Unreachable;
            _pausedUntil = DateTimeOffset.UtcNow + RetryAfter;
            return await FallbackAsync(text, cancellationToken).ConfigureAwait(false);
        }

        var builder = new StringBuilder();
        var translations = new List<string?>(pieces.Count);
        var next = 0;
        foreach (var piece in pieces)
        {
            var sentence = piece.Translate ? translated[next++] : null;
            builder.Append(sentence ?? piece.Text);
            translations.Add(sentence);
        }
        if (_speech.Count >= 512) _speech.Clear();
        _speech[text] = SpeechScript.Lines(pieces, translations);
        var result = builder.ToString();
        if (_cache.Count >= CacheLimit) _cache.Clear();
        _cache[text] = result;
        _previous = text;
        return result;
    }

    public IReadOnlyList<SpokenLine> SpokenLines(string text) =>
        _speech.TryGetValue(text, out var lines) ? lines
        : _cache.TryGetValue(text, out var translation) ? [new SpokenLine(text, translation)]
        : _fallback?.SpokenLines(text) ?? [];

    private async Task<string> FallbackAsync(string text, CancellationToken cancellationToken) =>
        _fallback is null ? throw new TranslatorException(TranslatorStatus.NeedsDownload)
            : await _fallback.TranslateAsync(text, cancellationToken).ConfigureAwait(false);

    private async Task<string[]> RequestAsync(List<string> sentences, CancellationToken cancellationToken)
    {
        // Oyun konuşması: "siz" değil "sen" (DeepL hedef dilde desteklemiyorsa yok sayar).
        var body = new TranslateRequest(sentences, "EN", _target, _previous, "prefer_less", true);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri(_key), "translate"))
        {
            Content = JsonContent.Create(body, DeepLJson.Default.TranslateRequest),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", _key);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        ThrowIfFailed(response);
        var reply = await response.Content.ReadFromJsonAsync(DeepLJson.Default.TranslateResponse, cancellationToken).ConfigureAwait(false);
        if (reply?.Translations is not { } list || list.Count != sentences.Count) throw new DeepLException(OnlineKeyState.Unreachable);
        return [.. list.Select(t => t.Text)];
    }

    /// <summary>Ayarlardaki "Dene" düğmesi: anahtar geçerli mi, bu ay ne kadar kullanıldı.</summary>
    public static async Task<OnlineKeyCheck> CheckAsync(string key, HttpClient? http = null, CancellationToken cancellationToken = default)
    {
        key = key.Trim();
        if (key.Length == 0) return new(OnlineKeyState.Invalid);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri(key), "usage"));
            request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", key);
            using var response = await (http ?? SharedClient.Value).SendAsync(request, cancellationToken).ConfigureAwait(false);
            ThrowIfFailed(response);
            var usage = await response.Content.ReadFromJsonAsync(DeepLJson.Default.UsageResponse, cancellationToken).ConfigureAwait(false);
            if (usage is null) return new(OnlineKeyState.Unreachable);
            return new(usage.CharacterLimit > 0 && usage.CharacterCount >= usage.CharacterLimit ? OnlineKeyState.QuotaExceeded : OnlineKeyState.Valid,
                usage.CharacterCount, usage.CharacterLimit);
        }
        catch (DeepLException error) { return new(error.State); }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new(OnlineKeyState.Unreachable);
        }
    }

    private static void ThrowIfFailed(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        throw new DeepLException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => OnlineKeyState.Invalid,
            (HttpStatusCode)456 => OnlineKeyState.QuotaExceeded,
            _ => OnlineKeyState.Unreachable,
        });
    }
}

internal sealed class DeepLException(OnlineKeyState state) : Exception($"DeepL: {state}")
{
    public OnlineKeyState State { get; } = state;
}

internal sealed record TranslateRequest(
    [property: JsonPropertyName("text")] IReadOnlyList<string> Text,
    [property: JsonPropertyName("source_lang")] string SourceLang,
    [property: JsonPropertyName("target_lang")] string TargetLang,
    [property: JsonPropertyName("context"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Context,
    [property: JsonPropertyName("formality")] string Formality,
    [property: JsonPropertyName("preserve_formatting")] bool PreserveFormatting);

internal sealed record TranslateResponse([property: JsonPropertyName("translations")] IReadOnlyList<TranslatedText>? Translations);

internal sealed record TranslatedText([property: JsonPropertyName("text")] string Text);

internal sealed record UsageResponse(
    [property: JsonPropertyName("character_count")] long CharacterCount,
    [property: JsonPropertyName("character_limit")] long CharacterLimit);

[JsonSerializable(typeof(TranslateRequest))]
[JsonSerializable(typeof(TranslateResponse))]
[JsonSerializable(typeof(UsageResponse))]
internal sealed partial class DeepLJson : JsonSerializerContext;
