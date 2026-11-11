// SPDX-License-Identifier: AGPL-3.0-only

using Velopack;

namespace GameTranslator.App;

public static class Program
{
    /// <summary>
    /// يستدعي Velopack التطبيق لوقت قصير أثناء التثبيت والتحديث والإزالة؛ تُعالج هذه الخطافات قبل بدء WPF
    /// ثم يُخرج. في التشغيل العادي يعود دون أن يفعل شيئا.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
