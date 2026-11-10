// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Core;

/// <summary>
/// Bir bölgeyi tek seferlik okuyup çevirir. Görev ya da bilgi paneli gibi, altyazı gibi akıp gitmeyen
/// yazılar içindir: bölge sürekli taranmaz, kullanıcı bir tuşa basınca bir kez okunur.
/// </summary>
public static class RegionTranslation
{
    /// <param name="Source">Okunan yazı (bloklara ayrılmış, her blok ayrı satırda).</param>
    /// <param name="Translation">Aynı blokların çevirisi; madde işaretli bloklar "• " ile başlar.</param>
    /// <param name="Box">Yazının karedeki yeri, kenarlara oranla; TV çeviriyi buraya yerleştirir.</param>
    /// <param name="Structured">Birden çok blok var (başlık, maddeler...): çeviri satır satır, sola yaslı gösterilmelidir.</param>
    public sealed record Result(string Source, string Translation, TextBox Box, bool Structured = false);

    /// <summary>Panelde bir paragraf, başlık ya da madde. <paramref name="Bullet"/> madde işaretiyle başlıyorsa true (işaret metinden çıkarılır).</summary>
    public sealed record Block(string Text, bool Bullet);

    /// <summary>Madde işareti olarak kullanılan simgeler.</summary>
    private const string BulletChars = "◇◆◈◊♦♢•●○◦▪■□▫►▶‣∙·*-–—✓✔✦❖◉⬥";

    /// <summary>Bölgede yazı yoksa null döner. Çevirmen kullanılamıyorsa <see cref="TranslatorException"/> fırlatır.</summary>
    public static async Task<Result?> TranslateOnceAsync(
        Frame frame, CropRegion region, ITextReader reader, ITextTranslator translator, CancellationToken cancellationToken = default)
    {
        var area = RegionCropper.Crop(frame, region);
        // Tek okuma olduğu için "derin" okuma açık: boş görünen bölgede büyütülmüş okuma da denenir.
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

    /// <summary>Blokların metinleri (madde işaretleri çıkarılmış).</summary>
    public static IReadOnlyList<string> Paragraphs(Reading reading) => [.. Blocks(reading).Select(block => block.Text)];

    /// <summary>
    /// Satırları bloklara toplar; böylece görev panelindeki başlık, açıklama ve maddeler ayrı ayrı çevrilir.
    /// Yeni blok başlar: satır madde işaretiyle başlıyorsa; önceki satır cümleyi bitirdiyse; satırlar arasında belirgin boşluk
    /// varsa; satır soldan belirgin biçimde dışarı çıkıyorsa (maddenin girintili devam satırından sonra yeni madde gibi).
    /// Küçük harfle başlayan satır, önceki cümle bitmediyse öncekinin devamıdır (cümle iki satıra bölünmüş).
    /// </summary>
    /// <param name="width">Okunan alanın genişliği (piksel); kutular oransal olduğu için yatay ve dikey ölçüyü aynı birime getirir.</param>
    /// <param name="height">Okunan alanın yüksekliği (piksel).</param>
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
        // Cümle bitmediyse ve satır küçük harfle başlıyorsa devam satırıdır.
        if (!finished && char.IsLower(lowerText[0])) return false;
        if (finished) return true;
        if (upper.Box is not { } a || lower.Box is not { } b || typicalHeight <= 0) return false;
        // Satırlar arasındaki boşluk satır yüksekliğinin %40'ından büyükse ayrı bloklardır.
        if (((b.Top - a.Bottom) * height) > typicalHeight * 0.4) return true;
        // Soldan yarım satır yüksekliğinden fazla dışarı çıkıyorsa (girintili devam satırından sonra yeni madde) yeni bloktur.
        return ((a.Left - b.Left) * width) > typicalHeight * 0.5;
    }

    /// <summary>Satırın başındaki madde işaretini ayırır. Yazı tanıma ◇ simgesini çoğu zaman "<>" ya da tek başına "o"/"0" okur.</summary>
    private static (bool Bullet, string Text) StripBullet(string text)
    {
        if (text.Length >= 2 && BulletChars.Contains(text[0])) return (true, text[1..].TrimStart());
        if (text.StartsWith("<>", StringComparison.Ordinal)) return (true, text[2..].TrimStart());
        if (text.Length > 2 && text[1] == ' ' && text[0] is 'o' or '0' && char.IsUpper(text[2])) return (true, text[2..]);
        return (false, text);
    }

    /// <summary>Çevirinin TV'de kalma süresi (ms): uzun panel yazısı okunacak kadar durur.</summary>
    public static int DisplayMilliseconds(string text) => Math.Clamp(4000 + (text.Length * 70), 6000, 25000);
}
