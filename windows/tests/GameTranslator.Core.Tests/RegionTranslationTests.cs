// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;

namespace GameTranslator.Core.Tests;

public class RegionTranslationTests
{
    private static readonly Frame Image = Frame.Blank(64, 64);

    private static Reading Lines(params string[] lines) => new(lines.Select(line => new TextLine(line)));

    [Fact]
    public void LinesOfOneSentenceAreJoined()
    {
        var paragraphs = RegionTranslation.Paragraphs(Lines("Find the blacksmith in", "the village square"));
        Assert.Equal(["Find the blacksmith in the village square"], paragraphs);
    }

    [Fact]
    public void FinishedSentenceStartsANewParagraph()
    {
        var paragraphs = RegionTranslation.Paragraphs(Lines("Find the blacksmith.", "Bring him three iron bars."));
        Assert.Equal(["Find the blacksmith.", "Bring him three iron bars."], paragraphs);
    }

    [Fact]
    public void BigGapBetweenLinesStartsANewParagraph()
    {
        var reading = new Reading(
        [
            new TextLine("Current quest", new TextBox(0, 0.00, 1, 0.10)),
            new TextLine("Find the blacksmith", new TextBox(0, 0.30, 1, 0.40)),
            new TextLine("in the square", new TextBox(0, 0.42, 1, 0.52)),
        ]);
        Assert.Equal(["Current quest", "Find the blacksmith in the square"], RegionTranslation.Paragraphs(reading));
    }

    [Fact]
    public async Task NoTextInTheRegionGivesNull()
    {
        var result = await RegionTranslation.TranslateOnceAsync(Image, new CropRegion(), new LinesReader(Reading.Empty), new FakeTranslator());
        Assert.Null(result);
    }

    [Fact]
    public async Task ButtonPromptsAreNotTranslated()
    {
        var result = await RegionTranslation.TranslateOnceAsync(
            Image, new CropRegion(), new LinesReader(Lines("R2 Swing", "Find the blacksmith.")), new FakeTranslator());
        Assert.NotNull(result);
        Assert.Equal("Find the blacksmith.", result.Source);
        Assert.Equal("AR:Find the blacksmith.", result.Translation);
    }

    [Fact]
    public async Task EachParagraphIsTranslatedSeparately()
    {
        var result = await RegionTranslation.TranslateOnceAsync(
            Image, new CropRegion(), new LinesReader(Lines("Find the blacksmith.", "Bring him three iron bars.")), new FakeTranslator());
        Assert.NotNull(result);
        Assert.Equal("AR:Find the blacksmith.\nAR:Bring him three iron bars.", result.Translation);
    }

    [Fact]
    public async Task BoxIsPlacedInsideTheWholeFrame()
    {
        // المربّع المقروء منسوب إلى المنطقة؛ والنتيجة تُعطى بنِسب الإطار كله.
        var region = new CropRegion(Top: 0.1, Bottom: 0.5, Left: 0.5, Right: 0.9);
        var reading = new Reading([new TextLine("Find the blacksmith.", new TextBox(0, 0, 1, 0.5))]);
        var result = await RegionTranslation.TranslateOnceAsync(Image, region, new LinesReader(reading), new FakeTranslator());
        Assert.NotNull(result);
        Assert.Equal(0.5, result.Box.Left, 6);
        Assert.Equal(0.1, result.Box.Top, 6);
        Assert.Equal(0.9, result.Box.Right, 6);
        Assert.Equal(0.3, result.Box.Bottom, 6);
    }

    [Fact]
    public async Task WholeRegionIsTheBoxWhenLinePlacesAreUnknown()
    {
        var region = new CropRegion(Top: 0.1, Bottom: 0.5, Left: 0.5, Right: 0.9);
        var result = await RegionTranslation.TranslateOnceAsync(Image, region, new LinesReader(Lines("Find the blacksmith.")), new FakeTranslator());
        Assert.NotNull(result);
        Assert.Equal(new TextBox(0.5, 0.1, 0.9, 0.5), result.Box);
    }

    [Fact]
    public async Task MissingTranslatorIsReported()
    {
        var translator = new FakeTranslator { Error = new TranslatorException(TranslatorStatus.NeedsDownload) };
        await Assert.ThrowsAsync<TranslatorException>(() =>
            RegionTranslation.TranslateOnceAsync(Image, new CropRegion(), new LinesReader(Lines("Find the blacksmith.")), translator));
    }

    [Theory]
    [InlineData(5, 6000)]
    [InlineData(100, 11000)]
    [InlineData(5000, 25000)]
    public void DisplayTimeGrowsWithTextButIsBounded(int length, int expected) =>
        Assert.Equal(expected, RegionTranslation.DisplayMilliseconds(new string('a', length)));
}
