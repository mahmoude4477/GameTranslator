// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.IO;
using System.Text;

namespace GameTranslator.App.Services;

/// <summary>
/// خدمة مساعدة تشغّل تطبيق Remote Play المسمى Chiaki أو chiaki-ng على Windows،
/// وتزامن إعداداته بأمان وتجلبه إلى المقدمة.
/// </summary>
public static class ChiakiLauncher
{
    private static readonly string[] PossibleRelativePaths =
    [
        Path.Combine("chiaki", "chiaki.exe"),
        Path.Combine("chiaki", "chiaki-ng-Win", "chiaki.exe"),
        Path.Combine("helpers", "chiaki", "chiaki.exe"),
        Path.Combine("helpers", "chiaki.exe")
    ];

    public static string? FindChiakiExecutable()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        foreach (var rel in PossibleRelativePaths)
        {
            var full = Path.Combine(baseDir, rel);
            if (File.Exists(full)) return full;
        }

        var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        var systemPaths = new[]
        {
            Path.Combine(localApp, "Programs", "Chiaki", "chiaki.exe"),
            Path.Combine(localApp, "Programs", "chiaki-ng", "chiaki-ng.exe"),
            Path.Combine(progFiles, "Chiaki", "chiaki.exe"),
            Path.Combine(progFiles, "chiaki-ng", "chiaki-ng.exe"),
            @"C:\Chiaki\chiaki.exe",
            @"C:\chiaki-ng\chiaki-ng.exe"
        };

        return systemPaths.FirstOrDefault(File.Exists);
    }

    public static bool IsRunning()
    {
        return Process.GetProcessesByName("chiaki").Length > 0 ||
               Process.GetProcessesByName("chiaki-ng").Length > 0;
    }

    /// <summary>
    /// عند بدء الترجمة يشغّل Chiaki بحيث يتصل مباشرة بجهاز الألعاب (وضع stream).
    /// إن كانت نافذة البث مفتوحة أصلا يجلبها إلى المقدمة. وإن وُجد Chiaki خامل يغلقه ويعيد تشغيله بأمر stream.
    /// </summary>
    public static bool LaunchAndStream(string? host = null, string? consoleName = null)
    {
        try
        {
            // إن وُجدت نافذة هدف مفتوحة وتبثّ أصلا فاجلبها إلى المقدمة
            var hWnd = WindowCaptureService.FindTargetWindow(out _);
            if (hWnd != IntPtr.Zero)
            {
                WindowCaptureService.RestoreAndActivate(hWnd);
                return true;
            }

            // إن كانت عملية Chiaki تعمل لكن نافذة البث لم تُنشأ بعد أو كانت خاملة
            // فأغلق العملية الخاملة القديمة بلطف كي تقبل أمر stream من سطر الأوامر
            if (IsRunning())
            {
                Stop();
                Thread.Sleep(300);
            }

            var exePath = FindChiakiExecutable();
            if (exePath == null) return false;

            var dir = Path.GetDirectoryName(exePath) ?? string.Empty;
            StartupGuard.UnblockFiles(dir);
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = dir,
                UseShellExecute = true
            };

            // إن وُجد اسم مستعار أو جهاز ألعاب مسجّل فأعطِ أمر stream مباشرة
            if (!string.IsNullOrWhiteSpace(host))
            {
                var nick = GetRegisteredNickname(host) ?? (string.IsNullOrWhiteSpace(consoleName) ? "PS5" : consoleName);
                var cleanNick = nick.Trim().Replace("\"", "");
                psi.Arguments = cleanNick.Contains(' ') ? $"stream \"{cleanNick}\" {host}" : $"stream {cleanNick} {host}";
            }
            else
            {
                psi.Arguments = "stream PS5";
            }

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// يفتح واجهة Chiaki كي يتمكن المستخدم من إجراء الاقتران الأول في معالج الإعداد.
    /// </summary>
    public static bool LaunchPairingGui(string? host = null)
    {
        try
        {
            if (IsRunning())
            {
                var hWnd = WindowCaptureService.FindTargetWindow(out _);
                if (hWnd != IntPtr.Zero)
                {
                    WindowCaptureService.RestoreAndActivate(hWnd);
                    return true;
                }
            }

            var exePath = FindChiakiExecutable();
            if (exePath == null) return false;

            var dir = Path.GetDirectoryName(exePath) ?? string.Empty;
            StartupGuard.UnblockFiles(dir);
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = dir,
                UseShellExecute = true,
                Arguments = string.Empty
            };

            Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// اسم بديل للتوافق مع الإصدارات السابقة: يستدعي LaunchAndStream.
    /// </summary>
    public static bool LaunchOrBringToFront(string? host = null, string? consoleName = null)
    {
        return LaunchAndStream(host, consoleName);
    }

    /// <summary>
    /// يغلق Chiaki والاتصال تلقائيا عند إيقاف الترجمة أو إغلاق التطبيق.
    /// </summary>
    public static void Stop()
    {
        try
        {
            var procs = Process.GetProcessesByName("chiaki")
                .Concat(Process.GetProcessesByName("chiaki-ng"))
                .ToArray();

            foreach (var p in procs)
            {
                try
                {
                    p.CloseMainWindow();
                    if (!p.WaitForExit(1000))
                    {
                        p.Kill();
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    /// <summary>
    /// يتحقق من وجود مفتاح تسجيل حقيقي في Registry أو ملفات INI الخاصة بـ Chiaki.
    /// لا يقبل المفاتيح الوهمية (dummy).
    /// </summary>
    public static bool IsRegistered(string host)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Chiaki\Chiaki\registered_hosts\1");
                if (key != null)
                {
                    var regBytes = key.GetValue("rp_regist_key") as byte[];
                    if (regBytes != null && regBytes.Length >= 8)
                    {
                        return true;
                    }
                    var regStr = key.GetValue("regist_key") as string;
                    if (!string.IsNullOrWhiteSpace(regStr) && !regStr.Contains("0123456789ABCDEF"))
                    {
                        return true;
                    }
                }
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData)) return false;

            foreach (var folder in new[] { "chiaki-ng", "Chiaki" })
            {
                var iniPath = Path.Combine(appData, folder, $"{folder}.ini");
                if (File.Exists(iniPath))
                {
                    var content = File.ReadAllText(iniPath);
                    if ((content.Contains("regist_key=") || content.Contains("rp_regist_key=")) &&
                        !content.Contains("0123456789ABCDEF"))
                    {
                        return true;
                    }
                }
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// يقرأ عنوان IP للـ host المُدخل سابقا في Registry أو ملفات INI الخاصة بـ Chiaki.
    /// </summary>
    public static string? GetSavedHost()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var hostKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Chiaki\Chiaki\registered_hosts\1");
                if (hostKey != null)
                {
                    var h = hostKey.GetValue("host") as string;
                    if (!string.IsNullOrWhiteSpace(h) && System.Net.IPAddress.TryParse(h.Trim(), out _))
                    {
                        return h.Trim();
                    }
                }

                using var manualKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Chiaki\Chiaki\manual_hosts\1");
                if (manualKey != null)
                {
                    var h = manualKey.GetValue("host") as string;
                    if (!string.IsNullOrWhiteSpace(h) && System.Net.IPAddress.TryParse(h.Trim(), out _))
                    {
                        return h.Trim();
                    }
                }
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData)) return null;

            foreach (var folder in new[] { "chiaki-ng", "Chiaki" })
            {
                var iniPath = Path.Combine(appData, folder, $"{folder}.ini");
                if (File.Exists(iniPath))
                {
                    var lines = File.ReadAllLines(iniPath);
                    foreach (var line in lines)
                    {
                        if (line.Contains("\\host="))
                        {
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                            {
                                var h = parts[1].Trim();
                                if (System.Net.IPAddress.TryParse(h, out _))
                                {
                                    return h;
                                }
                            }
                        }
                    }
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// يقرأ الاسم المستعار (nickname) لجهاز الألعاب المسجّل في Registry أو ملف INI الخاص بـ Chiaki.
    /// يعيد null إن لم يُعثر عليه.
    /// </summary>
    public static string? GetRegisteredNickname(string host)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Chiaki\Chiaki\registered_hosts\1");
                if (key != null)
                {
                    var nick = key.GetValue("server_nickname") as string;
                    if (!string.IsNullOrWhiteSpace(nick))
                    {
                        return nick.Trim();
                    }
                }
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData)) return null;

            foreach (var folder in new[] { "chiaki-ng", "Chiaki" })
            {
                var iniPath = Path.Combine(appData, folder, $"{folder}.ini");
                if (File.Exists(iniPath))
                {
                    var lines = File.ReadAllLines(iniPath);
                    string? lastName = null;
                    foreach (var line in lines)
                    {
                        if (line.Contains("\\name=") || line.Contains("\\server_nickname="))
                        {
                            var parts = line.Split('=', 2);
                            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                            {
                                lastName = parts[1].Trim();
                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(lastName))
                    {
                        return lastName;
                    }
                }
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// يجهّز إعدادات Chiaki. لا يحذف التسجيلات الحقيقية الموجودة أبدا.
    /// يضيف عنوان IP اليدوي للـ host، وإعداد العرض عبر OpenGL، و PSN Account ID إن وُجد.
    /// </summary>
    public static void SyncConfig(string host, byte[]? accountId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(host)) return;
            string? b64Account = accountId != null && accountId.Length > 0 ? Convert.ToBase64String(accountId) : null;

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    using var baseKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Chiaki\Chiaki");
                    if (baseKey != null)
                    {
                        if (!string.IsNullOrWhiteSpace(b64Account))
                        {
                            baseKey.SetValue("user_id", b64Account, Microsoft.Win32.RegistryValueKind.String);
                        }

                        using var manualHosts = baseKey.CreateSubKey("manual_hosts");
                        manualHosts.SetValue("size", 1, Microsoft.Win32.RegistryValueKind.DWord);
                        using var manual1 = manualHosts.CreateSubKey("1");
                        manual1.SetValue("host", host.Trim(), Microsoft.Win32.RegistryValueKind.String);

                        using var settingsKey = baseKey.CreateSubKey("settings");
                        settingsKey.SetValue("render_backend", "opengl", Microsoft.Win32.RegistryValueKind.String);
                        settingsKey.SetValue("fullscreen", 0, Microsoft.Win32.RegistryValueKind.DWord);
                        settingsKey.SetValue("remote_play_ask", 0, Microsoft.Win32.RegistryValueKind.DWord);
                        settingsKey.SetValue("audio_volume", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                }
                catch { }
            }

            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData)) return;

            foreach (var folder in new[] { "chiaki-ng", "Chiaki" })
            {
                var dir = Path.Combine(appData, folder);
                Directory.CreateDirectory(dir);
                var iniPath = Path.Combine(dir, $"{folder}.ini");

                if (File.Exists(iniPath))
                {
                    var text = File.ReadAllText(iniPath);
                    var sb = new StringBuilder(text);
                    bool modified = false;

                    if (!text.Contains(host))
                    {
                        if (!text.Contains("[manual_hosts]"))
                        {
                            sb.AppendLine();
                            sb.AppendLine("[manual_hosts]");
                            sb.AppendLine("size=1");
                            sb.AppendLine($"1\\host={host}");
                        }
                        else
                        {
                            sb.AppendLine($"1\\host={host}");
                        }
                        modified = true;
                    }

                    if (!string.IsNullOrWhiteSpace(b64Account) && !text.Contains("user_id="))
                    {
                        if (text.Contains("[General]"))
                        {
                            sb.Replace("[General]", $"[General]\nuser_id={b64Account}");
                        }
                        else
                        {
                            sb.AppendLine();
                            sb.AppendLine("[General]");
                            sb.AppendLine($"user_id={b64Account}");
                        }
                        modified = true;
                    }

                    if (modified)
                    {
                        File.WriteAllText(iniPath, sb.ToString(), Encoding.UTF8);
                    }
                }
                else
                {
                    var sb = new StringBuilder();
                    if (!string.IsNullOrWhiteSpace(b64Account))
                    {
                        sb.AppendLine("[General]");
                        sb.AppendLine($"user_id={b64Account}");
                        sb.AppendLine();
                    }
                    sb.AppendLine("[manual_hosts]");
                    sb.AppendLine("size=1");
                    sb.AppendLine($"1\\host={host}");
                    sb.AppendLine();
                    sb.AppendLine("[settings]");
                    sb.AppendLine("fullscreen=false");
                    sb.AppendLine("audio_volume=0");
                    sb.AppendLine("render_backend=opengl");
                    File.WriteAllText(iniPath, sb.ToString(), Encoding.UTF8);
                }
            }
        }
        catch { }
    }
}
