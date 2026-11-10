// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.TvInstall.Tests;

public class VersionsTests
{
    [Theory]
    [InlineData("0.10.0", "0.9.9", true)]
    [InlineData("0.7.0", "0.7.0", false)]
    [InlineData("0.7", "0.7.0", false)]
    [InlineData("5.0", "4.0", true)]
    [InlineData("5.0", "6.5", false)]
    public void ComparisonIsNumeric(string a, string b, bool newer) => Assert.Equal(newer, Versions.IsNewer(a, b));

    [Fact]
    public void BundledFileCarriesItsVersion()
    {
        Assert.Equal("0.7.0", Versions.OfBundledFile("gametranslator-androidtv_0.7.0.apk"));
        Assert.Equal("0.7.0", Versions.OfBundledFile("gametranslator-samsung_0.7.0.wgt"));
        Assert.Null(Versions.OfBundledFile("gametranslator.apk"));
    }
}
