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
    static bool Turkish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";

    static string LogPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameTranslator", "hata.log");

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
        return Turkish
            ? $"GameTranslator açılamadı: program klasöründe şu dosyalar yok:\n\n{list}\n\n" +
              "Bunun en sık nedeni antivirüs programının dosyayı karantinaya almasıdır. Windows Güvenliği › Virüs ve tehdit " +
              "koruması › Koruma geçmişi'nden dosyayı geri yükleyin ya da GameTranslator-windows-x64.zip'i yeniden indirip " +
              "\"Tümünü ayıkla\" ile bir klasöre çıkarın. Programı zip'in içinden çalıştırmayın."
            : $"GameTranslator could not start: these files are missing from its folder:\n\n{list}\n\n" +
              "This usually means an antivirus moved them to quarantine. Restore them from Windows Security › Virus & threat " +
              "protection › Protection history, or download GameTranslator-windows-x64.zip again and use \"Extract all\". " +
              "Do not run the app from inside the zip.";
    }

    public static string ErrorMessage(Exception error) =>
        (Turkish
            ? "GameTranslator beklenmeyen bir hatayla karşılaştı:\n\n"
            : "GameTranslator ran into an unexpected error:\n\n") +
        $"{error.GetType().Name}: {error.Message}\n\n" +
        (Turkish
            ? $"Ayrıntılar {LogPath} dosyasında. Sorunu bu dosyayla birlikte GitHub'daki Issues sayfasından bildirebilirsiniz."
            : $"Details are in {LogPath}. Please report it on the GitHub Issues page with that file.");

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
