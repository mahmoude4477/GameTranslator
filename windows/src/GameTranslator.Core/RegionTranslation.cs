// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Core;

/// <summary>
/// يقرأ منطقة ويترجمها مرة واحدة. مخصَّص للنصوص التي لا تمرّ كالترجمة النصية، مثل لوحة المهام أو المعلومات:
/// لا تُمسح المنطقة باستمرار، بل تُقرأ مرة واحدة عندما يضغط المستخدم زرًّا.
/// </summary>
public static class RegionTranslation
{
    /// <param name="Source">النص المقروء (مقسَّمًا إلى كتل، كل كتلة في سطر مستقل).</param>
    /// <param name="Translation">ترجمة الكتل نفسها؛ الكتل ذات علامة البند تبدأ بـ"• ".</param>
    /// <param name="Box">موضع النص في الإطار، نسبةً إلى الحواف؛ يضع التلفزيون الترجمة هنا.</param>
    /// <param name="Structured">يوجد أكثر من كتلة (عنوان، بنود...): يجب عرض الترجمة سطرًا سطرًا بمحاذاة بداية السطر.</param>
    public sealed record Result(string Source, string Translation, TextBox Box, bool Structured = false);

    /// <summary>فقرة أو عنوان أو بند في اللوحة. <paramref name="Bullet"/> يساوي true إذا بدأ بعلامة بند (تُحذف العلامة من النص).</summary>
    public sealed record Block(string Text, bool Bullet);

    /// <summary>الرموز المستخدمة علاماتٍ للبنود.</summary>
    private const string BulletChars = "◇◆◈◊♦♢•●○◦▪■□▫►▶‣∙·*-–—✓✔✦❖◉⬥";

    /// <summary>يعيد null إذا لم يكن في المنطقة نص. إذا تعذّر استخدام المترجم يرمي <see cref="TranslatorException"/>.</summary>
    public static async Task<Result?> TranslateOnceAsync(
        Frame frame, CropRegion region, ITextReader reader, ITextTranslator translator, CancellationToken cancellationToken = default)
    {
        var area = RegionCropper.Crop(frame, region);
        // لأنها قراءة واحدة فالقراءة «العميقة» مفعّلة: تُجرَّب أيضًا القراءة المكبَّرة في المنطقة التي تبدو فارغة.
        var reading = (await Task.Run(() => reader.ReadLines(area, true), cancellationToken).ConfigureAwait(false)).WithoutButtonPrompts();
        if (reading.Lines.Count == 0) return null;

        var whole = new TextBox(region.Left, region.Top, region.Right, region.Bottom);
        var box = reading.Box?.PlacedIn(whole) ?? whole;
        var blocks = Blocks(reading, area.Width, area.Height);
        var translated = new List<string>(blocks.Count);
        foreach (var block in blocks)
        {
            var text = await translator.TranslateAsync(block.Text, cancellationToken).ConfigureAwait(false);
            translated.Add(block.Bullet ? "• " + text : text);
        }
        return new Result(string.Join('\n', blocks.Select(block => block.Text)), string.Join('\n', translated), box, blocks.Count > 1);
    }

    /// <summary>نصوص الكتل (بعد حذف علامات البنود).</summary>
    public static IReadOnlyList<string> Paragraphs(Reading reading) => [.. Blocks(reading).Select(block => block.Text)];

    /// <summary>
    /// يجمع الأسطر في كتل؛ فيُترجم العنوان والوصف والبنود في لوحة المهام كلٌّ على حدة.
    /// تبدأ كتلة جديدة: إذا بدأ السطر بعلامة بند؛ إذا أنهى السطر السابق الجملة؛ إذا وُجد بين السطرين فراغ واضح؛
    /// إذا برز السطر إلى اليسار بوضوح (كبند جديد بعد سطر التتمة المُزاح لبند سابق).
    /// السطر الذي يبدأ بحرف صغير تتمة لسابقه ما لم تنتهِ الجملة السابقة (جملة مقسومة على سطرين).
    /// </summary>
    /// <param name="width">عرض المساحة المقروءة (بالبكسل)؛ ولأن المربّعات نسبية فهو يوحّد وحدة القياس الأفقي والرأسي.</param>
    /// <param name="height">ارتفاع المساحة المقروءة (بالبكسل).</param>
    public static IReadOnlyList<Block> Blocks(Reading reading, double width = 1, double height = 1)
    {
        var heights = reading.Lines.Where(line => line.Box is not null)
            .Select(line => (line.Box!.Value.Bottom - line.Box.Value.Top) * height).OrderBy(value => value).ToList();
        var typical = heights.Count == 0 ? 0 : heights[heights.Count / 2];

        var result = new List<Block>();
        var current = new System.Text.StringBuilder();
        var currentBullet = false;
        TextLine? previous = null;
        foreach (var line in reading.Lines)
        {
            var (bullet, text) = StripBullet(line.Text.Trim());
            if (text.Length == 0) continue;
            if (previous is not null && current.Length > 0)
            {
                if (bullet || StartsNewBlock(previous, line, text, typical, width, height))
                {
                    result.Add(new Block(current.ToString(), currentBullet));
                    current.Clear();
                    currentBullet = bullet;
                }
                else
                {
                    current.Append(' ');
                }
            }
            else
            {
                currentBullet = bullet;
            }
            current.Append(text);
            previous = line;
        }
        if (current.Length > 0) result.Add(new Block(current.ToString(), currentBullet));
        return result;
    }

    private static bool StartsNewBlock(TextLine upper, TextLine lower, string lowerText, double typicalHeight, double width, double height)
    {
        var finished = SubtitleStabilizer.EndsSentence(upper.Text);
        // إذا لم تنتهِ الجملة وبدأ السطر بحرف صغير فهو سطر تتمة.
        if (!finished && char.IsLower(lowerText[0])) return false;
        if (finished) return true;
        if (upper.Box is not { } a || lower.Box is not { } b || typicalHeight <= 0) return false;
        // إذا زاد الفراغ بين السطرين على 40% من ارتفاع السطر فهما كتلتان منفصلتان.
        if (((b.Top - a.Bottom) * height) > typicalHeight * 0.4) return true;
        // إذا برز إلى اليسار بأكثر من نصف ارتفاع السطر (بند جديد بعد سطر تتمة مُزاح) فهو كتلة جديدة.
        return ((a.Left - b.Left) * width) > typicalHeight * 0.5;
    }

    /// <summary>يفصل علامة البند في أول السطر. يقرأ التعرّف على النص الرمز ◇ غالبًا على شكل "<>" أو "o"/"0" منفردة.</summary>
    private static (bool Bullet, string Text) StripBullet(string text)
    {
        if (text.Length >= 2 && BulletChars.Contains(text[0])) return (true, text[1..].TrimStart());
        if (text.StartsWith("<>", StringComparison.Ordinal)) return (true, text[2..].TrimStart());
        if (text.Length > 2 && text[1] == ' ' && text[0] is 'o' or '0' && char.IsUpper(text[2])) return (true, text[2..]);
        return (false, text);
    }

    /// <summary>مدة بقاء الترجمة على التلفزيون (ms): نص اللوحة الطويل يبقى مدة تكفي لقراءته.</summary>
    public static int DisplayMilliseconds(string text) => Math.Clamp(4000 + (text.Length * 70), 6000, 25000);
}
