// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.TvInstall;

public static class Versions
{
    /// <summary>Sayısal karşılaştırma: 0.10.0, 0.9.9'dan yenidir.</summary>
    public static bool IsNewer(string a, string b)
    {
        var x = Parts(a);
        var y = Parts(b);
        for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            var left = i < x.Length ? x[i] : 0;
            var right = i < y.Length ? y[i] : 0;
            if (left != right) return left > right;
        }
        return false;
    }

    private static int[] Parts(string version) =>
        [.. version.Split('.', '-').Select(p => int.TryParse(p, out var n) ? n : 0)];

    /// <summary><c>gametranslator-androidtv_0.7.0.apk</c> → <c>0.7.0</c>; ad bu biçimde değilse null.</summary>
    public static string? OfBundledFile(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var underscore = name.LastIndexOf('_');
        return underscore > 0 && underscore < name.Length - 1 ? name[(underscore + 1)..] : null;
    }
}
