// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace GameTranslator.Translation.Tests;

public sealed class ModelStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gametranslator-modelstore-" + Guid.NewGuid().ToString("N"));
    private readonly byte[] _a = RandomBytes(300_000, 1);
    private readonly byte[] _b = RandomBytes(50_000, 2);
    private readonly FakeServer _server = new();

    public ModelStoreTests()
    {
        _server.Files["/a.bin"] = _a;
        _server.Files["/b.bin"] = _b;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static byte[] RandomBytes(int length, int seed)
    {
        var bytes = new byte[length];
        new Random(seed).NextBytes(bytes);
        return bytes;
    }

    private static string Sha(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    private ModelStore CreateStore(string? shaOfA = null) => new(_dir,
    [
        new ModelFile("a.bin", new Uri("https://models.test/a.bin"), _a.Length, shaOfA ?? Sha(_a)),
        new ModelFile("b.bin", new Uri("https://models.test/b.bin"), _b.Length, Sha(_b)),
    ], new HttpClient(_server));

    [Fact]
    public async Task DownloadsVerifiesAndInstalls()
    {
        var store = CreateStore();
        Assert.False(store.IsInstalled);
        Assert.Equal(_a.Length + _b.Length, store.TotalBytes);

        var reports = new List<double>();
        await store.DownloadAsync(new SyncProgress(reports.Add), CancellationToken.None);

        Assert.True(store.IsInstalled);
        Assert.Equal(_a, File.ReadAllBytes(store.PathOf("a.bin")));
        Assert.Equal(_b, File.ReadAllBytes(store.PathOf("b.bin")));
        Assert.Empty(Directory.GetFiles(_dir, "*.part"));
        Assert.Equal(1.0, reports[^1]);
        Assert.Equal(reports.OrderBy(r => r), reports);
    }

    [Fact]
    public async Task RejectsChecksumMismatch()
    {
        var store = CreateStore(shaOfA: new string('0', 64));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.DownloadAsync(null, CancellationToken.None));

        Assert.False(store.IsInstalled);
        Assert.False(File.Exists(store.PathOf("a.bin")));
        // Bozuk parça silinir ki bir sonraki deneme baştan başlasın.
        Assert.False(File.Exists(store.PathOf("a.bin") + ".part"));
    }

    [Fact]
    public async Task RejectsTruncatedBody()
    {
        _server.Files["/a.bin"] = _a[..1000];
        var store = CreateStore();

        await Assert.ThrowsAsync<InvalidDataException>(() => store.DownloadAsync(null, CancellationToken.None));
        Assert.False(store.IsInstalled);
    }

    [Fact]
    public void PartialDownloadIsNotInstalled()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(store.PathOf("a.bin") + ".part", _a[..1000]);
        File.WriteAllBytes(store.PathOf("b.bin"), _b);
        Assert.False(store.IsInstalled);

        // Asıl adla duran ama boyu tutmayan dosya da kurulu sayılmaz.
        File.WriteAllBytes(store.PathOf("a.bin"), _a[..1000]);
        Assert.False(store.IsInstalled);
    }

    [Fact]
    public async Task ResumesFromPartialFile()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(store.PathOf("a.bin") + ".part", _a[..120_000]);

        await store.DownloadAsync(null, CancellationToken.None);

        Assert.True(store.IsInstalled);
        Assert.Equal(_a, File.ReadAllBytes(store.PathOf("a.bin")));
        Assert.Contains("/a.bin bytes=120000-", _server.Requests);
    }

    [Fact]
    public async Task RestartsWhenServerIgnoresRange()
    {
        _server.SupportsRange = false;
        var store = CreateStore();
        Directory.CreateDirectory(_dir);
        // Eski parça bozuk olsa bile 200 yanıtında üzerine yazılır.
        File.WriteAllBytes(store.PathOf("a.bin") + ".part", new byte[120_000]);

        await store.DownloadAsync(null, CancellationToken.None);

        Assert.True(store.IsInstalled);
        Assert.Equal(_a, File.ReadAllBytes(store.PathOf("a.bin")));
    }

    [Fact]
    public async Task CorruptPartialFileIsRejectedThenRecovered()
    {
        var store = CreateStore();
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(store.PathOf("a.bin") + ".part", new byte[120_000]);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.DownloadAsync(null, CancellationToken.None));
        Assert.False(store.IsInstalled);

        await store.DownloadAsync(null, CancellationToken.None);
        Assert.True(store.IsInstalled);
    }

    [Fact]
    public async Task SkipsInstalledFiles()
    {
        var store = CreateStore();
        await store.DownloadAsync(null, CancellationToken.None);
        _server.Requests.Clear();

        var reports = new List<double>();
        await store.DownloadAsync(new SyncProgress(reports.Add), CancellationToken.None);

        Assert.Empty(_server.Requests);
        Assert.Equal(1.0, reports[^1]);
    }

    [Fact]
    public async Task CancelledDownloadLeavesNothingInstalled()
    {
        using var cts = new CancellationTokenSource();
        _server.OnChunk = sent => { if (sent > 100_000) cts.Cancel(); };
        var store = CreateStore();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.DownloadAsync(null, cts.Token));

        Assert.False(store.IsInstalled);
        Assert.False(File.Exists(store.PathOf("a.bin")));

        _server.OnChunk = null;
        await store.DownloadAsync(null, CancellationToken.None);
        Assert.True(store.IsInstalled);
    }

    [Fact]
    public void DefaultFilesArePinnedAndChecksummed()
    {
        var store = new ModelStore(_dir);
        Assert.Equal(4, store.Files.Count);
        Assert.All(store.Files, f =>
        {
            Assert.Matches("^[0-9a-f]{64}$", f.Sha256);
            Assert.Contains("/resolve/ce128ea834a090aca240e4e113e7756e06cc78e0/", f.Url.AbsoluteUri);
            Assert.True(f.Size > 0);
        });
        Assert.InRange(store.TotalBytes, 500_000_000, 560_000_000);
    }

    /// <summary>İlerlemeyi eşzamanlı toplar (Progress&lt;T&gt; olayları sonradan, başka iş parçacığında atar).</summary>
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }

    /// <summary>Range destekli, isteği kaydeden sahte sunucu; gövdeyi küçük parçalar halinde akıtır.</summary>
    private sealed class FakeServer : HttpMessageHandler
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public List<string> Requests { get; } = [];
        public bool SupportsRange { get; set; } = true;
        public Action<long>? OnChunk { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var range = request.Headers.Range?.Ranges.FirstOrDefault();
            lock (Requests) Requests.Add(range is null ? path : $"{path} bytes={range.From}-{range.To}");
            if (!Files.TryGetValue(path, out var data))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            var start = SupportsRange && range?.From is long from ? from : 0;
            var response = new HttpResponseMessage(start > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
            {
                Content = new StreamContent(new ChunkedStream(data, (int)start, OnChunk)),
            };
            if (start > 0)
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, data.Length - 1, data.Length);
            return Task.FromResult(response);
        }
    }

    private sealed class ChunkedStream(byte[] data, int start, Action<long>? onChunk) : Stream
    {
        private int _position = start;

        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = Math.Min(Math.Min(count, 8192), data.Length - _position);
            Array.Copy(data, _position, buffer, offset, n);
            _position += n;
            onChunk?.Invoke(_position);
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
