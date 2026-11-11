// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;

namespace GameTranslator.Core.Tests;

internal sealed class FakeReader(params string[] texts) : ITextReader
{
    private readonly Queue<string> texts = new(texts);
    public List<int> ReadThreads { get; } = [];
    public string Read(Frame image)
    {
        lock (ReadThreads) ReadThreads.Add(Environment.CurrentManagedThreadId);
        lock (texts) return texts.Count == 0 ? "" : texts.Dequeue();
    }
}

/// <summary>قارئ يعيد الأسطر مع مواضعها.</summary>
internal sealed class LinesReader(Reading reading) : ITextReader
{
    public string Read(Frame image) => reading.Text;
    public Reading ReadLines(Frame image, bool deep) => reading;
}

internal sealed class FakeTranslator : ITextTranslator
{
    public Exception? Error { get; set; }
    public int FailuresLeft { get; set; }
    public Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        if (Error is not null) throw Error;
        if (FailuresLeft > 0) { FailuresLeft--; throw new TimeoutException(); }
        return Task.FromResult("AR: " + text);
    }
}

/// يُبقي الترجمات منتظرة حتى يطلقها الاختبار؛ وما يأتي بعد الفتح لا ينتظر.
internal sealed class GatedTranslator : ITextTranslator
{
    private readonly object gate = new();
    private readonly List<(string Text, TaskCompletionSource Done)> waiting = [];
    private bool isOpen;
    public List<string> Requested { get; } = [];

    public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        TaskCompletionSource? done = null;
        lock (gate)
        {
            Requested.Add(text);
            if (!isOpen) { done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); waiting.Add((text, done)); }
        }
        if (done is not null) await done.Task;
        return "AR: " + text;
    }

    public void Open()
    {
        lock (gate) { isOpen = true; foreach (var w in waiting) w.Done.SetResult(); waiting.Clear(); }
    }

    /// ينهي ترجمة هذا السطر فقط (الاختبار يحدد الترتيب).
    public async Task Release(string text)
    {
        while (true)
        {
            lock (gate)
            {
                var index = waiting.FindIndex(w => w.Text == text);
                if (index >= 0) { var entry = waiting[index]; waiting.RemoveAt(index); entry.Done.SetResult(); return; }
            }
            await Task.Delay(5);
        }
    }
}

internal sealed class FakeSender : ISubtitleSender
{
    public List<string> Sent { get; } = [];
    public List<int> Durations { get; } = [];
    public List<TextBox?> Boxes { get; } = [];
    public int Clears { get; private set; }
    public void SendSubtitle(string text, int durationMs = 15000, TextBox? box = null)
    {
        lock (Sent) { Sent.Add(text); Durations.Add(durationMs); Boxes.Add(box); }
    }
    public void Clear() => Clears++;
}

public class SubtitlePipelineTests
{
    private static readonly Frame Image = Frame.Blank(64, 64);

    private static async Task Run(SubtitlePipeline pipeline, int frames, bool waitEach = true)
    {
        for (var i = 0; i < frames; i++)
        {
            await pipeline.ProcessAsync(Image, new CropRegion());
            if (waitEach) await pipeline.WaitForTranslationsAsync();
        }
    }

    [Fact]
    public async Task ButtonPromptsAreNotTranslated()
    {
        var sender = new FakeSender();
        var pipeline = new SubtitlePipeline(
            new FakeReader("R2 Swing", "R2 Swing", "Hold the gate\nR2 Swing", "Hold the gate\nR2 Swing"), new FakeTranslator(), sender);
        await Run(pipeline, 4);
        Assert.Equal(["AR: Hold the gate"], sender.Sent);
    }

    [Fact]
    public async Task SubtitleBoxIsSentInFrameCoordinates()
    {
        var sender = new FakeSender();
        var reading = new Reading([new TextLine("Hold the gate.", new TextBox(0.25, 0.5, 0.75, 1))]);
        var pipeline = new SubtitlePipeline(new LinesReader(reading), new FakeTranslator(), sender);
        await pipeline.ProcessAsync(Image, new CropRegion(Top: 0.6, Bottom: 1.0, Left: 0.1, Right: 0.9));
        await pipeline.WaitForTranslationsAsync();
        var box = Assert.Single(sender.Boxes)!.Value;
        Assert.Equal(0.3, box.Left, 9);
        Assert.Equal(0.8, box.Top, 9);
        Assert.Equal(0.7, box.Right, 9);
        Assert.Equal(1.0, box.Bottom, 9);
    }

    [Fact]
    public void CenterStripLinesAreReportedInRegionCoordinates()
    {
        var reading = ReadingFallback.FirstLines(Image, candidate => candidate.Width == 64
            ? Reading.Empty
            : new Reading([new TextLine("Go!", new TextBox(0, 0.5, 1, 1))]));
        var box = reading.Box!.Value;
        Assert.Equal(19.0 / 64, box.Left, 9);
        Assert.Equal(45.0 / 64, box.Right, 9);
        Assert.Equal(0.5, box.Top, 9);
    }

    [Fact]
    public async Task StableTextIsTranslatedAndSentOnce()
    {
        var sender = new FakeSender();
        var pipeline = new SubtitlePipeline(new FakeReader("Hold the gate", "Hold the gate", "Hold the gate"), new FakeTranslator(), sender);
        await Run(pipeline, 3);
        Assert.Equal(["AR: Hold the gate"], sender.Sent);
        Assert.Equal("Hold the gate", pipeline.LastPair?.Source);
    }

    [Fact]
    public async Task TextRecognitionRunsOffTheCallingThread()
    {
        var reader = new FakeReader("Hold the gate");
        var pipeline = new SubtitlePipeline(reader, new FakeTranslator(), new FakeSender());
        var caller = Environment.CurrentManagedThreadId;
        await pipeline.ProcessAsync(Image, new CropRegion());
        Assert.DoesNotContain(caller, reader.ReadThreads);
    }

    [Fact]
    public async Task MissingModelSendsNothing()
    {
        var sender = new FakeSender();
        var pipeline = new SubtitlePipeline(new FakeReader("Hold the gate", "Hold the gate"),
            new FakeTranslator { Error = new TranslatorException(TranslatorStatus.NeedsDownload) }, sender);
        await Run(pipeline, 2);
        Assert.Empty(sender.Sent);
        Assert.Equal(TranslatorStatus.NeedsDownload, pipeline.LastError);
    }

    [Fact]
    public async Task FailedTranslationIsRetriedWhileLineStaysOnScreen()
    {
        var sender = new FakeSender();
        var pipeline = new SubtitlePipeline(new FakeReader("Hold the gate", "Hold the gate", "Hold the gate", "Hold the gate"),
            new FakeTranslator { FailuresLeft = 1 }, sender);
        await Run(pipeline, 4);
        Assert.Equal(["AR: Hold the gate"], sender.Sent);
    }

    /// تُقرأ الإطارات نحو 20 مرة في الثانية؛ إطار متفرق تعذّرت قراءته يجب ألا يمسح الترجمة النصية من التلفزيون.
    [Fact]
    public async Task ShortReadingGapDoesNotClearTheTv()
    {
        var sender = new FakeSender();
        var pipeline = new SubtitlePipeline(new FakeReader("Hold the gate", "Hold the gate", "", "", "Hold the gate"), new FakeTranslator(), sender);
        await Run(pipeline, 5);
        Assert.Equal(0, sender.Clears);
        Assert.Equal(["AR: Hold the gate"], sender.Sent);
    }

    [Fact]
    public async Task SubtitleGoneForSixFramesClearsTheTv()
    {
        var sender = new FakeSender();
        var pipeline = new SubtitlePipeline(new FakeReader("Hold the gate", "Hold the gate", "", "", "", "", "", ""), new FakeTranslator(), sender);
        await Run(pipeline, 8);
        Assert.Equal(1, sender.Clears);
    }

    /// يجب ألا تتوقف القراءة أثناء الترجمة؛ وإلا فاتت الأسطر القصيرة.
    [Fact]
    public async Task ReadingContinuesWhileTranslating()
    {
        var translator = new GatedTranslator();
        var pipeline = new SubtitlePipeline(new FakeReader("Hold the gate", "Hold the gate", "Run", "Run"), translator, new FakeSender());
        await Run(pipeline, 4, waitEach: false);
        translator.Open();
        await pipeline.WaitForTranslationsAsync();
        Assert.Equal(["Hold the gate", "Run"], translator.Requested);
    }

    /// في الحوار السريع تُعرض ترجمة السطر السابق حتى لو وصل سطر جديد؛ فلا يضيع السطر.
    [Fact]
    public async Task EarlierLineIsShownWhenItsTranslationArrivesFirst()
    {
        var sender = new FakeSender();
        var translator = new GatedTranslator();
        var pipeline = new SubtitlePipeline(new FakeReader("Hold the gate.", "Run!"), translator, sender);
        await Run(pipeline, 2, waitEach: false);
        await translator.Release("Hold the gate.");
        await WaitUntil(() => sender.Sent.Count == 1);
        await translator.Release("Run!");
        await pipeline.WaitForTranslationsAsync();
        Assert.Equal(["AR: Hold the gate.", "AR: Run!"], sender.Sent);
    }

    /// إذا كانت ترجمة السطر الجديد قد عُرضت فلا تُكتب فوقها ترجمة السطر القديم.
    [Fact]
    public async Task OlderTranslationArrivingAfterNewerIsDropped()
    {
        var sender = new FakeSender();
        var translator = new GatedTranslator();
        var pipeline = new SubtitlePipeline(new FakeReader("Hold the gate.", "Run!"), translator, sender);
        await Run(pipeline, 2, waitEach: false);
        await translator.Release("Run!");
        await WaitUntil(() => sender.Sent.Count == 1);
        await translator.Release("Hold the gate.");
        await pipeline.WaitForTranslationsAsync();
        Assert.Equal(["AR: Run!"], sender.Sent);
    }

    /// إذا وصلت ترجمة السطر بعد اختفائه من الشاشة فإنها تُعرض مع ذلك مدة قصيرة تكفي لقراءتها.
    [Fact]
    public async Task TranslationArrivingAfterLineLeftIsShownBriefly()
    {
        var sender = new FakeSender();
        var translator = new GatedTranslator();
        var pipeline = new SubtitlePipeline(new FakeReader("Hold the gate.", "", "", "", "", "", ""), translator, sender);
        await Run(pipeline, 7, waitEach: false);
        Assert.Equal(1, sender.Clears);
        translator.Open();
        await pipeline.WaitForTranslationsAsync();
        Assert.Equal(["AR: Hold the gate."], sender.Sent);
        Assert.Equal(2000 + "AR: Hold the gate.".Length * 60, sender.Durations[0]);
    }

    /// إذا أُوقفت الترجمة فلا تُرسل الترجمة المتأخرة إلى التلفزيون.
    [Fact]
    public async Task TranslationArrivingAfterStopIsDropped()
    {
        var sender = new FakeSender();
        var translator = new GatedTranslator();
        var pipeline = new SubtitlePipeline(new FakeReader("Hold the gate", "Hold the gate"), translator, sender);
        await Run(pipeline, 2, waitEach: false);
        pipeline.Reset();
        translator.Open();
        await pipeline.WaitForTranslationsAsync();
        Assert.Empty(sender.Sent);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition()) { Assert.True(DateTime.UtcNow < deadline); await Task.Delay(5); }
    }
}

public class ReadingFallbackTests
{
    private static readonly Frame Image = Frame.Blank(64, 64);

    [Fact]
    public void ShortLinesGetAZoomedSecondReading()
    {
        var stripWidth = (int)Math.Round(64 * 0.4) * 2;
        var text = ReadingFallback.FirstReading(Image, candidate => candidate.Width == stripWidth ? "Go!" : "");
        Assert.Equal("Go!", text);
    }

    /// إذا وجدت القراءتان الاحتياطيتان نصا تُختار قراءة الشريط الأوسط.
    [Fact]
    public void CenterStripReadingWinsOverZoomedRegion()
    {
        var text = ReadingFallback.FirstReading(Image, candidate => candidate.Width switch { 64 => "", 128 => "G0!", _ => "Go!" });
        Assert.Equal("Go!", text);
    }

    [Fact]
    public void FullReadingIsUsedWhenItFindsText()
    {
        var calls = 0;
        var text = ReadingFallback.FirstReading(Image, _ => { Interlocked.Increment(ref calls); return "Hold the gate"; });
        Assert.Equal("Hold the gate", text);
        Assert.Equal(1, calls);
    }

    /// عندما تكون المنطقة فارغة تُجرى القراءتان الاحتياطيتان في الوقت نفسه لا بالتتابع؛ فيُلاحَظ السطر الجديد أبكر.
    [Fact]
    public void FallbackReadingsRunAtTheSameTime()
    {
        // القراءتان الاحتياطيتان تنتظر كل منهما الأخرى؛ لو أُجريتا بالتتابع لما التقتا ولانتهت المهلة.
        using var barrier = new Barrier(2);
        var met = 0;
        ReadingFallback.FirstReading(Image, candidate =>
        {
            if (candidate.Width != 64 && barrier.SignalAndWait(TimeSpan.FromSeconds(10))) Interlocked.Increment(ref met);
            return "";
        });
        Assert.Equal(2, met);
    }
}

public class RegionCropperTests
{
    [Fact]
    public void DefaultRegionCropsBottomCenterBand()
    {
        var cropped = RegionCropper.Crop(Frame.Blank(1920, 1080), new CropRegion());
        Assert.InRange(cropped.Width, 1382, 1384);
        Assert.InRange(cropped.Height, 378, 379);
    }

    [Fact]
    public void CropKeepsThePixelsOfTheRegion()
    {
        var image = Frame.Blank(10, 10);
        image.Pixels[(9 * image.Stride) + (9 * 4)] = 200; // الزاوية السفلية اليمنى، القناة الزرقاء
        var cropped = RegionCropper.Crop(image, new CropRegion(0.5, 1, 0.5, 1));
        Assert.Equal(200, cropped.Pixels[((cropped.Height - 1) * cropped.Stride) + ((cropped.Width - 1) * 4)]);
    }
}
