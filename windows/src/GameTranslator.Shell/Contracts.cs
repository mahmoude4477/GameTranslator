// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;
using GameTranslator.Core.LgTv;
using GameTranslator.RemotePlay;

namespace GameTranslator.Shell;

public static class StoreKeys
{
    public const string AccountId = "accountId";
    public const string Console = "console";
    public const string Tv = "tv";
    public const string TvKey = "tvKey";
    public const string TvDevKey = "tvDevKey";
    /// <summary>الإصدار الذي ثبّته GameTranslator على Samsung و Android TV (في LG يُقرأ الإصدار من التلفزيون).</summary>
    public const string TvAppVersion = "tvAppVersion";
    /// <summary>آخر لحظة مُدّدت فيها مدة وضع المطوّر في LG.</summary>
    public const string DevSessionRenewed = "devSessionRenewed";
    /// <summary>مفتاح DeepL الخاص بالمستخدم (ترجمة اختيارية عبر الإنترنت).</summary>
    public const string DeepLKey = "deepLKey";
}

/// <summary>الإعدادات الدائمة (ملف إعدادات عادي، لا المخزن المشفّر).</summary>
public sealed record UserSettings
{
    public CropRegion Region { get; init; } = new();
    /// <summary>المنطقة الإضافية 1 (مثلا لوحة المهام أعلى اليمين)؛ معطّلة إن كانت null. تُقرأ وتُترجم مرة واحدة بمفتاح Num 1.</summary>
    public CropRegion? ExtraRegion1 { get; init; }
    /// <summary>المنطقة الإضافية 2 (مثلا مربّع المعلومات أسفل اليمين)؛ معطّلة إن كانت null. تُقرأ وتُترجم مرة واحدة بمفتاح Num 2.</summary>
    public CropRegion? ExtraRegion2 { get; init; }
    /// <summary>المنطقة الإضافية 3 (مثلا وسط الشاشة)؛ معطّلة إن كانت null. تُقرأ وتُترجم مرة واحدة بمفتاح Num 3.</summary>
    public CropRegion? ExtraRegion3 { get; init; }
    /// <summary>أين تُعرض ترجمة المناطق الإضافية (على التلفزيون أو الكمبيوتر أو كليهما).</summary>
    public ResultTarget ExtraTarget1 { get; init; } = ResultTarget.Tv;
    public ResultTarget ExtraTarget2 { get; init; } = ResultTarget.Tv;
    public ResultTarget ExtraTarget3 { get; init; } = ResultTarget.Tv;
    /// <summary>أين تُعرض نتيجة ترجمة المنطقة الحرة (Num 5).</summary>
    public ResultTarget FreeAreaTarget { get; init; } = ResultTarget.Tv;
    public SubtitleStyle Style { get; init; } = new();
    public string SourceLanguage { get; init; } = "en";
    /// <summary>لغة الترجمة. العربية تُكتب من اليمين إلى اليسار؛ والتلفزيون و OBS يستنتجان الاتجاه من النص.</summary>
    public string TargetLanguage { get; init; } = "ar";
    /// <summary>إن أُدخل المفتاح تُجرى الترجمة عبر DeepL؛ وإن كانت معطّلة أو تعذّر الوصول إلى DeepL يُستخدم المترجم الافتراضي (Google).</summary>
    public bool OnlineTranslation { get; init; }

    /// <summary>عدد المناطق الإضافية؛ <see cref="ExtraRegion"/> يعمل ضمن 1..ExtraRegionCount.</summary>
    public const int ExtraRegionCount = 3;

    /// <summary>المنطقة الإضافية رقم 1 أو 2 أو 3؛ null إن كانت معطّلة.</summary>
    public CropRegion? ExtraRegion(int slot) => slot switch { 1 => ExtraRegion1, 2 => ExtraRegion2, 3 => ExtraRegion3, _ => null };

    public UserSettings WithExtraRegion(int slot, CropRegion? region) => slot switch
    {
        1 => this with { ExtraRegion1 = region },
        2 => this with { ExtraRegion2 = region },
        3 => this with { ExtraRegion3 = region },
        _ => this,
    };

    /// <summary>المكان الذي تُعرض فيه ترجمة المنطقة الإضافية.</summary>
    public ResultTarget ExtraTarget(int slot) => slot switch { 1 => ExtraTarget1, 2 => ExtraTarget2, 3 => ExtraTarget3, _ => ResultTarget.Tv };

    public UserSettings WithExtraTarget(int slot, ResultTarget target) => slot switch
    {
        1 => this with { ExtraTarget1 = target },
        2 => this with { ExtraTarget2 = target },
        3 => this with { ExtraTarget3 = target },
        _ => this,
    };
}

/// <summary>المكان الذي تُعرض فيه نتيجة الترجمة لمرة واحدة.</summary>
public enum ResultTarget { Tv, Pc, Both }

/// <summary>معرّف الحساب ومفاتيح PS5 والتلفزيون (في الواقع ملف مشفّر بـ DPAPI).</summary>
public interface IAppStore
{
    void Save<T>(string key, T value);
    T? Load<T>(string key);
    void Remove(string key);
}

public interface ISettingsStore
{
    UserSettings Load();
    void Save(UserSettings settings);
}

/// <summary>يمنع الكمبيوتر من الدخول في السكون عند الخمول ما دامت الترجمة جارية.</summary>
public interface ISleepGuard
{
    void Begin();
    void End();
}

/// <summary>المكلّف بإبقاء تطبيق التلفزيون مفتوحا (في الواقع TvKeeper).</summary>
public interface IKeeper
{
    void Start(TimeSpan interval);
    void Stop();
    void Reconnect();
}

/// <summary>محرك الترجمة الافتراضي: هل هو مدعوم وجاهز، نزّل ما يلزمه، أنشئ مترجما.</summary>
public interface ITranslationModel
{
    bool IsSupported(string source, string target);
    bool IsInstalled(string source, string target);
    Task DownloadAsync(string source, string target, IProgress<double> progress, CancellationToken cancellationToken);
    ITextTranslator Create(string source, string target);
}

/// <summary>كل ما يتواصل مع العالم الخارجي (PS5، PSN، التلفزيون، OCR، الترجمة)؛ يُستخدم بديل وهمي له في الاختبارات.</summary>
public interface IAppServices
{
    Task<IReadOnlyList<ConsoleInfo>> DiscoverConsolesAsync();
    Task<byte[]> AccountIdFromRedirectAsync(Uri redirect);
    Task<RegisteredConsole> RegisterAsync(string host, byte[] accountId, uint pin);
    Task<IReadOnlyList<LgTv>> DiscoverTvsAsync();
    /// <summary>يقترن بالتلفزيون ويحاول تشغيل تطبيق التلفزيون بعنوان الخادم.</summary>
    Task<TvPairing> PairTvAsync(LgTv tv, string? key, string server);
    IStreamSession MakeStream(RegisteredConsole console, byte[] accountId);
    /// <param name="appConnected">هل تطبيق التلفزيون متصل بالكمبيوتر (في Samsung لا يُمسّ التلفزيون ما دام متصلا).</param>
    /// <param name="onMoved">يُستدعى حين يُعثر على التلفزيون في عنوان جديد على الشبكة.</param>
    IKeeper MakeKeeper(LgTv tv, string? key, IReadOnlyDictionary<string, string> parameters, Func<bool> appConnected, Action<LgTv> onMoved);
    /// <summary>إصدار حزمة التلفزيون المرفقة بالتطبيق (قد لا توجد الحزمة في بناء المطوّر).</summary>
    string? BundledTvAppVersion { get; }
    Task<bool> IsDevModeOnAsync(string host);
    Task<string> FetchDevKeyAsync(string host, string passphrase);
    Task<string?> InstalledTvAppVersionAsync(string host, string devKey);
    Task InstallTvAppAsync(string host, string devKey);
    /// <summary>يعيد بدء مدة وضع المطوّر في LG؛ ويعيد الساعات المتبقية (null إن لم تُعرف).</summary>
    Task<int?> RenewDevSessionAsync(string host, string devKey);
    /// <summary>الحزمة المرفقة بالتطبيق لـ Samsung و Android TV (قد لا توجد في بناء المطوّر).</summary>
    BundledTvApp? BundledApp(TvBrand brand);
    Task<bool> IsSamsungDevModeOnAsync(string host);
    Task InstallSamsungAppAsync(string host, BundledTvApp app, Action<TvInstallEvent> onEvent);
    Task<bool> IsAndroidDebugOnAsync(string host);
    /// <summary>الأجهزة المفعّل فيها ADB على الشبكة.</summary>
    Task<IReadOnlyList<string>> ScanAndroidTvsAsync();
    /// <summary>يثبّته إن كان ناقصا أو قديما؛ ويعيد الإصدار الموجود على التلفزيون.</summary>
    Task<string> InstallAndroidAppAsync(string host, BundledTvApp app, Action<TvInstallEvent> onEvent);
    /// <summary>أداة التعرّف على النص للغة اللعبة؛ null إن لم توجد أي لغة OCR في الكمبيوتر.</summary>
    ITextReader? MakeReader(string language);
    /// <summary>
    /// هل حزمة التعرّف على النص الخاصة بلغة اللعبة مثبّتة. إن لم تكن، يقرأ القارئ بلغة النظام فتخرج الترجمة النصية
    /// الإنجليزية مشوّهة الحروف؛ ويُطلب من المستخدم تثبيت الحزمة.
    /// </summary>
    bool HasOcrLanguage(string language) => true;
    ITranslationModel Translation { get; }
    /// <summary>مترجم DeepL؛ <paramref name="fallback"/> يترجم الأسطر التي يتعذّر فيها الوصول إلى DeepL. null إن لم يوجد في هذا البناء.</summary>
    ITextTranslator? MakeOnlineTranslator(string key, ITextTranslator? fallback, string target) => null;
    Task<OnlineKeyCheck> CheckOnlineKeyAsync(string key) => Task.FromResult(new OnlineKeyCheck(OnlineKeyState.Unreachable));
    /// <summary>تحديث التطبيق؛ null في بناء المطوّر.</summary>
    IUpdater? Updater => null;
    /// <summary>إذن جدار حماية Windows؛ null خارج Windows وفي الاختبارات (لا يُقترح شيء).</summary>
    IFirewall? Firewall => null;
}

/// <param name="Launched">إن كان false فقد تعذّر فتح تطبيق التلفزيون؛ والأرجح أنه غير مثبّت على التلفزيون.</param>
public sealed record TvPairing(string Key, bool Launched);

/// <param name="Path">ملف الحزمة.</param>
public sealed record BundledTvApp(string Path, string Version);

public enum TvInstallEventKind { SignIn, ApproveOnTv, Progress }

/// <summary>حدث يطلب شيئا من المستخدم أثناء التثبيت على Samsung و Android TV.</summary>
public sealed record TvInstallEvent(TvInstallEventKind Kind, Uri? Url = null);

/// <summary>
/// SignIn: في انتظار تسجيل الدخول بحساب Samsung في المتصفح لأجل شهادة Samsung. ApproveOnTv: في انتظار إذن
/// تصحيح الأخطاء على شاشة Android TV.
/// </summary>
public enum TvAppKind { Unknown, Working, UpToDate, DevModeOff, NeedsPassphrase, Failed, SignIn, ApproveOnTv }

/// <summary>حالة تثبيت تطبيق التلفزيون.</summary>
public sealed record TvAppState(TvAppKind Kind, string? Version = null, DevModeError? Error = null, Uri? Url = null, string? Reason = null)
{
    public static readonly TvAppState Unknown = new(TvAppKind.Unknown);
}

/// <summary>لون نقطة الحالة: متصل (أخضر)، توجد مشكلة (برتقالي)، خامل (رمادي).</summary>
public enum StatusTone { Idle, Warning, Good }

public enum OnboardingStep { Welcome, SecondAccount, PsnLogin, Console, Tv, Translation, Test, Done }

public enum AppError { InvalidPin, InvalidLogin, MissingAccount, WrongAccount, RegistrationFailed, ConsoleNoAnswer, ConsoleInUse, ConsoleCrashed, ConsoleVersion }

/// <summary>خطأ يُعرض للمستخدم ويخبره بما عليه فعله.</summary>
public sealed class AppException(AppError error, string message) : Exception(message)
{
    public AppError Error { get; } = error;

    public static string Describe(AppError error, Localizer text) => text[error switch
    {
        AppError.InvalidPin => "The code must be 8 digits.",
        AppError.InvalidLogin => "Sign-in did not complete. Please try again.",
        AppError.MissingAccount => "Sign in to PlayStation Network first.",
        AppError.WrongAccount => "The PS5 did not recognise this account. Get the code from the PS5 user signed in with the account you used in GameTranslator, or sign in again with that user's account.",
        AppError.ConsoleNoAnswer => "The PS5 did not answer. Make sure it is on (not in rest mode), on the same network as this computer, and that Remote Play is turned on, then try again.",
        AppError.ConsoleInUse => "Remote Play is in use on another device. Close it there and enter a new code.",
        AppError.ConsoleCrashed => "The PS5 Remote Play service stopped unexpectedly. Restart the PS5 and enter a new code.",
        AppError.ConsoleVersion => "This PS5 system version is not supported yet. Update GameTranslator and try again.",
        _ => "The PS5 refused the code. Open Pair Device again on the PS5 and enter the new code.",
    }];
}
