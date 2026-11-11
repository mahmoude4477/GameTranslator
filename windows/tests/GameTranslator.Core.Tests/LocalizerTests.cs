// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;

namespace GameTranslator.Core.Tests;

/// تأتي النصوص من ملف Localizable.xcstrings المضمَّن في التطبيق؛ ولغة الواجهة هي العربية.
public class LocalizerTests
{
    [Fact]
    public void ArabicTextComesFromCatalog()
    {
        var ar = Localizer.ForLanguage("ar");
        Assert.Equal("ابدأ الترجمة", ar["Start translating"]);
        Assert.Equal("Start translating", Localizer.ForLanguage("en")["Start translating"]);
    }

    [Fact]
    public void UnknownLanguageFallsBackToArabic() =>
        Assert.Equal("ابدأ الترجمة", Localizer.ForLanguage("de")["Start translating"]);

    [Fact]
    public void MissingKeyShowsTheKey() =>
        Assert.Equal("No such text", Localizer.ForLanguage("ar")["No such text"]);

    [Fact]
    public void PositionalFormatArgumentsAreFilled()
    {
        var ar = Localizer.ForLanguage("ar");
        Assert.Equal("الخطوة 2 من 6", ar.Format("Step %lld of %lld", 2, 6));
        Assert.Contains("الإنجليزية", ar.Format("The %@–%@ language pack is not downloaded yet.", "الإنجليزية", "العربية"));
    }

    [Fact]
    public void EveryEntryHasArabic()
    {
        var missing = Localizer.MissingTranslations("ar");
        Assert.True(missing.Count == 0, string.Join(", ", missing));
    }
}
