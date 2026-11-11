// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Core;

/// <summary>القرار لإطار واحد: تُستخدم آخر قراءة أو يُجرى التعرّف على النص.</summary>
/// <param name="Reuse">المنطقة لم تتغيّر و<paramref name="Text"/> ما زال صالحًا.</param>
/// <param name="Deep">في المنطقة الفارغة تُجرَّب أيضًا القراءة المكبَّرة لأجل الأسطر القصيرة.</param>
public readonly record struct ReadPlan(bool Reuse, string Text, bool Deep)
{
    public static ReadPlan Reused(string text) => new(true, text, false);
    public static ReadPlan Read(bool deep) => new(false, "", deep);
}

/// <summary>
/// يختار الإطارات التي يُجرى فيها التعرّف على النص. في معظم وقت اللعب تكون المنطقة فارغة أو لا تتغيّر؛
/// ومعظم جهد المعالج يضيع في هذه الإطارات.
/// </summary>
public sealed class ReadScheduler(int changeThreshold = 16, int maxReuse = 20, int deepEvery = 3)
{
    private byte[]? previous;
    private string lastText = "";
    private int reused;
    private int emptyReads;

    /// <summary>يُقرأ كل إطار، والإطارات الفارغة تُقرأ دائمًا قراءة عميقة.</summary>
    public static ReadScheduler Always() => new(changeThreshold: -1, maxReuse: 0, deepEvery: 1);

    public ReadPlan Plan(byte[] signature)
    {
        var before = previous;
        previous = signature;
        if (before is not null && reused < maxReuse && !Changed(before, signature))
        {
            reused++;
            return ReadPlan.Reused(lastText);
        }
        reused = 0;
        return ReadPlan.Read(emptyReads % deepEvery == 0);
    }

    public void Record(string text)
    {
        lastText = text;
        // إذا اختفى النص للتوّ فلا يأتي دور القراءة العميقة فورًا.
        emptyReads = text.Length == 0 ? emptyReads + 1 : 1;
    }

    public void Reset()
    {
        previous = null;
        lastText = "";
        reused = 0;
        emptyReads = 0;
    }

    private bool Changed(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return true;
        for (var i = 0; i < a.Length; i++)
            if (Math.Abs(a[i] - b[i]) > changeThreshold) return true;
        return false;
    }
}
