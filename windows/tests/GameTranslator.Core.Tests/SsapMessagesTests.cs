// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.Json.Nodes;
using GameTranslator.Core.LgTv;

namespace GameTranslator.Core.Tests;

public class SsapMessagesTests
{
    [Fact]
    public void RegisterAsksForPromptPairingWithLaunchPermissions()
    {
        var message = JsonNode.Parse(SsapMessages.Register(null))!;
        Assert.Equal("register", (string?)message["type"]);
        var payload = message["payload"]!;
        Assert.Equal("PROMPT", (string?)payload["pairingType"]);
        Assert.Null(payload["client-key"]);
        var permissions = payload["manifest"]!["permissions"]!.AsArray().Select(p => (string?)p).ToList();
        Assert.Contains("LAUNCH", permissions);
        Assert.Contains("READ_RUNNING_APPS", permissions);
    }

    [Fact]
    public void RegisterCarriesStoredClientKey() =>
        Assert.Equal("abc123", (string?)JsonNode.Parse(SsapMessages.Register("abc123"))!["payload"]!["client-key"]);

    [Fact]
    public void LaunchRequestCarriesAppIdAndParams()
    {
        var payload = new JsonObject { ["id"] = "com.gametranslator.tv", ["params"] = new JsonObject { ["server"] = "http://1.2.3.4:8787" } };
        var message = JsonNode.Parse(SsapMessages.Request("7", "ssap://system.launcher/launch", payload))!;
        Assert.Equal("request", (string?)message["type"]);
        Assert.Equal("7", (string?)message["id"]);
        Assert.Equal("ssap://system.launcher/launch", (string?)message["uri"]);
        Assert.Equal("com.gametranslator.tv", (string?)message["payload"]!["id"]);
        Assert.Equal("http://1.2.3.4:8787", (string?)message["payload"]!["params"]!["server"]);
    }

    [Fact]
    public void SsdpResponseYieldsHost()
    {
        const string response = "HTTP/1.1 200 OK\r\nCACHE-CONTROL: max-age=1800\r\nLOCATION: http://192.168.1.153:1990/abc/description.xml\r\nST: urn:lge-com:service:webos-second-screen:1\r\nSERVER: WebOS/4.4.0 UPnP/1.0\r\n\r\n";
        Assert.Equal("192.168.1.153", LgDiscovery.HostFromSsdpResponse(response));
        Assert.Null(LgDiscovery.HostFromSsdpResponse("HTTP/1.1 200 OK\r\nST: urn:other\r\n\r\n"));
    }
}
