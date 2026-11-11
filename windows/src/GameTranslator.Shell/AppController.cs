// SPDX-License-Identifier: AGPL-3.0-only

using System.ComponentModel;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using GameTranslator.Core;
using GameTranslator.Core.LgTv;
using GameTranslator.RemotePlay;

namespace GameTranslator.Shell;

/// <summary>
/// حالة التطبيق وسير عمله: معالج الإعداد، بدء الترجمة وإيقافها،
/// وحالتا PS5 والتلفزيون. ترتبط الواجهة بهذا الكائن؛ وتُنقل الأحداث عبر <c>post</c>
/// إلى خيط الواجهة.
/// </summary>
public sealed class AppController : INotifyPropertyChanged, IDisposable
{
    public const string TvAppId = "com.gametranslator.tv";
    public const int DefaultServerPort = 8787;

    private readonly IAppServices services;
    private readonly IAppStore store;
    private readonly ISettingsStore settingsStore;
    private readonly ISleepGuard sleep;
    private readonly Localizer text;
    private readonly Action<Action> post;

    private OnboardingStep step;
    private byte[]? accountId;
    private RegisteredConsole? console;
    private LgTv? tv;
    private TvAppState tvApp = TvAppState.Unknown;
    /// <summary>تعذّر فتح تطبيق التلفزيون في آخر اقتران.</summary>
    private bool tvAppMissing;
    /// <summary>فحص تطبيق التلفزيون جارٍ (قد تستدعيه شاشة الانتظار وبدء الترجمة في الوقت نفسه).</summary>
    private bool refreshingTvApp;
    private int? devModeHoursLeft;
    private UserSettings settings;
    private bool isRunning;
    private bool isStarting;
    private SessionState sessionState = SessionState.Disconnected(DisconnectKind.Stopped);
    private int tvClients;
    private string lastSource = "";
    private string lastTranslation = "";
    private TranslatorStatus translatorStatus = TranslatorStatus.Ready;
    private bool ocrMissing;
    private Frame? lastFrame;
    private bool modelDownloading;
    private double modelProgress;
    private string? modelError;
    private Task? modelDownload;

    private SubtitleServer server = new(DefaultServerPort);
    private bool serverStarted;
    private IStreamSession? session;
    private IKeeper? keeper;
    private SubtitlePipeline? pipeline;
    /// <summary>قارئ النص في خط المعالجة؛ تستخدمه أيضا القراءة لمرة واحدة للمناطق الإضافية.</summary>
    private ITextReader? activeReader;
    private int extraBusy;
    private bool subtitlesPaused;
    /// <summary>آخر مكان شوهدت فيه ترجمة اللعبة النصية؛ تُوضع الترجمة التجريبية هناك على التلفزيون.</summary>
    private TextBox? lastSubtitleBox;
    private int processing;
    private string? updateVersion;
    private bool updating;
    private double updateProgress;
    private string? updateError;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppController(IAppServices services, IAppStore store, ISettingsStore settingsStore, ISleepGuard sleep, Localizer text, Action<Action> post)
    {
        this.services = services;
        this.store = store;
        this.settingsStore = settingsStore;
        this.sleep = sleep;
        this.text = text;
        this.post = post;
        accountId = store.Load<byte[]>(StoreKeys.AccountId);
        console = store.Load<RegisteredConsole>(StoreKeys.Console);
        tv = store.Load<LgTv>(StoreKeys.Tv);
        settings = settingsStore.Load();
        var ready = accountId is not null && console is not null && tv is not null && store.Load<string>(StoreKeys.TvKey) is not null;
        step = OnboardingStep.Done;
        server.ClientsChanged += OnClientsChanged;
    }

    // --- الحالة ---

    public OnboardingStep Step
    {
        get => step;
        set
        {
            // اقتراح جدار الحماية في خطوة الاقتران لا ينتقل إلى خطوة أخرى.
            if (Set(ref step, value, [nameof(CanAdvance), nameof(StepNumber)])) ClearFirewallPrompt();
        }
    }
    public static int StepCount => (int)OnboardingStep.Done;
    public int StepNumber => Math.Min((int)step + 1, StepCount);
    public IAppServices Services => services;
    public bool HasAccount => accountId is not null;
    public byte[]? AccountId => accountId;
    public string? AccountIdBase64 => accountId != null && accountId.Length > 0 ? Convert.ToBase64String(accountId) : null;
    public RegisteredConsole? Console => console;

    public void SetRegisteredConsole(RegisteredConsole registered)
    {
        console = registered;
        store.Save(StoreKeys.Console, registered);
        Changed(nameof(Console), nameof(CanAdvance), nameof(Ps5StatusText), nameof(Ps5Tone));
    }
    public LgTv? Tv => tv;
    public bool IsRunning { get => isRunning; private set => Set(ref isRunning, value, [nameof(Ps5StatusText), nameof(Ps5Tone), nameof(TvTone)]); }
    public bool IsStarting { get => isStarting; private set => Set(ref isStarting, value); }
    public SessionState SessionState { get => sessionState; private set => Set(ref sessionState, value, [nameof(Ps5StatusText), nameof(Ps5Tone)]); }
    public string Ps5StatusText
    {
        get
        {
            if (isRunning) return StatusText(sessionState, isRunning, text);
            if (console is not null) return $"مقترن ({console.Host})";
            return "غير مقترن";
        }
    }
    public StatusTone Ps5Tone => isRunning ? Ps5ToneFor(sessionState, isRunning) : (console is not null ? StatusTone.Good : StatusTone.Idle);
    public int TvClients { get => tvClients; private set => Set(ref tvClients, value, [nameof(TvTone), nameof(TvStatusText)]); }
    public StatusTone TvTone => tvClients > 0 ? StatusTone.Good : (tv is not null ? StatusTone.Good : StatusTone.Warning);
    public string TvStatusText
    {
        get
        {
            if (tvClients > 0) return $"{tvClients} جهاز متصل (جاهز)";
            if (tv is not null) return $"{tv.Name} (جاهز)";
            return "في انتظار تطبيق التلفزيون";
        }
    }
    public string LastSource { get => lastSource; private set => Set(ref lastSource, value); }
    public string LastTranslation { get => lastTranslation; private set => Set(ref lastTranslation, value); }
    public TranslatorStatus TranslatorStatus { get => translatorStatus; private set => Set(ref translatorStatus, value); }
    /// <summary>لا توجد لغة للتعرّف على النص في الكمبيوتر؛ يجب إضافتها من الإعدادات.</summary>
    public bool OcrMissing { get => ocrMissing; private set => Set(ref ocrMissing, value); }
    /// <summary>ترجمة النص الحية متوقفة مؤقتا (Num 0)؛ ترجمات المناطق الإضافية والمنطقة الحرة تواصل العمل.</summary>
    public bool SubtitlesPaused { get => subtitlesPaused; private set => Set(ref subtitlesPaused, value); }
    public Frame? LastFrame { get => lastFrame; private set => Set(ref lastFrame, value); }
    public string? ActiveSourceLanguage { get; private set; }
    /// <summary>المنفذ الذي يستمع إليه الخادم فعلا (منفذ عشوائي إن كان 8787 مشغولا).</summary>
    public int ServerPort { get; private set; } = DefaultServerPort;

    /// <summary>هل الترجمة من لغة اللعبة هذه إلى اللغة الهدف مدعومة (اللغات التي تُعرض في الإعدادات).</summary>
    public bool SupportsLanguage(string source) => services.Translation.IsSupported(source, settings.TargetLanguage);

    public bool ModelInstalled => true;
    public bool ModelDownloading { get => modelDownloading; private set => Set(ref modelDownloading, value); }
    public double ModelProgress { get => modelProgress; private set => Set(ref modelProgress, value); }
    public string? ModelError { get => modelError; private set => Set(ref modelError, value); }

    public UserSettings Settings
    {
        get => settings;
        set
        {
            var old = settings;
            if (!Set(ref settings, value)) return;
            settingsStore.Save(value);
            server.SendStyle(value.Style);
            if (isRunning && (old.SourceLanguage != value.SourceLanguage || old.TargetLanguage != value.TargetLanguage
                              || old.OnlineTranslation != value.OnlineTranslation))
            {
                RefreshTranslatorStatus();
                MakePipeline();
            }
        }
    }

    // --- المعالج ---

    public bool CanAdvance => true;

    public void Advance()
    {
        if (!CanAdvance || step == OnboardingStep.Done) return;
        // إن احتاج محرك الترجمة إلى تنزيل فإنه يجري في الخلفية بينما ينشغل المستخدم بالخطوات الأخرى.
        if (step == OnboardingStep.Welcome) StartModelDownload();
        Step = step + 1;
    }

    public void Back()
    {
        if (step > OnboardingStep.Welcome) Step = step - 1;
    }

    public async Task CompletePsnLoginAsync(Uri redirect)
    {
        var id = await services.AccountIdFromRedirectAsync(redirect);
        accountId = id;
        store.Save(StoreKeys.AccountId, id);
        Changed(nameof(HasAccount), nameof(CanAdvance));
    }

    public async Task CompletePsnLoginWithCodeAsync(string code)
    {
        var id = await PsnAccount.AccountIdAsync(code);
        accountId = id;
        store.Save(StoreKeys.AccountId, id);
        Changed(nameof(HasAccount), nameof(CanAdvance));
    }

    public async Task<string> InstallAndroidTvAppAsync(string host, Action<TvInstallEvent> onEvent)
    {
        var app = services.BundledApp(TvBrand.AndroidTv)
                  ?? throw new FileNotFoundException("لم يُعثر على حزمة APK الخاصة بـ Android TV (tv-app\\gametranslator-androidtv_1.0.0.apk).");
        var version = await services.InstallAndroidAppAsync(host, app, onEvent);
        var target = new LgTv("Android TV", host, TvBrand.AndroidTv);
        tv = target;
        store.Save(StoreKeys.Tv, target);
        store.Save(StoreKeys.TvAppVersion, version);
        Changed(nameof(Tv));
        TvApp = new TvAppState(TvAppKind.UpToDate, version);
        return version;
    }

    public Task<IReadOnlyList<ConsoleInfo>> DiscoverConsolesAsync() => services.DiscoverConsolesAsync();

    public async Task RegisterAsync(string host, string pin)
    {
        var digits = new string([.. pin.Where(char.IsDigit)]);
        if (digits.Length != 8 || !uint.TryParse(digits, out var value)) throw Error(AppError.InvalidPin);
        if (accountId is null)
        {
            // إن لم يُسجَّل الدخول إلى PSN فعيّن معرّف اقتران محلي افتراضي
            accountId = System.Text.Encoding.ASCII.GetBytes("GTPS5ACC");
            store.Save(StoreKeys.AccountId, accountId);
            Changed(nameof(HasAccount));
        }
        RegisteredConsole registered;
        try
        {
            registered = await services.RegisterAsync(host, accountId, value);
        }
        catch (RegistrationException error)
        {
            // إن أبلغ PS5 برمز الخطأ الخاص به فقد وصلنا الرد، وجدار الحماية ليس السبب.
            if (error.Reason is null && error.Failure != RegistrationFailure.WrongAccount) await OfferFirewallAsync();
            throw RegistrationError(error);
        }
        catch (Exception error) when (error is not AppException)
        {
            await OfferFirewallAsync();
            throw;
        }
        ClearFirewallPrompt();
        console = registered;
        store.Save(StoreKeys.Console, registered);
        Changed(nameof(Console), nameof(CanAdvance), nameof(Ps5StatusText), nameof(Ps5Tone));
    }

    /// <summary>الخطأ المعروض للمستخدم؛ يُكتب معه رمز PS5 كي يمكن تشخيص بلاغات الدعم.</summary>
    private AppException RegistrationError(RegistrationException error)
    {
        var kind = error.Failure switch
        {
            RegistrationFailure.WrongAccount => AppError.WrongAccount,
            RegistrationFailure.NoAnswer => AppError.ConsoleNoAnswer,
            RegistrationFailure.InUse => AppError.ConsoleInUse,
            RegistrationFailure.ConsoleCrashed => AppError.ConsoleCrashed,
            RegistrationFailure.VersionMismatch => AppError.ConsoleVersion,
            _ => AppError.RegistrationFailed,
        };
        var message = AppException.Describe(kind, text);
        return new AppException(kind, string.IsNullOrEmpty(error.Detail) ? message : $"{message} ({error.Detail})");
    }

    public Task<IReadOnlyList<LgTv>> DiscoverTvsAsync() => services.DiscoverTvsAsync();

    public async Task PairAsync(LgTv target)
    {
        try { await StartServerAsync(); } catch { }
        TvPairing pairing;
        try
        {
            pairing = await services.PairTvAsync(target, store.Load<string>(StoreKeys.TvKey), ServerAddress(target));
        }
        catch
        {
            pairing = new TvPairing("", false);
        }
        if (tv?.Brand != target.Brand || (target.Brand != TvBrand.AndroidTv && tv?.Host != target.Host))
            store.Remove(StoreKeys.TvAppVersion);
        tv = target;
        store.Save(StoreKeys.Tv, target);
        if (!string.IsNullOrEmpty(pairing.Key))
            store.Save(StoreKeys.TvKey, pairing.Key);
        tvAppMissing = !pairing.Launched;
        Changed(nameof(Tv), nameof(CanAdvance), nameof(CanInstallTvApp), nameof(TvStatusText), nameof(TvTone));
        try
        {
            if (target.Brand == TvBrand.Lg) await RefreshTvAppAsync();
            else _ = RefreshTvAppAsync();
        }
        catch { }
    }

    // --- تثبيت تطبيق التلفزيون ---

    public TvAppState TvApp
    {
        get => tvApp;
        private set
        {
            tvApp = value;
            Changed(nameof(TvApp));
        }
    }

    /// <summary>المدة المتبقية لوضع المطوّر في LG (بالساعات)؛ تُعرف بعد أن يمدّدها GameTranslator.</summary>
    public int? DevModeHoursLeft
    {
        get => devModeHoursLeft;
        private set
        {
            devModeHoursLeft = value;
            Changed(nameof(DevModeHoursLeft));
        }
    }

    /// <summary>هل يستطيع GameTranslator تثبيت التطبيق على التلفزيون المحدد (هل جاءت الحزمة مع التطبيق)؟</summary>
    public bool CanInstallTvApp => tv is { } target &&
        (target.Brand == TvBrand.Lg ? services.BundledTvAppVersion is not null : services.BundledApp(target.Brand) is not null);

    /// <summary>يفتح صفحة شهادة Samsung في المتصفح (تعيّنه الواجهة؛ ويبقى فارغا في الاختبارات).</summary>
    public Action<Uri> OpenInBrowser { get; set; } = _ => { };

    /// <summary>عنوان هذا الكمبيوتر الذي يُكتب في Samsung في خانة "Host PC IP" ضمن وضع المطوّر.</summary>
    public string? ComputerAddress => LocalAddress(tv is { Host.Length: > 0 } target ? target.Host : "192.168.1.1");

    /// <summary>
    /// على تلفزيون وضع المطوّر مفعّل فيه: يثبّت التطبيق إن كان ناقصا ويحدّثه إن كان قديما.
    /// إن لم يوجد مفتاح والتطبيق يعمل فلا يُطلب شيء من المستخدم.
    /// </summary>
    public async Task RefreshTvAppAsync()
    {
        if (tv is not { } target || refreshingTvApp) return;
        refreshingTvApp = true;
        try
        {
            switch (target.Brand)
            {
                case TvBrand.Lg: await RefreshLgAppAsync(target); break;
                case TvBrand.Samsung: await RefreshSamsungAppAsync(target); break;
                case TvBrand.AndroidTv: await RefreshAndroidAppAsync(target); break;
            }
        }
        finally
        {
            refreshingTvApp = false;
        }
    }

    private string? InstalledAppVersion => store.Load<string>(StoreKeys.TvAppVersion);

    private bool NeedsInstall(BundledTvApp bundled) =>
        InstalledAppVersion is not { } installed || DevModeInstaller.IsNewer(bundled.Version, installed);

    private async Task RefreshLgAppAsync(LgTv target)
    {
        if (services.BundledTvAppVersion is not { } bundled) return;
        if (!await services.IsDevModeOnAsync(target.Host))
        {
            TvApp = tvAppMissing ? new TvAppState(TvAppKind.DevModeOff) : TvAppState.Unknown;
            return;
        }
        if (store.Load<string>(StoreKeys.TvDevKey) is not { } devKey)
        {
            TvApp = tvAppMissing ? new TvAppState(TvAppKind.NeedsPassphrase) : TvAppState.Unknown;
            return;
        }
        await InstallTvAppAsync(target, devKey, bundled);
    }

    /// <summary>عند فشل التثبيت يُعرض أيضا السبب الذي ذكره التلفزيون؛ فالرسالة العامة وحدها لم تكن تكفي لمعرفة المشكلة.</summary>
    private static TvAppState FailedState(DevModeException error) =>
        new(TvAppKind.Failed, Error: error.Error,
            Reason: error.Error is DevModeError.InstallFailed or DevModeError.NotEnoughSpace && !string.IsNullOrWhiteSpace(error.Detail)
                ? error.Detail.Trim() : null);

    /// <summary>Samsung: إن كان وضع المطوّر مفعّلا يوقّع التطبيق ويثبّته. إن لم توجد شهادة يُطلب تسجيل الدخول إلى Samsung.</summary>
    private async Task RefreshSamsungAppAsync(LgTv target)
    {
        if (services.BundledApp(TvBrand.Samsung) is not { } bundled) return;
        if (!NeedsInstall(bundled))
        {
            TvApp = new TvAppState(TvAppKind.UpToDate, InstalledAppVersion ?? bundled.Version);
            return;
        }
        if (!await services.IsSamsungDevModeOnAsync(target.Host))
        {
            TvApp = new TvAppState(TvAppKind.DevModeOff);
            return;
        }
        TvApp = new TvAppState(TvAppKind.Working);
        try
        {
            await services.InstallSamsungAppAsync(target.Host, bundled, e => post(() => InstallEvent(e)));
            store.Save(StoreKeys.TvAppVersion, bundled.Version);
            TvApp = new TvAppState(TvAppKind.UpToDate, bundled.Version);
            await OpenTvAppAfterInstallAsync(target);
        }
        catch (DevModeException error)
        {
            TvApp = FailedState(error);
        }
    }

    /// <summary>Android TV: يجد على الشبكة التلفزيون المفعّل فيه ADB، ثم يثبّت التطبيق ويفتحه.</summary>
    private async Task RefreshAndroidAppAsync(LgTv target)
    {
        if (services.BundledApp(TvBrand.AndroidTv) is not { } bundled) return;
        var host = target.Host;
        if (host.Length == 0 || !await services.IsAndroidDebugOnAsync(host))
        {
            // إن كان التطبيق مثبّتا وتصحيح الأخطاء معطّلا في التلفزيون فلا شيء يُفعل.
            if (!NeedsInstall(bundled))
            {
                TvApp = new TvAppState(TvAppKind.UpToDate, InstalledAppVersion ?? bundled.Version);
                return;
            }
            if ((await services.ScanAndroidTvsAsync()).FirstOrDefault() is not { } found)
            {
                TvApp = new TvAppState(TvAppKind.DevModeOff);
                return;
            }
            host = found;
        }
        TvApp = new TvAppState(TvAppKind.Working);
        try
        {
            var version = await services.InstallAndroidAppAsync(host, bundled, e => post(() => InstallEvent(e)));
            // عنوان التلفزيون يُستخدم بعد الآن لفحص الإصدار.
            var located = target with { Host = host };
            tv = located;
            store.Save(StoreKeys.Tv, located);
            store.Save(StoreKeys.TvAppVersion, version);
            Changed(nameof(Tv));
            TvApp = new TvAppState(TvAppKind.UpToDate, version);
        }
        catch (DevModeException error)
        {
            TvApp = FailedState(error);
        }
    }

    private void InstallEvent(TvInstallEvent e)
    {
        // حدث متأخر يصل بعد انتهاء التثبيت يجب ألا يطمس النتيجة.
        if (TvApp.Kind is not (TvAppKind.Working or TvAppKind.SignIn or TvAppKind.ApproveOnTv)) return;
        switch (e.Kind)
        {
            case TvInstallEventKind.SignIn when e.Url is { } url:
                TvApp = new TvAppState(TvAppKind.SignIn, Url: url);
                OpenInBrowser(url);
                break;
            case TvInstallEventKind.ApproveOnTv:
                TvApp = new TvAppState(TvAppKind.ApproveOnTv);
                break;
            case TvInstallEventKind.Progress when TvApp.Kind == TvAppKind.SignIn:
                TvApp = new TvAppState(TvAppKind.Working);
                break;
        }
    }

    /// <summary>يجلب المفتاح من Key Server (بكلمة المرور الظاهرة على شاشة التلفزيون) ويثبّت التطبيق.</summary>
    public async Task InstallTvAppAsync(string passphrase)
    {
        if (tv is not { } target || services.BundledTvAppVersion is not { } bundled) return;
        TvApp = new TvAppState(TvAppKind.Working);
        try
        {
            var devKey = await services.FetchDevKeyAsync(target.Host, passphrase.Trim());
            store.Save(StoreKeys.TvDevKey, devKey);
            await InstallTvAppAsync(target, devKey, bundled);
        }
        catch (DevModeException error)
        {
            TvApp = new TvAppState(TvAppKind.NeedsPassphrase, Error: error.Error);
        }
    }

    private async Task InstallTvAppAsync(LgTv target, string devKey, string bundled)
    {
        TvApp = new TvAppState(TvAppKind.Working);
        try
        {
            var installed = await services.InstalledTvAppVersionAsync(target.Host, devKey);
            if (installed is not null && !DevModeInstaller.IsNewer(bundled, installed))
            {
                TvApp = new TvAppState(TvAppKind.UpToDate, installed);
                await RenewDevSessionIfDueAsync(target, devKey);
                return;
            }
            await services.InstallTvAppAsync(target.Host, devKey);
            TvApp = new TvAppState(TvAppKind.UpToDate, bundled);
            tvAppMissing = false;
            await RenewDevSessionIfDueAsync(target, devKey);
            await OpenTvAppAfterInstallAsync(target);
        }
        catch (DevModeException error) when (error.Error == DevModeError.KeyRefused)
        {
            // عند إعادة تفعيل وضع المطوّر يولّد التلفزيون مفتاحا جديدا؛ والقديم لا ينفع.
            store.Remove(StoreKeys.TvDevKey);
            TvApp = new TvAppState(TvAppKind.NeedsPassphrase);
        }
        catch (DevModeException error)
        {
            TvApp = FailedState(error);
        }
    }

    /// <summary>
    /// يعيد بدء مدة الـ 1000 ساعة لوضع المطوّر في LG مرة واحدة يوميا على الأكثر؛ فلا يضطر المستخدم
    /// إلى تمديدها من التلفزيون.
    /// </summary>
    private async Task RenewDevSessionIfDueAsync(LgTv target, string devKey)
    {
        var last = store.Load<DateTimeOffset?>(StoreKeys.DevSessionRenewed) ?? DateTimeOffset.MinValue;
        if (DateTimeOffset.UtcNow - last < TimeSpan.FromHours(20)) return;
        if (await services.RenewDevSessionAsync(target.Host, devKey) is { } hours)
        {
            DevModeHoursLeft = hours;
            store.Save(StoreKeys.DevSessionRenewed, (DateTimeOffset?)DateTimeOffset.UtcNow);
        }
    }

    private async Task OpenTvAppAfterInstallAsync(LgTv target)
    {
        if (keeper is not null)
        {
            keeper.Reconnect();
            return;
        }
        try { await services.PairTvAsync(target, store.Load<string>(StoreKeys.TvKey), ServerAddress(target)); }
        catch (Exception) { /* ثُبّت تطبيق التلفزيون؛ إن لم يُفتح فالمستخدم يستطيع فتحه */ }
    }

    /// <summary>عُثر على التلفزيون في عنوان جديد على الشبكة.</summary>
    public void TvMoved(LgTv moved)
    {
        tv = moved;
        store.Save(StoreKeys.Tv, moved);
        Changed(nameof(Tv));
    }

    public void StartModelDownload()
    {
        if (ModelInstalled || modelDownload is { IsCompleted: false }) return;
        ModelDownloading = true;
        ModelError = null;
        var download = services.Translation.DownloadAsync(settings.SourceLanguage, settings.TargetLanguage, new ImmediateProgress(value => post(() => ModelProgress = value)), CancellationToken.None);
        modelDownload = FinishDownload(download);
    }

    private async Task FinishDownload(Task download)
    {
        try
        {
            await download;
            post(() => ModelProgress = 1);
        }
        catch (Exception error)
        {
            post(() => ModelError = error.Message);
        }
        finally
        {
            post(() =>
            {
                ModelDownloading = false;
                Changed(nameof(ModelInstalled), nameof(CanAdvance));
                RefreshTranslatorStatus();
                // إن اكتمل التنزيل أثناء الترجمة يُعاد بناء خط المعالجة بالمترجم الجديد.
                if (isRunning && TranslatorStatus == TranslatorStatus.Ready) MakePipeline();
            });
        }
    }

    public Task WaitForModelAsync() => modelDownload ?? Task.CompletedTask;

    public void SyncStyle()
    {
        server.SendStyle(settings.Style);
    }

    public void SendTestSubtitle()
    {
        server.SendStyle(settings.Style);
        // إن لم تُشاهد ترجمة نصية في اللعبة بعد، يُستخدم مكان نموذجي قرب أسفل منطقة القراءة.
        var region = settings.Region;
        var box = lastSubtitleBox ?? new TextBox(0.3, region.Bottom - 0.1, 0.7, region.Bottom - 0.03);
        server.SendSubtitle(text["This is a test subtitle. Can you see it on top of the game?"], 8000, box);
    }

    /// <summary>
    /// Num 0: يوقف ترجمة النص الحية مؤقتا أو يستأنفها. يُستخدم كي لا تظهر الترجمة المستمرة على التلفزيون
    /// وتختلط أثناء ترجمة منطقة. يظهر إشعار قصير على التلفزيون. لا يفعل شيئا إن لم تكن الترجمة جارية.
    /// </summary>
    public void ToggleSubtitles()
    {
        if (!isRunning) return;
        var pause = !subtitlesPaused;
        SubtitlesPaused = pause;
        if (pause)
        {
            pipeline?.Reset();
            server.Clear();
        }
        server.SendStyle(settings.Style);
        server.SendSubtitle(text[pause ? "Subtitle translation paused" : "Subtitle translation resumed"], 1800);
    }

    /// <summary>Num 8: لتلتقط شاشة الإعدادات صورة اللعبة الحالية من جديد.</summary>
    public event Action? SnapshotRequested;

    public void RequestSnapshot() => SnapshotRequested?.Invoke();

    /// <summary>يُستدعى بـ (النص، مدة البقاء بالمللي ثانية) إن كانت الترجمة ستُعرض على الكمبيوتر أيضا.</summary>
    public event Action<string, int>? PcResultReady;

    /// <summary>
    /// يقرأ المنطقة الإضافية رقم 1 أو 2 أو 3 الآن مرة واحدة ويترجمها؛ تذهب النتيجة إلى المكان المحدد في الإعدادات (التلفزيون أو الكمبيوتر أو كليهما)
    /// (لنصوص مثل لوحة المهام). لا يُفعل شيء إن لم تكن الترجمة جارية أو كانت المنطقة معطّلة أو لم ينتهِ الطلب السابق.
    /// </summary>
    public async Task<ExtraRegionOutcome> TranslateExtraRegionAsync(int slot)
    {
        if (settings.ExtraRegion(slot) is not { } region) return ExtraRegionOutcome.NotConfigured;
        if (lastFrame is not { } frame) return ExtraRegionOutcome.NotRunning;
        var target = settings.ExtraTarget(slot);
        return (await RunRegionAsync(frame, region, target != ResultTarget.Pc, target != ResultTarget.Tv)).Outcome;
    }

    /// <summary>
    /// المنطقة الحرة (Num 5): يقرأ المنطقة في الإطار المعطى مرة واحدة ويترجمها. تُرسل النتيجة إلى التلفزيون أيضا حسب الهدف في الإعدادات؛
    /// وتُعاد في كل الأحوال لتُعرض على الكمبيوتر (نافذة المنطقة الحرة تعرضها بنفسها).
    /// </summary>
    public Task<(ExtraRegionOutcome Outcome, RegionTranslation.Result? Result)> TranslateAreaAsync(Frame frame, CropRegion region) =>
        RunRegionAsync(frame, region, settings.FreeAreaTarget != ResultTarget.Pc, false);

    private async Task<(ExtraRegionOutcome Outcome, RegionTranslation.Result? Result)> RunRegionAsync(Frame frame, CropRegion region, bool toTv, bool toPcPopup)
    {
        var reader = activeReader;
        if (!isRunning || reader is null) return (ExtraRegionOutcome.NotRunning, null);
        // الضغط المتكرر على المفتاح لا يبدأ قراءة ثانية.
        if (Interlocked.Exchange(ref extraBusy, 1) == 1) return (ExtraRegionOutcome.Busy, null);
        try
        {
            RegionTranslation.Result? result;
            try
            {
                result = await RegionTranslation.TranslateOnceAsync(frame, region, reader, MakeTranslator());
            }
            catch (TranslatorException error)
            {
                post(() => TranslatorStatus = error.Status);
                return (ExtraRegionOutcome.TranslatorUnavailable, null);
            }
            catch
            {
                return (ExtraRegionOutcome.Failed, null);
            }
            if (result is null)
            {
                var message = text["No text found in this area."];
                if (toTv)
                {
                    server.SendStyle(settings.Style);
                    server.SendSubtitle(message, 2500, new TextBox(region.Left, region.Top, region.Right, region.Bottom));
                }
                if (toPcPopup) post(() => PcResultReady?.Invoke(message, 2500));
                return (ExtraRegionOutcome.NoText, null);
            }
            var milliseconds = RegionTranslation.DisplayMilliseconds(result.Translation);
            if (toTv)
            {
                server.SendStyle(settings.Style);
                server.SendSubtitle(result.Translation, milliseconds, result.Box, result.Structured);
            }
            post(() =>
            {
                LastSource = result.Source;
                LastTranslation = result.Translation;
                if (toPcPopup) PcResultReady?.Invoke(result.Translation, milliseconds);
            });
            return (ExtraRegionOutcome.Shown, result);
        }
        finally
        {
            Volatile.Write(ref extraBusy, 0);
        }
    }

    /// <summary>مهمة "ترجِم الشاشة" الجارية؛ تُلغى عند الضغط مجددا.</summary>
    private CancellationTokenSource? screenWork;
    /// <summary>اللحظة التي ستُزال فيها ترجمات الشاشة عن التلفزيون؛ الضغط مجددا قبلها يزيلها.</summary>
    private DateTime screenShownUntil;

    /// <summary>
    /// Num 9: يقرأ كامل إطار اللعبة الحالي مرة واحدة، ويترجم كل مجموعة نص على حدة ويضعها في مكانها على التلفزيون.
    /// تذهب الترجمات إلى التلفزيون فور تجهيزها. الضغط مجددا أثناء الترجمة أو أثناء ظهور الترجمات على التلفزيون يزيلها.
    /// </summary>
    public async Task<ScreenOutcome> TranslateScreenAsync()
    {
        var reader = activeReader;
        if (!isRunning || reader is null || lastFrame is not { } frame) return ScreenOutcome.NotRunning;
        if (screenWork is { } running)
        {
            running.Cancel();
            screenWork = null;
            HideScreen();
            return ScreenOutcome.Cleared;
        }
        if (DateTime.UtcNow < screenShownUntil)
        {
            HideScreen();
            return ScreenOutcome.Cleared;
        }

        using var work = new CancellationTokenSource();
        screenWork = work;
        server.SendStyle(settings.Style);
        server.SendSubtitle(text["Translating the screen…"], 2500);
        try
        {
            var translator = MakeTranslator();
            var items = await ScreenTranslation.TranslateAsync(frame, reader, translator, shown =>
            {
                if (work.IsCancellationRequested) return;
                server.SendScreen(shown, ScreenTranslation.DisplayMilliseconds(shown));
                screenShownUntil = DateTime.UtcNow.AddMilliseconds(ScreenTranslation.DisplayMilliseconds(shown));
            }, work.Token);
            server.Clear();
            if (items.Count == 0)
            {
                server.SendSubtitle(text["No text found on the screen."], 2500);
                return ScreenOutcome.NoText;
            }
            return ScreenOutcome.Shown;
        }
        catch (OperationCanceledException)
        {
            return ScreenOutcome.Cleared;
        }
        catch (TranslatorException error)
        {
            server.Clear();
            post(() => TranslatorStatus = error.Status);
            return ScreenOutcome.TranslatorUnavailable;
        }
        catch
        {
            server.Clear();
            return ScreenOutcome.Failed;
        }
        finally
        {
            if (screenWork == work) screenWork = null;
        }
    }

    private void HideScreen()
    {
        screenShownUntil = default;
        server.ClearScreen();
        server.Clear();
    }

    /// <summary>يعيد الإعداد من البداية (يحذف كل ما حُفظ).</summary>
    public void ResetSetup()
    {
        Stop();
        foreach (var key in new[] { StoreKeys.AccountId, StoreKeys.Console, StoreKeys.Tv, StoreKeys.TvKey, StoreKeys.TvDevKey, StoreKeys.TvAppVersion }) store.Remove(key);
        accountId = null;
        console = null;
        tv = null;
        tvAppMissing = false;
        TvApp = TvAppState.Unknown;
        Step = OnboardingStep.Welcome;
        Changed(nameof(HasAccount), nameof(Console), nameof(Tv));
    }

    /// <summary>يعيد كل إعدادات المستخدم إلى قيمها الافتراضية.</summary>
    public void ResetSettings()
    {
        Settings = new UserSettings();
    }

    // --- الترجمة ---

    public async Task StartAsync()
    {
        // الضغط على الزر مرتين أو الاستدعاء مجددا أثناء الانتظار لا يفتح جلسة ثانية.
        if (isRunning || isStarting || console is null || accountId is null || tv is null) return;
        IsStarting = true;
        try
        {
            RefreshTranslatorStatus();
            try { await StartServerAsync(); } catch { /* إن تعذّر فتح المنفذ تبقى حالة التلفزيون "في الانتظار" */ }
            server.SendStyle(settings.Style);
            SubtitlesPaused = false;
            MakePipeline();

            var stream = services.MakeStream(console, accountId);
            stream.StateChanged += state => post(() => SessionStateChanged(state));
            stream.ConsoleMoved += moved => post(() => ConsoleMoved(moved));
            stream.FrameReceived += HandleFrame;
            stream.Start();
            session = stream;

            // شغّل تطبيق التلفزيون وأبقه مفتوحا؛ إن كان التلفزيون مطفأ يتصل عند تشغيله.
            var running = server;
            var tvKeeper = services.MakeKeeper(tv, store.Load<string>(StoreKeys.TvKey), new Dictionary<string, string> { ["server"] = ServerAddress(tv) },
                () => running.ConnectedClients > 0, moved => post(() => TvMoved(moved)));
            tvKeeper.Start(TimeSpan.FromSeconds(5));
            keeper = tvKeeper;
            _ = RefreshTvAppAsync();

            sleep.Begin();
            IsRunning = true;
            UpdateTroubleClocks();
        }
        finally
        {
            IsStarting = false;
        }
    }

    public void Stop()
    {
        session?.Stop();
        session?.Dispose();
        session = null;
        keeper?.Stop();
        keeper = null;
        pipeline?.Reset();
        pipeline = null;
        activeReader = null;
        screenWork?.Cancel();
        screenWork = null;
        if (DateTime.UtcNow < screenShownUntil) server.ClearScreen();
        screenShownUntil = default;
        SubtitlesPaused = false;
        ActiveSourceLanguage = null;
        if (isRunning) sleep.End();
        IsRunning = false;
        SessionState = SessionState.Disconnected(DisconnectKind.Stopped);
        UpdateTroubleClocks();
        firewallRuleSeen = false;
        ClearFirewallPrompt();
    }

    /// <summary>عند استيقاظ الكمبيوتر من السكون تُجدَّد الاتصالات فورا.</summary>
    public void SystemResumed()
    {
        session?.ReconnectNow();
        keeper?.Reconnect();
    }

    /// <summary>عدم الاتصال قبل بدء الترجمة ليس مشكلة؛ تبقى النقطة رمادية.</summary>
    public static StatusTone Ps5ToneFor(SessionState state, bool running) =>
        state.Phase == SessionPhase.Streaming ? StatusTone.Good : running ? StatusTone.Warning : StatusTone.Idle;

    public static StatusTone TvToneFor(int clients, bool running) =>
        clients > 0 ? StatusTone.Good : running ? StatusTone.Warning : StatusTone.Idle;

    /// <summary>نص حالة PS5 الذي يُعرض للمستخدم.</summary>
    public static string StatusText(SessionState state, bool running, Localizer text)
    {
        switch (state.Phase)
        {
            case SessionPhase.Streaming: return text["Connected"];
            case SessionPhase.Connecting: return text["Connecting…"];
        }
        if (!running) return text["Not connected"];
        return text[state.Reason?.Kind switch
        {
            DisconnectKind.ConsoleAsleep => "Turn on your PS5",
            DisconnectKind.InUse => "Remote Play is in use on another device",
            DisconnectKind.Refused => "The PS5 refused the connection. Set up the PS5 again.",
            DisconnectKind.LoginPasscode => "The second user has a login passcode on the PS5. Turn it off in Settings › Users and Accounts.",
            _ => "Reconnecting…",
        }];
    }

    public void Dispose()
    {
        Stop();
        server.Dispose();
    }

    // --- الآلية الداخلية ---

    /// <summary>يبني خط القراءة (من جديد) باللغات الحالية.</summary>
    private void MakePipeline()
    {
        pipeline?.Reset();
        pipeline = null;
        ActiveSourceLanguage = settings.SourceLanguage;
        var reader = services.MakeReader(settings.SourceLanguage);
        activeReader = reader;
        OcrMissing = reader is null || !services.HasOcrLanguage(settings.SourceLanguage);
        if (reader is null) return;
        var built = new SubtitlePipeline(reader, MakeTranslator(), server, new ReadScheduler());
        built.PairShown += (source, translation) =>
        {
            var box = built.LastBox;
            post(() =>
            {
                LastSource = source;
                LastTranslation = translation;
                if (box is not null) lastSubtitleBox = box;
            });
        };
        // قد يصبح المترجم جاهزا في هذه الأثناء؛ كل نتيجة ترجمة تحدّث الحالة.
        built.TranslationResult += error => post(() => TranslatorStatus = error ?? TranslatorStatus.Ready);
        pipeline = built;
    }

    private ITextTranslator MakeTranslator()
    {
        var local = MakeLocalTranslator();
        if (OnlineTranslationActive && store.Load<string>(StoreKeys.DeepLKey) is { } key &&
            services.MakeOnlineTranslator(key, local is UnavailableTranslator ? null : local, settings.TargetLanguage) is { } online)
            return online;
        return local;
    }

    private ITextTranslator MakeLocalTranslator()
    {
        var translation = services.Translation;
        var (source, target) = (settings.SourceLanguage, settings.TargetLanguage);
        return translation.Create(source, target);
    }

    /// <summary>DeepL يُفعّل في الألعاب الإنجليزية فقط.</summary>
    private bool OnlineTranslationActive =>
        settings.OnlineTranslation && settings.SourceLanguage == "en" && HasDeepLKey;

    /// <summary>هل يوجد مفتاح DeepL محفوظ (المفتاح نفسه لا يعود إلى الواجهة أبدا).</summary>
    public bool HasDeepLKey => store.Load<string>(StoreKeys.DeepLKey) is { Length: > 0 };

    /// <summary>يُجرَّب المفتاح؛ إن كان صالحا (ولو انتهت حصته) يُكتب في المخزن المشفّر وتُفعّل الترجمة عبر الإنترنت.</summary>
    public async Task<OnlineKeyCheck> SetDeepLKeyAsync(string key)
    {
        key = key.Trim();
        var check = await services.CheckOnlineKeyAsync(key);
        if (check.State is OnlineKeyState.Valid or OnlineKeyState.QuotaExceeded)
        {
            store.Save(StoreKeys.DeepLKey, key);
            Changed(nameof(HasDeepLKey));
            if (settings.OnlineTranslation && isRunning) MakePipeline();
            else Settings = settings with { OnlineTranslation = true };
            RefreshTranslatorStatus();
        }
        return check;
    }

    /// <summary>استخدام المفتاح المحفوظ في هذا الشهر.</summary>
    public Task<OnlineKeyCheck> CheckDeepLKeyAsync() =>
        store.Load<string>(StoreKeys.DeepLKey) is { Length: > 0 } key ? services.CheckOnlineKeyAsync(key)
            : Task.FromResult(new OnlineKeyCheck(OnlineKeyState.Invalid));

    public void RemoveDeepLKey()
    {
        store.Remove(StoreKeys.DeepLKey);
        Changed(nameof(HasDeepLKey));
        Settings = settings with { OnlineTranslation = false };
        RefreshTranslatorStatus();
    }

    private void RefreshTranslatorStatus()
    {
        TranslatorStatus = TranslatorStatus.Ready;
    }

    /// <summary>تغيّر عنوان PS5: لتبدأ التشغيلات التالية بالعنوان الجديد.</summary>
    private void ConsoleMoved(RegisteredConsole moved)
    {
        if (console is null) return;
        console = moved;
        store.Save(StoreKeys.Console, moved);
        Changed(nameof(Console));
    }

    private void SessionStateChanged(SessionState state)
    {
        SessionState = state;
        UpdateTroubleClocks();
        // إن انقطع الاتصال فلا تبقى ترجمة قديمة على التلفزيون.
        if (state.Phase == SessionPhase.Disconnected)
        {
            pipeline?.Reset();
            server.Clear();
        }
    }

    public void FeedFrame(Frame frame) => HandleFrame(frame);

    private async void HandleFrame(Frame frame)
    {
        post(() => LastFrame = frame);
        // عند الإيقاف المؤقت يُحفظ الإطار للمناطق الإضافية والمنطقة الحرة فقط؛ ولا تُقرأ الترجمة النصية.
        if (subtitlesPaused) return;
        // إن كان الإطار السابق ما زال قيد المعالجة يُتجاوز هذا الإطار؛ فالقراءة لا تراكم الإطارات.
        var current = pipeline;
        if (current is null || Interlocked.Exchange(ref processing, 1) == 1) return;
        try
        {
            await current.ProcessAsync(frame, settings.Region);
        }
        catch
        {
            // تعذّر قراءة إطار واحد لا يوقف سير العمل.
        }
        finally
        {
            Volatile.Write(ref processing, 0);
        }
    }

    private async Task StartServerAsync()
    {
        if (serverStarted) return;
        try
        {
            ServerPort = await server.StartAsync();
        }
        catch (SocketException)
        {
            // 8787 مستخدم في تطبيق آخر؛ ولأن العنوان يُبلَّغ للتلفزيون في كل مرة فأي منفذ حر يصلح.
            server.ClientsChanged -= OnClientsChanged;
            server = new SubtitleServer(0);
            server.ClientsChanged += OnClientsChanged;
            ServerPort = await server.StartAsync();
        }
        serverStarted = true;
    }

    private void OnClientsChanged(int count) => post(() =>
    {
        TvClients = count;
        UpdateTroubleClocks();
    });

    /// <summary>عنوان الكمبيوتر على الشبكة نفسها مع التلفزيون.</summary>
    private string ServerAddress(LgTv target) => $"http://{LocalAddress(target.Host) ?? "127.0.0.1"}:{ServerPort}";

    public static string? LocalAddress(string near)
    {
        if (!IPAddress.TryParse(near, out var target) || target.AddressFamily != AddressFamily.InterNetwork) return null;
        string? fallback = null;
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up || network.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var unicast in network.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var mask = unicast.IPv4Mask.GetAddressBytes();
                var address = unicast.Address.GetAddressBytes();
                var wanted = target.GetAddressBytes();
                if (Enumerable.Range(0, 4).All(i => (address[i] & mask[i]) == (wanted[i] & mask[i]))) return unicast.Address.ToString();
                fallback ??= unicast.Address.ToString();
            }
        }
        return fallback;
    }

    // --- جدار حماية Windows ---

    /// <summary>إن بقي PS5 على "جارٍ إعادة الاتصال…" هذه المدة تُفحص قاعدة جدار الحماية.</summary>
    public static readonly TimeSpan Ps5TroubleDelay = TimeSpan.FromSeconds(20);
    /// <summary>إن لم يتصل تطبيق التلفزيون طوال هذه المدة تُفحص قاعدة جدار الحماية.</summary>
    public static readonly TimeSpan TvWaitDelay = TimeSpan.FromSeconds(60);

    private FirewallPrompt firewallPrompt;
    /// <summary>وُجدت القاعدة في جلسة الترجمة هذه؛ فلا تُفحص القواعد من جديد في كل تحقق.</summary>
    private bool firewallRuleSeen;
    private bool checkingFirewall;
    private DateTime? ps5TroubleSince;
    private DateTime? tvWaitingSince;

    /// <summary>بطاقة جدار الحماية أو الزر في خطوة الاقتران؛ يُقترح فقط عند غياب القاعدة.</summary>
    public FirewallPrompt FirewallPrompt { get => firewallPrompt; private set => Set(ref firewallPrompt, value); }

    /// <summary>الوقت (يُقدَّم إلى الأمام في الاختبارات).</summary>
    public Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

    /// <summary>
    /// تستدعيه الشاشة الرئيسية كل بضع ثوان: إن كان PS5 يعيد الاتصال منذ 20 ثانية أو لم يتصل التلفزيون منذ 60 ثانية
    /// ولم يكن GameTranslator مسموحا به في جدار الحماية تُقترح البطاقة. وتُزال البطاقة حين يتصل الاثنان.
    /// </summary>
    public async Task CheckConnectionsAsync()
    {
        UpdateTroubleClocks();
        if (!isRunning || services.Firewall is not { } firewall) return;
        if (ps5TroubleSince is null && tvWaitingSince is null)
        {
            ClearFirewallPrompt();
            return;
        }
        var now = Clock();
        var stuck = now - ps5TroubleSince >= Ps5TroubleDelay || now - tvWaitingSince >= TvWaitDelay;
        if (!stuck || firewallPrompt != FirewallPrompt.None || firewallRuleSeen || checkingFirewall) return;
        checkingFirewall = true;
        try
        {
            var hasRule = await HasFirewallRuleAsync(firewall);
            if (!isRunning) return;
            if (hasRule) firewallRuleSeen = true;
            else FirewallPrompt = FirewallPrompt.Offer;
        }
        finally
        {
            checkingFirewall = false;
        }
    }

    /// <summary>
    /// يضيف القاعدة بإذن المسؤول. إن كانت الترجمة جارية يُعاد الاتصال بـ PS5 والتلفزيون فورا؛ وفي الاقتران يعيد المستخدم
    /// الاتصال برمز جديد.
    /// </summary>
    public async Task<bool> AllowThroughFirewallAsync()
    {
        if (services.Firewall is not { } firewall || firewallPrompt == FirewallPrompt.Working) return false;
        FirewallPrompt = FirewallPrompt.Working;
        bool added;
        try { added = await firewall.AddRuleAsync(); }
        catch (Exception) { added = false; }
        FirewallPrompt = added ? FirewallPrompt.Allowed : FirewallPrompt.Failed;
        if (!added) return false;
        firewallRuleSeen = true;
        if (isRunning)
        {
            session?.ReconnectNow();
            keeper?.Reconnect();
        }
        return true;
    }

    /// <summary>فشل الاقتران: إن كانت القاعدة ناقصة يُقترح الزر.</summary>
    private async Task OfferFirewallAsync()
    {
        if (services.Firewall is not { } firewall || firewallPrompt == FirewallPrompt.Working) return;
        FirewallPrompt = await HasFirewallRuleAsync(firewall) ? FirewallPrompt.None : FirewallPrompt.Offer;
    }

    /// <summary>تُفحص القواعد في الخلفية؛ إن تعذّرت قراءتها تُعدّ القاعدة موجودة (كي لا يُقدَّم اقتراح في غير محله).</summary>
    private static async Task<bool> HasFirewallRuleAsync(IFirewall firewall)
    {
        try { return await Task.Run(firewall.HasRule); }
        catch (Exception) { return true; }
    }

    private void ClearFirewallPrompt()
    {
        if (firewallPrompt != FirewallPrompt.Working) FirewallPrompt = FirewallPrompt.None;
    }

    /// <summary>
    /// منذ متى يتعذّر اتصال PS5 والتلفزيون. إن كان PS5 في وضع السكون أو مستخدما على جهاز آخر أو رفض الاتصال
    /// فالمشكلة ليست جدار الحماية؛ ولا يُحتسب ذلك.
    /// </summary>
    private void UpdateTroubleClocks()
    {
        var now = Clock();
        var ps5Trouble = isRunning && sessionState.Phase != SessionPhase.Streaming && sessionState.Reason?.Kind is not
            (DisconnectKind.ConsoleAsleep or DisconnectKind.InUse or DisconnectKind.Refused or DisconnectKind.LoginPasscode);
        ps5TroubleSince = ps5Trouble ? ps5TroubleSince ?? now : null;
        tvWaitingSince = isRunning && tvClients == 0 ? tvWaitingSince ?? now : null;
    }

    // --- تحديث التطبيق ---

    /// <summary>رقم الإصدار الأحدث إن وُجد على GitHub.</summary>
    public string? UpdateVersion { get => updateVersion; private set => Set(ref updateVersion, value); }
    public bool Updating { get => updating; private set => Set(ref updating, value); }
    public double UpdateProgress { get => updateProgress; private set => Set(ref updateProgress, value); }
    public string? UpdateError { get => updateError; private set => Set(ref updateError, value); }
    /// <summary>إن ثُبّت بملف التثبيت يُحدَّث بنقرة واحدة؛ وإلا تُفتح صفحة الإصدار.</summary>
    public bool CanUpdateItself => services.Updater?.CanUpdateItself == true;

    /// <summary>مرة واحدة عند التشغيل؛ يمرّ بصمت إن لم توجد شبكة.</summary>
    public async Task CheckForUpdateAsync()
    {
        if (services.Updater is not { } updater) return;
        try { UpdateVersion = await updater.CheckAsync(); }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException) { }
    }

    public async Task InstallUpdateAsync()
    {
        if (services.Updater is not { } updater || updating) return;
        if (!updater.CanUpdateItself)
        {
            OpenInBrowser(updater.DownloadPage);
            return;
        }
        Updating = true;
        UpdateError = null;
        try
        {
            // إن كانت الترجمة جارية تتوقف أولا؛ ثم يُحدَّث التطبيق ويُعاد فتحه.
            Stop();
            await updater.InstallAsync(new ImmediateProgress(value => post(() => UpdateProgress = value)));
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException
                                          or UnauthorizedAccessException)
        {
            UpdateError = error.Message;
        }
        finally
        {
            Updating = false;
        }
    }

    private AppException Error(AppError error) => new(error, AppException.Describe(error, text));

    /// <summary>يكتب القيمة إن تغيّرت ويُبلّغ عن الخاصية (وما يرتبط بها).</summary>
    private bool Set<T>(ref T field, T value, string[]? also = null, [CallerMemberName] string name = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Changed([name, .. also ?? []]);
        return true;
    }

    private void Changed(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>مترجم يُستخدم حين لا تتوفر الترجمة أو لا تكون اللغة مدعومة: يُبلّغ عن السبب.</summary>
    private sealed class UnavailableTranslator(TranslatorStatus status) : ITextTranslator
    {
        public Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default) => throw new TranslatorException(status);
    }

    /// <summary>يمرّر التقدّم مباشرة دون التقاط السياق (post ينقله إلى الواجهة أصلا).</summary>
    private sealed class ImmediateProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}

/// <summary>نتيجة ترجمة المنطقة الإضافية لمرة واحدة.</summary>
public enum ExtraRegionOutcome { Shown, NoText, NotConfigured, NotRunning, Busy, TranslatorUnavailable, Failed }

/// <summary>نتيجة "ترجِم الشاشة"؛ <see cref="Cleared"/>: أُزيلت الترجمات عن التلفزيون أو أُلغيت المهمة الجارية.</summary>
public enum ScreenOutcome { Shown, NoText, Cleared, NotRunning, TranslatorUnavailable, Failed }
