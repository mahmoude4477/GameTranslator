// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using TizenSdb;
using TizenSdb.SdbClient;
using TizenSdb.SigningManager;

namespace GameTranslator.TvInstall.Samsung;

public enum SamsungInstallError
{
    /// <summary>TV'de geliştirici modu kapalı ya da TV'ye girilen bilgisayar IP'si bu bilgisayarınki değil.</summary>
    Unreachable,
    LoginFailed,
    CertificateFailed,
    /// <summary>TV, Tizen 5.0'dan (2019) eski.</summary>
    TooOld,
    InstallFailed,
}

public sealed class SamsungInstallException(SamsungInstallError error, string? detail = null)
    : Exception(detail is null ? error.ToString() : $"{error}: {detail}")
{
    public SamsungInstallError Error { get; } = error;
}

/// <summary>
/// GameTranslator'ın Samsung TV uygulamasını geliştirici modu açık TV'ye kurar: TV'nin DUID'ini okur, gerekirse Samsung
/// hesabıyla TV'ye özel sertifika alır, paketi imzalar ve kurar. sdb bağlantısı ve imza TizenSdb.Core'dan (MIT).
/// </summary>
public sealed partial class SamsungTvInstaller(string host, string profilesDirectory, SamsungCertificates certificates)
{
    public const int SdbPort = 26101;
    public const string AppId = "GameTranslatorT.GameTranslator";
    private const string PackageId = "GameTranslatorT";

    /// <summary>Sertifika yoksa Samsung girişini başlatır; yoksa null döner ve kurulum giriş adımında durur.</summary>
    public Func<CancellationToken, Task<SamsungAccount>>? SignIn { get; init; }

    public Action<string>? Progress { get; init; }

    /// <summary>
    /// TV'de geliştirici modu açık ve bu bilgisayar kabul ediliyor mu? Samsung yalnız geliştirici modunda girilen IP'den
    /// gelen bağlantıyı kabul eder; başka bilgisayardan port açık görünse de bağlantı hemen kapanır.
    /// </summary>
    public static async Task<bool> IsDevModeOnAsync(string host, TimeSpan? timeout = null)
    {
        using var tcp = new TcpClient();
        using var limit = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(2));
        try
        {
            await tcp.ConnectAsync(host, SdbPort, limit.Token);
            return true;
        }
        catch (Exception error) when (error is SocketException or OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Bu TV için kayıtlı geçerli sertifika var mı (giriş gerekmeden kurulabilir mi)?</summary>
    public async Task<bool> HasCertificateAsync(CancellationToken cancellationToken = default)
    {
        await using var device = await ConnectAsync(cancellationToken);
        return SigningProfile.In(ProfileDirectory(await ReadDuidAsync(device, cancellationToken))).IsValid(DateTime.UtcNow);
    }

    /// <summary>Paketi imzalayıp kurar ve uygulamayı açar.</summary>
    /// <param name="wgtPath">İmzasız ya da imzalı .wgt; dosyanın kendisine dokunulmaz.</param>
    public async Task InstallAsync(string wgtPath, CancellationToken cancellationToken = default)
    {
        await using var device = await ConnectAsync(cancellationToken);
        Progress?.Invoke("reading");
        var capabilities = await device.CapabilityAsync(cancellationToken);
        var platform = capabilities.GetValueOrDefault("platform_version", "").Trim();
        if (platform.Length > 0 && Versions.IsNewer("5.0", platform))
            throw new SamsungInstallException(SamsungInstallError.TooOld, platform);
        var toolPath = capabilities.GetValueOrDefault("sdk_toolpath")?.Trim() is { Length: > 0 } path ? path : "/opt/usr/apps/tmp";

        var duid = await ReadDuidAsync(device, cancellationToken);
        var profile = SigningProfile.In(ProfileDirectory(duid));
        if (!profile.IsValid(DateTime.UtcNow))
        {
            if (SignIn is null) throw new SamsungInstallException(SamsungInstallError.LoginFailed, "giriş gerekiyor");
            Progress?.Invoke("signin");
            var account = await SignIn(cancellationToken);
            Progress?.Invoke("certificate");
            profile = await certificates.CreateAsync(duid, account, ProfileDirectory(duid), cancellationToken);
        }

        Progress?.Invoke("signing");
        DeleteLeftovers(Path.GetTempPath());
        var staged = Path.Combine(Path.GetTempPath(), $"gametranslator-{Guid.NewGuid():N}.wgt");
        File.Copy(wgtPath, staged);
        try
        {
            await TizenWgtSigner.ReSignWgtWithCertsInPlace(staged, profile.AuthorP12, profile.DistributorP12, profile.Password);
            Progress?.Invoke("installing");
            var log = await CaptureAsync(() => new TizenInstaller(staged, device, toolPath).InstallApp());
            if (InstallFailed(log))
            {
                // Başka sertifikayla kurulmuş eski kopya varsa ("Author certificate not match") kaldırıp bir kez daha
                // dene. TV sürümüne göre kaldırma uygulama kimliğini ya da paket kimliğini bekliyor; ikisi de denenir.
                Progress?.Invoke("uninstalling");
                var removal = "";
                foreach (var id in new[] { AppId, PackageId })
                {
                    try { removal += await device.ShellCommandAsync($"0 vd_appuninstall {id}", cancellationToken) + "\n"; }
                    catch (Exception error) when (error is not OperationCanceledException) { removal += error.Message + "\n"; }
                }
                log = await CaptureAsync(() => new TizenInstaller(staged, device, toolPath).InstallApp());
                if (InstallFailed(log))
                    throw new SamsungInstallException(SamsungInstallError.InstallFailed, Tail(log) + "\nuninstall: " + Tail(removal));
            }
        }
        finally
        {
            DeleteStaged(staged);
        }
        try { await device.ShellCommandAsync($"0 execute {AppId}", cancellationToken); } catch (Exception) { /* açmak isteğe bağlı */ }
    }

    /// <summary>
    /// TizenSdb'nin TizenInstaller'ı paketi açıp kapatmıyor; Windows açık dosyanın silinmesine izin vermiyor. Silme
    /// hatası kurulumun asıl sonucunu örtmesin diye tutamak çöp toplayıcıyla bırakılır, yine silinemezse dosya geçici
    /// klasörde kalır (bir sonraki kurulumda temizlenir).
    /// </summary>
    internal static bool DeleteStaged(string path)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        return false;
    }

    /// <summary>Önceki kurulumlardan geçici klasörde kalmış paketler.</summary>
    internal static void DeleteLeftovers(string directory)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "gametranslator-*.wgt"))
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddMinutes(-10)) DeleteStaged(file);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { /* temizlik isteğe bağlı */ }
    }

    private string ProfileDirectory(string duid) => Path.Combine(profilesDirectory, SafeName().Replace(duid, "_"));

    private async Task<SdbTcpDevice> ConnectAsync(CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(host, out var address)) throw new SamsungInstallException(SamsungInstallError.Unreachable, host);
        var device = new SdbTcpDevice(address, SdbPort);
        try
        {
            await device.ConnectAsync(cancellationToken);
            return device;
        }
        catch (Exception error) when (error is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await device.DisposeAsync();
            throw new SamsungInstallException(SamsungInstallError.Unreachable, error.Message);
        }
    }

    private static async Task<string> ReadDuidAsync(SdbTcpDevice device, CancellationToken cancellationToken)
    {
        // Yavaş TV'lerde ilk yanıt boş gelebiliyor; birkaç kez dene.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var duid = (await device.ShellCommandAsync("0 getduid", cancellationToken)).Split('\n')[0].Trim();
            if (IsDuid(duid)) return duid;
            await Task.Delay(500, cancellationToken);
        }
        throw new SamsungInstallException(SamsungInstallError.Unreachable, "DUID okunamadı");
    }

    internal static bool IsDuid(string text) => DuidPattern().IsMatch(text);

    /// <summary>TizenInstaller sonucu konsola yazar; çıktıyı yakalayıp hatayı oradan anlarız.</summary>
    private static readonly SemaphoreSlim ConsoleGate = new(1, 1);

    private static async Task<string> CaptureAsync(Func<Task> body)
    {
        await ConsoleGate.WaitAsync();
        var original = Console.Out;
        var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            await body();
        }
        catch (Exception error)
        {
            writer.WriteLine($"install failed: {error.Message}");
        }
        finally
        {
            Console.SetOut(original);
            ConsoleGate.Release();
        }
        return writer.ToString();
    }

    /// <summary>
    /// TV, kurulum başarısız olsa da komutu başarılı bitirir; hata çıktıdadır (ör. <c>install failed[118]</c>).
    /// İşaretler Apps2Samsung'un TizenInstallDiagnostics'inden.
    /// </summary>
    internal static bool InstallFailed(string log) =>
        log.Contains("install failed", StringComparison.OrdinalIgnoreCase) ||
        log.Contains("val[fail]", StringComparison.OrdinalIgnoreCase) ||
        log.Contains("not enough space", StringComparison.OrdinalIgnoreCase);

    private static string Tail(string log) => log.Length <= 400 ? log.Trim() : log[^400..].Trim();

    [GeneratedRegex(@"^[A-Za-z0-9_\-+=/]{8,}$")]
    private static partial Regex DuidPattern();

    [GeneratedRegex(@"[^A-Za-z0-9_\-]")]
    private static partial Regex SafeName();
}
