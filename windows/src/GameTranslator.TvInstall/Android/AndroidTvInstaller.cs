// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace GameTranslator.TvInstall.Android;

/// <summary>
/// يثبّت تطبيق GameTranslator لـ Android TV عبر الشبكة. يجب تفعيل خيارات المطوّر وتصحيح أخطاء USB على التلفزيون؛
/// ومعظم أجهزة Android TV تنتظر ADB على المنفذ 5555 بهذا الإعداد.
/// </summary>
public sealed partial class AndroidTvInstaller(string host, string keyDirectory, int port = AndroidTvInstaller.DefaultPort)
{
    public const int DefaultPort = 5555;
    public const string PackageName = "com.gametranslator.tv";

    /// <summary>يُستدعى حين يعرض التلفزيون سؤال الموافقة أثناء التثبيت.</summary>
    public Action? OnApprovalNeeded { get; init; }

    /// <summary>هل ADB مفعّل على التلفزيون (هل يرد المنفذ)؟</summary>
    public static async Task<bool> IsReachableAsync(string host, int port = DefaultPort, TimeSpan? timeout = null)
    {
        using var tcp = new TcpClient();
        using var limit = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(1));
        try
        {
            await tcp.ConnectAsync(host, port, limit.Token);
            return true;
        }
        catch (Exception error) when (error is SocketException or OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>يبحث في شبكات هذا الكمبيوتر عن الأجهزة التي فيها ADB مفعّل (قسم /24 من كل شبكة).</summary>
    public static async Task<IReadOnlyList<string>> ScanAsync(int port = DefaultPort, CancellationToken cancellationToken = default)
    {
        var hosts = ScanTargets(LocalAddresses()).ToList();
        var found = new System.Collections.Concurrent.ConcurrentBag<string>();
        await Parallel.ForEachAsync(hosts, new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = cancellationToken },
            async (candidate, _) =>
            {
                if (await IsReachableAsync(candidate, port, TimeSpan.FromMilliseconds(700))) found.Add(candidate);
            });
        return [.. found.OrderBy(h => IPAddress.Parse(h).GetAddressBytes()[3])];
    }

    internal static IEnumerable<string> ScanTargets(IEnumerable<IPAddress> locals)
    {
        var seen = new HashSet<string>();
        foreach (var local in locals)
        {
            var bytes = local.GetAddressBytes();
            for (var last = 1; last < 255; last++)
            {
                if (last == bytes[3]) continue;
                var candidate = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{last}";
                if (seen.Add(candidate)) yield return candidate;
            }
        }
    }

    private static IEnumerable<IPAddress> LocalAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && IsPrivate(a));

    internal static bool IsPrivate(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
    }

    /// <summary>
    /// يثبّت التطبيق ويفتحه إن كان مفقودًا أو قديمًا؛ كل ذلك في اتصال واحد، وتُطلب موافقة التلفزيون مرة واحدة على الأكثر.
    /// </summary>
    /// <returns>الإصدار الموجود الآن على التلفزيون، وهل جرى تثبيت.</returns>
    public async Task<(string Version, bool Installed)> EnsureInstalledAsync(string apkPath, string version,
        CancellationToken cancellationToken = default, bool forceReinstall = false)
    {
        await using var adb = await ConnectAsync(cancellationToken);

        // في Android TV متعدد المستخدمين (مثل ملفات TCL / Google TV الشخصية) اربط الحزمة بالمستخدم النشط أيضًا
        await adb.RunAsync($"shell:cmd package install-existing {PackageName}", cancellationToken: cancellationToken);

        var dumpsys = await adb.RunAsync($"shell:dumpsys package {PackageName}", cancellationToken: cancellationToken);
        var installed = VersionFromDumpsys(dumpsys);

        var needsInstall = forceReinstall || installed is null || Versions.IsNewer(version, installed);
        if (!needsInstall)
        {
            var startRes = await StartAppAsync(adb, cancellationToken);
            if (!startRes.Contains("Error"))
            {
                return (installed!, false);
            }
        }

        await InstallAsync(adb, apkPath, cancellationToken);
        await StartAppAsync(adb, cancellationToken);
        return (version, true);
    }

    private static async Task<string> StartAppAsync(AdbConnection adb, CancellationToken cancellationToken)
    {
        var result = await adb.RunAsync($"shell:am start -n {PackageName}/.MainActivity", cancellationToken: cancellationToken);
        if (result.Contains("Error"))
        {
            await adb.RunAsync($"shell:cmd package install-existing {PackageName}", cancellationToken: cancellationToken);
            var cur = (await adb.RunAsync("shell:am get-current-user", cancellationToken: cancellationToken)).Trim();
            if (!string.IsNullOrEmpty(cur))
            {
                await adb.RunAsync($"shell:cmd package install-existing --user {cur} {PackageName}", cancellationToken: cancellationToken);
                result = await adb.RunAsync($"shell:am start --user {cur} -n {PackageName}/.MainActivity", cancellationToken: cancellationToken);
            }
        }
        return result;
    }

    internal static string? VersionFromDumpsys(string output) =>
        VersionName().Match(output) is { Success: true } match ? match.Groups[1].Value : null;

    /// <summary>يثبّت ملف APK. إن وُجدت نسخة قديمة بتوقيع مختلف أزالها أولًا.</summary>
    private static async Task InstallAsync(AdbConnection adb, string apkPath, CancellationToken cancellationToken)
    {
        var result = await StreamInstallAsync(adb, apkPath, cancellationToken);
        if (result.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE") || result.Contains("INSTALL_FAILED_VERSION_DOWNGRADE"))
        {
            await adb.RunAsync($"shell:pm uninstall {PackageName}", cancellationToken: cancellationToken);
            result = await StreamInstallAsync(adb, apkPath, cancellationToken);
        }
        if (!result.Contains("Success"))
            throw new AndroidInstallException(AndroidInstallError.InstallFailed, result.Trim());
    }

    private static async Task<string> StreamInstallAsync(AdbConnection adb, string apkPath, CancellationToken cancellationToken)
    {
        await using var apk = File.OpenRead(apkPath);
        // Android 7 وما بعده: يُدفَّق الملف مباشرة إلى مدير الحزم دون نسخه إلى التلفزيون أولًا.
        return await adb.RunAsync($"exec:cmd package install -r -d -g -S {apk.Length}", apk, cancellationToken);
    }

    private Task<AdbConnection> ConnectAsync(CancellationToken cancellationToken) =>
        AdbConnection.ConnectAsync(host, port, LoadOrCreateKey(keyDirectory), OnApprovalNeeded, cancellationToken: cancellationToken);

    /// <summary>
    /// مفتاح ADB الخاص بهذا الكمبيوتر. موافقة التلفزيون تُمنح لهذا المفتاح؛ وإن تغيّر المفتاح سأل التلفزيون من جديد.
    /// </summary>
    public static RSA LoadOrCreateKey(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "adbkey");
        var key = RSA.Create();
        if (File.Exists(path))
        {
            try
            {
                key.ImportFromPem(File.ReadAllText(path));
                if (key.KeySize == 2048) return key;
            }
            catch (CryptographicException) { /* ملف تالف: يُنشأ مفتاح جديد */ }
        }
        key.Dispose();
        key = RSA.Create(2048);
        File.WriteAllText(path, key.ExportPkcs8PrivateKeyPem());
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        return key;
    }

    [GeneratedRegex(@"versionName=(\S+)")]
    private static partial Regex VersionName();
}
