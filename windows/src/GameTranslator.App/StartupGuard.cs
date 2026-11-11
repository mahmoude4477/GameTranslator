// SPDX-License-Identifier: AGPL-3.0-only

using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace GameTranslator.App;

/// <summary>
/// يعرض أخطاء بدء التشغيل بدل الإغلاق بصمت. هذا الصنف يستخدم أنواع .NET نفسها فقط: يجب أن يُحمَّل ويعمل
/// حتى لو كان أحد ملفات DLL الأخرى للتطبيق ناقصا.
/// </summary>
internal static class StartupGuard
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    public static extern bool DeleteFile(string name);

    /// <summary>
    /// يزيل الـ alternate stream كي لا تصطدم الملفات المستخرجة من ملف zip منزّل من الإنترنت بحظر
    /// Windows Smart App Control (0xc0e90002) و Mark-of-the-Web (Zone.Identifier).
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
    /// ملفات DLL المذكورة في deps.json وغير الموجودة في المجلد. حين ينقل برنامج مكافحة الفيروسات ملفا إلى الحجر الصحي
    /// يرمي .NET عند التشغيل FileNotFoundException ولا تُفتح النافذة أبدا.
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

    /// <summary>يسجّل الخطأ ويعرضه ويغلق التطبيق. في وضع لقطات الشاشة يُكتب errors.txt بدل النافذة.</summary>
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
    /// يعرض أيضا الأخطاء غير الملتقطة بعد بدء التشغيل دون السكوت عنها. لا يُغلق التطبيق: كي لا يفقد المستخدم ما أنجزه
    /// (مثلا تسجيل الدخول إلى PSN الذي أنهاه للتو).
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

    /// <summary>يكتب في الملف %LOCALAPPDATA%\GameTranslator\error.log؛ وإن تعذّرت الكتابة يمرّ بصمت.</summary>
    internal static void Log(string message, Exception? error)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n{error}\n\n");
        }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException)
        {
            // تُعرض الرسالة حتى لو تعذّرت كتابة السجل.
        }
    }
}
