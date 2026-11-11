// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Core;

/// <summary>
/// «ترجِم الشاشة»: يُقرأ الإطار كله مرة واحدة، وتُقسم النصوص إلى مجموعات بحسب مواضعها (قائمة، لوحة مهام، مربّع
/// معلومات، ترجمة نصية...) وتُترجم كل مجموعة على حدة. يضع التلفزيون كل ترجمة فوق نصها الإنجليزي وبحجم ذلك
/// النص. لا يعمل باستمرار؛ يُنفَّذ مرة واحدة عندما يضغط المستخدم الزر.
/// </summary>
public static class ScreenTranslation
{
    /// <summary>مجموعة نصوص على الشاشة.</summary>
    /// <param name="Paragraphs">أجزاء المجموعة التي تُترجم كلٌّ على حدة (عنوان، جملة، سطر قائمة...).</param>
    /// <param name="Box">موضع المجموعة في الإطار، نسبةً إلى الحواف.</param>
    /// <param name="LineHeight">الارتفاع المعتاد لأسطرها نسبةً إلى ارتفاع الإطار؛ يختار التلفزيون حجم الخط وفقًا له.</param>
    public sealed record Group(IReadOnlyList<string> Paragraphs, TextBox Box, double LineHeight);

    /// <param name="Translation">ترجمة الأجزاء، كل جزء في سطر مستقل.</param>
    public sealed record Item(string Source, string Translation, TextBox Box, double LineHeight)
    {
        /// <summary>يوجد أكثر من جزء: يعرضها التلفزيون سطرًا سطرًا بمحاذاة بداية السطر.</summary>
        public bool Paragraphs => Translation.Contains('\n');
    }

    /// <summary>أقصى عدد من المجموعات يُترجم، حتى لا يمتلئ التلفزيون بالمربّعات ولا تطول الترجمة في الشاشات المزدحمة.</summary>
    public const int MaxGroups = 30;

    /// <summary>
    /// يقرأ الإطار ويترجم مجموعاته بالترتيب. بعد ترجمة كل مجموعة يُستدعى <paramref name="progress"/> بكل النتائج
    /// حتى تلك اللحظة؛ فيمتلئ التلفزيون كلما جهزت الترجمات. المجموعة التي تطابق ترجمتُها أصلَها
    /// (اسم، رقم، نص بلغة الترجمة أصلًا) لا تُعرض. إذا تعذّر استخدام المترجم يرمي <see cref="TranslatorException"/>.
    /// </summary>
    public static async Task<IReadOnlyList<Item>> TranslateAsync(
        Frame frame, ITextReader reader, ITextTranslator translator,
        Action<IReadOnlyList<Item>>? progress = null, CancellationToken cancellationToken = default)
    {
        // في الإطار الصغير (720p) يكون ارتفاع نصوص الزوايا بضعة بكسلات؛ والتكبير يجعل التعرّف على النص يجدها أيضًا.
        var reading = await Task.Run(() => reader.ReadLines(frame.Height < 900 ? frame.Scaled(2) : frame, false), cancellationToken)
            .ConfigureAwait(false);
        var items = new List<Item>();
        foreach (var group in Groups(reading))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var translated = new List<string>(group.Paragraphs.Count);
            var changed = false;
            foreach (var paragraph in group.Paragraphs)
            {
                var text = (await translator.TranslateAsync(paragraph, cancellationToken).ConfigureAwait(false)).Trim();
                if (text.Length == 0) text = paragraph;
                changed |= !TextMatch.IsSimilar(TextMatch.Normalize(paragraph), TextMatch.Normalize(text), 0.90);
                translated.Add(text);
            }
            if (!changed) continue;
            items.Add(new Item(string.Join('\n', group.Paragraphs), string.Join('\n', translated), group.Box, group.LineHeight));
            progress?.Invoke([.. items]);
        }
        return items;
    }

    /// <summary>
    /// يجمع الأسطر في مجموعات. تُحذف تلميحات ذراع التحكم وشوائب التعرّف على النص؛ والأسطر التي بلا حروف أو بحرف واحد
    /// ("23m"، "9"، "+") مؤشرات لا تُترجم. ينضم السطر إلى المجموعة إذا كان تحت سطرها الأخير مباشرة ومتراكبًا معه
    /// أفقيًا وبحجم خط مقارب. المجموعات من الأعلى إلى الأسفل، وبحدّ أقصى <see cref="MaxGroups"/>.
    /// </summary>
    public static IReadOnlyList<Group> Groups(Reading reading)
    {
        var lines = reading.WithoutButtonPrompts().WithoutOcrNoise().Lines
            .Where(line => line.Box is not null && IsWords(line.Text))
            .OrderBy(line => line.Box!.Value.Top)
            .ToList();
        var groups = new List<List<TextLine>>();
        foreach (var line in lines)
        {
            var index = groups.FindLastIndex(group => Belongs(line.Box!.Value, group[^1].Box!.Value));
            if (index >= 0) groups[index].Add(line);
            else groups.Add([line]);
        }
        return [.. groups.Take(MaxGroups).Select(group =>
        {
            var heights = group.Select(line => line.Box!.Value.Bottom - line.Box!.Value.Top).OrderBy(value => value).ToList();
            var box = group.Select(line => line.Box!.Value).Aggregate((a, b) => a.Union(b));
            return new Group(Paragraphs(group), box, heights[heights.Count / 2]);
        })];
    }

    /// <summary>السطر السفلي تحت العلوي مباشرة (الفاصل أقل من 80% من ارتفاع السطر)، متراكب معه أفقيًا وبحجم خط مقارب.</summary>
    private static bool Belongs(TextBox lower, TextBox upper)
    {
        double a = upper.Bottom - upper.Top, b = lower.Bottom - lower.Top;
        if (Math.Max(a, b) > Math.Min(a, b) * 1.6) return false;
        if (lower.Top - upper.Bottom >= Math.Max(a, b) * 0.8 || lower.Top < upper.Top) return false;
        var overlap = Math.Min(lower.Right, upper.Right) - Math.Max(lower.Left, upper.Left);
        var narrower = Math.Min(lower.Right - lower.Left, upper.Right - upper.Left);
        return overlap > 0 && overlap >= narrower * 0.5;
    }

    /// <summary>
    /// يقسم أسطر المجموعة إلى أجزاء تُترجم كلٌّ على حدة. يبدأ جزء جديد إذا أنهى السطر السابق الجملة، أو كان سطر
    /// عنوان/قائمة قصيرًا (ثلاث كلمات على الأكثر بلا علامات ترقيم) والسطر الجديد يبدأ بحرف كبير؛ وإلا فالسطر
    /// تتمة لسابقه (جملة ملتفّة على سطرين).
    /// </summary>
    internal static IReadOnlyList<string> Paragraphs(IReadOnlyList<TextLine> lines)
    {
        var result = new List<string>();
        foreach (var line in lines.Select(line => line.Text.Trim()))
        {
            if (result.Count > 0 && !StartsNew(result[^1], line)) result[^1] += " " + line;
            else result.Add(line);
        }
        return result;
    }

    private static bool StartsNew(string previous, string line) =>
        SubtitleStabilizer.EndsSentence(previous) ||
        (char.IsUpper(line[0]) && WordCount(previous) <= 3 && !previous.EndsWith(','));

    /// <summary>السطر الذي فيه حرفان على الأقل وكلمة من حرفين نصٌّ؛ والباقي أرقام ورموز.</summary>
    internal static bool IsWords(string text) =>
        text.Count(char.IsLetter) >= 2 &&
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Any(word => word.Count(char.IsLetter) >= 2);

    private static int WordCount(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>مدة بقاء الترجمات على التلفزيون (ms): أطول كلما كثر النص؛ وتزول فورًا عند ضغط الزر من جديد.</summary>
    public static int DisplayMilliseconds(IReadOnlyList<Item> items) =>
        Math.Clamp(8000 + (items.Sum(item => item.Translation.Length) * 50), 12000, 60000);
}
