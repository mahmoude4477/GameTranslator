// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.Json;
using System.Text.Json.Nodes;
using GameTranslator.Core.LgTv;
using Xunit;
using Tv = GameTranslator.Core.LgTv.LgTv;

namespace GameTranslator.Core.Tests;

public class SamsungTvTests
{
    [Fact]
    public void AddressCarriesAppNameAndToken()
    {
        Assert.Equal("wss://192.168.1.20:8002/api/v2/channels/samsung.remote.control?name=R2FtZVRyYW5zbGF0b3I=",
            SamsungMessages.Url("192.168.1.20", null).ToString());
        Assert.EndsWith("&token=123", SamsungMessages.Url("192.168.1.20", "123").ToString());
    }

    [Fact]
    public void LaunchMessageCarriesParametersAsMetaTag()
    {
        var node = JsonNode.Parse(SamsungMessages.Launch("GameTranslatorT.GameTranslator", new Dictionary<string, string> { ["server"] = "http://1.2.3.4:8787" }))!;
        Assert.Equal("ed.apps.launch", node["params"]!["event"]!.GetValue<string>());
        var data = node["params"]!["data"]!;
        Assert.Equal("GameTranslatorT.GameTranslator", data["appId"]!.GetValue<string>());
        Assert.Equal("http://1.2.3.4:8787", JsonNode.Parse(data["metaTag"]!.GetValue<string>())!["server"]!.GetValue<string>());
    }

    [Fact]
    public void ConnectionResults()
    {
        Assert.Equal("55", SamsungMessages.ConnectResult("{\"event\":\"ms.channel.connect\",\"data\":{\"token\":\"55\"}}"));
        Assert.Equal("", SamsungMessages.ConnectResult("{\"event\":\"ms.channel.connect\",\"data\":{}}"));
        Assert.Null(SamsungMessages.ConnectResult("{\"event\":\"ms.channel.ready\"}"));
        Assert.Null(SamsungMessages.ConnectResult("not json"));
        Assert.Throws<SsapException>(() => SamsungMessages.ConnectResult("{\"event\":\"ms.channel.unauthorized\"}"));
    }

    [Fact]
    public void DeviceNameIsRead()
    {
        Assert.Equal("[TV] Samsung QN70F", SamsungMessages.DeviceName("{\"device\":{\"name\":\"[TV] Samsung QN70F\"}}"));
        Assert.Null(SamsungMessages.DeviceName("{}"));
    }

    [Fact]
    public void SavedTvWithoutBrandIsLg() =>
        Assert.Equal(TvBrand.Lg, JsonSerializer.Deserialize<Tv>("{\"Name\":\"LG TV\",\"Host\":\"1.2.3.4\"}")!.Brand);

    [Fact]
    public void LocatorDoesNotMoveToAnotherBrand() =>
        Assert.Null(TvLocator.Moved(new Tv("غرفة المعيشة", "1.1.1.1"), [new Tv("غرفة المعيشة", "1.1.1.2", TvBrand.Samsung)]));
}
