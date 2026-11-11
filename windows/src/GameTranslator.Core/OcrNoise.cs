// SPDX-License-Identifier: AGPL-3.0-only

using System.Globalization;
using System.Text.RegularExpressions;

namespace GameTranslator.Core;

/// <summary>
/// في الخطوط المزخرفة وفي الترجمة النصية الباهتة أو المهتزّة يحوّل التعرّف على النص بعض الكلمات إلى خليط عشوائي
/// من حروف وأرقام ورموز ("ou$3e"، "waters2aljd"). هذه ليست كلمات في أي لغة؛ والمترجم ينقلها
/// كما هي. تُحذف الأجزاء التي في وسطها رمز أو تتداخل فيها الحروف والأرقام؛
/// والشوائب الملتصقة بكلمة حقيقية ("waters2aljd") تُفصل عن الكلمة. الصيغ المألوفة مثل "PS5" و"R2" و"2nd" و"100m"
/// و"$500" تبقى كما هي.
/// </summary>
public static partial class OcrNoise
{
    private const string Symbols = "$@#%^*_=|\\~<>{}¢£¥§©®°±µ¶¤";

    public static string Clean(string line)
    {
        var kept = new List<string>();
        foreach (var token in line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var (lead, core, trail) = Split(token);
            if (CleanCore(core) is { } fixedCore)
                kept.Add(lead + fixedCore + trail);
            else if (trail.Length > 0 && kept.Count > 0)
                // علامة الترقيم بعد الشوائب المحذوفة تخص الجملة ("ou$3e," ← تبقى الفاصلة).
                kept[^1] += trail;
        }
        // رقم المؤشر المجاور للترجمة النصية يلتصق بأول السطر ("9? We'll lose", "100. Mac and John").
        // وكذلك علامة المسافة ("23m CESARE: Here!"). رقم منفرد تليه جملة تبدأ بحرف كبير: يُحذف الرقم.
        if (kept.Count >= 3 && LeadingNumber().IsMatch(kept[0]) && char.IsUpper(kept[1][0])) kept.RemoveAt(0);
        return string.Join(' ', kept);
    }

    [GeneratedRegex(@"^\d+(?:m|km|ft|yd)?[.?!:;]?$")]
    private static partial Regex LeadingNumber();

    /// <summary>
    /// مدى شبه القراءة بالكلمات: حروف الكلمات السليمة تُحسب بالموجب، والشوائب والأجزاء المختلطة الحروف الكبيرة
    /// والصغيرة ("CESAREiVJütt'h") بالسالب. لاختيار الأفضل من قراءتين للإطار نفسه.
    /// </summary>
    public static int Score(string text)
    {
        var score = 0;
        foreach (var token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var core = Split(token).Core;
            var letters = core.Count(char.IsLetter);
            if (letters == 0) continue;
            // القوس في وسط الكلمة ("the[lil") خلل قراءة أيضًا.
            var clean = CleanCore(core) == core && NormalCase(core) && core.IndexOfAny(['[', ']', '(', ')']) < 0;
            score += clean ? letters : -2 * letters;
        }
        return score;
    }

    /// <summary>"word", "WORD", "Word", "McDonald", "don't": الحرف الكبير في الأول فقط أو سلسلة من الأول.</summary>
    private static bool NormalCase(string core)
    {
        var letters = core.Where(char.IsLetter).ToArray();
        if (letters.All(char.IsUpper) || letters.Skip(1).All(char.IsLower)) return true;
        // حرف كبير واحد في الداخل ("McDonald", "iPhone") مألوف؛ وما زاد عليه خلل من OCR.
        return letters.Skip(1).Count(char.IsUpper) == 1 && letters.Count(char.IsLower) >= 2;
    }

    /// <summary>الكلمة نفسها؛ وnull إذا كانت شوائب كلها.</summary>
    private static string? CleanCore(string core)
    {
        var letters = core.Count(char.IsLetter);
        var digits = core.Count(char.IsNumber);
        var hasSymbol = core.Any(c => Symbols.Contains(c));
        if (letters == 0) return core;  // "24", "$500", "…"
        if (!hasSymbol && digits == 0) return core;
        if (!hasSymbol && IsUsualMix(core)) return core;
        // شوائب ملتصقة بكلمة حقيقية: تبقى الحروف الأولى إن كانت 3 على الأقل ("waters2aljd" → "waters").
        var prefix = new string(core.TakeWhile(c => char.IsLetter(c) || c is '\'' or '’' or '-').ToArray());
        return prefix.Count(char.IsLetter) >= 3 ? prefix : null;
    }

    /// <summary>"PS5", "R2", "MK2", "2nd", "100m", "4K", "60fps", "x2", "1980s", "R2-D2": أخلاط مألوفة من الحروف والأرقام.</summary>
    private static bool IsUsualMix(string core) =>
        core.Split('-').All(part => !part.Any(char.IsNumber) || Usual().IsMatch(part));

    [GeneratedRegex(@"^(?:[A-Za-z]{1,4}\d{1,4}|\d+(?:st|nd|rd|th|s|k|K|m|km|kg|cm|mm|p|x|fps|hp|HP|XP|xp|v|V|D|d)|[xX]\d{1,3}|\d+[.,]\d+[A-Za-z]{0,3}|\d{1,2}[:.]\d{2}(?:am|pm|AM|PM)?)$")]
    private static partial Regex Usual();

    private static bool IsEdge(char c) =>
        !Symbols.Contains(c) && (char.IsPunctuation(c) || char.IsSymbol(c) || "\"'“”‘’«»()[]".Contains(c));

    private static (string Lead, string Core, string Trail) Split(string token)
    {
        int start = 0, end = token.Length;
        while (start < end && IsEdge(token[start])) start++;
        while (end > start && IsEdge(token[end - 1])) end--;
        return (token[..start], token[start..end], token[end..]);
    }
}
