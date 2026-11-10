// SPDX-License-Identifier: AGPL-3.0-only

using System.Security.Cryptography;
using GameTranslator.Core.LgTv;
using Xunit;

namespace GameTranslator.Core.Tests;

/// <summary>
/// Gerçek SSH sunucusuyla denemeler için GAMETRANSLATOR_TEST_SSH="host:port:kullanıcı:anahtar-dosyası" verilir; sunucu LG
/// geliştirici modu gibi yalnız ssh-rsa kabul edecek biçimde kurulabilir. Verilmezse yalnız bağlantısız denemeler çalışır.
/// </summary>
public class SshNetShellTests
{
    private static (string Host, int Port, string User, string Key)? Server =>
        Environment.GetEnvironmentVariable("GAMETRANSLATOR_TEST_SSH")?.Split(':', 4) is [var host, var port, var user, var key]
            ? (host, int.Parse(port), user, key)
            : null;

    private static string RandomKeyFile()
    {
        using var rsa = RSA.Create(2048);
        var file = Path.GetTempFileName();
        File.WriteAllText(file, rsa.ExportRSAPrivateKeyPem());
        return file;
    }

    [Fact]
    public async Task ClosedPortIsUnreachable()
    {
        var key = RandomKeyFile();
        try
        {
            var shell = new SshNetShell("127.0.0.1", 1, key) { ConnectTimeout = TimeSpan.FromSeconds(2) };
            var error = await Assert.ThrowsAsync<DevModeException>(() => shell.RunAsync("true"));
            Assert.Equal(DevModeError.Unreachable, error.Error);
        }
        finally
        {
            File.Delete(key);
        }
    }

    [Fact]
    public async Task UnreadableKeyIsRefused()
    {
        var key = Path.GetTempFileName();
        await File.WriteAllTextAsync(key, "anahtar değil");
        try
        {
            var error = await Assert.ThrowsAsync<DevModeException>(() => new SshNetShell("127.0.0.1", 1, key).RunAsync("true"));
            Assert.Equal(DevModeError.KeyRefused, error.Error);
        }
        finally
        {
            File.Delete(key);
        }
    }

    [Fact]
    public async Task CommandOutputAndExitStatusAreRead()
    {
        if (Server is not var (host, port, user, key)) return;
        var result = await new SshNetShell(host, port, key, user).RunAsync("echo merhaba; echo uyarı >&2; exit 3");
        Assert.Equal(3, result.Status);
        Assert.Equal("merhaba\nuyarı\n", result.Output);
    }

    [Fact]
    public async Task InputFileReachesTheCommandWhole()
    {
        if (Server is not var (host, port, user, key)) return;
        var package = Path.GetTempFileName();
        await File.WriteAllBytesAsync(package, RandomNumberGenerator.GetBytes(300_000));
        var remote = $"/tmp/gametranslator-test-{Guid.NewGuid():N}";
        try
        {
            var shell = new SshNetShell(host, port, key, user);
            var upload = await shell.RunAsync($"cat > {remote}", package);
            Assert.Equal(0, upload.Status);
            var size = await shell.RunAsync($"wc -c < {remote}; rm -f {remote}");
            Assert.Equal("300000", size.Output.Trim());
        }
        finally
        {
            File.Delete(package);
        }
    }

    [Fact]
    public async Task UnknownKeyIsRefused()
    {
        if (Server is not var (host, port, user, _)) return;
        var key = RandomKeyFile();
        try
        {
            var error = await Assert.ThrowsAsync<DevModeException>(() => new SshNetShell(host, port, key, user).RunAsync("true"));
            Assert.Equal(DevModeError.KeyRefused, error.Error);
        }
        finally
        {
            File.Delete(key);
        }
    }
}
