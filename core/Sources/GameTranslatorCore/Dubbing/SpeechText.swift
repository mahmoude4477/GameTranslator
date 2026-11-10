// SPDX-License-Identifier: AGPL-3.0-only

import Foundation

/// Pocket TTS Turkish'in duygu etiketleri; etiketsiz cümle düz okunur.
public enum SpeechEmotion: String, CaseIterable, Sendable {
    case happy = "mutlu"
    case sad = "üzgün"
    case angry = "kızgın"
    case surprised = "şaşkın"
    case calm = "sakin"

    /// Cümlenin İngilizcesine bakarak duygu tahmini. Emin olunmayan cümle düz okunur: yanlış duygu düz okumadan kötü.
    public static func guess(english: String) -> SpeechEmotion? {
        let text = english.lowercased()
        let words = Set(text.split(whereSeparator: { !$0.isLetter && $0 != "'" && $0 != "’" }).map(String.init))
        func has(_ phrases: [String]) -> Bool {
            phrases.contains { phrase in phrase.contains(" ") ? text.contains(phrase) : words.contains(phrase) }
        }
        let exclaims = text.contains("!")

        if text.contains("?!") || text.contains("!?") { return .surprised }
        if has(["i'm sorry", "i am sorry", "forgive me", "i miss", "she's gone", "he's gone", "they're gone", "is dead",
                "are dead", "goodbye", "farewell", "it's my fault", "i failed", "i couldn't save", "rest in peace"]) {
            return .sad
        }
        if exclaims {
            if has(["what", "whoa", "wow", "huh", "impossible", "no way", "how", "wait", "look out", "what's that"]) {
                return .surprised
            }
            if has(["we did it", "yes", "finally", "haha", "ha", "hooray", "great", "amazing", "perfect", "thank you",
                    "thanks", "welcome back", "well done", "nice", "awesome"]) {
                return .happy
            }
            if has(["stop", "enough", "get back", "get out", "back off", "shut up", "fool", "idiot", "damn", "die",
                    "kill", "never", "how dare", "coward", "leave", "move", "now", "fight", "run"]) {
                return .angry
            }
            return nil
        }
        if has(["easy", "calm down", "breathe", "take your time", "it's okay", "it's alright", "don't worry",
                "rest", "slowly", "quiet", "shh", "listen to me"]) {
            return .calm
        }
        return nil
    }
}

/// Türkçe cümleyi Pocket TTS Turkish'e hazırlar: model sayıları yazıyla, cümleyi küçük harfle başlayarak görmüş;
/// duygu etiketi en başa gelir.
public enum SpeechText {
    public static func prepare(_ turkish: String, emotion: SpeechEmotion? = nil) -> String {
        var text = turkish
            .replacingOccurrences(of: "…", with: "...")
            .replacingOccurrences(of: "“", with: "\"")
            .replacingOccurrences(of: "”", with: "\"")
            .replacingOccurrences(of: "’", with: "'")
        text = TurkishNumbers.spellOut(in: text)
        text = text.split(whereSeparator: \.isWhitespace).joined(separator: " ")
        // "?!" ve "!!!" gibi yığılmış işaretler modeli erken susturabiliyor; tek işarete iner.
        text = text.replacingOccurrences(of: #"[?!]*\?[?!]*"#, with: "?", options: .regularExpression)
            .replacingOccurrences(of: #"!{2,}"#, with: "!", options: .regularExpression)
        text = lowercasingSentenceStarts(text)
        guard let emotion else { return text }
        return "[\(emotion.rawValue)] " + text
    }

    /// Her cümlenin ilk harfini Türkçe kurala göre küçültür (I → ı, İ → i); model böyle eğitilmiş.
    static func lowercasingSentenceStarts(_ text: String) -> String {
        var output = ""
        var atStart = true
        for character in text {
            if atStart, character.isLetter {
                let letter = String(character)
                output += letter == "I" ? "ı" : letter == "İ" ? "i" : letter.lowercased(with: Locale(identifier: "tr_TR"))
                atStart = false
            } else {
                output.append(character)
                if ".!?".contains(character) { atStart = true }
            }
        }
        return output
    }
}

/// Sayıları Türkçe yazıya çevirir: "1250" → "bin iki yüz elli", "3,5" → "üç virgül beş", "%20" → "yüzde yirmi",
/// "14:30" → "on dört otuz".
public enum TurkishNumbers {
    private static let ones = ["", "bir", "iki", "üç", "dört", "beş", "altı", "yedi", "sekiz", "dokuz"]
    private static let tens = ["", "on", "yirmi", "otuz", "kırk", "elli", "altmış", "yetmiş", "seksen", "doksan"]
    private static let scales = ["", "bin", "milyon", "milyar", "trilyon"]

    public static func words(_ number: Int) -> String {
        if number == 0 { return "sıfır" }
        if number < 0 { return "eksi " + words(-number) }
        var groups: [Int] = []
        var rest = number
        while rest > 0 { groups.append(rest % 1000); rest /= 1000 }
        var parts: [String] = []
        for (scale, group) in groups.enumerated().reversed() where group > 0 {
            guard scale < scales.count else { return String(number) }
            // "bir bin" denmez: 1000 → "bin".
            if scale == 1 && group == 1 { parts.append("bin"); continue }
            parts.append(hundreds(group))
            if scale > 0 { parts.append(scales[scale]) }
        }
        return parts.joined(separator: " ")
    }

    private static func hundreds(_ number: Int) -> String {
        var parts: [String] = []
        let hundred = number / 100, ten = (number % 100) / 10, one = number % 10
        if hundred > 0 { parts.append(hundred == 1 ? "yüz" : ones[hundred] + " yüz") }
        if ten > 0 { parts.append(tens[ten]) }
        if one > 0 { parts.append(ones[one]) }
        return parts.joined(separator: " ")
    }

    private static let pattern = try! NSRegularExpression(pattern: #"(%)?(\d{1,3}(?:\.\d{3})+|\d+)(?:([,:])(\d+))?(%)?"#)

    public static func spellOut(in text: String) -> String {
        let source = text as NSString
        var output = ""
        var last = 0
        for match in pattern.matches(in: text, range: NSRange(location: 0, length: source.length)) {
            output += source.substring(with: NSRange(location: last, length: match.range.location - last))
            last = match.range.location + match.range.length
            func group(_ index: Int) -> String? {
                let range = match.range(at: index)
                return range.location == NSNotFound ? nil : source.substring(with: range)
            }
            let whole = group(2)!.replacingOccurrences(of: ".", with: "")
            guard let value = Int(whole) else { output += source.substring(with: match.range); continue }
            var spoken = words(value)
            if let separator = group(3), let fraction = group(4) {
                if separator == ":" {
                    // Saat: "14:30" → "on dört otuz", "09:05" → "dokuz sıfır beş".
                    let minutes = Int(fraction) ?? 0
                    spoken += " " + (fraction.hasPrefix("0") && minutes > 0 ? "sıfır " + words(minutes) : minutes == 0 ? "" : words(minutes))
                    spoken = spoken.trimmingCharacters(in: .whitespaces)
                } else {
                    // Ondalık: rakam rakam değil, sayı olarak ("3,25" → "üç virgül yirmi beş").
                    let leadingZeros = fraction.prefix(while: { $0 == "0" }).count
                    let rest = Int(fraction) ?? 0
                    spoken += " virgül " + Array(repeating: "sıfır", count: leadingZeros).joined(separator: " ")
                    if rest > 0 { spoken += (leadingZeros > 0 ? " " : "") + words(rest) }
                }
            }
            if group(1) != nil || group(5) != nil { spoken = "yüzde " + spoken }
            output += spoken
        }
        output += source.substring(from: last)
        return output
    }
}
