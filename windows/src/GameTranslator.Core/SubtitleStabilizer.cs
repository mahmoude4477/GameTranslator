// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Core;

public enum StabilizerKind { None, Translate, Clear }

/// <summary>ردّ فعل مسار القراءة على إطار واحد.</summary>
public readonly record struct StabilizerAction(StabilizerKind Kind, string? Text = null)
{
    public static readonly StabilizerAction None = new(StabilizerKind.None);
    public static readonly StabilizerAction Clear = new(StabilizerKind.Clear);
    public static StabilizerAction Translate(string text) => new(StabilizerKind.Translate, text);
}

/// <summary>
/// يتابع مخرجات OCR الواردة إطارًا بعد إطار؛ يطلب ترجمة الترجمة النصية عندما تستقر،
/// ومسحها من التلفزيون عندما تختفي من الشاشة. لا يرسل الترجمة النصية نفسها مرتين.
/// </summary>
public sealed class SubtitleStabilizer(double similarity = 0.85, int stableFrames = 2, int emptyFramesToClear = 2)
{
    private string candidate = "";
    private int candidateCount;
    private string sent = "";
    private int emptyCount;

    public StabilizerAction Feed(string rawText)
    {
        var text = TextMatch.Normalize(rawText);
        // القراءات الأقصر من 3 أحرف (ضجيج، رمز منفرد) تُعدّ إطارًا فارغًا.
        if (text.Length < 3)
        {
            candidate = "";
            candidateCount = 0;
            emptyCount++;
            if (emptyCount == emptyFramesToClear && sent.Length > 0)
            {
                sent = "";
                return StabilizerAction.Clear;
            }
            return StabilizerAction.None;
        }

        emptyCount = 0;
        if (TextMatch.IsSimilar(text, candidate, similarity))
        {
            candidateCount++;
        }
        else
        {
            candidate = text;
            candidateCount = 1;
        }
        // في تأثير الآلة الكاتبة يتغيّر المرشَّح مع نموّ النص؛ ويُرسل عندما يكتمل ويستقر.
        // السطر المنتهي بعلامة ترقيم مكتمل أصلًا؛ في الحوار السريع يُرسل من أول إطار دون انتظار.
        var ready = candidateCount == stableFrames || (candidateCount == 1 && EndsSentence(rawText));
        // إعادة القراءة المشوَّشة لما أُرسل ليست سطرًا جديدًا؛ حتى لا تظهر الجملة نفسها مرتين على التلفزيون.
        if (!ready || TextMatch.IsSimilar(text, sent, similarity) || TextMatch.IsSameSubtitle(text, sent)) return StabilizerAction.None;
        sent = text;
        return StabilizerAction.Translate(rawText.Trim());
    }

    /// <summary>هل ينتهي السطر بنقطة أو علامة تعجب أو استفهام أو ثلاث نقاط (تُهمل علامات التنصيص والأقواس المغلِقة).</summary>
    public static bool EndsSentence(string raw)
    {
        var text = raw.Trim().TrimEnd('"', '\'', '”', '’', '»', ')', ']');
        return text.Length > 0 && ".!?…".Contains(text[^1]);
    }

    /// <summary>إذا كادت الترجمة تطابق الأصل فالنص بلغة الترجمة أصلًا (مثل قوائم جهاز الألعاب بالعربية).</summary>
    public bool ShouldSend(string source, string translation) =>
        !TextMatch.IsSimilar(TextMatch.Normalize(source), TextMatch.Normalize(translation), 0.90);

    /// <summary>ينسى آخر سطر أُرسل (مثلًا فشلت ترجمته)؛ وما دام السطر على الشاشة يُعدّ مستقرًّا من جديد ويُترجم.</summary>
    public void ForgetSent()
    {
        sent = "";
        candidate = "";
        candidateCount = 0;
    }

    public StabilizerAction Reset()
    {
        candidate = "";
        candidateCount = 0;
        emptyCount = 0;
        if (sent.Length == 0) return StabilizerAction.None;
        sent = "";
        return StabilizerAction.Clear;
    }
}
