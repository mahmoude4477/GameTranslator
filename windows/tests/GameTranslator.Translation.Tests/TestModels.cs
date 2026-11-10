// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Translation;

namespace GameTranslator.Translation.Tests;

/// <summary>
/// Gerçek model dosyaları depoya girmez; testler depo dışındaki bir önbellekten okur.
/// GAMETRANSLATOR_TEST_MODELS ile yer değiştirilir. GAMETRANSLATOR_DOWNLOAD_TEST_MODELS=1 ise eksik dosyalar
/// ModelStore ile bir kez indirilir; yoksa modele bağlı testler atlanır.
/// </summary>
internal static class TestModels
{
    public static string Directory { get; } = ResolveDirectory();

    private static readonly Lazy<bool> Available = new(Prepare);

    public static bool TokenizerAvailable =>
        File.Exists(Path.Combine(Directory, ModelStore.SourceSpmFile)) &&
        File.Exists(Path.Combine(Directory, ModelStore.VocabFile)) || Available.Value;

    public static bool ModelAvailable => Available.Value;

    private static string ResolveDirectory()
    {
        var fromEnv = Environment.GetEnvironmentVariable("GAMETRANSLATOR_TEST_MODELS");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;
        var cacheRoot = OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Caches")
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(cacheRoot, "gametranslator-test-models", "en-tr");
    }

    private static bool Prepare()
    {
        var store = new ModelStore(Directory);
        if (store.IsInstalled) return true;
        if (Environment.GetEnvironmentVariable("GAMETRANSLATOR_DOWNLOAD_TEST_MODELS") != "1") return false;
        store.DownloadAsync(null, CancellationToken.None).GetAwaiter().GetResult();
        return store.IsInstalled;
    }

    public static MarianTokenizer LoadTokenizer() => MarianTokenizer.Load(
        Path.Combine(Directory, ModelStore.SourceSpmFile), Path.Combine(Directory, ModelStore.VocabFile));
}

/// <summary>Model kurulu değilse testi açıklamayla atlar (xUnit 2'de çalışma anı atlama yok).</summary>
public sealed class ModelFactAttribute : FactAttribute
{
    public ModelFactAttribute()
    {
        if (!TestModels.ModelAvailable)
            Skip = $"Translation model not found in {TestModels.Directory} (set GAMETRANSLATOR_DOWNLOAD_TEST_MODELS=1)";
    }
}

public sealed class TokenizerFactAttribute : FactAttribute
{
    public TokenizerFactAttribute()
    {
        if (!TestModels.TokenizerAvailable)
            Skip = $"Tokenizer files not found in {TestModels.Directory} (set GAMETRANSLATOR_DOWNLOAD_TEST_MODELS=1)";
    }
}

public sealed class TokenizerTheoryAttribute : TheoryAttribute
{
    public TokenizerTheoryAttribute()
    {
        if (!TestModels.TokenizerAvailable)
            Skip = $"Tokenizer files not found in {TestModels.Directory} (set GAMETRANSLATOR_DOWNLOAD_TEST_MODELS=1)";
    }
}
