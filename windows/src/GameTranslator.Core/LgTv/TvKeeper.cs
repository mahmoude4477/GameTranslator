// SPDX-License-Identifier: AGPL-3.0-only

namespace GameTranslator.Core.LgTv;

/// <summary>
/// يُبقي تطبيق التلفزيون مفتوحًا: إذا انتقل التلفزيون من نفسه إلى مدخل HDMI أو مدخل البث (SimpLink/CEC) يعيد
/// التطبيق إلى المقدمة. إذا كان التلفزيون مطفأ أو انقطع الاتصال يعيد الاتصال في الفحص التالي.
/// </summary>
/// <param name="appConnected">
/// إذا أُعطي (Samsung) لا يُمسّ التلفزيون إلا عندما يكون تطبيق التلفزيون غير متصل بالكمبيوتر، ومرة واحدة على الأكثر كل
/// <paramref name="relaunchInterval"/>: يُفتح التطبيق ويُغلق الاتصال فورًا. ولأن Samsung لا يفصح عن التطبيق الظاهر في المقدمة
/// لم تكن لاتصال «التحكم عن بُعد» المفتوح دائمًا فائدة؛ وكانت إعادة إنشائه تُظهر إشعارًا على التلفزيون وتفصل ذراع تحكم PS5 (#5).
/// </param>
public sealed class TvKeeper(Func<Task<ITvControl>> connect, string appId, IReadOnlyDictionary<string, string> parameters,
                             Func<bool>? appConnected = null, TimeSpan? relaunchInterval = null) : IDisposable
{
    private readonly SemaphoreSlim tickLock = new(1, 1);
    private ITvControl? control;
    private CancellationTokenSource? loop;
    private DateTime? lastLaunch;

    /// <summary>التطبيقات التي فتحها المستخدم عمدًا (Netflix وغيره) لا تُمسّ؛ فقط شاشات المداخل والبث.</summary>
    public static bool IsInputOrLiveTv(string appId) =>
        appId.StartsWith("com.webos.app.hdmi", StringComparison.Ordinal)
        || appId.StartsWith("com.webos.app.externalinput", StringComparison.Ordinal)
        || appId == "com.webos.app.livetv";

    /// <summary>فحص واحد. لا يرمي خطأ؛ وعند مشكلة في الاتصال يعيد المحاولة في الفحص التالي.</summary>
    public async Task TickAsync()
    {
        await tickLock.WaitAsync();
        try
        {
            if (appConnected is not null)
            {
                await LaunchIfAppIsAwayAsync(appConnected());
                return;
            }
            if (control is null)
            {
                var fresh = await connect();
                control = fresh;
                await fresh.LaunchAsync(appId, parameters);
                return;
            }
            var current = await control.ForegroundAppIdAsync();
            if (IsInputOrLiveTv(current)) await control.LaunchAsync(appId, parameters);
        }
        catch
        {
            control?.Dispose();
            control = null;
        }
        finally
        {
            tickLock.Release();
        }
    }

    private async Task LaunchIfAppIsAwayAsync(bool connected)
    {
        if (connected) return;
        if (lastLaunch is { } last && DateTime.UtcNow - last < (relaunchInterval ?? TimeSpan.FromSeconds(30))) return;
        lastLaunch = DateTime.UtcNow;
        try
        {
            using var fresh = await connect();
            await fresh.LaunchAsync(appId, parameters);
        }
        catch (Exception)
        {
            // التلفزيون مطفأ أو يتعذّر الوصول إليه؛ تُعاد المحاولة في المرة التالية.
        }
    }

    public void Start(TimeSpan? interval = null)
    {
        loop?.Cancel();
        var cancellation = new CancellationTokenSource();
        loop = cancellation;
        var delay = interval ?? TimeSpan.FromSeconds(5);
        _ = Task.Run(async () =>
        {
            while (!cancellation.IsCancellationRequested)
            {
                await TickAsync();
                try { await Task.Delay(delay, cancellation.Token); } catch (OperationCanceledException) { }
            }
        });
    }

    /// <summary>عند استيقاظ الكمبيوتر من السكون: يجدّد الاتصال.</summary>
    public void Reconnect()
    {
        control?.Dispose();
        control = null;
        lastLaunch = null;
    }

    public void Stop()
    {
        loop?.Cancel();
        loop = null;
        control?.Dispose();
        control = null;
    }

    public void Dispose() => Stop();
}
