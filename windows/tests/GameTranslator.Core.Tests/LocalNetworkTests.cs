// SPDX-License-Identifier: AGPL-3.0-only

using System.Net;
using GameTranslator.Core.LgTv;
using Xunit;

namespace GameTranslator.Core.Tests;

public class LocalNetworkTests
{
    [Fact]
    public void ScanCoversTheWholeHomeNetworkExceptThisComputer()
    {
        var targets = LocalNetwork.ScanTargets([IPAddress.Parse("192.168.1.40")]).ToList();
        Assert.Equal(253, targets.Count);
        Assert.Contains("192.168.1.1", targets);
        Assert.Contains("192.168.1.254", targets);
        Assert.DoesNotContain("192.168.1.40", targets);
    }

    [Fact]
    public void TwoAdaptersOnTheSameNetworkAreScannedOnce()
    {
        var targets = LocalNetwork.ScanTargets([IPAddress.Parse("10.0.0.5"), IPAddress.Parse("10.0.0.6")]).ToList();
        Assert.Equal(targets.Count, targets.Distinct().Count());
    }

    [Fact]
    public void OnlyHomeNetworkAddressesArePrivate()
    {
        Assert.True(LocalNetwork.IsPrivate(IPAddress.Parse("192.168.0.10")));
        Assert.True(LocalNetwork.IsPrivate(IPAddress.Parse("172.20.1.1")));
        Assert.True(LocalNetwork.IsPrivate(IPAddress.Parse("10.1.2.3")));
        Assert.False(LocalNetwork.IsPrivate(IPAddress.Parse("8.8.8.8")));
        Assert.False(LocalNetwork.IsPrivate(IPAddress.Parse("172.32.0.1")));
    }
}
