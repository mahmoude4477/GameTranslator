// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.IO;
using GameTranslator.Core;
using GameTranslator.RemotePlay;

namespace GameTranslator.App.Services;

/// <summary>
/// Windows'ta Chiaki veya PlayStation Remote Play penceresini periyodik olarak yakalayan IStreamSession uygulaması.
/// Yerel gametranslator_native DLL bulunmadığında veya pencere yakalama modunda kullanılır.
/// </summary>
public sealed class WindowCaptureStreamSession : IStreamSession
{
    public event Action<Frame>? FrameReceived;
    public event Action<SessionState>? StateChanged;
    public event Action<RegisteredConsole>? ConsoleMoved;

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
            var possiblePaths = new[]
            {
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
                    if (Process.GetProcessesByName(name).Length == 0)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        });
                        break;
                    }
                }
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
