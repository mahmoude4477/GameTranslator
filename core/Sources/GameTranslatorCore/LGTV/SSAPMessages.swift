// SPDX-License-Identifier: AGPL-3.0-only

import Foundation

/// LG webOS'un ikinci ekran arayüzü (SSAP) mesajları.
enum SSAPMessages {
    /// Uygulamanın TV'den istediği izinler: uygulama başlatma ve önplandaki uygulamayı okuma.
    static let permissions = ["LAUNCH", "LAUNCH_WEBAPP", "APP_TO_APP", "READ_RUNNING_APPS", "READ_INSTALLED_APPS"]

    static func register(clientKey: String?) -> Data {
        var payload: [String: Any] = [
            "forcePairing": false,
            "pairingType": "PROMPT",
            "manifest": [
                "manifestVersion": 1,
                "appVersion": "1.0",
                "permissions": permissions,
            ],
        ]
        if let clientKey { payload["client-key"] = clientKey }
        return encode(["type": "register", "id": "register_0", "payload": payload])
    }

    static func request(id: String, uri: String, payload: [String: Any]?) -> Data {
        var message: [String: Any] = ["type": "request", "id": id, "uri": uri]
        if let payload { message["payload"] = payload }
        return encode(message)
    }

    private static func encode(_ object: [String: Any]) -> Data {
        (try? JSONSerialization.data(withJSONObject: object)) ?? Data()
    }
}
