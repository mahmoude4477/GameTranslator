// SPDX-License-Identifier: AGPL-3.0-only

using System.Net.Http;
using GameTranslator.Shell;
using Velopack;
using Velopack.Sources;

namespace GameTranslator.App.Services;

/// <summary>
/// التحديث من الإصدارات على GitHub (Velopack). التطبيق المثبّت بملف التثبيت (GameTranslator-Setup.exe) وحده
/// يحدّث نفسه؛ أما النسخة المفتوحة من ملف zip فتُفتح فيها صفحة الإصدار.
/// </summary>
internal sealed class Updater : IUpdater
{
    private const string Repository = "https://github.com/mahmoude4477/GameTranslator";
    private readonly UpdateManager manager = new(new GithubSource(Repository, null, false));
    private UpdateInfo? pending;

    public bool CanUpdateItself => manager.IsInstalled;
    public Uri DownloadPage { get; } = new($"{Repository}/releases/latest");

    public async Task<string?> CheckAsync()
    {
        if (!manager.IsInstalled) return await LatestTagAsync();
        pending = await manager.CheckForUpdatesAsync();
        return pending?.TargetFullRelease.Version.ToString();
    }

    public async Task InstallAsync(IProgress<double> progress)
    {
        if (pending is null) return;
        await manager.DownloadUpdatesAsync(pending, percent => progress.Report(percent / 100.0));
        manager.ApplyUpdatesAndRestart(pending);
    }

    /// <summary>لمستخدم ملف zip: وسم أحدث إصدار على GitHub إن كان أحدث من هذا الإصدار.</summary>
    private static async Task<string?> LatestTagAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("GameTranslator");
            using var document = System.Text.Json.JsonDocument.Parse(
                await http.GetStringAsync("https://api.github.com/repos/mahmoude4477/GameTranslator/releases/latest"));
            var tag = document.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
            return tag is not null && AppVersion.IsNewer(tag, AppVersion.Current(typeof(Updater).Assembly)) ? tag : null;
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException
                                          or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
