// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;

namespace GameTranslator.Core.Tests;

public class ReadingSplitTests
{
    [Fact]
    public void FarApartMenuAndGaugeAreSeparated() =>
        // بعرض 1920، 900 بكسل بين نصين ارتفاعهما 30 بكسل: القائمة على اليسار والمؤشر على اليمين.
        Assert.True(Reading.IsSeparateText(900, 30, 1920));

    [Fact]
    public void MissedWordsInsideASubtitleDoNotSplitIt()
    {
        // GTA V: فراغ 150-200 بكسل تتركه كلمتان أو ثلاث تعذّرت قراءتها على خلفية فاتحة هو الجملة نفسها.
        Assert.False(Reading.IsSeparateText(150, 30, 1920));
        Assert.False(Reading.IsSeparateText(200, 30, 1920));
    }

    [Fact]
    public void WideGapOnSmallTextIsStillOneLineUnlessFarAcrossTheScreen() =>
        // في النص الصغير تبقى 4 أضعاف ارتفاع السطر ضيقة؛ يجب أن يتجاوز الفراغ 12% من الصورة أيضا.
        Assert.False(Reading.IsSeparateText(120, 20, 1920));
}

public class CenteredBoxTests
{
    [Fact]
    public void PartlyReadCenteredSubtitleIsWidenedSymmetrically()
    {
        // إذا قُرئ النصف الأيمن من السطر فقط يُعكس المربّع حول المنتصف فيُغطى اليسار أيضا.
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
