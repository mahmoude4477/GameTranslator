// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;

namespace GameTranslator.Core.Tests;

public class ReadingSplitTests
{
    [Fact]
    public void FarApartMenuAndGaugeAreSeparated() =>
        // 1920 genişlikte, 30 piksel yüksek yazılar arasında 900 piksel: soldaki menü, sağdaki gösterge.
        Assert.True(Reading.IsSeparateText(900, 30, 1920));

    [Fact]
    public void MissedWordsInsideASubtitleDoNotSplitIt()
    {
        // GTA V: açık zeminde okunamayan iki-üç sözcüğün bıraktığı 150-200 piksellik boşluk aynı cümledir.
        Assert.False(Reading.IsSeparateText(150, 30, 1920));
        Assert.False(Reading.IsSeparateText(200, 30, 1920));
    }

    [Fact]
    public void WideGapOnSmallTextIsStillOneLineUnlessFarAcrossTheScreen() =>
        // Küçük yazıda satır yüksekliğinin 4 katı dar kalır; görüntünün %12'sini de geçmeli.
        Assert.False(Reading.IsSeparateText(120, 20, 1920));
}

public class CenteredBoxTests
{
    [Fact]
    public void PartlyReadCenteredSubtitleIsWidenedSymmetrically()
    {
        // Yalnız satırın sağ yarısı okunduysa kutu ortaya göre aynalanır, solu da örtülür.
        var box = new TextBox(0.45, 0.85, 0.75, 0.9).CenteredOnScreen();
        Assert.Equal(0.25, box.Left, 3);
        Assert.Equal(0.75, box.Right, 3);
        Assert.Equal(0.85, box.Top);
    }

    [Fact]
    public void CornerTextIsLeftAlone()
    {
        var box = new TextBox(0.02, 0.1, 0.2, 0.15);
        Assert.Equal(box, box.CenteredOnScreen());
    }
}
