// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.Json.Nodes;

namespace GameTranslator.Core.LgTv;

/// <summary>LG webOS'un ikinci ekran arayüzü (SSAP) mesajları.</summary>
public static class SsapMessages
{
    /// <summary>Uygulamanın TV'den istediği izinler: uygulama başlatma ve önplandaki uygulamayı okuma.</summary>
    internal static readonly string[] Permissions = ["LAUNCH", "LAUNCH_WEBAPP", "APP_TO_APP", "READ_RUNNING_APPS", "READ_INSTALLED_APPS"];

    public static string Register(string? clientKey)
    {
        var payload = new JsonObject
        {
            ["forcePairing"] = false,
            ["pairingType"] = "PROMPT",
            ["manifest"] = new JsonObject
            {
                ["manifestVersion"] = 1,
                ["appVersion"] = "1.0",
                ["permissions"] = new JsonArray([.. Permissions.Select(p => (JsonNode)p!)]),
            },
        };
        if (clientKey is not null) payload["client-key"] = clientKey;
        return new JsonObject { ["type"] = "register", ["id"] = "register_0", ["payload"] = payload }.ToJsonString();
    }

    public static string Request(string id, string uri, JsonNode? payload)
    {
        var message = new JsonObject { ["type"] = "request", ["id"] = id, ["uri"] = uri };
        if (payload is not null) message["payload"] = payload;
        return message.ToJsonString();
    }
}
