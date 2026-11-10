// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;

namespace GameTranslator.Core.Tests;

public class ScreenTranslationTests
{
    private static readonly Frame Image = Frame.Blank(64, 64);

    private static TextLine Line(string text, double left, double top, double right, double height = 0.03) =>
        new(text, new TextBox(left, top, right, top + height));

    [Fact]
    public void LinesFarApartBecomeSeparateGroups()
    {
        // Sol üstte görev paneli, sağ altta bilgi kutusu, ortada altyazı.
        var groups = ScreenTranslation.Groups(new Reading([
            Line("Find the blacksmith in", 0.05, 0.10, 0.30),
            Line("the village square", 0.05, 0.135, 0.25),
            Line("Press the lever to open the gate.", 0.40, 0.85, 0.60),
            Line("New item unlocked", 0.75, 0.60, 0.95),
        ]));
        Assert.Equal(3, groups.Count);
        Assert.Equal(["Find the blacksmith in the village square"], groups[0].Paragraphs);
        Assert.Equal(0.05, groups[0].Box.Left, 6);
        Assert.Equal(0.165, groups[0].Box.Bottom, 6);
        Assert.Equal(0.03, groups[0].LineHeight, 6);
        Assert.Equal(["New item unlocked"], groups[1].Paragraphs);
        Assert.Equal(["Press the lever to open the gate."], groups[2].Paragraphs);
    }

    [Fact]
    public void LinesOnTheSameRowButFarApartAreSeparate()
    {
        var groups = ScreenTranslation.Groups(new Reading([Line("Settings", 0.05, 0.05, 0.15), Line("Back to menu", 0.80, 0.05, 0.95)]));
        Assert.Equal(2, groups.Count);
    }

    [Fact]
    public void MenuLinesAreTranslatedOneByOne()
    {
        var groups = ScreenTranslation.Groups(new Reading([
            Line("Map", 0.05, 0.30, 0.15), Line("Inventory", 0.05, 0.34, 0.20), Line("Settings", 0.05, 0.38, 0.18),
        ]));
        Assert.Single(groups);
        Assert.Equal(["Map", "Inventory", "Settings"], groups[0].Paragraphs);
    }

    [Fact]
    public void TitleInBigLettersIsNotMergedWithTheSmallTextBelow()
    {
        var groups = ScreenTranslation.Groups(new Reading([
            Line("Chapter One", 0.30, 0.20, 0.70, 0.08),
            Line("The journey begins in the north.", 0.30, 0.29, 0.70, 0.03),
        ]));
        Assert.Equal(2, groups.Count);
        Assert.Equal(0.08, groups[0].LineHeight, 6);
    }

    [Fact]
    public void NumbersAndPromptsAreNotTranslated()
    {
        var groups = ScreenTranslation.Groups(new Reading([
            Line("23m", 0.50, 0.05, 0.55), Line("9", 0.90, 0.05, 0.92), Line("R2 Swing", 0.80, 0.90, 0.95),
            Line("HP 120/300", 0.05, 0.90, 0.20),
        ]));
        Assert.Equal(["HP 120/300"], Assert.Single(groups).Paragraphs);
    }

    [Fact]
    public void LinesWithoutPlacesAreSkipped() =>
        Assert.Empty(ScreenTranslation.Groups(new Reading([new TextLine("Find the blacksmith.")])));

    [Fact]
    public async Task EveryGroupIsTranslatedAndProgressIsReported()
    {
        var reported = new List<int>();
        var items = await ScreenTranslation.TranslateAsync(Image, new LinesReader(new Reading([
            Line("Find the blacksmith.", 0.05, 0.10, 0.30),
            Line("Bring him three iron bars.", 0.05, 0.135, 0.30),
            Line("Leave the camp", 0.60, 0.80, 0.90),
        ])), new FakeTranslator(), list => reported.Add(list.Count));
        Assert.Equal(2, items.Count);
        Assert.Equal("TR: Find the blacksmith.\nTR: Bring him three iron bars.", items[0].Translation);
        Assert.True(items[0].Paragraphs);
        Assert.False(items[1].Paragraphs);
        Assert.Equal([1, 2], reported);
    }

    [Fact]
    public async Task TextThatTranslatesToItselfIsNotShown()
    {
        var items = await ScreenTranslation.TranslateAsync(Image, new LinesReader(new Reading([
            Line("KRATOS", 0.05, 0.10, 0.30), Line("Open the door.", 0.50, 0.80, 0.70),
        ])), new SameNameTranslator());
        Assert.Equal("Kapıyı aç.", Assert.Single(items).Translation);
    }

    [Fact]
    public async Task UnavailableTranslatorIsReported()
    {
        var translator = new FakeTranslator { Error = new TranslatorException(TranslatorStatus.NeedsDownload) };
        await Assert.ThrowsAsync<TranslatorException>(() => ScreenTranslation.TranslateAsync(
            Image, new LinesReader(new Reading([Line("Open the door.", 0.5, 0.8, 0.7)])), translator));
    }

    [Theory]
    [InlineData(0, 12000)]
    [InlineData(200, 18000)]
    [InlineData(5000, 60000)]
    public void DisplayTimeGrowsWithTextButIsBounded(int length, int expected) =>
        Assert.Equal(expected, ScreenTranslation.DisplayMilliseconds([new("a", new string('a', length), new TextBox(0, 0, 1, 1), 0.03)]));

    private sealed class SameNameTranslator : ITextTranslator
    {
        public Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(text == "Open the door." ? "Kapıyı aç." : text);
    }
}
