// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core;
using GameTranslator.RemotePlay;

namespace GameTranslator.App.Services;

/// <summary>
/// Windows'ta Chiaki veya PlayStation Remote Play penceresini periyodik olarak yakalayan IStreamSession uygulaması.
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

        if (console != null && !string.IsNullOrWhiteSpace(console.Host))
        {
            ChiakiLauncher.SyncConfig(console.Host);
            ChiakiLauncher.LaunchOrBringToFront(console.Host, console.Name);
        }

        loopTask = Task.Run(() => CaptureLoopAsync(cts.Token));
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
                    var frame = WindowCaptureService.Capture(hWnd);
                    if (frame != null)
                    {
                        if (!emittedStreaming)
                        {
                            emittedStreaming = true;
                            StateChanged?.Invoke(SessionState.Streaming);
                        }
                        FrameReceived?.Invoke(frame);
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
