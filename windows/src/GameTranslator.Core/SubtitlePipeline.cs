// SPDX-License-Identifier: AGPL-3.0-only


namespace GameTranslator.Core;

/// <summary>الإطار ← منطقة القراءة ← التعرّف على النص ← الاستقرار ← الترجمة ← التلفزيون.</summary>
public sealed class SubtitlePipeline(ITextReader reader, ITextTranslator translator, ISubtitleSender sender, ReadScheduler? scheduler = null)
{
    private readonly object gate = new();
    /// <summary>الافتراضي يقرأ كل إطار؛ التطبيق يمرّر الخطة التي تتخطى القراءات غير اللازمة.</summary>
    private readonly ReadScheduler schedule = scheduler ?? ReadScheduler.Always();
    /// <summary>تُقرأ الإطارات نحو 20 مرة في الثانية؛ بقاء المنطقة فارغة نحو 0.3 ثانية يعني أن الترجمة النصية اختفت.</summary>
    private readonly SubtitleStabilizer stabilizer = new(emptyFramesToClear: 6);
    private readonly Dictionary<int, Task> translations = [];
    /// <summary>
    /// يأخذ كل طلب ترجمة رقمًا تسلسليًا. في الحوار السريع قد تعود الترجمات بغير ترتيبها:
    /// تُعرض ترجمة السطر الأقدم ما لم تكن ترجمة سطر أحدث قد عُرضت.
    /// </summary>
    private int lastRequested;
    private int lastShown;
    /// <summary>رقم السطر الظاهر الآن على شاشة اللعبة (null عندما تفرغ المنطقة).</summary>
    private int? lineOnScreen;
    /// <summary>يزداد عند إيقاف القراءة؛ لا تُعرض الترجمات العائدة من الجلسة السابقة.</summary>
    private int session;
    /// <summary>آخر الأسطر المقروءة؛ إذا لم يتغيّر الإطار لا يُقرأ من جديد بل تُستخدم هذه.</summary>
    private Reading lastReading = Reading.Empty;

    public (string Source, string Translation)? LastPair { get; private set; }
    /// <summary>موضع الترجمة النصية للعبة في الإطار، لآخر ترجمة عُرضت.</summary>
    public TextBox? LastBox { get; private set; }
    public TranslatorStatus? LastError { get; private set; }

    /// <summary>يُستدعى عند عرض ترجمة جديدة (الأصل، الترجمة).</summary>
    public event Action<string, string>? PairShown;
    /// <summary>يُستدعى بعد كل محاولة ترجمة (null إذا نجحت).</summary>
    public event Action<TranslatorStatus?>? TranslationResult;

    /// <summary>القراءة لا تنتظر الترجمة؛ فتُلتقط أيضًا الأسطر التي تظهر أثناء الترجمة.</summary>
    public async Task ProcessAsync(Frame frame, CropRegion region)
    {
        // قد يطول التعرّف على النص؛ يُنفَّذ في مجمّع الخيوط حتى لا ينتظر المستدعي (الواجهة).
        var raw = await Task.Run(() =>
        {
            try
            {
                var area = RegionCropper.Crop(frame, region);
                ReadPlan plan;
                lock (gate)
                {
                    plan = schedule.Plan(RegionCropper.Signature(area));
                    if (plan.Reuse) return lastReading;
                }
                var read = reader.ReadLines(area, plan.Deep);
                lock (gate)
                {
                    schedule.Record(read.Text);
                    lastReading = read;
                }
                return read;
            }
            catch { return Reading.Empty; }
        });
        // تلميحات ذراع التحكم ("R2 Swing") ليست ترجمة نصية؛ لا تُترجم ولا يدخل موضعها في موضع الترجمة النصية.
        // والكلمات العشوائية التي يختلقها التعرّف على النص ("ou$3e") لا تصل إلى المترجم أيضًا.
        // والقوائم ومؤشرات الزوايا في المنطقة ليست ترجمة نصية؛ تُترجم كتلة الترجمة النصية وحدها ويُحسب مربّعها منها.
        var area = new TextBox(region.Left, region.Top, region.Right, region.Bottom);
        var reading = raw.WithoutButtonPrompts().WithoutOcrNoise().SubtitleBlock(area);

        lock (gate)
        {
            var action = stabilizer.Feed(reading.Text);
            switch (action.Kind)
            {
                case StabilizerKind.Clear:
                    lineOnScreen = null;
                    sender.Clear();
                    break;
                case StabilizerKind.Translate:
                    var number = ++lastRequested;
                    var current = session;
                    lineOnScreen = number;
                    // يُستدعى المترجم هنا بترتيب الطلبات (لو بدأ في مهمة منفصلة لاختلط الترتيب).
                    // إذا انتهت الترجمة فورًا فقد أزالت نفسها من القائمة؛ لا تُضاف من جديد.
                    var box = reading.Box?.PlacedIn(area).CenteredOnScreen();
                    var task = TranslateAndForget(action.Text!, box, number, current);
                    if (!task.IsCompleted) translations[number] = task;
                    break;
            }
        }
    }

    /// <summary>ينتظر انتهاء كل الترجمات الجارية.</summary>
    public async Task WaitForTranslationsAsync()
    {
        while (true)
        {
            Task[] pending;
            lock (gate) pending = [.. translations.Values];
            if (pending.Length == 0) return;
            await Task.WhenAll(pending);
        }
    }

    /// <summary>مدة بقاء الترجمة على التلفزيون إذا وصلت بعد اختفاء السطر من الشاشة (القاعدة نفسها في تطبيق التلفزيون).</summary>
    public static int ReadingTime(string text) => Math.Min(8000, 2000 + (text.Length * 60));

    private async Task TranslateAndForget(string source, TextBox? box, int number, int current)
    {
        await Translate(source, box, number, current);
        lock (gate) translations.Remove(number);
    }

    private async Task Translate(string source, TextBox? box, int number, int current)
    {
        string translated;
        try
        {
            translated = await translator.TranslateAsync(source);
        }
        catch (Exception error)
        {
            lock (gate)
            {
                if (current != session) return;
                // إذا كان المترجم غير متاح أو حدث خطأ مؤقت تُعاد المحاولة ما دام السطر على الشاشة.
                LastError = (error as TranslatorException)?.Status;
                if (number == lineOnScreen) stabilizer.ForgetSent();
            }
            TranslationResult?.Invoke(LastError);
            return;
        }

        lock (gate)
        {
            if (current != session) return;
            LastError = null;
        }
        TranslationResult?.Invoke(null);

        lock (gate)
        {
            if (number <= lastShown || !stabilizer.ShouldSend(source, translated)) return;
            lastShown = number;
            // إذا كان السطر ما زال على الشاشة يُمسح عند اختفائه؛ وإن اختفى يُعرض مدة تكفي لقراءته.
            sender.SendSubtitle(translated, number == lineOnScreen ? 15000 : ReadingTime(translated), box);
            LastPair = (source, translated);
            LastBox = box;
        }
        PairShown?.Invoke(source, translated);
    }

    /// <summary>يزيل آخر ترجمة نصية من التلفزيون عند توقف القراءة.</summary>
    public void Reset()
    {
        lock (gate)
        {
            session++;
            lineOnScreen = null;
            lastReading = Reading.Empty;
            schedule.Reset();
            if (stabilizer.Reset() == StabilizerAction.Clear) sender.Clear();
        }
    }
}
