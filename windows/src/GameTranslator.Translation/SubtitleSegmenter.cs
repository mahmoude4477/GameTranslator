// SPDX-License-Identifier: AGPL-3.0-only

using System.Text;
using System.Text.RegularExpressions;

namespace GameTranslator.Translation;

/// <summary>
/// يسلّم الترجمة النصية إلى المترجم جملةً جملة. في التجربة، حين تُرجم النص كله دفعة واحدة أسقط المترجم كثيرًا
/// الجمل القصيرة ("Boy!", "What is that?") والسطر الثاني؛ أما الترجمة جملةً جملة فلم تُضِع شيئًا منها.
/// لا تُمَس تسمية المتكلم ("ATREUS:", "Kratos:") ولا شرطات الحوار، لأن المترجم يحذفها أو
/// يترجمها.
/// </summary>
internal static partial class SubtitleSegmenter
{
    /// <summary>جزء يُترجَم، أو فاصل/تسمية تبقى كما هي.</summary>
    internal readonly record struct Piece(string Text, bool Translate);

    // التسمية: في بداية السطر أو الجملة، اسم من ثلاث كلمات على الأكثر تليه نقطتان. إما كله بأحرف كبيرة ("ATREUS:", "OLD MAN:")
    // أو تبدأ كل كلمة فيه بحرف كبير ("Kratos:", "Mimir Head:").
    [GeneratedRegex(@"^(?<label>(?<name>\p{Lu}[\p{Lu}\d'’.\-]+(?:[ ]\p{Lu}[\p{Lu}\d'’.\-]*){0,2}|\p{Lu}\p{Ll}[\p{L}'’.\-]*(?:[ ]\p{Lu}[\p{L}'’.\-]*){0,2}):)\s+")]
    private static partial Regex SpeakerLabelPattern();

    /// <summary>عناوين في اللعبة تبدأ بحرف كبير وتنتهي بنقطتين لكنها ليست أسماء؛ وهذه تُترجَم.</summary>
    private static readonly HashSet<string> NotNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "note", "warning", "tip", "hint", "objective", "objectives", "quest", "goal", "new", "reward", "rewards",
        "location", "chapter", "mission", "task", "tutorial", "update", "info", "status", "level", "controls",
        "press", "hold", "tap", "remember", "example", "question", "answer", "result", "results", "time",
        "listen", "look", "wait", "okay", "ok", "well", "so", "yes", "no", "now", "then", "and", "but",
    };

    /// <summary>تسمية المتكلم في بداية السطر؛ العناوين ذات الكلمة الواحدة ("Note:", "Objective:") لا تُعد أسماء.</summary>
    private static Match? SpeakerLabel(string text)
    {
        var match = SpeakerLabelPattern().Match(text);
        if (!match.Success) return null;
        var name = match.Groups["name"].Value;
        return !name.Contains(' ') && NotNames.Contains(name) ? null : match;
    }

    /// <summary>الاسم الموجود في جزء التسمية ("ATREUS: " → "ATREUS")؛ و null إن لم يكن تسمية.</summary>
    public static string? LabelName(Piece piece) =>
        !piece.Translate && piece.Text.EndsWith(": ", StringComparison.Ordinal) ? piece.Text[..^2] : null;

    [GeneratedRegex(@"^(?<dash>[-–—]+)\s*")]
    private static partial Regex DialogueDash();

    // نهاية الجملة: . ! ? … (قد تتكرر)، تليها اختياريًا علامة اقتباس/قوس إغلاق ثم فراغ.
    [GeneratedRegex(@"(?<=[.!?…]+[""'”’)\]]*)\s+")]
    private static partial Regex SentenceGap();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>اختصارات لا تنتهي الجملة بعد نقطتها ("Mr. Smith").</summary>
    private static readonly HashSet<string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        "mr", "mrs", "ms", "dr", "st", "jr", "sr", "prof", "mt", "vs", "etc", "e.g", "i.e", "lt", "sgt", "capt", "gen", "col",
    };

    public static List<Piece> Split(string text)
    {
        var pieces = new List<Piece>();
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var block = new StringBuilder();
        var pendingSeparator = "";

        void FlushBlock()
        {
            if (block.Length == 0) return;
            if (pendingSeparator.Length > 0) pieces.Add(new Piece(pendingSeparator, false));
            AddBlock(pieces, block.ToString());
            block.Clear();
            pendingSeparator = "\n";
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            // السطر ذو التسمية أو الشرطة كلام جديد؛ والأسطر الأخرى التفاف للسطر السابق فتُدمج.
            if (SpeakerLabel(line) is not null || DialogueDash().IsMatch(line)) FlushBlock();
            if (block.Length > 0) block.Append(' ');
            block.Append(line);
        }
        FlushBlock();
        return pieces;
    }

    private static void AddBlock(List<Piece> pieces, string block)
    {
        block = Whitespace().Replace(block, " ");
        var dash = DialogueDash().Match(block);
        if (dash.Success)
        {
            pieces.Add(new Piece(dash.Groups["dash"].Value + " ", false));
            block = block[dash.Length..];
        }

        var first = true;
        foreach (var sentence in SplitSentences(block))
        {
            if (!first) pieces.Add(new Piece(" ", false));
            first = false;
            var body = sentence;
            if (SpeakerLabel(body) is { } label)
            {
                pieces.Add(new Piece(label.Groups["label"].Value + " ", false));
                body = body[label.Length..];
            }
            if (body.Length > 0) pieces.Add(new Piece(body, HasLetter(body)));
        }
    }

    private static IEnumerable<string> SplitSentences(string block)
    {
        var start = 0;
        foreach (Match gap in SentenceGap().Matches(block))
        {
            var candidate = block[start..gap.Index];
            var next = gap.Index + gap.Length < block.Length ? block[gap.Index + gap.Length] : '\0';
            // الموضع الذي يستمر بحرف صغير مثل "wait... what" والاختصارات ليست نهاية جملة.
            if (char.IsLower(next) || EndsWithAbbreviation(candidate)) continue;
            yield return candidate;
            start = gap.Index + gap.Length;
        }
        if (start < block.Length) yield return block[start..];
    }

    private static bool EndsWithAbbreviation(string sentence)
    {
        if (!sentence.EndsWith('.')) return false;
        var lastSpace = sentence.LastIndexOf(' ');
        var word = sentence[(lastSpace + 1)..^1];
        // الحرف المفرد هو الحرف الأول من اسم ("J. R. R.").
        return (word.Length == 1 && char.IsUpper(word[0])) || Abbreviations.Contains(word);
    }

    /// <summary>
    /// يحوّل الجملة المكتوبة كلها بأحرف كبيرة ("THE GATE WILL NOT HOLD.") إلى حالة الجملة العادية؛ فالمترجم كان
    /// يظن الكلمات ذات الأحرف الكبيرة أسماء فيتركها دون ترجمة ("GATE لن يصمد أكثر"). الأسماء المعروفة (المستخلصة
    /// من تسميات المتكلمين) تبدأ بحرف كبير. الكلمة المفردة لا تُمَس.
    /// </summary>
    public static string NormalizeCase(string sentence, IReadOnlySet<string>? names = null)
    {
        var letters = 0;
        foreach (var c in sentence)
        {
            if (char.IsLower(c)) return sentence;
            if (char.IsLetter(c)) letters++;
        }
        if (letters < 2 || !sentence.Trim().Contains(' ')) return sentence;

        var output = new StringBuilder(sentence.Length);
        var word = new StringBuilder();
        var startOfSentence = true;

        void FlushWord()
        {
            if (word.Length == 0) return;
            var lower = word.ToString().ToLowerInvariant();
            // في الإنجليزية تُكتب "I" بحرف كبير دائمًا.
            var capital = startOfSentence || lower == "i" || (names?.Contains(lower) ?? false);
            output.Append(capital ? char.ToUpperInvariant(lower[0]) + lower[1..] : lower);
            startOfSentence = false;
            word.Clear();
        }

        foreach (var c in sentence)
        {
            if (char.IsLetter(c) || c is '\'' or '’')
            {
                word.Append(c);
                continue;
            }
            FlushWord();
            if (c is '.' or '!' or '?' or '…') startOfSentence = true;
            output.Append(c);
        }
        FlushWord();
        return output.ToString();
    }

    /// <summary>الجزء الخالي من الحروف ("...", "?!", "3") لا يُرسل إلى المترجم؛ ويبقى كما هو.</summary>
    private static bool HasLetter(string text)
    {
        foreach (var c in text)
            if (char.IsLetter(c)) return true;
        return false;
    }
}
