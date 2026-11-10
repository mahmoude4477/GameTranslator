// SPDX-License-Identifier: AGPL-3.0-only

import Foundation

/// Oturumda çevrilen altyazılar, en yenisi başta. Hızlı geçen bir satır sonradan yeniden okunabilsin
/// veya tüm oturum diyalogları topluluk çevirisi için dışa aktarılabilsin diye tutulur.
public struct SubtitleHistory: Equatable, Sendable {
    public struct Entry: Equatable, Identifiable, Sendable, Codable {
        public let id: Int
        public let source: String
        public let translation: String
        public let timestamp: Date
        public let sourceKind: String

        public init(id: Int, source: String, translation: String, timestamp: Date = Date(), sourceKind: String = "machine") {
            self.id = id
            self.source = source
            self.translation = translation
            self.timestamp = timestamp
            self.sourceKind = sourceKind
        }
    }

    public private(set) var entries: [Entry] = []
    public let limit: Int
    private var nextID = 0

    public init(limit: Int = 5000) {
        self.limit = max(1, limit)
    }

    /// Yeni çeviriyi başa ekler. Aynı satırın yeniden okuması (benzer kaynak metin) yeni kayıt açmaz, son kaydı günceller.
    public mutating func add(source: String, translation: String, timestamp: Date = Date(), sourceKind: String = "machine") {
        let translation = translation.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !translation.isEmpty else { return }
        if let last = entries.first,
           TextMatch.isSimilar(TextMatch.normalize(last.source), TextMatch.normalize(source)) {
            entries[0] = Entry(id: last.id, source: source, translation: translation, timestamp: last.timestamp, sourceKind: sourceKind)
            return
        }
        entries.insert(Entry(id: nextID, source: source, translation: translation, timestamp: timestamp, sourceKind: sourceKind), at: 0)
        nextID += 1
        if entries.count > limit { entries.removeLast(entries.count - limit) }
    }

    public mutating func clear() {
        entries.removeAll()
    }

    /// Tüm oturum diyaloglarını kronolojik sırayla (eskiden yeniye) döner.
    public var chronologicalEntries: [Entry] {
        entries.reversed()
    }

    /// JSON formatında dışa aktarır.
    public func exportJSON() -> String {
        let formatter = ISO8601DateFormatter()
        let items: [[String: Any]] = chronologicalEntries.map { entry in
            [
                "id": entry.id,
                "time": formatter.string(from: entry.timestamp),
                "source": entry.source,
                "translation": entry.translation,
                "kind": entry.sourceKind
            ]
        }
        guard let data = try? JSONSerialization.data(withJSONObject: items, options: [.prettyPrinted, .sortedKeys]),
              let jsonString = String(data: data, encoding: .utf8) else {
            return "[]"
        }
        return jsonString
    }

    /// Düz metin (TXT senaryo) formatında dışa aktarır.
    public func exportTXT() -> String {
        let timeFormatter = DateFormatter()
        timeFormatter.dateFormat = "HH:mm:ss"
        var lines: [String] = []
        lines.append("# GameTranslator - Oturum Diyalog Çıktısı")
        lines.append("# Toplam Satır: \(entries.count)")
        lines.append("# Tarih: \(DateFormatter.localizedString(from: Date(), dateStyle: .medium, timeStyle: .short))")
        lines.append(String(repeating: "-", count: 60))
        lines.append("")
        for entry in chronologicalEntries {
            let timeStr = timeFormatter.string(from: entry.timestamp)
            lines.append("[\(timeStr)] [\(entry.sourceKind)]")
            lines.append("EN: \(entry.source)")
            lines.append("TR: \(entry.translation)")
            lines.append("")
        }
        return lines.joined(separator: "\n")
    }

    /// Topluluk el çevirisi paketi şablonu üretir (Örn: GTA 6 çevirisi için hazır JSON).
    public func exportPackTemplate(gameName: String = "Oyun Çevirisi") -> String {
        var terms: [[String: String]] = []
        for entry in chronologicalEntries {
            terms.append([
                "en": entry.source,
                "tr": entry.translation
            ])
        }
        let pack: [String: Any] = [
            "name": "\(gameName) - Türkçe Topluluk Çevirisi",
            "version": "1.0.0",
            "game": gameName,
            "author": "GameTranslator Topluluğu",
            "language": "en-to-tr",
            "description": "GameTranslator oturumunda kaydedilen diyaloglardan üretilmiştir.",
            "terms": terms
        ]
        guard let data = try? JSONSerialization.data(withJSONObject: pack, options: [.prettyPrinted, .sortedKeys]),
              let jsonString = String(data: data, encoding: .utf8) else {
            return "{}"
        }
        return jsonString
    }
}
