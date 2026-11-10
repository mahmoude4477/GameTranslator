// SPDX-License-Identifier: AGPL-3.0-only

import Foundation

/// Oyunlarda sık geçen, cihaz üstü çevirinin kelime kelime çevirdiği ifadeler ("I've got your six" →
/// "Altını aldım") çeviriden önce aynı anlamdaki düz İngilizceye çevrilir. Windows'taki `GameEnglish` ile aynı liste.
public enum GameEnglish {
    private static let phrases: [(NSRegularExpression, String)] = [
        ("friendlies", "allies"),
        ("(?:I've|I) got your six", "I've got your back"),
        ("check your six", "watch behind you"),
        ("heads up", "watch out"),
        ("took you long enough", "you're late"),
        ("we've got company", "enemies are coming"),
        ("lock and load", "get ready"),
        ("sitting ducks", "easy targets"),
        ("moving in", "going in"),
    ].map { (try! NSRegularExpression(pattern: "\\b\($0.0)\\b", options: .caseInsensitive), $0.1) }

    /// Bilinen ifadeler düz İngilizceye çevrilir; cümle başındaki büyük harf korunur.
    public static func plain(_ sentence: String) -> String {
        var text = sentence
        for (pattern, plain) in phrases {
            let range = NSRange(text.startIndex..., in: text)
            for match in pattern.matches(in: text, range: range).reversed() {
                guard let found = Range(match.range, in: text) else { continue }
                let upper = text[found].first?.isUppercase == true
                text.replaceSubrange(found, with: upper ? plain.prefix(1).uppercased() + plain.dropFirst() : plain)
            }
        }
        return text
    }
}
