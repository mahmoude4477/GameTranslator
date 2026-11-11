// SPDX-License-Identifier: AGPL-3.0-only

using System.Formats.Asn1;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using GameTranslator.TvInstall.Samsung;
using TizenSdb.SigningManager;

namespace GameTranslator.TvInstall.Tests;

public class SamsungTests
{

    /// <summary>مثل TizenInstaller: يفتح الملف ويتركه دون إغلاق.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void OpenAndForget(string path) => _ = File.OpenRead(path);

    [Fact]
    public void StagedPackageIsDeletedEvenWhenALibraryLeftItOpen()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gametranslator-test-{Guid.NewGuid():N}.wgt");
        File.WriteAllBytes(path, [1, 2, 3]);
        OpenAndForget(path);
        Assert.True(SamsungTvInstaller.DeleteStaged(path));
        Assert.False(File.Exists(path));
        Assert.True(SamsungTvInstaller.DeleteStaged(path), "الملف غير الموجود ليس مشكلة");
    }

    [Fact]
    public void SignInCallbackCarriesTheAccount()
    {
        var json = "{\"access_token\":\"tok\",\"userId\":\"u1\",\"inputEmailID\":\"a@b.c\",\"token_type\":\"bearer\"}";
        var body = $"code={Uri.EscapeDataString(json)}&state=accountcheckdogeneratedstatetext";
        Assert.Equal(new SamsungAccount("tok", "u1", "a@b.c"), SamsungLogin.ParseCallback(body));
    }

    [Fact]
    public void ForeignCallbackIsIgnored()
    {
        var json = Uri.EscapeDataString("{\"access_token\":\"tok\",\"userId\":\"u1\"}");
        Assert.Null(SamsungLogin.ParseCallback($"code={json}&state=other"));
        Assert.Null(SamsungLogin.ParseCallback("state=accountcheckdogeneratedstatetext&code=broken"));
    }

    [Fact]
    public void SignInUrlReturnsToThisComputer()
    {
        Assert.StartsWith("https://account.samsung.com/", SamsungLogin.AuthorizeUrl);
        Assert.Contains(Uri.EscapeDataString("http://localhost:4794/signin/callback"), SamsungLogin.AuthorizeUrl);
    }

    [Fact]
    public async Task SignInWaitsForSamsungsPost()
    {
        string? opened = null;
        var signIn = SamsungLogin.SignInAsync(url => opened = url, TimeSpan.FromSeconds(10));
        while (opened is null) await Task.Delay(20);
        using var http = new HttpClient();
        var json = "{\"access_token\":\"tok\",\"userId\":\"u1\",\"inputEmailID\":\"a@b.c\"}";
        // الطلب الذي لا يأتي من المتصفح يُتجاهل، ثم يصل الرد الحقيقي.
        var stray = await http.GetAsync($"http://localhost:{SamsungLogin.CallbackPort}/favicon.ico");
        Assert.Equal(HttpStatusCode.NotFound, stray.StatusCode);
        var response = await http.PostAsync($"http://localhost:{SamsungLogin.CallbackPort}/signin/callback",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["code"] = json, ["state"] = "accountcheckdogeneratedstatetext" }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new SamsungAccount("tok", "u1", "a@b.c"), await signIn);
    }

    [Fact]
    public void DistributorRequestNamesTheTv()
    {
        using var key = RSA.Create(2048);
        var pem = Encoding.ASCII.GetString(SamsungCertificates.DistributorRequest(key, "ABCDEF0123456789", "a@b.c"));
        var request = CertificateRequest.LoadSigningRequestPem(pem, HashAlgorithmName.SHA256,
            CertificateRequestLoadOptions.UnsafeLoadCertificateExtensions);
        Assert.Contains("CN=TizenSDK", request.SubjectName.Name);
        var san = request.CertificateExtensions.Single(e => e.Oid?.Value == "2.5.29.17");
        var reader = new AsnReader(san.RawData, AsnEncodingRules.DER).ReadSequence();
        var uris = new List<string>();
        while (reader.HasData) uris.Add(reader.ReadCharacterString(UniversalTagNumber.IA5String, new Asn1Tag(TagClass.ContextSpecific, 6)));
        Assert.Equal(["URN:tizen:packageid=", "URN:tizen:deviceid=ABCDEF0123456789"], uris);
    }

    [Fact]
    public void SamsungIntermediatesAreBundled()
    {
        var subjects = SamsungCa.All().Select(c => c.Subject).ToList();
        Assert.Contains(subjects, s => s.Contains("Samsung VD Author CA"));
        Assert.Contains(subjects, s => s.Contains("VD DEVELOPER Public CA Class"));
    }

    [Fact]
    public void DuidsAreRecognised()
    {
        Assert.True(SamsungTvInstaller.IsDuid("CJCHTFRXVXQDQ"));
        Assert.False(SamsungTvInstaller.IsDuid(""));
        Assert.False(SamsungTvInstaller.IsDuid("error: closed"));
    }

    [Fact]
    public void FailedInstallIsReadFromTheOutput()
    {
        Assert.True(SamsungTvInstaller.InstallFailed("app_id[GameTranslatorT.GameTranslator] install failed[118, -12]"));
        Assert.False(SamsungTvInstaller.InstallFailed("Push complete.\napp_id[GameTranslatorT.GameTranslator] installing[100]\nspend time for pkgcmd is [1234]ms"));
    }

    /// <summary>يوقّع الحزمة بشهادتي مؤلف وموزّع وهميتين وقّعناهما بأنفسنا.</summary>
    [Fact]
    public async Task PackageIsSignedWithBothCertificates()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        using var key = RSA.Create(2048);
        const string password = "password";
        var author = Path.Combine(dir, "author.p12");
        var distributor = Path.Combine(dir, "distributor.p12");
        await File.WriteAllBytesAsync(author, SamsungCertificates.Pkcs12(SelfSigned(key, "CN=Author"), key, password));
        await File.WriteAllBytesAsync(distributor, SamsungCertificates.Pkcs12(SelfSigned(key, "CN=Distributor"), key, password));

        var wgt = Path.Combine(dir, "app.wgt");
        using (var zip = ZipFile.Open(wgt, ZipArchiveMode.Create))
        {
            foreach (var (name, text) in new[] { ("config.xml", "<widget/>"), ("index.html", "<p>مرحبا</p>") })
            {
                await using var entry = zip.CreateEntry(name).Open();
                await entry.WriteAsync(Encoding.UTF8.GetBytes(text));
            }
        }

        await TizenWgtSigner.ReSignWgtWithCertsInPlace(wgt, author, distributor, password);

        using var signed = ZipFile.OpenRead(wgt);
        var names = signed.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("author-signature.xml", names);
        Assert.Contains("signature1.xml", names);
        Assert.Contains("index.html", names);
    }

    [Fact]
    public void ProfileWithoutFilesIsNotUsable()
    {
        var profile = SigningProfile.In(Directory.CreateTempSubdirectory().FullName);
        Assert.False(profile.Exists);
        Assert.False(profile.IsValid(DateTime.UtcNow));
    }

    private static X509Certificate2 SelfSigned(RSA key, string subject)
    {
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var withKey = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        // بلا مفتاح، مثل الشهادة القادمة من Samsung.
        return X509CertificateLoader.LoadCertificate(withKey.RawData);
    }
}
