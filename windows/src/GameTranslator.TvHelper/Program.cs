// SPDX-License-Identifier: AGPL-3.0-only
// Mac uygulamasının Samsung ve Android TV kurulumu için çağırdığı yardımcı. Windows uygulaması aynı kitaplığı
// (GameTranslator.TvInstall) doğrudan kullanır. Her satır bir JSON olaydır:
//   {"event":"progress","step":"signing"}   {"event":"login","url":"https://…"}   {"event":"approve"}
//   {"event":"done","version":"0.7.0","installed":true}   {"event":"error","code":"Unreachable","message":"…"}
//   {"event":"found","hosts":["192.168.1.40"]}

using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using GameTranslator.TvInstall.Android;
using GameTranslator.TvInstall.Samsung;
using TizenSdb.SigningManager;

// TizenInstaller konsola yazar ve kurulum sırasında Console.Out yakalanır; olaylar her zaman gerçek çıktıya gider.
var output = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
var gate = new object();

void Emit(string name, params (string Key, JsonNode? Value)[] fields)
{
    var json = new JsonObject { ["event"] = name };
    foreach (var (key, value) in fields) json[key] = value;
    lock (gate) output.WriteLine(json.ToJsonString());
}

string Option(string name) =>
    args.SkipWhile(a => a != name).Skip(1).FirstOrDefault()
    ?? throw new ArgumentException($"{name} eksik");

using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
// Mac uygulaması kapanırsa yardımcı da kapansın: girdi kapanınca iptal et.
_ = Task.Run(async () =>
{
    try { await Console.OpenStandardInput().CopyToAsync(Stream.Null); } catch (IOException) { }
    if (args.Contains("--exit-with-input")) cancel.Cancel();
});

try
{
    switch (args.FirstOrDefault())
    {
        case "android-scan":
        {
            var hosts = await AndroidTvInstaller.ScanAsync(cancellationToken: cancel.Token);
            Emit("found", ("hosts", new JsonArray([.. hosts.Select(h => (JsonNode?)JsonValue.Create(h))])));
            return 0;
        }

        case "adb-shell":
        {
            var keyDir = Option("--key-dir");
            var key = AndroidTvInstaller.LoadOrCreateKey(keyDir);
            await using var adb = await AdbConnection.ConnectAsync(Option("--host"), 5555, key, cancellationToken: cancel.Token);
            var cmd = Option("--cmd");
            var res = await adb.RunAsync($"shell:{cmd}", cancellationToken: cancel.Token);
            Console.WriteLine(res);
            return 0;
        }

        case "android-install":
        {
            var installer = new AndroidTvInstaller(Option("--host"), Option("--key-dir"))
            {
                OnApprovalNeeded = () => Emit("approve"),
            };
            var (version, installed) = await installer.EnsureInstalledAsync(Option("--package"), Option("--version"), cancel.Token);
            Emit("done", ("version", version), ("installed", installed));
            return 0;
        }

        case "samsung-install":
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
            var installer = new SamsungTvInstaller(Option("--host"), Option("--profiles"), new SamsungCertificates(http))
            {
                Progress = step => Emit("progress", ("step", step)),
                // Tarayıcıyı Mac uygulaması açar; giriş geri dönüşünü burası dinler.
                SignIn = token => SamsungLogin.SignInAsync(url => Emit("login", ("url", url)), TimeSpan.FromMinutes(10), token),
            };
            await installer.InstallAsync(Option("--package"), cancel.Token);
            Emit("done", ("version", Option("--version")), ("installed", true));
            return 0;
        }

        case "self-test":
            // Yayın derlemesinde XML imzasının AOT ile çalıştığını doğrular (scripts/build-tv-helper.sh).
            await SelfTestAsync();
            Emit("done", ("version", "self-test"), ("installed", false));
            return 0;

        default:
            Console.Error.WriteLine("kullanım: gametranslator-tv-helper android-scan | android-install --host IP --package APK --version V --key-dir DİZİN | samsung-install --host IP --package WGT --version V --profiles DİZİN | self-test");
            return 2;
    }
}
catch (AndroidInstallException error)
{
    Emit("error", ("code", error.Error.ToString()), ("message", error.Message));
    return 1;
}
catch (SamsungInstallException error)
{
    Emit("error", ("code", error.Error.ToString()), ("message", error.Message));
    return 1;
}
catch (Exception error) when (error is not OperationCanceledException)
{
    Emit("error", ("code", "InstallFailed"), ("message", error.Message));
    return 1;
}

static async Task SelfTestAsync()
{
    var dir = Directory.CreateTempSubdirectory("gametranslator-selftest").FullName;
    try
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=GameTranslator self-test", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var withKey = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var p12 = Path.Combine(dir, "test.p12");
        await File.WriteAllBytesAsync(p12, new X509Certificate2Collection { withKey }.ExportPkcs12(
            new PbeParameters(PbeEncryptionAlgorithm.TripleDes3KeyPkcs12, HashAlgorithmName.SHA1, 2048), "x"));
        var wgt = Path.Combine(dir, "app.wgt");
        using (var zip = ZipFile.Open(wgt, ZipArchiveMode.Create))
        {
            await using var entry = zip.CreateEntry("config.xml").Open();
            await entry.WriteAsync("<widget/>"u8.ToArray());
        }
        await TizenWgtSigner.ReSignWgtWithCertsInPlace(wgt, p12, p12, "x");
        using var signed = ZipFile.OpenRead(wgt);
        if (signed.GetEntry("author-signature.xml") is null || signed.GetEntry("signature1.xml") is null)
            throw new InvalidOperationException("imza dosyaları oluşmadı");
    }
    finally
    {
        Directory.Delete(dir, recursive: true);
    }
}
