// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Translation.Tests;

public class SubtitleSegmenterTests
{
    /// <summary>الأجزاء التي ستُترجم بين قوسين مربعين، والباقي كما هو.</summary>
    private static string Render(string text) =>
        string.Concat(SubtitleSegmenter.Split(text).Select(p => p.Translate ? $"[{p.Text}]" : p.Text));

    [Theory]
    [InlineData("Wait!", "[Wait!]")]
    [InlineData("Boy! Focus. The draugr are still coming.", "[Boy!] [Focus.] [The draugr are still coming.]")]
    [InlineData("What is that?! It looks like a door.", "[What is that?!] [It looks like a door.]")]
    [InlineData("\"Run!\" he said. Then nothing.", "[\"Run!\" he said.] [Then nothing.]")]
    [InlineData("I know… but we must go.", "[I know… but we must go.]")]
    [InlineData("Wait... what is that?", "[Wait... what is that?]")]
    [InlineData("Mr. Smith is here. Dr. Jones too.", "[Mr. Smith is here.] [Dr. Jones too.]")]
    [InlineData("No. We go now.", "[No.] [We go now.]")]
    [InlineData("Kratos.", "[Kratos.]")]
    public void SplitsSentences(string text, string expected) => Assert.Equal(expected, Render(text));

    [Theory]
    [InlineData("ATREUS: Father, look over there!", "ATREUS: [Father, look over there!]")]
    [InlineData("ATREUS: Look! KRATOS: Stay behind me.", "ATREUS: [Look!] KRATOS: [Stay behind me.]")]
    [InlineData("SINDRI JR.: Hello there.", "SINDRI JR.: [Hello there.]")]
    [InlineData("Note: this is not a label.", "[Note: this is not a label.]")]
    [InlineData("Kratos: Stay close.", "Kratos: [Stay close.]")]
    [InlineData("Old Man: Who goes there?", "Old Man: [Who goes there?]")]
    [InlineData("Objective: Find the gate.", "[Objective: Find the gate.]")]
    public void KeepsSpeakerLabels(string text, string expected) => Assert.Equal(expected, Render(text));

    [Fact]
    public void JoinsWrappedLines()
    {
        Assert.Equal("[If we do not find the giant before nightfall, we will have to make camp.]",
            Render("If we do not find the giant before nightfall,\nwe will have to make camp."));
    }

    [Fact]
    public void KeepsLineBreaksBetweenSpeakers()
    {
        Assert.Equal("ATREUS: [Father, look!]\nKRATOS: [Stay behind me.]",
            Render("ATREUS: Father, look!\r\nKRATOS: Stay behind me."));
        Assert.Equal("- [Who are you?]\n- [A friend.]", Render("- Who are you?\n- A friend."));
    }

    [Theory]
    [InlineData("...", "...")]
    [InlineData("?!", "?!")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void DoesNotTranslatePiecesWithoutLetters(string text, string expected) => Assert.Equal(expected, Render(text));

    [Theory]
    [InlineData("THE GATE WILL NOT HOLD MUCH LONGER.", "The gate will not hold much longer.")]
    [InlineData("I KNOW, BUT I CAN'T.", "I know, but I can't.")]
    [InlineData("WAIT!", "WAIT!")]
    [InlineData("KRATOS.", "KRATOS.")]
    [InlineData("Who are you?", "Who are you?")]
    [InlineData("NASA is here.", "NASA is here.")]
    public void NormalizesAllCapsSentences(string sentence, string expected) =>
        Assert.Equal(expected, SubtitleSegmenter.NormalizeCase(sentence));

    [Fact]
    public void KnownNamesStayCapitalized() =>
        Assert.Equal("The gate will not hold, Atreus.",
            SubtitleSegmenter.NormalizeCase("THE GATE WILL NOT HOLD, ATREUS.", new HashSet<string> { "atreus" }));
}
