// SPDX-License-Identifier: AGPL-3.0-only

using System.Globalization;
using System.Text;

namespace GameTranslator.Core;

/// <summary>تبسيط وتشابه يتحمّل الضجيج لمقارنة مخرجات OCR.</summary>
public static class TextMatch
{
    /// <summary>حروف صغيرة، حروف/أرقام فقط، مسافة واحدة.</summary>
    public static string Normalize(string s)
    {
        var builder = new StringBuilder(s.Length);
        foreach (var c in s.ToLowerInvariant())
            builder.Append(IsAlphanumeric(c) ? c : ' ');
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>يُعدّان متشابهين إذا بلغت نسبة Levenshtein <paramref name="threshold"/> أو تجاوزتها. النص الفارغ لا يشبه أي نص.</summary>
    public static bool IsSimilar(string a, string b, double threshold = 0.85)
    {
        if (a == b) return a.Length > 0;
        if (a.Length == 0 || b.Length == 0) return false;
        var previous = new int[b.Length + 1];
        var row = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) previous[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            row[0] = i;
            for (var j = 1; j <= b.Length; j++)
                row[j] = Math.Min(Math.Min(previous[j] + 1, row[j - 1] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, row) = (row, previous);
        }
        return 1 - (double)previous[b.Length] / Math.Max(a.Length, b.Length) >= threshold;
    }

    /// <summary>
    /// هل القراءة الجديدة إعادة قراءة للترجمة النصية المرسَلة (نصوص مبسَّطة)؟ في الخط المزخرف أو أثناء البهتان
    /// يحوّل OCR كلمة أو كلمتين إلى شوائب مختلفة في كل إطار ("ou$3e"، "waters2aljd") أو يفوّت سطرًا من ترجمة نصية
    /// من سطرين؛ فتبقى الكلمات نفسها وإن انخفض تشابه الأحرف. الكلمات التي خالطتها أرقام والحروف المنفردة
    /// تُعدّ شوائب ولا تدخل المقارنة. إذا طال النص بوضوح (تأثير الآلة الكاتبة) فهو سطر جديد.
    /// </summary>
    public static bool IsSameSubtitle(string reread, string sent)
    {
        if (IsSimilar(reread, sent)) return true;
        var x = CleanWords(reread);
        var remaining = CleanWords(sent);
        var sentCount = remaining.Count;
        var shared = 0;
        foreach (var word in x)
            if (remaining.Remove(word)) shared++;
        // سطر فائت: كلمات القراءة الجديدة كلها تقريبًا موجودة في المرسَل.
        if (x.Count >= 4 && x.Count <= sentCount && shared >= x.Count * 0.85) return true;
        // في السطر القصير كلمة واحدة تغيّر المعنى ("where is the boy" / "ship")؛ هناك يُعتمد تشابه الأحرف وحده.
        int longer = Math.Max(x.Count, sentCount), shorter = Math.Min(x.Count, sentCount);
        if (shorter < 6 || (double)shorter / longer < 0.75) return false;
        return (double)shared / longer >= 0.75;
    }

    // الحروف المنفردة ("a" و"I" أو "O" التي يختلقها OCR) لا تضيف شيئًا إلى المقارنة.
    private static List<string> CleanWords(string normalized) =>
        normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 1 && !w.Any(char.IsNumber)).ToList();

    private static bool IsAlphanumeric(char c) => char.GetUnicodeCategory(c) switch
    {
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
            or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
            or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber => true,
        _ => false,
    };
}
