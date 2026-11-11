// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GameTranslator.Core.LgTv;

public enum TvBrand
{
    Lg,
    Samsung,
    /// <summary>Android TV: الكمبيوتر لا يتحكم في التلفزيون، وتطبيق التلفزيون يجد الكمبيوتر بنفسه.</summary>
    AndroidTv,
}

/// <param name="Brand">السجلات القديمة بلا علامة تجارية؛ وكلها LG.</param>
public sealed record LgTv(string Name, string Host, TvBrand Brand = TvBrand.Lg);

/// <summary>يعثر على أجهزة تلفزيون LG webOS في الشبكة المحلية عبر SSDP.</summary>
public static class LgDiscovery
{
    internal const string SearchTarget = "urn:lge-com:service:webos-second-screen:1";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(2) };

    public static async Task<IReadOnlyList<LgTv>> SearchAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var hosts = await CollectResponses(SearchTarget, timeout ?? TimeSpan.FromSeconds(3), cancellationToken);
        var tvs = new List<LgTv>();
        foreach (var host in hosts.Distinct().Order(StringComparer.Ordinal))
            tvs.Add(new LgTv(await FriendlyName(host) ?? "LG TV", host));
        return tvs;
    }

    /// <summary>يستخرج عنوان التلفزيون من رد SSDP؛ وnull إن لم يكن الرد من خدمة الشاشة الثانية في LG.</summary>
    public static string? HostFromSsdpResponse(string response, string searchTarget = SearchTarget)
    {
        string? location = null;
        var isLg = false;
        foreach (var line in response.Split("\r\n"))
        {
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var name = line[..colon].Trim().ToUpperInvariant();
            var value = line[(colon + 1)..].Trim();
            if (name == "LOCATION") location = value;
            else if (name is "ST" or "NT") isLg = isLg || value == searchTarget;
        }
        return isLg && Uri.TryCreate(location, UriKind.Absolute, out var url) ? url.Host : null;
    }

    internal static async Task<List<string>> CollectResponses(string searchTarget, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var hosts = new List<string>();
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        var request = Encoding.ASCII.GetBytes(
            $"M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: {searchTarget}\r\n\r\n");
        var target = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
        // يُرسل الطلب من كل محوّل شبكة على حدة: قد يكون المحوّل الافتراضي VPN أو شبكة افتراضية (Hyper-V, WSL)
        // والتلفزيون ليس في تلك الشبكة. تصل الردود إلى منفذ هذا المقبس واحدًا واحدًا.
        var interfaces = LocalNetwork.PrivateAddresses();
        for (var i = 0; i < 2; i++)
        {
            if (interfaces.Count == 0) await udp.SendAsync(request, target, cancellationToken);
            foreach (var local in interfaces)
            {
                try
                {
                    udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes());
                    await udp.SendAsync(request, target, cancellationToken);
                }
                catch (SocketException) { /* تعذّر الإرسال من هذا المحوّل؛ تُجرَّب البقية */ }
            }
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            while (true)
            {
                var result = await udp.ReceiveAsync(deadline.Token);
                if (HostFromSsdpResponse(Encoding.UTF8.GetString(result.Buffer), searchTarget) is { } host) hosts.Add(host);
            }
        }
        catch (OperationCanceledException) { }
        return hosts;
    }

    /// <summary>يقرأ اسم التلفزيون في الشبكة (مثل "[LG] webOS TV UK7550") من وصف الجهاز.</summary>
    private static async Task<string?> FriendlyName(string host)
    {
        // وصف جهاز webOS يُنشر على المنفذ 1990؛ وإذا تعذّرت قراءته يُستخدم اسم عام.
        try
        {
            var xml = await Http.GetStringAsync($"http://{host}:1990/");
            var start = xml.IndexOf("<friendlyName>", StringComparison.Ordinal);
            var end = start < 0 ? -1 : xml.IndexOf("</friendlyName>", start, StringComparison.Ordinal);
            return end < 0 ? null : xml[(start + "<friendlyName>".Length)..end];
        }
        catch
        {
            return null;
        }
    }
}
