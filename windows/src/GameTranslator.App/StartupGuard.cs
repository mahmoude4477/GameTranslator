// SPDX-License-Identifier: AGPL-3.0-only

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace GameTranslator.App;

/// <summary>
/// Açılış hatalarını sessizce kapanmak yerine gösterir. Bu sınıf yalnız .NET'in kendi türlerini kullanır: uygulamanın
/// başka bir DLL'i eksik olsa da yüklenip çalışabilmeli.
/// </summary>
internal static class StartupGuard
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    public static extern bool DeleteFile(string name);

    /// <summary>
    /// İnternetten indirilen zip'ten çıkarılan dosyaların Windows Smart App Control (0xc0e90002)
    /// ve Mark-of-the-Web (Zone.Identifier) engeline takılmasını önlemek için alternate stream'i kaldırır.
    /// </summary>
    public static void UnblockFiles(string folder)
    {
        try
        {
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    DeleteFile(file + ":Zone.Identifier");
                }
                catch { }
            }
        }
        catch { }
    }

    static string LogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameTranslator", "error.log");

    /// <summary>
    /// deps.json'da listelenen ama klasörde olmayan DLL'ler. Antivirüs bir dosyayı karantinaya alınca .NET açılışta
    /// FileNotFoundException verir ve pencere hiç açılmaz.
    /// </summary>
    public static IReadOnlyList<string> MissingFiles(string folder)
    {
        var deps = Path.Combine(folder, "GameTranslator.deps.json");
        if (!File.Exists(deps)) return [];
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(deps));
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in json.RootElement.GetProperty("targets").EnumerateObject())
                foreach (var library in target.Value.EnumerateObject())
                    if (library.Value.TryGetProperty("runtime", out var runtime))
                        foreach (var file in runtime.EnumerateObject())
                            names.Add(Path.GetFileName(file.Name));
            return [.. names.Where(name => !File.Exists(Path.Combine(folder, name)))];
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or IOException)
        {
            return [];
        }
    }

    public static string MissingMessage(IReadOnlyList<string> files)
    {
        var list = string.Join("\n", files.Take(8).Select(f => "  • " + f)) + (files.Count > 8 ? "\n  • …" : "");
        return $"تعذّر تشغيل GameTranslator: الملفات التالية غير موجودة في مجلد البرنامج:\n\n{list}\n\n" +
               "السبب الأكثر شيوعًا أن برنامج مكافحة الفيروسات نقل الملف إلى الحجر الصحي. استعد الملف من أمان Windows › الحماية من " +
               "الفيروسات والمخاطر › محفوظات الحماية، أو نزّل GameTranslator-windows-x64.zip من جديد واستخرجه في مجلد " +
               "عبر \"استخراج الكل\". لا تشغّل البرنامج من داخل ملف zip.";
    }

    public static string ErrorMessage(Exception error) =>
        "واجه GameTranslator خطأ غير متوقع:\n\n" +
        $"{error.GetType().Name}: {error.Message}\n\n" +
        $"التفاصيل في الملف {LogPath}. يمكنك الإبلاغ عن المشكلة مع هذا الملف من صفحة Issues على GitHub.";

    /// <summary>Hatayı kaydeder, gösterir ve uygulamayı kapatır. Ekran görüntüsü kipinde pencere yerine errors.txt yazılır.</summary>
    public static void Fail(Application app, string message, Exception? error, string? renderFolder)
    {
        Log(message, error);
        if (renderFolder is null)
        {
            MessageBox.Show(message, "GameTranslator", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        else
        {
            Directory.CreateDirectory(renderFolder);
            File.AppendAllText(Path.Combine(renderFolder, "errors.txt"), message + "\n" + error + "\n");
        }
        app.Shutdown(3);
    }

    /// <summary>
    /// Açılıştan sonraki yakalanmamış hataları da sessiz kalmadan gösterir. Uygulama kapanmaz: kullanıcı yaptığı işi
    /// (ör. az önce bitirdiği PSN girişini) kaybetmesin.
    /// </summary>
    public static void Watch(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            Log("DispatcherUnhandledException", e.Exception);
            MessageBox.Show(ErrorMessage(e.Exception), "GameTranslator", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log("UnhandledException", e.ExceptionObject as Exception);
    }

    /// <summary>%LOCALAPPDATA%\GameTranslator\hata.log dosyasına yazar; yazılamazsa sessizce geçer.</summary>
    internal static void Log(string message, Exception? error)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n{error}\n\n");
        }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException)
        {
            // Kayıt yazılamasa da mesaj gösterilir.
        }
    }
}
