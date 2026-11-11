// SPDX-License-Identifier: AGPL-3.0-only

using System.IO;
using System.Net.Http;
using System.Text.Json;
using GameTranslator.Core;
using GameTranslator.Core.LgTv;
using GameTranslator.RemotePlay;
using GameTranslator.Shell;
using GameTranslator.Translation;
using GameTranslator.TvInstall.Android;
using GameTranslator.TvInstall.Samsung;

namespace GameTranslator.App.Services;

/// <summary>صلات متحكّم التطبيق بالعالم الحقيقي (PS5، PSN، التلفزيون، OCR، الترجمة).</summary>
public sealed class LiveServices : IAppServices
{
    public ITranslationModel Translation { get; } = new OnlineModel();

    public ITextTranslator? MakeOnlineTranslator(string key, ITextTranslator? fallback, string target) => new DeepLTranslator(key, fallback, target);

    public Task<OnlineKeyCheck> CheckOnlineKeyAsync(string key) => DeepLTranslator.CheckAsync(key);
    public IUpdater? Updater { get; } = new Updater();
    public IFirewall? Firewall { get; } = new WindowsFirewall();

    public Task<IReadOnlyList<ConsoleInfo>> DiscoverConsolesAsync() => ConsoleDiscovery.DiscoverAsync();

    public async Task<byte[]> AccountIdFromRedirectAsync(Uri redirect)
    {
        var code = PsnAccount.CodeFromRedirect(redirect)
                   ?? throw new AppException(AppError.InvalidLogin, AppException.Describe(AppError.InvalidLogin, Localizer.Current));
        return await PsnAccount.AccountIdAsync(code);
    }

    public Task<RegisteredConsole> RegisterAsync(string host, byte[] accountId, uint pin) => Registration.RegisterAsync(host, accountId, pin);

    public async Task<IReadOnlyList<LgTv>> DiscoverTvsAsync()
    {
        var lg = LgDiscovery.SearchAsync();
        var samsung = SamsungDiscovery.SearchAsync();
        return [.. await lg, .. await samsung];
    }

    public async Task<TvPairing> PairTvAsync(LgTv tv, string? key, string server)
    {
        // الكمبيوتر لا يتحكم في Android TV؛ تطبيق التلفزيون يجد الكمبيوتر بنفسه.
        if (tv.Brand == TvBrand.AndroidTv) return new TvPairing("", true);
        if (tv.Brand == TvBrand.Samsung)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var samsung = new SamsungClient(tv, key);
                var token = await samsung.ConnectAsync(cts.Token);
                try { await samsung.LaunchAsync(SamsungClient.AppId, new Dictionary<string, string> { ["server"] = server }); }
                catch (Exception) { /* تطبيق التلفزيون يستطيع أيضا أن يجد الكمبيوتر بنفسه */ }
                return new TvPairing(token, true);
            }
            catch (Exception)
            {
                // حتى لو كان التلفزيون مطفأ أو عنوان IP مختلفا أو لم يُمنح إذن التحكم بعد
                // أكمل الاقتران كي يتمكن تطبيق التلفزيون (أو المتصفح) من الاتصال بالكمبيوتر
                return new TvPairing(key ?? "", false);
            }
        }
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = new SsapClient(tv, key);
            var newKey = await client.ConnectAsync(cts.Token);
            try
            {
                await client.LaunchAsync(AppController.TvAppId, new Dictionary<string, string> { ["server"] = server });
                return new TvPairing(newKey, true);
            }
            catch (Exception)
            {
                return new TvPairing(newKey, false);
            }
        }
        catch (Exception)
        {
            return new TvPairing(key ?? "", false);
        }
    }

    public IStreamSession MakeStream(RegisteredConsole console, byte[] accountId)
    {
        if (StreamSession.IsNativeAvailable)
        {
            return new StreamSession(console, accountId);
        }
        return new WindowCaptureStreamSession(console, accountId);
    }

    public IKeeper MakeKeeper(LgTv tv, string? key, IReadOnlyDictionary<string, string> parameters, Func<bool> appConnected, Action<LgTv> onMoved)
    {
        if (tv.Brand == TvBrand.AndroidTv) return new IdleKeeper();
        if (tv.Brand == TvBrand.Samsung)
        {
            var samsungLocator = new TvLocator(tv, async target =>
            {
                var client = new SamsungClient(target, key);
                try
                {
                    await client.ConnectAsync();
                    return client;
                }
                catch
                {
                    client.Dispose();
                    throw;
                }
            }, () => SamsungDiscovery.SearchAsync(), onMoved);
            return new Keeper(new TvKeeper(samsungLocator.ConnectAsync, SamsungClient.AppId, parameters, appConnected));
        }
        var locator = new TvLocator(tv, async target =>
        {
            var client = new SsapClient(target, key);
            try
            {
                await client.ConnectAsync();
                return client;
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }, () => LgDiscovery.SearchAsync(), onMoved);
        return new Keeper(new TvKeeper(locator.ConnectAsync, AppController.TvAppId, parameters));
    }

    // --- تثبيت تطبيق التلفزيون ---

    private static string? BundledTvPackage
    {
        get
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "tv-app");
            return Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.ipk").FirstOrDefault() : null;
        }
    }

    public string? BundledTvAppVersion =>
        BundledTvPackage is { } package ? DevModeInstaller.VersionOfPackage(Path.GetFileName(package)) : null;

    public Task<bool> IsDevModeOnAsync(string host) => DevModeInstaller.IsDevModeOnAsync(host);

    public Task<string> FetchDevKeyAsync(string host, string passphrase) =>
        DevModeInstaller.FetchKeyAsync(host, passphrase, new ProcessRunner(), DevModeTools.ForThisSystem());

    public Task<string?> InstalledTvAppVersionAsync(string host, string devKey) =>
        WithKeyFileAsync(devKey, file => Installer(host, file).InstalledVersionAsync());

    public async Task InstallTvAppAsync(string host, string devKey)
    {
        try
        {
            await WithKeyFileAsync<object?>(devKey, async file =>
            {
                if (BundledTvPackage is not { } package || BundledTvAppVersion is not { } version)
                    throw new DevModeException(DevModeError.InstallFailed, "لا توجد حزمة LG (tv-app/*.ipk) بجانب التطبيق");
                await Installer(host, file).InstallAsync(package, version);
                return null;
            });
        }
        catch (DevModeException error)
        {
            StartupGuard.Log("تثبيت تلفزيون LG", error);
            throw;
        }
    }

    public async Task<int?> RenewDevSessionAsync(string host, string devKey)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            return await WithKeyFileAsync(devKey, file => Installer(host, file).RenewSessionAsync(http));
        }
        catch (DevModeException)
        {
            return null;
        }
    }

    // --- Samsung و Android TV (GameTranslator.TvInstall) ---

    /// <summary>الملفات الدائمة: مفتاح ADB وشهادات Samsung.</summary>
    private static string DataDirectory(string name) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameTranslator", name);

    public BundledTvApp? BundledApp(TvBrand brand)
    {
        var pattern = brand switch { TvBrand.Samsung => "*.wgt", TvBrand.AndroidTv => "*.apk", _ => null };
        if (pattern is null) return null;
        var folder = Path.Combine(AppContext.BaseDirectory, "tv-app");
        string? file = null;
        if (Directory.Exists(folder))
        {
            file = Directory.EnumerateFiles(folder, pattern).FirstOrDefault();
        }
        if (file is null)
        {
            var fallback = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tv", "packages");
            if (Directory.Exists(fallback))
            {
                file = Directory.EnumerateFiles(fallback, pattern).FirstOrDefault();
            }
        }
        return file is not null && (TvInstall.Versions.OfBundledFile(file) ?? "1.0.0") is { } version ? new BundledTvApp(file, version) : null;
    }

    public Task<bool> IsSamsungDevModeOnAsync(string host) => SamsungTvInstaller.IsDevModeOnAsync(host);

    public async Task InstallSamsungAppAsync(string host, BundledTvApp app, Action<TvInstallEvent> onEvent)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
        var installer = new SamsungTvInstaller(host, DataDirectory("samsung"), new SamsungCertificates(http))
        {
            Progress = _ => onEvent(new TvInstallEvent(TvInstallEventKind.Progress)),
            SignIn = token => SamsungLogin.SignInAsync(url => onEvent(new TvInstallEvent(TvInstallEventKind.SignIn, new Uri(url))),
                TimeSpan.FromMinutes(10), token),
        };
        try
        {
            await Task.Run(() => installer.InstallAsync(app.Path));
        }
        catch (SamsungInstallException error)
        {
            StartupGuard.Log("تثبيت تلفزيون Samsung", error);
            throw new DevModeException(error.Error switch
            {
                SamsungInstallError.Unreachable => DevModeError.Unreachable,
                SamsungInstallError.LoginFailed => DevModeError.SignInFailed,
                SamsungInstallError.CertificateFailed => DevModeError.CertificateFailed,
                SamsungInstallError.TooOld => DevModeError.TooOld,
                _ => DevModeError.InstallFailed,
            }, error.Message);
        }
        catch (Exception error) when (error is not DevModeException)
        {
            StartupGuard.Log("تثبيت التلفزيون", error);
            throw new DevModeException(DevModeError.InstallFailed, error.Message);
        }
    }

    public Task<bool> IsAndroidDebugOnAsync(string host) => AndroidTvInstaller.IsReachableAsync(host);

    public Task<IReadOnlyList<string>> ScanAndroidTvsAsync() => AndroidTvInstaller.ScanAsync();

    public async Task<string> InstallAndroidAppAsync(string host, BundledTvApp app, Action<TvInstallEvent> onEvent)
    {
        var installer = new AndroidTvInstaller(host, DataDirectory("adb"))
        {
            OnApprovalNeeded = () => onEvent(new TvInstallEvent(TvInstallEventKind.ApproveOnTv)),
        };
        try
        {
            var (version, _) = await Task.Run(() => installer.EnsureInstalledAsync(app.Path, app.Version));
            return version;
        }
        catch (AndroidInstallException error)
        {
            StartupGuard.Log("تثبيت Android TV", error);
            throw new DevModeException(error.Error switch
            {
                AndroidInstallError.Unreachable => DevModeError.Unreachable,
                AndroidInstallError.NotApproved => DevModeError.NotApproved,
                _ => DevModeError.InstallFailed,
            }, error.Message);
        }
        catch (Exception error) when (error is not DevModeException)
        {
            StartupGuard.Log("تثبيت التلفزيون", error);
            throw new DevModeException(DevModeError.InstallFailed, error.Message);
        }
    }

    private static DevModeInstaller Installer(string host, string keyFile) => DevModeInstaller.ForTv(host, keyFile);

    /// <summary>ssh يقرأ المفتاح من ملف؛ ويبقى الملف طوال مدة الأمر فقط.</summary>
    private static async Task<T> WithKeyFileAsync<T>(string key, Func<string, Task<T>> body)
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(file, key);
        try
        {
            return await body(file);
        }
        finally
        {
            File.Delete(file);
        }
    }

    public ITextReader? MakeReader(string language) => WindowsOcrReader.Create(language);
    public bool HasOcrLanguage(string language) => WindowsOcrReader.HasLanguage(language);

    /// <summary>للتلفزيونات التي لا يمكن التحكم فيها: تطبيق التلفزيون يتصل بنفسه.</summary>
    private sealed class IdleKeeper : IKeeper
    {
        public void Start(TimeSpan interval) { }
        public void Stop() { }
        public void Reconnect() { }
    }

    private sealed class Keeper(TvKeeper keeper) : IKeeper
    {
        public void Start(TimeSpan interval) => keeper.Start(interval);
        public void Stop() => keeper.Stop();
        public void Reconnect() => keeper.Reconnect();
    }

    /// <summary>تُجرى الترجمة عبر Google GTX: لا يوجد ما يُنزَّل، وكل زوج لغات جاهز.</summary>
    private sealed class OnlineModel : ITranslationModel
    {
        public bool IsSupported(string source, string target) => true;

        public bool IsInstalled(string source, string target) => true;

        public Task DownloadAsync(string source, string target, IProgress<double> progress, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public ITextTranslator Create(string source, string target) => new GoogleGtxTranslator(source, target);
    }
}

/// <summary>المخزن المشفّر (الحساب ومفاتيح PS5 والتلفزيون).</summary>
public sealed class DpapiAppStore : IAppStore
{
    public void Save<T>(string key, T value) => SecureStore.Default.Save(key, value);
    public T? Load<T>(string key) => SecureStore.Default.Load<T>(key);
    public void Remove(string key) => SecureStore.Default.Remove(key);
}

/// <summary>الإعدادات العادية: %LOCALAPPDATA%\GameTranslator\settings.json.</summary>
public sealed class FileSettingsStore : ISettingsStore
{
    private static readonly string FilePath = Path.Combine(AppPaths.Data, "settings.json");

    public UserSettings Load()
    {
        try { return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(FilePath)) ?? new UserSettings(); }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { return new UserSettings(); }
    }

    public void Save(UserSettings settings)
    {
        Directory.CreateDirectory(AppPaths.Data);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings));
    }
}

public sealed class WindowsSleepGuard : ISleepGuard
{
    public void Begin() => SleepGuard.Begin();
    public void End() => SleepGuard.End();
}
