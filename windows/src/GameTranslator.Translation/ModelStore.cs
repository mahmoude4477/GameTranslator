// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace GameTranslator.Translation;

/// <summary>İndirilecek tek model dosyası: adı, kaynağı, beklenen boyutu ve SHA-256 özeti.</summary>
public sealed record ModelFile(string Name, Uri Url, long Size, string Sha256);

/// <summary>
/// Çeviri modelinin diskteki yeri ve indirilmesi. Dosyalar önce <c>.part</c> olarak iner, özet tutarsa
/// asıl adına taşınır; böylece asıl adla duran her dosya doğrulanmıştır ve yarım iniş kurulu sayılmaz.
/// Kesilen iniş, sunucu izin verirse kaldığı yerden sürer.
/// </summary>
public sealed class ModelStore
{
    public const string EncoderFile = "encoder_model_int8.onnx";
    public const string DecoderFile = "decoder_model_merged_int8.onnx";
    public const string SourceSpmFile = "source.spm";
    public const string VocabFile = "vocab.json";

    private const string PartSuffix = ".part";

    /// <summary>Uzun süre veri gelmezse bağlantı takılmış sayılır; iniş yeniden denenebilsin diye hata verir.</summary>
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(60);

    // Depo belirli bir sürüme sabitlenir: dosyalar değişirse özetler de geçersiz kalırdı.
    private const string Revision = "ce128ea834a090aca240e4e113e7756e06cc78e0";
    private static readonly string BaseUrl = $"https://huggingface.co/onnx-community/opus-mt-tc-big-en-tr/resolve/{Revision}/";

    /// <summary>Helsinki-NLP opus-mt-tc-big-en-tr (CC BY 4.0), onnx-community'nin int8 ONNX dönüşümü.</summary>
    public static IReadOnlyList<ModelFile> EnglishTurkish { get; } =
    [
        new(EncoderFile, new Uri(BaseUrl + "onnx/" + EncoderFile), 135_730_164,
            "58bfc076347346049a52a8d53b1f6e4664af74e9737aded374d7d1f78805f46e"),
        new(DecoderFile, new Uri(BaseUrl + "onnx/" + DecoderFile), 395_716_769,
            "23223bce303d925b119ec905b9422a2814e7ad5f96275407be1d387b13879147"),
        new(SourceSpmFile, new Uri(BaseUrl + SourceSpmFile), 796_953,
            "180d3f94ddfee5585d78b702290ded0f4f9bd40f299349c8383b32a579fefad4"),
        new(VocabFile, new Uri(BaseUrl + VocabFile), 1_501_287,
            "01ef522455d2bf22b0716416ee34688cc2b3f5fa257f742665d21b6acc450ac1"),
    ];

    private static readonly Lazy<HttpClient> SharedClient = new(() =>
        // Büyük dosyada toplam süre sınırı anlamsız; takılmayı StallTimeout yakalar.
        new HttpClient { Timeout = Timeout.InfiniteTimeSpan });

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _downloadGate = new(1, 1);

    public ModelStore(string installDirectory, HttpClient? httpClient = null)
        : this(installDirectory, EnglishTurkish, httpClient) { }

    public ModelStore(string installDirectory, IReadOnlyList<ModelFile> files, HttpClient? httpClient = null)
    {
        InstallDirectory = installDirectory;
        Files = files;
        _http = httpClient ?? SharedClient.Value;
    }

    public string InstallDirectory { get; }
    public IReadOnlyList<ModelFile> Files { get; }
    public long TotalBytes => Files.Sum(f => f.Size);

    /// <summary>Tüm dosyalar asıl adlarıyla ve doğru boyutta duruyorsa kurulu.</summary>
    public bool IsInstalled => Files.All(IsFileInstalled);

    public string PathOf(string fileName) => Path.Combine(InstallDirectory, fileName);

    private bool IsFileInstalled(ModelFile file)
    {
        var info = new FileInfo(PathOf(file.Name));
        return info.Exists && info.Length == file.Size;
    }

    /// <summary>Eksik dosyaları indirir; ilerleme 0–1 arası toplam bayt oranıdır.</summary>
    /// <exception cref="InvalidDataException">İnen dosyanın boyutu ya da özeti tutmadı.</exception>
    public async Task DownloadAsync(IProgress<double>? progress, CancellationToken ct)
    {
        await _downloadGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(InstallDirectory);
            var reporter = new ProgressReporter(progress, TotalBytes);
            foreach (var file in Files)
            {
                if (IsFileInstalled(file))
                {
                    reporter.Add(file.Size);
                    continue;
                }
                await DownloadFileAsync(file, reporter, ct).ConfigureAwait(false);
            }
            reporter.Complete();
        }
        finally
        {
            _downloadGate.Release();
        }
    }

    private async Task DownloadFileAsync(ModelFile file, ProgressReporter reporter, CancellationToken ct)
    {
        var finalPath = PathOf(file.Name);
        var partPath = finalPath + PartSuffix;
        // Dosya adı alt klasör içerebilir ("voices/male_1.wav").
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        var existing = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
        if (existing > file.Size)
        {
            File.Delete(partPath);
            existing = 0;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        if (existing > 0 && existing < file.Size)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, file.Url);
            request.Headers.Range = new RangeHeaderValue(existing, null);
            using var response = await SendAsync(request, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.PartialContent)
            {
                // Önceki inişin baytları da özete girmeli.
                await HashExistingAsync(partPath, hash, ct).ConfigureAwait(false);
                reporter.Add(existing);
                await WriteBodyAsync(response, partPath, FileMode.Append, hash, reporter, ct).ConfigureAwait(false);
            }
            else
            {
                // Sunucu kaldığı yerden vermiyorsa baştan.
                response.EnsureSuccessStatusCode();
                await WriteBodyAsync(response, partPath, FileMode.Create, hash, reporter, ct).ConfigureAwait(false);
            }
        }
        else if (existing == file.Size)
        {
            await HashExistingAsync(partPath, hash, ct).ConfigureAwait(false);
            reporter.Add(existing);
        }
        else
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, file.Url);
            using var response = await SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await WriteBodyAsync(response, partPath, FileMode.Create, hash, reporter, ct).ConfigureAwait(false);
        }

        var length = new FileInfo(partPath).Length;
        var actual = Convert.ToHexStringLower(hash.GetHashAndReset());
        if (length != file.Size || !string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            // Bozuk parçayı tutmak, sonraki denemede de aynı hatayı verirdi.
            File.Delete(partPath);
            throw new InvalidDataException(
                $"{file.Name}: expected {file.Size} bytes / {file.Sha256}, got {length} bytes / {actual}");
        }
        File.Move(partPath, finalPath, overwrite: true);
    }

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

    private static async Task HashExistingAsync(string path, IncrementalHash hash, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, true);
        var buffer = new byte[1 << 16];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            hash.AppendData(buffer, 0, read);
    }

    private static async Task WriteBodyAsync(HttpResponseMessage response, string partPath, FileMode mode,
        IncrementalHash hash, ProgressReporter reporter, CancellationToken ct)
    {
        await using var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var output = new FileStream(partPath, mode, FileAccess.Write, FileShare.None, 1 << 16, true);
        var buffer = new byte[1 << 16];
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
        while (true)
        {
            stall.CancelAfter(StallTimeout);
            int read;
            try
            {
                read = await body.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException("Model download stalled");
            }
            if (read == 0) break;
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            reporter.Add(read);
        }
    }

    /// <summary>Arayüzü boğmamak için ilerlemeyi yalnızca binde bir değiştiğinde bildirir.</summary>
    private sealed class ProgressReporter(IProgress<double>? progress, long total)
    {
        private long _done;
        private double _lastReported = -1;

        public void Add(long bytes)
        {
            _done += bytes;
            if (progress is null || total <= 0) return;
            var value = Math.Min(1.0, (double)_done / total);
            if (value - _lastReported < 0.001) return;
            _lastReported = value;
            progress.Report(value);
        }

        public void Complete()
        {
            if (_lastReported < 1.0) progress?.Report(1.0);
        }
    }
}
