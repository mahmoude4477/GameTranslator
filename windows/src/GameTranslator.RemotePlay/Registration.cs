// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace GameTranslator.RemotePlay;

public static class Registration
{
    /// <summary>libchiaki'nin CHIAKI_RP_APPLICATION_REASON_INVALID_PSN_ID değeri.</summary>
    internal const uint InvalidPsnIdReason = 0x80108b02;
    private const uint InUseReason = 0x80108b10;
    private const uint VersionReason = 0x80108b11;
    private const uint CrashReason = 0x80108b15;

    /// <summary>Kod PS5'e gitmeden biten aşama: arama tek UDP paketiyle yapılır, paket kaybında yeniden denemek güvenlidir.</summary>
    private const int SearchAttempts = 3;
    private const string ReasonPrefix = "Reported Application Reason: ";

    /// <summary>PS5'in "Cihazı Bağla" ekranındaki 8 haneli kodla kayıt olur. Başarısızlıkta
    /// <see cref="RegistrationException"/>; iptal beklemeyi bırakır (OperationCanceledException), PS5'le
    /// konuşma kendi zaman aşımında (en çok ~30 sn) biter.</summary>
    public static Task<RegisteredConsole> RegisterAsync(string host, byte[] accountId, uint pin, CancellationToken ct = default)
    {
        if (accountId.Length != 8) throw new ArgumentException("PSN account id must be 8 bytes", nameof(accountId));
        return Task.Factory.StartNew(() => Register(host, accountId, pin), CancellationToken.None,
                                     TaskCreationOptions.LongRunning, TaskScheduler.Default).WaitAsync(ct);
    }

    /// <summary>PS5'in bildirdiği nedenden kullanıcıya anlatılacak hata.</summary>
    /// <param name="errors">Kayıt sırasında libchiaki'nin yazdığı hata satırları.</param>
    internal static RegistrationFailure FailureFrom(uint? reason, IReadOnlyList<string>? errors = null) => reason switch
    {
        InvalidPsnIdReason => RegistrationFailure.WrongAccount,
        InUseReason => RegistrationFailure.InUse,
        VersionReason => RegistrationFailure.VersionMismatch,
        CrashReason => RegistrationFailure.ConsoleCrashed,
        null when errors?.Any(IsSearchFailure) == true => RegistrationFailure.NoAnswer,
        _ => RegistrationFailure.Failed,
    };

    private static bool IsSearchFailure(string line) =>
        line.Contains("search response", StringComparison.Ordinal) || line.Contains("connect for search", StringComparison.Ordinal);

    /// <summary>Hata iletisine eklenen ayrıntı: PS5'in kodu, o yoksa libchiaki'nin ilk (en belirleyici) hata satırı.</summary>
    internal static string? DetailFrom(IReadOnlyList<string> errors)
    {
        var reported = errors.FirstOrDefault(line => line.Contains(ReasonPrefix, StringComparison.Ordinal));
        if (reported is not null) return reported[(reported.IndexOf(ReasonPrefix, StringComparison.Ordinal) + ReasonPrefix.Length)..];
        return errors.Count > 0 ? errors[0] : null;
    }

    private static unsafe RegisteredConsole Register(string host, byte[] accountId, uint pin)
    {
        if (!NativeMethods.IsAvailable)
        {
            return RegisterViaScript(host, accountId, pin);
        }

        NativeMethods.EnsureInitialized();
        for (var attempt = 1; ; attempt++)
        {
            var errors = new List<string>();
            var handle = GCHandle.Alloc(errors);
            NativeMethods.RegisteredConsoleNative output;
            uint reason;
            int result;
            try
            {
                delegate* unmanaged[Cdecl]<int, byte*, nint, void> log = &OnLog;
                fixed (byte* account = accountId)
                    result = NativeMethods.cs_register(host, account, pin, &output, &reason, (nint)log, GCHandle.ToIntPtr(handle));
            }
            finally
            {
                handle.Free();
            }

            if (result == 0)
                return new RegisteredConsole(host, NativeMethods.FixedString(output.Nickname, 0x20),
                                             new ReadOnlySpan<byte>(output.RegistKey, 0x10).ToArray(),
                                             new ReadOnlySpan<byte>(output.Morning, 0x10).ToArray());
            if (result == 2) throw new RegistrationException(RegistrationFailure.Canceled);

            IReadOnlyList<string> lines;
            lock (errors) lines = [.. errors];
            uint? reported = result == 1 && reason != 0 ? reason : null;
            var failure = result == 1 ? FailureFrom(reported, lines) : RegistrationFailure.Failed;
            var detail = DetailFrom(lines) ?? (result == 1 ? null : $"native error {result}");
            RemotePlayLog.Write(0, $"PS5 kaydı başarısız ({host}, deneme {attempt}): {failure} {detail}");
            if (failure != RegistrationFailure.NoAnswer || attempt >= SearchAttempts)
                throw new RegistrationException(failure, reported, detail);
            Thread.Sleep(500);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnLog(int level, byte* message, nint user)
    {
        try
        {
            var text = NativeMethods.Utf8(message);
            RemotePlayLog.Write(level, text);
            if (level == 0 && text is not null && GCHandle.FromIntPtr(user).Target is List<string> errors)
                lock (errors) errors.Add(text);
        }
        catch
        {
            // Yerel iş parçacığına istisna sızmamalı.
        }
    }

    private static RegisteredConsole RegisterViaScript(string host, byte[] accountId, uint pin)
    {
        var nodeExe = FindNodeExecutable();
        var script = FindRegisterScript();
        if (nodeExe == null || script == null)
        {
            RemotePlayLog.Write(0, "ps5-register betiği veya node.exe bulunamadı.");
            throw new RegistrationException(RegistrationFailure.Failed, null, "Sistemde ps5-register betiği veya node.exe bulunamadı.");
        }

        string b64Account = Convert.ToBase64String(accountId);
        var psi = new ProcessStartInfo
        {
            FileName = nodeExe,
            Arguments = $"\"{script}\" --host \"{host}\" --pin \"{pin:D8}\" --account \"{b64Account}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        RemotePlayLog.Write(2, $"ps5-register başlatılıyor: {host}, PIN: {pin:D8}");
        using var process = new Process { StartInfo = psi };
        var outputLines = new List<string>();
        process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) lock (outputLines) outputLines.Add(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) lock (outputLines) outputLines.Add(e.Data); };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            throw new RegistrationException(RegistrationFailure.Failed, null, $"ps5-register başlatılamadı: {ex.Message}");
        }

        bool exited = process.WaitForExit(35000);
        if (!exited)
        {
            try { process.Kill(); } catch { }
            throw new RegistrationException(RegistrationFailure.NoAnswer, null, "PS5 eşleştirme zaman aşımına uğradı (35 sn). Lütfen konsolun açık ve 'Cihaz Eşleştir' ekranında olduğunu kontrol edin.");
        }

        string? lastDoneLine = null;
        string? lastErrorLine = null;
        lock (outputLines)
        {
            foreach (var line in outputLines)
            {
                RemotePlayLog.Write(2, $"[ps5-register] {line}");
                if (line.Contains("\"done\"")) lastDoneLine = line;
                if (line.Contains("\"error\"")) lastErrorLine = line;
            }
        }

        if (lastDoneLine != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(lastDoneLine);
                var root = doc.RootElement;
                if (root.TryGetProperty("success", out var s) && s.GetBoolean())
                {
                    string registKey = root.GetProperty("registKey").GetString() ?? "";
                    string rpKey = root.TryGetProperty("rpKey", out var rpk) ? (rpk.GetString() ?? "") : "";

                    byte[] registKeyBytes = Encoding.ASCII.GetBytes(registKey.PadRight(16)[..16]);
                    byte[] morningBytes = Encoding.ASCII.GetBytes((rpKey.Length > 0 ? rpKey : registKey).PadRight(16)[..16]);

                    WriteChiakiConfig(host, registKey, rpKey, b64Account);
                    return new RegisteredConsole(host, "PlayStation 5", registKeyBytes, morningBytes);
                }
            }
            catch (Exception ex)
            {
                RemotePlayLog.Write(0, $"JSON parse hatası: {ex.Message}");
            }
        }

        if (lastErrorLine != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(lastErrorLine);
                string msg = doc.RootElement.GetProperty("message").GetString() ?? "Bilinmeyen eşleştirme hatası";
                throw new RegistrationException(RegistrationFailure.Failed, null, msg);
            }
            catch (RegistrationException) { throw; }
            catch { }
        }

        throw new RegistrationException(RegistrationFailure.Failed, null, "PS5 eşleştirme tamamlanamadı. Konsolun açık ve PIN kodunun doğru olduğunu kontrol edin.");
    }

    private static string? FindNodeExecutable()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "helpers", "node.exe"),
            Path.Combine(baseDir, "scripts", "bin", "node.exe"),
            Path.Combine(baseDir, "..", "..", "..", "scripts", "bin", "node.exe"),
            Path.Combine(baseDir, "..", "..", "..", "..", "scripts", "bin", "node.exe"),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "scripts", "bin", "node.exe"),
            @"C:\Users\mehme\OneDrive\Belgeler\gametranslator\scripts\bin\node.exe",
            @"C:\Program Files\nodejs\node.exe"
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c)) return Path.GetFullPath(c);
        }
        return null;
    }

    private static string? FindRegisterScript()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "helpers", "ps5-register.js"),
            Path.Combine(baseDir, "scripts", "ps5-register.js"),
            Path.Combine(baseDir, "..", "..", "..", "scripts", "ps5-register.js"),
            Path.Combine(baseDir, "..", "..", "..", "..", "scripts", "ps5-register.js"),
            Path.Combine(baseDir, "..", "..", "..", "..", "..", "scripts", "ps5-register.js"),
            @"C:\Users\mehme\OneDrive\Belgeler\gametranslator\scripts\ps5-register.js"
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c)) return Path.GetFullPath(c);
        }
        return null;
    }

    private static void WriteChiakiConfig(string host, string registKey, string? rpKey, string b64Account)
    {
        try
        {
            string key = registKey.Trim();
            string rpk = string.IsNullOrWhiteSpace(rpKey) ? key : rpKey.Trim();

            // 1. Windows Registry: Chiaki on Windows reads HKCU:\Software\Chiaki\Chiaki exclusively
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
                        baseKey.SetValue("version", 2, Microsoft.Win32.RegistryValueKind.DWord);

                        using var hostsKey = baseKey.CreateSubKey("registered_hosts");
                        hostsKey.SetValue("size", 1, Microsoft.Win32.RegistryValueKind.DWord);

                        using var host1Key = hostsKey.CreateSubKey("1");
                        host1Key.SetValue("host", host, Microsoft.Win32.RegistryValueKind.String);
                        host1Key.SetValue("server_nickname", "PS5-sefflex", Microsoft.Win32.RegistryValueKind.String);
                        host1Key.SetValue("target", 1000100, Microsoft.Win32.RegistryValueKind.DWord); // CHIAKI_TARGET_PS5_1

                        // rp_regist_key: Chiaki checks size == 16 (REG_BINARY)
                        // PS5 regist_key is hex string (e.g. 16 hex chars = 8 bytes), zero-padded to 16 bytes
                        byte[] regKeyBytes = new byte[16];
                        try
                        {
                            byte[] parsed = Convert.FromHexString(key);
                            Array.Copy(parsed, regKeyBytes, Math.Min(parsed.Length, 16));
                        }
                        catch
                        {
                            byte[] raw = Encoding.ASCII.GetBytes(key);
                            Array.Copy(raw, regKeyBytes, Math.Min(raw.Length, 16));
                        }
                        host1Key.SetValue("rp_regist_key", regKeyBytes, Microsoft.Win32.RegistryValueKind.Binary);
                        host1Key.SetValue("regist_key", key, Microsoft.Win32.RegistryValueKind.String);

                        // rp_key: 16 bytes (REG_BINARY)
                        byte[] rpKeyBytes = new byte[16];
                        try
                        {
                            if (rpk.Length == 32)
                            {
                                byte[] parsed = Convert.FromHexString(rpk);
                                Array.Copy(parsed, rpKeyBytes, Math.Min(parsed.Length, 16));
                            }
                            else
                            {
                                byte[] raw = Encoding.ASCII.GetBytes(rpk);
                                Array.Copy(raw, rpKeyBytes, Math.Min(raw.Length, 16));
                            }
                        }
                        catch
                        {
                            byte[] raw = Encoding.ASCII.GetBytes(rpk);
                            Array.Copy(raw, rpKeyBytes, Math.Min(raw.Length, 16));
                        }
                        host1Key.SetValue("rp_key", rpKeyBytes, Microsoft.Win32.RegistryValueKind.Binary);
                        host1Key.SetValue("rp_key_type", 0, Microsoft.Win32.RegistryValueKind.DWord);

                        // server_mac: 6 bytes (REG_BINARY)
                        byte[] macBytes = [0xE8, 0x6E, 0x3A, 0x94, 0xB1, 0x64];
                        host1Key.SetValue("server_mac", macBytes, Microsoft.Win32.RegistryValueKind.Binary);

                        host1Key.SetValue("console_pin", "", Microsoft.Win32.RegistryValueKind.String);
                        host1Key.SetValue("ap_ssid", "", Microsoft.Win32.RegistryValueKind.String);
                        host1Key.SetValue("ap_bssid", "", Microsoft.Win32.RegistryValueKind.String);
                        host1Key.SetValue("ap_key", "", Microsoft.Win32.RegistryValueKind.String);
                        host1Key.SetValue("ap_name", "", Microsoft.Win32.RegistryValueKind.String);

                        using var manualHosts = baseKey.CreateSubKey("manual_hosts");
                        manualHosts.SetValue("size", 1, Microsoft.Win32.RegistryValueKind.DWord);
                        using var manual1 = manualHosts.CreateSubKey("1");
                        manual1.SetValue("host", host, Microsoft.Win32.RegistryValueKind.String);

                        using var settingsKey = baseKey.CreateSubKey("settings");
                        settingsKey.SetValue("render_backend", "opengl", Microsoft.Win32.RegistryValueKind.String);
                        settingsKey.SetValue("fullscreen", 0, Microsoft.Win32.RegistryValueKind.DWord);
                        settingsKey.SetValue("remote_play_ask", 0, Microsoft.Win32.RegistryValueKind.DWord);
                        settingsKey.SetValue("audio_volume", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                }
                catch (Exception regEx)
                {
                    RemotePlayLog.Write(0, $"Chiaki registry yazma hatası: {regEx.Message}");
                }
            }

            // 2. INI dosyaları (%APPDATA%\chiaki-ng ve %APPDATA%\Chiaki)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                foreach (var folder in new[] { "chiaki-ng", "Chiaki" })
                {
                    var dir = Path.Combine(appData, folder);
                    Directory.CreateDirectory(dir);
                    var iniPath = Path.Combine(dir, $"{folder}.ini");

                    var sb = new StringBuilder();
                    if (!string.IsNullOrWhiteSpace(b64Account))
                    {
                        sb.AppendLine("[General]");
                        sb.AppendLine($"user_id={b64Account}");
                        sb.AppendLine();
                    }

                    sb.AppendLine("[registered_hosts]");
                    sb.AppendLine("size=1");
                    sb.AppendLine($"1\\host={host}");
                    sb.AppendLine("1\\server_nickname=PS5-sefflex");
                    sb.AppendLine("1\\target=1000100");
                    sb.AppendLine($"1\\regist_key={key}");
                    sb.AppendLine($"1\\rp_regist_key={key}");
                    sb.AppendLine($"1\\rp_key={rpk}");
                    sb.AppendLine("1\\rp_key_type=0");
                    sb.AppendLine();

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
