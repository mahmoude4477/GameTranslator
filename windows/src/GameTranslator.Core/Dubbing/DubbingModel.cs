// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Core.Dubbing;

/// <summary>
/// Pocket TTS Turkish (wite-tech, CC-BY-4.0): ilk kullanımda indirilen dosyalar. Sürüm sabittir, özetler doğrulanır
/// (Mac: DubbingModel.swift; tts-samples iş akışı listeyi oradan okur).
/// </summary>
public static class DubbingModel
{
    public const string Revision = "fcbd37c74dc127be846face0ff36062e9a939f7b";
    public const string Repository = "wite-tech/pocket-tts-turkish";

    public sealed record File(string Name, long Bytes, string Sha256)
    {
        public Uri Url => new($"https://huggingface.co/{Repository}/resolve/{Revision}/{Name}");
    }

    public static readonly IReadOnlyList<File> Files =
    [
        new("config.yaml", 1212, "fa60a7799ac09778360ce1aa5c2216fa2ce6f1be06aabb2e0dd4f6ea63a6d3d7"),
        new("model.safetensors", 438_033_312, "c43b72ed336b64c66adceba8fbbb059e869f753b60de086e3f32bf1b118d5a77"),
        new("tokenizer.model", 303_502, "a6c3a7ca48ecf3c9254bf72dff58e3f09f2798f49effcbd7debcf6f7a87f3568"),
        new("voices/female_1.wav", 188_684, "82be41762735f5004cec992badc67813a02206fe53888ba7342a589197850225"),
        new("voices/female_2.wav", 229_964, "98cd697c9dd6f77f253bd1233675496b6d4882cd9c0ef72434b6a12e06564aa7"),
        new("voices/female_3.wav", 212_684, "555eab5c0640649c1323e6e51c2fae10a334ac3988e6ba0052822147fd9bc40c"),
        new("voices/male_1.wav", 235_724, "e6538a1f0ce60d08994c3a733946589d327cf9256b35b77e4155c0baefadc81a"),
        new("voices/male_2.wav", 195_884, "d5585323c634900b054ea20d1c8c9e39d35516b89dba693eaabf8587143dd6d8"),
        new("voices/male_3.wav", 237_644, "b5bbbca120837cca1b93c3b8d740e70bcc3f32f53c557af04b207fc69a31545e"),
    ];

    public static long TotalBytes => Files.Sum(file => file.Bytes);

    /// <summary>
    /// İndirilen ayar dosyasından yerel çalışma ayarı: ağırlık ve sözlük yolları klasördeki dosyalara çevrilir, model
    /// küçük harfle başlayan cümlelerle eğitildiği için ilk harf büyütülmez.
    /// </summary>
    public static string LocalConfig(string original, string directory)
    {
        var lines = new List<string>();
        foreach (var line in original.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            var indent = new string(' ', line.TakeWhile(c => c == ' ').Count());
            if (trimmed.StartsWith("weights_path:", StringComparison.Ordinal))
                lines.Add(indent + "weights_path: " + Quoted(Path.Combine(directory, "model.safetensors")));
            else if (trimmed.StartsWith("tokenizer_path:", StringComparison.Ordinal))
                lines.Add(indent + "tokenizer_path: " + Quoted(Path.Combine(directory, "tokenizer.model")));
            else if (!trimmed.StartsWith("capitalize_first_letter:", StringComparison.Ordinal))
                lines.Add(line);
        }
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return string.Join('\n', lines) + "\ncapitalize_first_letter: false\n";
    }

    /// <summary>YAML'da tek tırnaklı dize: Windows yollarındaki ters bölü olduğu gibi kalır.</summary>
    private static string Quoted(string path) => "'" + path.Replace("'", "''") + "'";
}
