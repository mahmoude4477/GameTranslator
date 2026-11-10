// SPDX-License-Identifier: AGPL-3.0-only

using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using GameTranslator.Core.Dubbing;
using GameTranslator.Shell;
using Windows.Media.SpeechSynthesis;

namespace GameTranslator.App.Services;

/// <summary>
/// Hızlı dublaj: Windows'un kendi Türkçe sesi (Microsoft Tolga). İndirme gerektirmez ve bir cümleyi işlemciyi
/// yormadan anında seslendirir; doğal sesler kadar canlı değildir. Windows'ta tek Türkçe ses olduğu için karakterler
/// ses tonu ve hızıyla ayrılır.
/// </summary>
internal sealed class WindowsVoiceModel : IDubbingModel
{
    private static VoiceInformation? Turkish => SpeechSynthesizer.AllVoices
        .FirstOrDefault(voice => voice.Language.StartsWith("tr", StringComparison.OrdinalIgnoreCase));

    public static bool Available
    {
        get
        {
            try { return Turkish is not null; }
            catch (Exception) { return false; }
        }
    }

    public bool IsInstalled => true;
    public long DownloadBytes => 0;
    public Task DownloadAsync(IProgress<double> progress, CancellationToken cancellationToken) => Task.CompletedTask;
    public byte[] VoiceFile(DubbingVoice voice) => [];
    public ISpeechSynthesizer CreateSynthesizer() => new Synthesizer();

    /// <summary>Ses türü adresin adından ("…/voices/female_2.wav") anlaşılır: (ton, hız).</summary>
    internal static (double Pitch, double Rate) Tone(Uri voice) => Path.GetFileNameWithoutExtension(voice.AbsolutePath) switch
    {
        "female_1" => (1.55, 1.1),
        "female_2" => (1.4, 1.0),
        "female_3" => (1.48, 1.05),
        "male_1" => (0.85, 0.95),
        "male_2" => (1.05, 1.15),
        _ => (0.95, 1.05),
    };

    private sealed class Synthesizer : ISpeechSynthesizer
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly SpeechSynthesizer engine = new();

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (Turkish is { } voice) engine.Voice = voice;
            return Task.CompletedTask;
        }

        public async Task<SpeechAudio> SynthesizeAsync(string text, Uri voice, CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (Turkish is { } turkish && engine.Voice.Id != turkish.Id) engine.Voice = turkish;
                var (pitch, rate) = Tone(voice);
                engine.Options.AudioPitch = pitch;
                engine.Options.SpeakingRate = rate;
                using var stream = await engine.SynthesizeTextToStreamAsync(text).AsTask(cancellationToken);
                using var memory = new MemoryStream();
                await stream.AsStreamForRead().CopyToAsync(memory, cancellationToken);
                return SpeechAudio.FromWav(memory.ToArray());
            }
            finally
            {
                gate.Release();
            }
        }

        public ValueTask DisposeAsync()
        {
            engine.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
