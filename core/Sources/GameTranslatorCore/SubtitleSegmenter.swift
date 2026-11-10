// SPDX-License-Identifier: AGPL-3.0-only

import Foundation

/// Altyazıyı çevirmene cümle cümle verir (Windows'taki GameTranslator.Translation/SubtitleSegmenter.cs ile aynı kurallar).
/// Alt alta sarılmış satırlar tek cümle olarak birleştirilir. Konuşmacı etiketi ("ATREUS:", "Kratos:") ve diyalog
/// tireleri çevrilmez: çeviri motorları adı çevirmeye ya da silmeye çalışıyor.
public enum SubtitleSegmenter {
    /// Çevrilecek bir parça ya da olduğu gibi kalacak ayraç/etiket.
    public struct Piece: Equatable, Sendable {
        public var text: String
        public var translate: Bool
        public init(_ text: String, translate: Bool) {
            self.text = text
            self.translate = translate
        }
    }

    // Etiket: satır ya da cümle başında, en çok üç sözcüklü ad ve iki nokta.
    // Ya tümü büyük harf ("ATREUS:", "OLD MAN:") ya da her sözcüğü büyük harfle başlayan ("Kratos:", "Mimir Head:").
    private static let label = try! NSRegularExpression(pattern:
        #"^(\p{Lu}[\p{Lu}\d'’.\-]+(?: \p{Lu}[\p{Lu}\d'’.\-]*){0,2}|\p{Lu}\p{Ll}[\p{L}'’.\-]*(?: \p{Lu}[\p{L}'’.\-]*){0,2}):\s+"#)
    private static let dash = try! NSRegularExpression(pattern: #"^[-–—]+\s*"#)

    /// Büyük harfle başlayıp iki noktayla biten ama ad olmayan oyun başlıkları; bunlar çevrilir.
    private static let notNames: Set<String> = [
        "note", "warning", "tip", "hint", "objective", "objectives", "quest", "goal", "new", "reward", "rewards",
        "location", "chapter", "mission", "task", "tutorial", "update", "info", "status", "level", "controls",
        "press", "hold", "tap", "remember", "example", "question", "answer", "result", "results", "time",
        "listen", "look", "wait", "okay", "ok", "well", "so", "yes", "no", "now", "then", "and", "but",
    ]

    /// Noktadan sonra cümle bitmeyen kısaltmalar ("Mr. Smith").
    private static let abbreviations: Set<String> = [
        "mr", "mrs", "ms", "dr", "st", "jr", "sr", "prof", "mt", "vs", "etc", "e.g", "i.e", "lt", "sgt", "capt", "gen", "col",
    ]

    public static func split(_ text: String) -> [Piece] {
        var pieces: [Piece] = []
        var block = ""
        var separator = ""

        func flush() {
            guard !block.isEmpty else { return }
            if !separator.isEmpty { pieces.append(Piece(separator, translate: false)) }
            addBlock(block, to: &pieces)
            block = ""
            separator = "\n"
        }

        for raw in text.components(separatedBy: .newlines) {
            let line = raw.trimmingCharacters(in: .whitespaces)
            guard !line.isEmpty else { continue }
            // Etiketli ya da tireli satır yeni bir konuşmadır; diğer satırlar sarmadır, birleştirilir.
            if speakerLabel(in: line) != nil || match(dash, in: line) != nil { flush() }
            if !block.isEmpty { block += " " }
            block += line
        }
        flush()
        return pieces
    }

    /// Satır başındaki konuşmacı etiketi, boşluğuyla birlikte ("ATREUS: ").
    public static func speakerLabel(in line: String) -> String? {
        guard let found = match(label, in: line) else { return nil }
        let name = found.trimmingCharacters(in: .whitespaces).dropLast()
        // Tek sözcüklü başlıklar ("Note:", "Objective:") ad değildir.
        if !name.contains(" "), notNames.contains(name.lowercased()) { return nil }
        return found
    }

    /// Etiketteki ad ("ATREUS: " → "ATREUS").
    public static func name(fromLabel label: String) -> String {
        String(label.trimmingCharacters(in: .whitespaces).dropLast())
    }

    private static func addBlock(_ raw: String, to pieces: inout [Piece]) {
        var block = raw.split(whereSeparator: \.isWhitespace).joined(separator: " ")
        if let dashText = match(dash, in: block) {
            pieces.append(Piece(dashText.trimmingCharacters(in: .whitespaces) + " ", translate: false))
            block = String(block.dropFirst(dashText.count))
        }
        for (index, sentence) in sentences(of: block).enumerated() {
            if index > 0 { pieces.append(Piece(" ", translate: false)) }
            var body = sentence
            if let labelText = speakerLabel(in: body) {
                pieces.append(Piece(name(fromLabel: labelText) + ": ", translate: false))
                body = String(body.dropFirst(labelText.count))
            }
            if !body.isEmpty { pieces.append(Piece(body, translate: body.contains(where: \.isLetter))) }
        }
    }

    /// Cümle sonu: . ! ? … (yinelenebilir), ardından isteğe bağlı kapanış tırnağı/parantez ve boşluk.
    /// Küçük harfle süren yer ("wait... what") ve kısaltmalar cümle sonu değildir.
    static func sentences(of block: String) -> [String] {
        let characters = Array(block)
        let enders: Set<Character> = [".", "!", "?", "…"]
        let closers: Set<Character> = ["\"", "'", "”", "’", ")", "]"]
        var result: [String] = []
        var start = 0
        var index = 0
        while index < characters.count {
            guard characters[index] == " " else { index += 1; continue }
            var back = index - 1
            while back >= start, closers.contains(characters[back]) { back -= 1 }
            let candidate = String(characters[start..<index])
            let next = index + 1 < characters.count ? characters[index + 1] : nil
            if back >= start, enders.contains(characters[back]), next?.isLowercase != true, !endsWithAbbreviation(candidate) {
                result.append(candidate)
                start = index + 1
            }
            index += 1
        }
        if start < characters.count { result.append(String(characters[start...])) }
        return result
    }

    private static func endsWithAbbreviation(_ sentence: String) -> Bool {
        guard sentence.hasSuffix(".") else { return false }
        let word = String(sentence.split(separator: " ").last ?? "").dropLast()
        // Tek harf ad baş harfidir ("J. R. R.").
        if word.count == 1, word.first!.isUppercase { return true }
        return abbreviations.contains(word.lowercased())
    }

    /// Tümü büyük harfli cümleyi ("THE GATE WILL NOT HOLD.") cümle düzenine çevirir; çeviri motorları büyük harfli
    /// sözcükleri ad sanıp çevirmeden bırakıyor. Bilinen adlar (konuşmacı etiketlerinden öğrenilen) büyük harfle kalır.
    /// Tek sözcüğe dokunulmaz.
    public static func normalizeCase(_ sentence: String, names: Set<String> = []) -> String {
        guard !sentence.contains(where: \.isLowercase),
              sentence.filter(\.isLetter).count >= 2,
              sentence.trimmingCharacters(in: .whitespaces).contains(" ") else { return sentence }

        let lowerNames = Set(names.map { $0.lowercased() })
        var output = ""
        var word = ""
        var startOfSentence = true

        func flushWord() {
            guard !word.isEmpty else { return }
            let lower = word.lowercased()
            if lower == "i" || startOfSentence || lowerNames.contains(lower) {
                output += lower.prefix(1).uppercased() + lower.dropFirst()
            } else {
                output += lower
            }
            startOfSentence = false
            word = ""
        }

        for character in sentence {
            if character.isLetter || character == "'" || character == "’" {
                word.append(character)
            } else {
                flushWord()
                if ".!?…".contains(character) { startOfSentence = true }
                output.append(character)
            }
        }
        flushWord()
        return output
    }

    private static func match(_ expression: NSRegularExpression, in text: String) -> String? {
        let range = NSRange(text.startIndex..., in: text)
        guard let found = expression.firstMatch(in: text, range: range), let swiftRange = Range(found.range, in: text) else { return nil }
        return String(text[swiftRange])
    }
}

/// Konuşmacı etiketlerinden öğrenilen karakter adları. Adlar cümle içinde de büyük harfle kalır ve çevrilmez.
public final class SpeakerNames: @unchecked Sendable {
    private let lock = NSLock()
    private var names: Set<String> = []

    public init() {}

    public var all: Set<String> { lock.withLock { names } }

    /// Parçalardaki etiketlerden adları öğrenir ("OLD MAN: " → "Old", "Man" değil, "Old Man").
    public func learn(from pieces: [SubtitleSegmenter.Piece]) {
        let found = pieces.compactMap { piece -> String? in
            guard !piece.translate, piece.text.hasSuffix(": ") else { return nil }
            let name = SubtitleSegmenter.name(fromLabel: piece.text)
            return name.isEmpty ? nil : name
        }
        guard !found.isEmpty else { return }
        lock.withLock {
            for name in found {
                // Çok sözcüklü adın her sözcüğü ayrıca tanınır: "ATREUS" etiketi cümledeki "ATREUS"u korur.
                for word in name.split(separator: " ") where word.count >= 2 { names.insert(String(word)) }
            }
        }
    }
}
