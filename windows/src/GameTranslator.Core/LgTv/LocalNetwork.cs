// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace GameTranslator.Core.LgTv;

/// <summary>عناوين الكمبيوتر في الشبكة المحلية والعناوين المجاورة التي تُمسح بحثًا عن التلفزيون.</summary>
public static class LocalNetwork
{
    /// <summary>عناوين IPv4 الخاصة (الشبكة المنزلية) لمحوّلات الشبكة العاملة.</summary>
    public static IReadOnlyList<IPAddress> PrivateAddresses()
    {
        try
        {
            return
            [
                .. NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                    .Select(a => a.Address)
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork && IsPrivate(a)),
            ];
        }
        catch (NetworkInformationException)
        {
            return [];
        }
    }

    public static bool IsPrivate(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b.Length == 4 && (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168));
    }

    /// <summary>العناوين الأخرى في شبكة /24 لكل عنوان محلي (عدا العنوان نفسه)، بلا تكرار.</summary>
    public static IEnumerable<string> ScanTargets(IEnumerable<IPAddress> locals)
    {
        var seen = new HashSet<string>();
        foreach (var local in locals)
        {
            var bytes = local.GetAddressBytes();
            for (var last = 1; last < 255; last++)
            {
                if (last == bytes[3]) continue;
                var candidate = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{last}";
                if (seen.Add(candidate)) yield return candidate;
            }
        }
    }

    /// <summary>العناوين التي فيها المنفذ المعطى مفتوح.</summary>
    public static async Task<IReadOnlyList<string>> HostsWithOpenPortAsync(int port, TimeSpan connectTimeout,
        CancellationToken cancellationToken = default)
    {
        var found = new System.Collections.Concurrent.ConcurrentBag<string>();
        await Parallel.ForEachAsync(ScanTargets(PrivateAddresses()),
            new ParallelOptions { MaxDegreeOfParallelism = 64, CancellationToken = cancellationToken },
            async (host, token) =>
            {
                using var client = new TcpClient();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(connectTimeout);
                try
                {
                    await client.ConnectAsync(host, port, timeout.Token);
                    found.Add(host);
                }
                catch (Exception error) when (error is SocketException or OperationCanceledException) { }
            });
        cancellationToken.ThrowIfCancellationRequested();
        return [.. found.OrderBy(h => IPAddress.Parse(h).GetAddressBytes()[3])];
    }
}
