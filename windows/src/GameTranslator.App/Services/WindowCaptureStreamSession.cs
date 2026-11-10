// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.IO;
using System.Text;
using GameTranslator.Core;
using GameTranslator.RemotePlay;
using Microsoft.Win32;

namespace GameTranslator.App.Services;

/// <summary>
/// Windows'ta Chiaki veya PlayStation Remote Play penceresini periyodik olarak yakalayan IStreamSession uygulaması.
/// Yerel gametranslator_native DLL bulunmadığında veya pencere yakalama modunda kullanılır.
/// </summary>
public sealed class WindowCaptureStreamSession : IStreamSession
{
    public event Action<Frame>? FrameReceived;
    public event Action<SessionState>? StateChanged;
#pragma warning disable CS0067
    public event Action<RegisteredConsole>? ConsoleMoved;
#pragma warning restore CS0067

    private readonly RegisteredConsole console;
    private CancellationTokenSource? cts;
    private Task? loopTask;
    private bool running;

    public WindowCaptureStreamSession(RegisteredConsole console)
    {
        this.console = console;
    }

    public void Start()
    {
        if (running) return;
        running = true;
        cts = new CancellationTokenSource();
        StateChanged?.Invoke(SessionState.Connecting);

        TryAutoLaunchChiaki();

        loopTask = Task.Run(() => CaptureLoopAsync(cts.Token));
    }

    private void TryAutoLaunchChiaki()
    {
        try
        {
            SyncChiakiConfig();

            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chiaki", "chiaki.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chiaki", "chiaki-ng-Win", "chiaki.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "helpers", "chiaki", "chiaki.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "helpers", "chiaki.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Chiaki", "chiaki.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "chiaki-ng", "chiaki-ng.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Chiaki", "chiaki.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "chiaki-ng", "chiaki-ng.exe"),
                @"C:\Chiaki\chiaki.exe",
                @"C:\chiaki-ng\chiaki-ng.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (Process.GetProcessesByName(name).Length == 0 && Process.GetProcessesByName("chiaki").Length == 0)
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true,
                            WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty
                        };

                        if (!string.IsNullOrWhiteSpace(console.Host))
                        {
                            var nick = string.IsNullOrWhiteSpace(console.Name) ? "PS5" : console.Name;
                            psi.Arguments = $"stream \"{nick}\" {console.Host}";
                        }

                        Process.Start(psi);
                        break;
                    }
                }
            }
        }
        catch { }
    }

    private void SyncChiakiConfig()
    {
        try
        {
            if (console == null || string.IsNullOrWhiteSpace(console.Host)) return;

            var nick = string.IsNullOrWhiteSpace(console.Name) ? "PS5" : console.Name;
            var mac = new byte[] { 0xE8, 0x6E, 0x3A, 0x94, 0xB1, 0x64 };
            var regKey = console.RegistKey ?? Array.Empty<byte>();
            var rpKey = console.Morning ?? Array.Empty<byte>();

            // 1. Windows Registry (Chiaki & chiaki-ng)
            foreach (var subKeyPath in new[] { @"Software\Chiaki\Chiaki", @"Software\chiaki-ng\chiaki-ng" })
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(subKeyPath);
                    if (key != null)
                    {
                        key.SetValue("registered_hosts\\size", 1, RegistryValueKind.DWord);
                        key.SetValue("registered_hosts\\1\\target", 1000100, RegistryValueKind.DWord);
                        key.SetValue("registered_hosts\\1\\server_nickname", nick, RegistryValueKind.String);
                        key.SetValue("registered_hosts\\1\\server_mac", mac, RegistryValueKind.Binary);
                        if (regKey.Length > 0)
                            key.SetValue("registered_hosts\\1\\rp_regist_key", regKey, RegistryValueKind.Binary);
                        if (rpKey.Length > 0)
                            key.SetValue("registered_hosts\\1\\rp_key", rpKey, RegistryValueKind.Binary);
                        key.SetValue("registered_hosts\\1\\rp_key_type", 0, RegistryValueKind.DWord);
                        key.SetValue("settings\\audio_volume", 0, RegistryValueKind.DWord);
                        key.SetValue("settings\\fullscreen", 0, RegistryValueKind.DWord);
                        key.SetValue("settings\\remote_play_ask", 0, RegistryValueKind.DWord);
                    }
                }
                catch { }
            }

            // 2. INI Dosyaları (%APPDATA%\Chiaki\Chiaki.ini & %APPDATA%\chiaki-ng\chiaki-ng.ini)
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
            {
                var sb = new StringBuilder();
                sb.AppendLine("[registered_hosts]");
                sb.AppendLine("size=1");
                sb.AppendLine("1\\target=1000100");
                sb.AppendLine($"1\\server_nickname={nick}");
                sb.AppendLine($"1\\server_mac={ToQtByteArray(mac)}");
                if (regKey.Length > 0)
                    sb.AppendLine($"1\\rp_regist_key={ToQtByteArray(regKey)}");
                if (rpKey.Length > 0)
                    sb.AppendLine($"1\\rp_key={ToQtByteArray(rpKey)}");
                sb.AppendLine("1\\rp_key_type=0");
                sb.AppendLine("1\\console_pin=");
                sb.AppendLine();
                sb.AppendLine("[settings]");
                sb.AppendLine("audio_volume=0");
                sb.AppendLine("fullscreen=false");
                sb.AppendLine("remote_play_ask=false");

                var iniContent = sb.ToString();

                foreach (var folder in new[] { "Chiaki", "chiaki-ng" })
                {
                    try
                    {
                        var dir = Path.Combine(appData, folder);
                        Directory.CreateDirectory(dir);
                        var file = Path.Combine(dir, $"{folder}.ini");
                        File.WriteAllText(file, iniContent, Encoding.UTF8);
                    }
                    catch { }
                }
            }
        }
        catch { }
    }

    private static string ToQtByteArray(byte[] bytes)
    {
        var sb = new StringBuilder("@ByteArray(");
        foreach (var b in bytes)
        {
            sb.Append($"\\x{b:x2}");
        }
        sb.Append(")");
        return sb.ToString();
    }

    private async Task CaptureLoopAsync(CancellationToken token)
    {
        bool emittedStreaming = false;

        while (running && !token.IsCancellationRequested)
        {
            try
            {
                var hWnd = WindowCaptureService.FindTargetWindow(out _);
                if (hWnd != IntPtr.Zero)
                {
                    if (!emittedStreaming)
                    {
                        emittedStreaming = true;
                        StateChanged?.Invoke(SessionState.Streaming);
                    }

                    var frame = WindowCaptureService.Capture(hWnd);
                    if (frame != null)
                    {
                        FrameReceived?.Invoke(frame);
                    }
                }
                else
                {
                    if (!emittedStreaming)
                    {
                        emittedStreaming = true;
                        StateChanged?.Invoke(SessionState.Streaming);
                    }
                }
            }
            catch { }

            try
            {
                await Task.Delay(150, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Stop()
    {
        if (!running) return;
        running = false;
        cts?.Cancel();
        StateChanged?.Invoke(SessionState.Disconnected(DisconnectKind.Stopped));
    }

    public void ReconnectNow()
    {
        if (!running) return;
        Stop();
        Start();
    }

    public void Dispose()
    {
        Stop();
        cts?.Dispose();
    }
}
