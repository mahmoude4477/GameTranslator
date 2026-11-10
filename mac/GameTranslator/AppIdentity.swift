// SPDX-License-Identifier: AGPL-3.0-only

import Foundation

/// Uygulamanın adı ve kimlikleri tek yerde.
enum AppIdentity {
    static let name = "GameTranslator"
    static let tvAppId = "com.gametranslator.tv"
    static let samsungAppId = "GameTranslatorT.GameTranslator"
    static let tvGuideURL = URL(string: "https://github.com/Sefflex/GameTranslate/blob/main/docs/tv-kurulum.md")!
    static let sourceURL = URL(string: "https://github.com/Sefflex/GameTranslate")!
    static var version: String { Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "0" }
    static let serverPort: UInt16 = 8787
}
