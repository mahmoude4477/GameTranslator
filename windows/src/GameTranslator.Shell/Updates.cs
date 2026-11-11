// SPDX-License-Identifier: AGPL-3.0-only

using System.Reflection;

namespace GameTranslator.Shell;

/// <summary>تحديث التطبيق (في الواقع Velopack، من إصدارات GitHub).</summary>
public interface IUpdater
{
    /// <summary>إن ثُبّت بملف التثبيت يحدّث نفسه؛ وإن فُتح من ملف zip نعرض صفحة الإصدار فقط.</summary>
    bool CanUpdateItself { get; }
    Uri DownloadPage { get; }
    /// <summary>رقم الإصدار الأحدث إن وُجد، وإلا null.</summary>
    Task<string?> CheckAsync();
    /// <summary>ينزّل التحديث ويثبّته ويعيد تشغيل التطبيق.</summary>
    Task InstallAsync(IProgress<double> progress);
}

/// <summary>أرقام الإصدارات: "0.1.0-beta.10" (إصدار دلالي؛ أجزاء الإصدار التجريبي تُقارن كأرقام إن كانت أرقاما).</summary>
public static class AppVersion
{
    /// <summary>إصدار البناء (يحدّده CI من الوسم: -p:Version=0.1.0-beta.10)؛ وتُحذف اللاحقة "+commit".</summary>
    public static string Current(Assembly assembly) =>
        (assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];

    public static bool IsNewer(string candidate, string current) => Compare(candidate, current) > 0;

    public static int Compare(string a, string b)
    {
        var (coreA, preA) = Split(a);
        var (coreB, preB) = Split(b);
        for (var i = 0; i < Math.Max(coreA.Length, coreB.Length); i++)
        {
            var x = i < coreA.Length ? coreA[i] : 0;
            var y = i < coreB.Length ? coreB[i] : 0;
            if (x != y) return x.CompareTo(y);
        }
        // الإصدار بلا جزء تجريبي أحدث من الإصدار التجريبي (0.1.0 > 0.1.0-beta.9).
        if (preA.Length == 0 || preB.Length == 0) return preB.Length.CompareTo(preA.Length);
        for (var i = 0; i < Math.Max(preA.Length, preB.Length); i++)
        {
            if (i >= preA.Length) return -1;
            if (i >= preB.Length) return 1;
            var numberA = int.TryParse(preA[i], out var na);
            var numberB = int.TryParse(preB[i], out var nb);
            var result = numberA && numberB ? na.CompareTo(nb)
                : numberA ? -1 : numberB ? 1 : string.CompareOrdinal(preA[i], preB[i]);
            if (result != 0) return Math.Sign(result);
        }
        return 0;
    }

    private static (int[] Core, string[] Pre) Split(string version)
    {
        var text = version.Trim().TrimStart('v').Split('+')[0];
        var dash = text.IndexOf('-');
        var core = (dash < 0 ? text : text[..dash]).Split('.').Select(part => int.TryParse(part, out var n) ? n : 0).ToArray();
        var pre = dash < 0 ? [] : text[(dash + 1)..].Split('.');
        return (core, pre);
    }
}
