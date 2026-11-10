// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace GameTranslator.Core.Dubbing;

public sealed class PocketTtsException(string message) : Exception(message);

/// <summary>
/// Kyutai'nin pocket-tts programını (MIT) yerel sunucu olarak çalıştırır: model bir kez yüklenir, her cümle
/// <c>POST /tts</c> ile seslendirilir. Sunucu yalnız bu bilgisayardan (127.0.0.1) erişilebilir (Mac: PocketTTSServer.swift).
/// </summary>
public sealed class PocketTtsServer(string executable, string config, string defaultVoice, int threads = 4) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private Process? _process;
    private int _port;

    /// <summary>Sunucu çalışmıyorsa başlatır ve modelin yüklenmesini bekler (ilk açılış birkaç saniye sürer).</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_process is { HasExited: false }) return;
            await LaunchAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task LaunchAsync(CancellationToken cancellationToken)
    {
        var port = FreePort();
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = false,
            RedirectStandardError = true,
        };
        foreach (var argument in new[] { "serve", "--config", config, "--host", "127.0.0.1", "--port", port.ToString(),
                                         "--default-voice", defaultVoice })
            start.ArgumentList.Add(argument);
        // İşlemcinin geri kalanı (oyun akışı, çeviri) yavaşlamasın.
        start.Environment["RAYON_NUM_THREADS"] = threads.ToString();
        Process process;
        try
        {
            process = Process.Start(start) ?? throw new PocketTtsException("pocket-tts başlamadı");
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            throw new PocketTtsException($"pocket-tts başlamadı: {error.Message}");
        }
        // Seslendirme, altyazı okuma ve çeviriyle yarışıp onları geciktirmesin: işlemciyi en son o alır.
        try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception error) when (error is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        // Kapanırsa sebebi hata mesajında görünsün: stderr'in son satırları saklanır.
        var errors = new Queue<string>();
        process.ErrorDataReceived += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(e.Data)) return;
            lock (errors)
            {
                errors.Enqueue(e.Data.Trim());
                while (errors.Count > 8) errors.Dequeue();
            }
        };
        process.BeginErrorReadLine();
        _process = process;
        _port = port;
        ChildProcessGuard.Attach(process);

        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            if (process.HasExited)
            {
                process.WaitForExit(); // stderr'in kalanı da okunsun
                throw new PocketTtsException(ExitMessage(process.ExitCode, errors));
            }
            try
            {
                using var response = await _http.GetAsync($"http://127.0.0.1:{port}/health", cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK) return;
            }
            catch (HttpRequestException) { /* henüz açılmadı */ }
            await Task.Delay(300, cancellationToken).ConfigureAwait(false);
        }
        Stop();
        throw new PocketTtsException("pocket-tts 90 saniyede açılmadı");
    }

    /// <summary>Kapanma iletisi: çıkış kodu ve pocket-tts'in yazdığı son anlamlı hata satırı.</summary>
    public static string ExitMessage(int exitCode, IEnumerable<string> errorLines)
    {
        string? last;
        lock (errorLines) last = errorLines.LastOrDefault(line => line.Length > 0);
        if (last is { Length: > 300 }) last = last[..300] + "…";
        return last is null ? $"pocket-tts kapandı (çıkış kodu {exitCode})" : $"pocket-tts kapandı (çıkış kodu {exitCode}): {last}";
    }

    /// <summary><paramref name="voice"/>: sesin adresi (http://…, sunucu sesi bir kez hazırlayıp saklar).</summary>
    public async Task<SpeechAudio> SynthesizeAsync(string text, Uri voice, CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken).ConfigureAwait(false);
        using var content = new FormUrlEncodedContent([new("text", text), new("voice_url", voice.AbsoluteUri)]);
        using var response = await _http.PostAsync($"http://127.0.0.1:{_port}/tts", content, cancellationToken).ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
            throw new PocketTtsException(System.Text.Encoding.UTF8.GetString(bytes.AsSpan(0, Math.Min(300, bytes.Length))));
        return SpeechAudio.FromWav(bytes);
    }

    public void Stop()
    {
        try
        {
            if (_process is { HasExited: false }) _process.Kill();
        }
        catch (InvalidOperationException) { /* zaten kapandı */ }
        _process?.Dispose();
        _process = null;
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        _http.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>İşletim sisteminin verdiği boş bir yerel port.</summary>
    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
