// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;

namespace GameTranslator.Core.Tests;

public class OcrNoiseTests
{
    /// في الخط المزخرف كانت الكلمة تتحول في كل إطار إلى رموز عشوائية مختلفة.
    [Theory]
    [InlineData("Across the deep waters, Pbo ou$3e went, hoping a boat would carry him home.",
                "Across the deep waters, Pbo went, hoping a boat would carry him home.")]
    [InlineData("Across the deep waters2aljd O went, Hoping a boat.", "Across the deep waters O went, Hoping a boat.")]
    [InlineData("He left 3e, at dawn.", "He left, at dawn.")]
    public void GarbledWordsAreRemoved(string line, string expected) => Assert.Equal(expected, OcrNoise.Clean(line));

    [Theory]
    [InlineData("Turn on the PS5 now.")]
    [InlineData("Hold R2-D2 still.")]
    [InlineData("It costs $500.")]
    [InlineData("Meet me at 3:00pm.")]
    [InlineData("The 2nd gate is 100m away.")]
    [InlineData("Run at 60fps, 4K.")]
    [InlineData("Score x2!")]
    [InlineData("Year 1984.")]
    [InlineData("I can't — wait…")]
    [InlineData("\"Kratos!\" she said.")]
    [InlineData("Mk2 rifle, 9.5kg.")]
    [InlineData("L1 + R1 Spartan Rage")]
    public void UsualTokensAreKept(string line) => Assert.Equal(line, OcrNoise.Clean(line));

    [Fact]
    public void ReadingDropsLinesThatWereOnlyNoise()
    {
        var reading = new Reading([new TextLine("We go now."), new TextLine("ou$3e 2aljd")]).WithoutOcrNoise();
        Assert.Equal("We go now.", reading.Text);
    }
}

public class ReadingChoiceTests
{
    [Fact]
    public void CleanReadingScoresHigherThanGarbled()
    {
        Assert.True(OcrNoise.Score("CESARE: Wait for me!") > OcrNoise.Score("- CESAREiVJütt'h!"));
        Assert.True(OcrNoise.Score("We go to McDonald's now.") > 0);
        Assert.True(OcrNoise.Score("T hats the[lil Should be quiet now.") < OcrNoise.Score("That should be quiet now."));
    }

    /// النص الأبيض ذو الحافة الداكنة يصير أسود، والخلفية الساطعة والحافة تصيران أبيضين.
    [Fact]
    public void WhiteTextOnlyKeepsOutlinedWhiteAndDropsBrightBackground()
    {
        var frame = Frame.Blank(60, 20);
        void Paint(int x0, int x1, byte value)
        {
            for (var y = 0; y < 20; y++)
                for (var x = x0; x < x1; x++)
                {
                    var i = y * frame.Stride + x * 4;
                    frame.Pixels[i] = frame.Pixels[i + 1] = frame.Pixels[i + 2] = value;
                    frame.Pixels[i + 3] = 255;
                }
        }
        Paint(0, 30, 230);   // parlak kum: yakınında koyu yok
        Paint(40, 44, 20);   // yazının koyu kenarı
        Paint(44, 48, 250);  // beyaz harf
        var result = TextImage.WhiteTextOnly(frame, radius: 3);
        byte At(int x) => result.Pixels[10 * result.Stride + x * 4];
        Assert.Equal(255, At(5));   // zemin
        Assert.Equal(0, At(45));    // harf
        Assert.Equal(255, At(41));  // kenar
    }
}
