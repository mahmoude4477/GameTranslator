// SPDX-License-Identifier: AGPL-3.0-only

using System.Collections.Concurrent;
using System.Text;
using GameTranslator.Core;
using GameTranslator.Core.Dubbing;

namespace GameTranslator.Translation;

/// <summary>
/// Cihaz üstü İngilizce→Türkçe çeviri (OPUS-MT, ONNX Runtime). Model ilk çeviride yüklenir; aynı satır
/// oyunda defalarca okunduğu için sonuçlar girdi metnine göre önbelleklenir (Mac'teki gibi).
/// </summary>
public sealed class MarianTranslator : ITextTranslator, IDisposable
{
    /// <summary>Önbellek sınırsız büyümesin; dolunca boşaltmak bir oturum için yeterli.</summary>
    private const int CacheLimit = 4096;

    private readonly Func<ISentenceEngine> _engineFactory;
    private readonly Func<bool> _isInstalled;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);
    /// <summary>Konuşmacı etiketlerinden öğrenilen adlar (küçük harfle); cümle içinde de büyük harfle kalırlar.</summary>
    private readonly ConcurrentDictionary<string, byte> _names = new(StringComparer.Ordinal);
    /// <summary>Altyazı → seslendirilecek cümleler (dublaj).</summary>
    private readonly ConcurrentDictionary<string, IReadOnlyList<SpokenLine>> _speech = new(StringComparer.Ordinal);
    // Model tek seferde bir cümle çevirir: paralel çalıştırmak çekirdekleri paylaştırıp her satırı yavaşlatır.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ISentenceEngine? _engine;
    private bool _disposed;

    public MarianTranslator(string modelDirectory, int maxOutputTokens = 128, int beams = OnnxMarianEngine.DefaultBeams)
        : this(() => new OnnxMarianEngine(modelDirectory, maxOutputTokens, beams), () => IsInstalledIn(modelDirectory)) { }

    internal MarianTranslator(Func<ISentenceEngine> engineFactory, Func<bool> isInstalled)
    {
        _engineFactory = engineFactory;
        _isInstalled = isInstalled;
    }

    public TranslatorStatus Status => _engine is not null || _isInstalled() ? TranslatorStatus.Ready : TranslatorStatus.NeedsDownload;

    /// <summary>Çeviri için gereken dosyalar yerinde mi (ModelStore yalnızca doğrulanmış dosyayı asıl adına taşır).</summary>
    public static bool IsInstalledIn(string modelDirectory) =>
        OnnxMarianEngine.RequiredFiles.All(name => File.Exists(Path.Combine(modelDirectory, name)));

    /// <summary>Modeli önceden yükler ve bir kez çalıştırır; ilk altyazı beklemesin.</summary>
    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        await TranslateUncachedAsync("Hello.", cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_cache.TryGetValue(text, out var cached)) return cached;

        var builder = new StringBuilder();
        var pieces = SubtitleSegmenter.Split(text);
        // "OLD MAN: " → "old", "man": çok sözcüklü adın her sözcüğü ayrıca tanınır.
        foreach (var name in pieces.Select(SubtitleSegmenter.LabelName).OfType<string>())
            foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 2))
                _names[word.ToLowerInvariant()] = 0;
        var translations = new List<string?>(pieces.Count);
        foreach (var piece in pieces)
        {
            if (!piece.Translate)
            {
                builder.Append(piece.Text);
                translations.Add(null);
                continue;
            }
            if (!_cache.TryGetValue(piece.Text, out var sentence))
                sentence = await TranslateUncachedAsync(piece.Text, cancellationToken).ConfigureAwait(false);
            builder.Append(sentence);
            translations.Add(sentence);
        }
        if (_speech.Count >= 512) _speech.Clear();
        _speech[text] = SpeechScript.Lines(pieces, translations);
        var result = builder.ToString();
        Remember(text, result);
        return result;
    }

    public IReadOnlyList<SpokenLine> SpokenLines(string text) =>
        _speech.TryGetValue(text, out var lines) ? lines
        : _cache.TryGetValue(text, out var translation) ? [new SpokenLine(text, translation)] : [];

    private void Remember(string source, string translation)
    {
        if (_cache.Count >= CacheLimit) _cache.Clear();
        _cache[source] = translation;
    }

    private async Task<string> TranslateUncachedAsync(string sentence, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // Beklerken başka bir çağrı aynı cümleyi çevirmiş olabilir.
            if (_cache.TryGetValue(sentence, out var cached)) return cached;
            var engine = _engine ??= CreateEngine();
            // Çıkarım CPU'yu saniyenin onda biri kadar tutar; çağıranın (arayüz) iş parçacığını bloklamasın.
            var input = GameEnglish.Plain(SubtitleSegmenter.NormalizeCase(sentence, _names.Keys.ToHashSet(StringComparer.Ordinal)));
            var translation = await Task.Run(() => engine.Translate(input, cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            // Sıra bırakılmadan önce önbelleğe yazılır; sırada bekleyen aynı cümle yeniden çevrilmez.
            Remember(sentence, translation);
            return translation;
        }
        finally
        {
            _gate.Release();
        }
    }

    private ISentenceEngine CreateEngine()
    {
        if (!_isInstalled()) throw new TranslatorException(TranslatorStatus.NeedsDownload);
        return _engineFactory();
    }

    public void Dispose()
    {
        _gate.Wait();
        try
        {
            if (_disposed) return;
            _disposed = true;
            _engine?.Dispose();
            _engine = null;
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>Tek cümleyi çeviren çekirdek; testlerde sahtesi kullanılır.</summary>
internal interface ISentenceEngine : IDisposable
{
    string Translate(string sentence, CancellationToken cancellationToken);
}
