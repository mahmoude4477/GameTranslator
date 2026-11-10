// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Core;

/// <summary>Bir kare için karar: son okuma kullanılır ya da yazı tanıma yapılır.</summary>
/// <param name="Reuse">Bölge değişmedi, <paramref name="Text"/> geçerli.</param>
/// <param name="Deep">Boş bölgede kısa satırlar için büyütülmüş okuma da denenir.</param>
public readonly record struct ReadPlan(bool Reuse, string Text, bool Deep)
{
    public static ReadPlan Reused(string text) => new(true, text, false);
    public static ReadPlan Read(bool deep) => new(false, "", deep);
}

/// <summary>
/// Hangi karede yazı tanıma yapılacağını seçer. Oyun süresinin çoğunda bölge boştur ya da değişmez;
/// işlemcinin büyük kısmı bu karelerde boşa gider.
/// </summary>
public sealed class ReadScheduler(int changeThreshold = 16, int maxReuse = 20, int deepEvery = 3)
{
    private byte[]? previous;
    private string lastText = "";
    private int reused;
    private int emptyReads;

    /// <summary>Her kare okunur ve boş kareler hep derin okunur.</summary>
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
        // Yazı yeni kalktıysa derin okuma sırası hemen gelmez.
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
