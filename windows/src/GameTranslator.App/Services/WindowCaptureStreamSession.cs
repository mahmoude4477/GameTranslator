// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.IO;
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
            SyncChiakiRegistry();

            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chiaki", "chiaki.exe"),
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

    private void SyncChiakiRegistry()
    {
        try
        {
            if (console == null || string.IsNullOrWhiteSpace(console.Host)) return;

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Chiaki\Chiaki");
            if (key != null)
            {
                key.SetValue("registered_hosts\\size", 1, RegistryValueKind.DWord);
                key.SetValue("registered_hosts\\1\\target", 1000100, RegistryValueKind.DWord);
                key.SetValue("registered_hosts\\1\\server_nickname", string.IsNullOrWhiteSpace(console.Name) ? "PS5" : console.Name, RegistryValueKind.String);
                key.SetValue("registered_hosts\\1\\server_mac", new byte[] { 0xE8, 0x6E, 0x3A, 0x94, 0xB1, 0x64 }, RegistryValueKind.Binary);
                if (console.RegistKey != null && console.RegistKey.Length > 0)
                    key.SetValue("registered_hosts\\1\\rp_regist_key", console.RegistKey, RegistryValueKind.Binary);
                if (console.Morning != null && console.Morning.Length > 0)
                    key.SetValue("registered_hosts\\1\\rp_key", console.Morning, RegistryValueKind.Binary);
                key.SetValue("registered_hosts\\1\\rp_key_type", 0, RegistryValueKind.DWord);
                key.SetValue("settings\\audio_volume", 0, RegistryValueKind.DWord);
                key.SetValue("settings\\fullscreen", 0, RegistryValueKind.DWord);
                key.SetValue("settings\\remote_play_ask", 0, RegistryValueKind.DWord);
            }
        }
        catch { }
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
