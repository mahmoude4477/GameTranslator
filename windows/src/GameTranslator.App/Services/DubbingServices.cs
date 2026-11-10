// SPDX-License-Identifier: AGPL-3.0-only

using System.IO;
using System.Runtime.InteropServices;
using GameTranslator.Core.Dubbing;
using GameTranslator.Shell;
using GameTranslator.Translation;

namespace GameTranslator.App.Services;

/// <summary>
/// Pocket TTS Turkish (wite-tech, CC-BY-4.0) ilk kullanımda %LOCALAPPDATA%\GameTranslator\models\tts-tr\&lt;sürüm&gt;
/// altına indirilir; ses motoru uygulamayla gelen tts\pocket-tts.exe (Kyutai, MIT).
/// </summary>
internal sealed class PocketDubbingModel : IDubbingModel
{
    private readonly ModelStore store = new(Path.Combine(AppPaths.Models, "tts-tr", DubbingModel.Revision),
        [.. DubbingModel.Files.Select(file => new ModelFile(file.Name, file.Url, file.Bytes, file.Sha256))]);

    public static string Executable => Path.Combine(AppContext.BaseDirectory, "tts", "pocket-tts.exe");

    /// <summary>Ses motoru pakette yoksa (geliştirici derlemesi) dublaj gösterilmez.</summary>
    public static bool EngineBundled => File.Exists(Executable);

    public bool IsInstalled => store.IsInstalled;
    public long DownloadBytes => store.TotalBytes;

    public async Task DownloadAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        await store.DownloadAsync(progress, cancellationToken).ConfigureAwait(false);
        WriteConfig();
        // Önceki sürümlerin modelleri yer kaplamasın.
        foreach (var old in Directory.EnumerateDirectories(Path.Combine(AppPaths.Models, "tts-tr")))
            if (!string.Equals(Path.GetFileName(old), DubbingModel.Revision, StringComparison.Ordinal))
                try { Directory.Delete(old, recursive: true); } catch (IOException) { }
    }

    public byte[] VoiceFile(DubbingVoice voice) => File.ReadAllBytes(store.PathOf($"voices/{voice.FileName()}.wav"));

    public ISpeechSynthesizer CreateSynthesizer()
    {
        var config = WriteConfig();
        return new Synthesizer(new PocketTtsServer(Executable, config, store.PathOf("voices/male_1.wav")));
    }

    private string WriteConfig()
    {
        var path = store.PathOf("gametranslator.yaml");
        var local = DubbingModel.LocalConfig(File.ReadAllText(store.PathOf("config.yaml")), store.InstallDirectory);
        if (!File.Exists(path) || File.ReadAllText(path) != local) File.WriteAllText(path, local);
        return path;
    }

    private sealed class Synthesizer(PocketTtsServer server) : ISpeechSynthesizer
    {
        public Task StartAsync(CancellationToken cancellationToken = default) => server.StartAsync(cancellationToken);
        public Task<SpeechAudio> SynthesizeAsync(string text, Uri voice, CancellationToken cancellationToken = default) =>
            server.SynthesizeAsync(text, voice, cancellationToken);
        public ValueTask DisposeAsync() => server.DisposeAsync();
    }
}

/// <summary>Bellekteki WAV'ı Windows'un kendi sesiyle (winmm PlaySound) çalar; ek kitaplık gerekmez.</summary>
internal sealed partial class WindowsAudioOutput : IAudioOutput
{
    private const uint SndAsync = 0x0001, SndMemory = 0x0004, SndNoDefault = 0x0002;
    private readonly object gate = new();
    /// <summary>Çalan ses bitene kadar bellekte sabit kalmalı.</summary>
    private GCHandle playing;

    public void Play(byte[] wav)
    {
        lock (gate)
        {
            PlaySound(0, 0, 0);
            if (playing.IsAllocated) playing.Free();
            playing = GCHandle.Alloc(wav, GCHandleType.Pinned);
            PlaySound(playing.AddrOfPinnedObject(), 0, SndMemory | SndAsync | SndNoDefault);
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            PlaySound(0, 0, 0);
            if (playing.IsAllocated) playing.Free();
        }
    }

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySound(nint sound, nint module, uint flags);
}
