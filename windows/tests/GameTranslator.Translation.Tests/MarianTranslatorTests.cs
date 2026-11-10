// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;

namespace GameTranslator.Translation.Tests;

public class MarianTranslatorTests
{
    /// <summary>Cümleyi büyük harfe çeviren, çağrıları sayan ve eşzamanlı girişi yakalayan sahte çekirdek.</summary>
    private sealed class FakeEngine : ISentenceEngine
    {
        private int _active;
        public List<string> Calls { get; } = [];
        public bool SawConcurrentCall { get; private set; }
        public bool Disposed { get; private set; }
        public int Delay { get; init; }
        /// <summary>Verilirse çeviri, test bunu açana kadar bekler (zamanlamaya bağlı olmayan testler için).</summary>
        public ManualResetEventSlim? Hold { get; init; }

        public string Translate(string sentence, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _active) > 1) SawConcurrentCall = true;
            try
            {
                Hold?.Wait();
                if (Delay > 0) Thread.Sleep(Delay);
                lock (Calls) Calls.Add(sentence);
                return sentence.ToUpperInvariant();
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        public void Dispose() => Disposed = true;
    }

    private static (MarianTranslator, FakeEngine) Create(int delay = 0)
    {
        var engine = new FakeEngine { Delay = delay };
        return (new MarianTranslator(() => engine, () => true), engine);
    }

    [Fact]
    public async Task SpokenLinesKeepTheirSpeakers()
    {
        var (translator, _) = Create();
        await translator.TranslateAsync("KRATOS: We go now. Stay close.\nATREUS: Okay.\n- Where?\n- Home.");
        var lines = translator.SpokenLines("KRATOS: We go now. Stay close.\nATREUS: Okay.\n- Where?\n- Home.");
        Assert.Equal(["KRATOS", "KRATOS", "ATREUS", null, null], lines.Select(line => line.Speaker));
        Assert.Equal([false, false, false, true, true], lines.Select(line => line.NewTurn));
        Assert.Equal("We go now.", lines[0].English);
        Assert.Equal("WE GO NOW.", lines[0].Turkish);
    }

    [Fact]
    public async Task CachesExactInput()
    {
        var (translator, engine) = Create();

        Assert.Equal("WAIT!", await translator.TranslateAsync("Wait!"));
        Assert.Equal("WAIT!", await translator.TranslateAsync("Wait!"));
        Assert.Single(engine.Calls);

        // Önbellek tam metne bakar; harf farkı yeni çeviridir.
        await translator.TranslateAsync("wait!");
        Assert.Equal(2, engine.Calls.Count);
    }

    [Fact]
    public async Task ReusesCachedSentencesAcrossLines()
    {
        var (translator, engine) = Create();

        await translator.TranslateAsync("Boy! Focus.");
        Assert.Equal("BOY! FOCUS. WE GO.", await translator.TranslateAsync("Boy! Focus. We go."));
        Assert.Equal(["Boy!", "Focus.", "We go."], engine.Calls);
    }

    [Fact]
    public async Task KeepsLabelsAndSkipsPunctuationOnly()
    {
        var (translator, engine) = Create();

        Assert.Equal("ATREUS: FATHER, LOOK!\nKRATOS: STAY.", await translator.TranslateAsync("ATREUS: Father, look!\nKRATOS: Stay."));
        Assert.Equal("...", await translator.TranslateAsync("..."));
        Assert.Equal(["Father, look!", "Stay."], engine.Calls);
    }

    [Fact]
    public async Task NamesFromLabelsStayCapitalizedInLaterSentences()
    {
        var (translator, engine) = Create();

        await translator.TranslateAsync("ATREUS: Father, look!");
        await translator.TranslateAsync("THE GATE WILL NOT HOLD, ATREUS.");
        Assert.Equal("The gate will not hold, Atreus.", engine.Calls[^1]);
    }

    [Fact]
    public async Task ThrowsNeedsDownloadWhenModelMissing()
    {
        var created = false;
        var translator = new MarianTranslator(() => { created = true; return new FakeEngine(); }, () => false);

        Assert.Equal(TranslatorStatus.NeedsDownload, translator.Status);
        var error = await Assert.ThrowsAsync<TranslatorException>(() => translator.TranslateAsync("Wait!"));
        Assert.Equal(TranslatorStatus.NeedsDownload, error.Status);
        Assert.False(created);
    }

    [Fact]
    public async Task RealConstructorReportsMissingFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gametranslator-empty-" + Guid.NewGuid().ToString("N"));
        using var translator = new MarianTranslator(dir);

        Assert.Equal(TranslatorStatus.NeedsDownload, translator.Status);
        var error = await Assert.ThrowsAsync<TranslatorException>(() => translator.TranslateAsync("Wait!"));
        Assert.Equal(TranslatorStatus.NeedsDownload, error.Status);
    }

    [Fact]
    public async Task SerializesConcurrentCalls()
    {
        var (translator, engine) = Create(delay: 5);
        var lines = Enumerable.Range(0, 40).Select(i => $"Line number {i % 10}.").ToArray();

        var results = await Task.WhenAll(lines.Select(l => Task.Run(() => translator.TranslateAsync(l))));

        Assert.Equal(lines.Select(l => l.ToUpperInvariant()), results);
        Assert.False(engine.SawConcurrentCall);
        // Aynı cümle beklerken başkası çevirdiyse tekrar çevrilmez.
        Assert.Equal(10, engine.Calls.Count);
    }

    [Fact]
    public async Task HonorsCancellationWhileWaiting()
    {
        // Süreye değil açık iptale dayanır: yoğun makinede zamanlayıcı geç tetiklenebiliyor.
        using var hold = new ManualResetEventSlim(false);
        var engine = new FakeEngine { Hold = hold };
        var translator = new MarianTranslator(() => engine, () => true);
        var first = translator.TranslateAsync("Slow line.");
        using var cts = new CancellationTokenSource();
        var second = translator.TranslateAsync("Other line.", cts.Token);
        Assert.False(second.IsCompleted);

        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
        hold.Set();
        Assert.Equal("SLOW LINE.", await first);
    }

    [Fact]
    public async Task DisposeReleasesEngine()
    {
        var (translator, engine) = Create();
        await translator.TranslateAsync("Wait!");

        translator.Dispose();

        Assert.True(engine.Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => translator.TranslateAsync("New line."));
        // Önbellekteki satır hâlâ döner; motor gerekmez.
        Assert.Equal("WAIT!", await translator.TranslateAsync("Wait!"));
    }
}
