// SPDX-License-Identifier: AGPL-3.0-only

using System.Net.Sockets;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace GameTranslator.Core.LgTv;

/// <summary>TV'de komut çalıştırır; <paramref name="inputFile"/> verilirse dosya komutun girişine akar.</summary>
public interface IRemoteShell
{
    Task<CommandResult> RunAsync(string command, string? inputFile = null);
}

/// <summary>
/// LG geliştirici modunun SSH sunucusuna (9922, kullanıcı prisoner) uygulamanın içinden bağlanır.
/// Windows'un ssh.exe'si, pencere açmadan başlatılınca komut çıktısını vermeyebiliyordu: TV'deki sürüm boş okunuyor,
/// kurulum bitmemiş sanılıyordu. SSH.NET ile dış araç ve konsol gerekmez. TV'nin anahtarı her fabrika ayarında
/// değişir; anahtar denetlenmez (ssh'deki StrictHostKeyChecking=no gibi).
/// </summary>
public sealed class SshNetShell(string host, int port, string keyFile, string user = "prisoner") : IRemoteShell
{
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(6);
    public TimeSpan CommandTimeout { get; init; } = TimeSpan.FromMinutes(2);

    public async Task<CommandResult> RunAsync(string command, string? inputFile = null)
    {
        PrivateKeyFile key;
        try
        {
            key = new PrivateKeyFile(keyFile);
        }
        catch (Exception error) when (error is SshException or InvalidOperationException or ArgumentException or IOException)
        {
            throw new DevModeException(DevModeError.KeyRefused, $"anahtar okunamadı: {error.Message}");
        }

        using (key)
        using (var client = new SshClient(new ConnectionInfo(host, port, user, new PrivateKeyAuthenticationMethod(user, key))
               { Timeout = ConnectTimeout }))
        {
            try
            {
                using var connecting = new CancellationTokenSource(ConnectTimeout + TimeSpan.FromSeconds(2));
                await client.ConnectAsync(connecting.Token);
            }
            catch (SshAuthenticationException error)
            {
                throw new DevModeException(DevModeError.KeyRefused, error.Message);
            }
            catch (Exception error) when (error is SocketException or SshException or OperationCanceledException or TimeoutException)
            {
                throw new DevModeException(DevModeError.Unreachable, error.Message);
            }

            try
            {
                using var ssh = client.CreateCommand(command);
                ssh.CommandTimeout = CommandTimeout;
                var running = ssh.ExecuteAsync();
                if (inputFile is not null)
                {
                    // Giriş kapatılınca komut dosyanın bittiğini anlar ("cat > dosya").
                    await using var input = ssh.CreateInputStream();
                    await using var file = File.OpenRead(inputFile);
                    await file.CopyToAsync(input);
                }
                await running;
                return new CommandResult(ssh.ExitStatus ?? -1, ssh.Result + ssh.Error);
            }
            catch (Exception error) when (error is SocketException or SshException)
            {
                throw new DevModeException(DevModeError.Unreachable, error.Message);
            }
            finally
            {
                client.Disconnect();
            }
        }
    }
}
