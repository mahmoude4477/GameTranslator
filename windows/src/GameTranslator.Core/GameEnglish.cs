// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.RegularExpressions;

namespace GameTranslator.Core;

/// <summary>
/// العبارات الشائعة في الألعاب التي قد يترجمها المترجم الآلي حرفيًّا ("I've got your six") تُحوَّل قبل الترجمة
/// إلى إنجليزية بسيطة بالمعنى نفسه. القائمة تقتصر على العبارات التي تحسّن الترجمة؛
/// وليس فيها ما يُترجم صحيحًا أصلًا ("Copy that", "Stand down").
/// </summary>
public static partial class GameEnglish
{
    private static readonly (Regex Pattern, string Plain)[] Phrases =
    [
        (Phrase("friendlies"), "allies"),
        (Phrase("(?:I've|I) got your six"), "I've got your back"),
        (Phrase("check your six"), "watch behind you"),
        (Phrase("heads up"), "watch out"),
        (Phrase("took you long enough"), "you're late"),
        (Phrase("we've got company"), "enemies are coming"),
        (Phrase("lock and load"), "get ready"),
        (Phrase("sitting ducks"), "easy targets"),
        (Phrase("moving in"), "going in"),
    ];

    private static Regex Phrase(string words) =>
        new($@"\b{words}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>تُحوَّل العبارات المعروفة إلى إنجليزية بسيطة؛ ويبقى الحرف الكبير في أول الجملة.</summary>
    public static string Plain(string sentence)
    {
        foreach (var (pattern, plain) in Phrases)
            sentence = pattern.Replace(sentence, match => char.IsUpper(match.Value[0]) ? char.ToUpperInvariant(plain[0]) + plain[1..] : plain);
        return sentence;
    }
}
