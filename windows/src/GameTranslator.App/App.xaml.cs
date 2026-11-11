// SPDX-License-Identifier: AGPL-3.0-only

using System.Runtime.CompilerServices;
using System.Windows;
using GameTranslator.App.Services;
using GameTranslator.App.Theme;
using GameTranslator.Core;
using GameTranslator.Shell;

namespace GameTranslator.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // أزل تلقائيا حظر Windows Smart App Control (0xc0e90002) و Zone.Identifier عن الملفات المنزّلة
        StartupGuard.UnblockFiles(AppContext.BaseDirectory);

        var renderFolder = e.Args is ["--render-screens", var folder] ? folder : null;
        // ملف DLL ناقص يسبب خطأ عند تصريف الدالة التي تستخدمه (JIT). لذلك يبقى الفحص والالتقاط هنا، أما الكود
        // الذي يمسّ أنواع التطبيق نفسه فيبقى في دالة منفصلة (Start).
        var missing = StartupGuard.MissingFiles(AppContext.BaseDirectory);
        if (missing.Count > 0)
        {
            StartupGuard.Fail(this, StartupGuard.MissingMessage(missing), null, renderFolder);
            return;
        }
        if (renderFolder is null) StartupGuard.Watch(this);
        try
        {
            Start(renderFolder);
        }
        catch (Exception error)
        {
            StartupGuard.Fail(this, StartupGuard.ErrorMessage(error), error, renderFolder);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void Start(string? renderFolder)
    {
        if (renderFolder is not null)
        {
            ScreenRenderer.Run(this, renderFolder);
            return;
        }
        RemotePlayDiagnostics.Start();
        ThemeManager.Apply(Resources);
        ThemeManager.Follow(Resources);
        var controller = new AppController(new LiveServices(), new DpapiAppStore(), new FileSettingsStore(), new WindowsSleepGuard(),
            Localizer.Current, action => Dispatcher.BeginInvoke(action))
        {
            // صفحة تسجيل الدخول لشهادة Samsung تُفتح في المتصفح الافتراضي.
            OpenInBrowser = url => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true }),
        };
        var window = new ShellWindow(controller);
        MainWindow = window;
        window.Show();
    }
}
