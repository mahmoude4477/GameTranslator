// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Core;

/// <summary>موضع النص نسبةً إلى حواف المساحة التي يقع فيها (0–1، من الأعلى ومن اليسار).</summary>
public readonly record struct TextBox(double Left, double Top, double Right, double Bottom)
{
    public TextBox Union(TextBox other) =>
        new(Math.Min(Left, other.Left), Math.Min(Top, other.Top), Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));

    /// <summary>إذا كان هذا المربّع نسبًا داخل <paramref name="area"/> فهذه نسب الموضع نفسه في الإطار الذي يحوي <paramref name="area"/>.</summary>
    public TextBox PlacedIn(TextBox area)
    {
        var width = area.Right - area.Left;
        var height = area.Bottom - area.Top;
        return new(area.Left + (Left * width), area.Top + (Top * height), area.Left + (Right * width), area.Top + (Bottom * height));
    }

    /// <summary>
    /// مربّع الترجمة النصية الواقعة وسط الشاشة يُوسَّع بالتناظر حول المنتصف. على خلفية فاتحة أو مع نص ملوّن قد لا يقرأ
    /// التعرّف على النص أحد طرفي السطر؛ وإذا غطّى المربّع الجزء المقروء فقط ظهر باقي الإنجليزية جانبًا
    /// وبدت الترجمة غير موسَّطة. النص البعيد عن المنتصف (في الزاوية) لا يُمسّ.
    /// </summary>
    public TextBox CenteredOnScreen()
    {
        var middle = (Left + Right) / 2;
        if (middle <= 0.3 || middle >= 0.7) return this;
        return this with { Left = Math.Max(0, Math.Min(Left, 1 - Right)), Right = Math.Min(1, Math.Max(Right, 1 - Left)) };
    }
}

/// <summary>سطر وجده التعرّف على النص؛ المربّع نسبةً إلى المساحة المقروءة، وnull إن لم يُعرف.</summary>
public sealed record TextLine(string Text, TextBox? Box = null);

/// <summary>الأسطر المقروءة من إطار واحد، من الأعلى إلى الأسفل.</summary>
public sealed class Reading
{
    public static readonly Reading Empty = new([]);

    public Reading(IEnumerable<TextLine> lines) =>
        Lines = [.. lines.Where(line => !string.IsNullOrWhiteSpace(line.Text))];

    public IReadOnlyList<TextLine> Lines { get; }

    /// <summary>للقارئات التي لا تعطي الأسطر منفصلة: كل نهاية سطر تفصل سطرًا.</summary>
    public static Reading FromText(string text) =>
        new(text.Replace("\r\n", "\n").Split('\n').Select(line => new TextLine(line.Trim())));

    public string Text => string.Join('\n', Lines.Select(line => line.Text));

    /// <summary>
    /// هل تدل الفجوة (بالبكسل) بين كلمتين في السطر نفسه على نصّين منفصلين؟ فقط إذا تجاوزت أربعة أضعاف ارتفاع السطر
    /// و12% من عرض الصورة معًا: هكذا تُفصل القائمة على اليسار عن المؤشر على اليمين.
    /// الفجوة الأضيق غالبًا كلمات لم تُقرأ (في الترجمة النصية الرفيعة على خلفية فاتحة يتخطى التعرّف على النص بعض
    /// الكلمات)؛ والتقسيم هناك يفتّت الجملة فتفقد ترجمة الجزء معناها ولا يغطي مربّعه السطر (GTA V).
    /// </summary>
    public static bool IsSeparateText(double gap, double lineHeight, double imageWidth) =>
        gap > lineHeight * 4 && gap > imageWidth * 0.12;

    /// <summary>المربّع الذي يحوي كل الأسطر؛ null إذا لم يُعرف موضع أي سطر.</summary>
    public TextBox? Box
    {
        get
        {
            TextBox? total = null;
            foreach (var line in Lines)
                if (line.Box is { } box) total = total?.Union(box) ?? box;
            return total;
        }
    }

    /// <summary>القراءة بعد حذف تلميحات أزرار اللعبة ("R2 Swing"، "△ Pick up")؛ فهي ليست ترجمة نصية.</summary>
    public Reading WithoutButtonPrompts() => new(Lines.Where(line => !ButtonPrompts.IsPrompt(line.Text)));

    /// <summary>
    /// القراءة بعد تنقيتها من الكلمات العشوائية التي يختلقها التعرّف على النص ("ou$3e"، "waters2aljd")؛ كان المترجم
    /// ينقلها كما هي فتظهر على التلفزيون حروف بلا معنى.
    /// </summary>
    public Reading WithoutOcrNoise() => new(Lines.Select(line => line with { Text = OcrNoise.Clean(line.Text) }));

    /// <summary>
    /// كتلة الترجمة النصية في المنطقة. كانت تُقرأ في المنطقة المحددة مع الترجمة النصية قائمةٌ ("MAP / HELP / SETTINGS"
    /// تحت بعضها) ومؤشرُ الزاوية ("9"، "PHOTO MODE")؛ فيُترجم الكل كجملة واحدة ويُعرض على التلفزيون في مربّع يملأ
    /// المنطقة كلها. تُقسم الأسطر إلى كتل بحسب مواضعها؛ وأسفل كتلة موسَّطة ليست قائمة هي الترجمة النصية.
    /// إن لم توجد كتلة موسَّطة تؤخذ أسفل كتلة من ثلاث كلمات على الأقل ليست قائمة. إذا لم تُعرف مواضع الأسطر
    /// تبقى القراءة كما هي. <paramref name="area"/> هو موضع المساحة المقروءة في الإطار.
    /// </summary>
    public Reading SubtitleBlock(TextBox area)
    {
        if (Lines.Count == 0 || Lines.Any(line => line.Box is null)) return this;
        var blocks = new List<List<TextLine>>();
        foreach (var line in Lines.OrderBy(line => line.Box!.Value.Top))
        {
            var box = line.Box!.Value.PlacedIn(area);
            var index = blocks.FindLastIndex(block => Belongs(box, block, area));
            if (index >= 0) blocks[index].Add(line);
            else blocks.Add([line]);
        }
        var candidates = blocks.Select(block => new Reading(block)).Where(reading => !reading.IsMenu).ToList();
        var centered = candidates.Where(reading =>
        {
            var box = reading.Box!.Value.PlacedIn(area);
            return Math.Abs(((box.Left + box.Right) / 2) - 0.5) <= 0.12;
        }).ToList();
        if (centered.Count > 0) return centered.MaxBy(reading => reading.Box!.Value.Bottom)!;
        return candidates.Where(reading => WordCount(reading.Text) >= 3).MaxBy(reading => reading.Box!.Value.Bottom) ?? Empty;
    }

    /// <summary>السطر الواقع تحت آخر سطر في الكتلة مباشرة والمتراكب معه أفقيًا ينتمي إلى تلك الكتلة.</summary>
    private static bool Belongs(TextBox box, List<TextLine> block, TextBox area)
    {
        var last = block[^1].Box!.Value.PlacedIn(area);
        var height = Math.Max(box.Bottom - box.Top, last.Bottom - last.Top);
        if (box.Top - last.Bottom >= height * 0.8 || box.Top < last.Top) return false;
        var overlap = Math.Min(box.Right, last.Right) - Math.Max(box.Left, last.Left);
        var narrower = Math.Min(box.Right - box.Left, last.Right - last.Left);
        return overlap > 0 && overlap >= narrower * 0.5;
    }

    /// <summary>الأسطر القصيرة المتتالية بلا علامات ترقيم ("MAP"، "HELP"، "SOCIAL CLUB") قائمة وليست حوارًا.</summary>
    internal bool IsMenu => Lines.Count >= 3 &&
        Lines.All(line => WordCount(line.Text) <= 3 && !SubtitleStabilizer.EndsSentence(line.Text));

    private static int WordCount(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>ينقل مربّعات الأسطر من النسب داخل <paramref name="area"/> إلى نسب الإطار الخارجي.</summary>
    public Reading PlacedIn(TextBox area) => new(Lines.Select(line => line with { Box = line.Box?.PlacedIn(area) }));
}

/// <summary>
/// يتعرّف على تلميحات ذراع التحكم التي تعرضها الألعاب على الشاشة: "R2 Swing"، "L1 + R1 Spartan Rage"، "△ Pick up"،
/// "Press ✕ to jump". أسطر الحوار تبقى دون مساس.
/// </summary>
public static class ButtonPrompts
{
    /// <summary>رموز وأسماء هي دائمًا أزرار ذراع التحكم (يقرؤها التعرّف على النص أحيانًا بين أقواس مربّعة).</summary>
    private static readonly HashSet<string> Buttons = new(StringComparer.OrdinalIgnoreCase)
    {
        "R1", "R2", "R3", "L1", "L2", "L3", "OPTIONS", "TOUCHPAD",
        "✕", "×", "⨯", "✖", "○", "◯", "●", "□", "▢", "◻", "■", "△", "▲", "⨂",
    };

    /// <summary>قد يتحول الرمز في التعرّف على النص إلى حرف؛ لا يُعدّ زرًّا إلا في صيغة التلميح.</summary>
    private static readonly HashSet<string> Lookalikes = new(StringComparer.Ordinal) { "X", "O" };

    private static readonly HashSet<string> Verbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "press", "hold", "tap", "push", "use", "mash", "release",
    };

    private static readonly char[] Separators = [' ', '\t', '+', '/'];
    private static readonly char[] Brackets = ['(', ')', '[', ']', '{', '}', '<', '>', '「', '」'];

    public static bool IsPrompt(string line)
    {
        var tokens = line.Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim(Brackets))
            .Where(token => token.Length > 0)
            .ToList();
        if (tokens.Count == 0) return false;
        var first = tokens[0];
        var words = tokens.Count(token => !Buttons.Contains(token) && token.Any(char.IsLetter));

        // "R2 Swing", "△ Pick up", "L1 R1 Spartan Rage"
        if (Buttons.Contains(first)) return words <= 5;
        // "PHOTO MODE L1 + R1": الزر في الآخر وقبله اسم فعل قصير بلا علامات ترقيم.
        if (Buttons.Contains(tokens[^1]) && words <= 4 && !SubtitleStabilizer.EndsSentence(line)) return true;
        // "X Interact", "O Back": يليه فعل قصير يبدأ بحرف كبير وبلا علامات ترقيم.
        if (Lookalikes.Contains(first) && tokens.Count is >= 2 and <= 4 && char.IsUpper(tokens[1][0]) &&
            !SubtitleStabilizer.EndsSentence(line))
            return true;
        // "Press ✕ to jump", "Hold R2 to aim"
        if (Verbs.Contains(first) && tokens.Count is >= 2 and <= 10)
            return Buttons.Contains(tokens[1]) || Lookalikes.Contains(tokens[1]);
        return false;
    }
}
