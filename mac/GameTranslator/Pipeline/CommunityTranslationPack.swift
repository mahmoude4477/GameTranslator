// SPDX-License-Identifier: AGPL-3.0-only

import Foundation
import GameTranslatorCore

/// Topluluk veya oyuncular tarafından elle hazırlanan çeviri terimi veya diyalog satırı.
public struct TranslationTerm: Codable, Identifiable, Equatable, Hashable {
    public var id: String { en.lowercased() }
    public var en: String
    public var tr: String
    public var type: String?

    public init(en: String, tr: String, type: String? = "dialogue") {
        self.en = en
        self.tr = tr
        self.type = type
    }
}

/// Özel Topluluk Çeviri Paketi (Örn: GTA 6 Topluluk Çevirisi, Oyun Sözlüğü).
public struct CommunityPack: Codable, Identifiable, Equatable {
    public var id: String { "\(name)_\(version)" }
    public var name: String
    public var version: String
    public var game: String?
    public var author: String?
    public var language: String
    public var description: String?
    public var terms: [TranslationTerm]
    public var isEnabled: Bool = true

    enum CodingKeys: String, CodingKey {
        case name, version, game, author, language, description, terms
    }

    public init(name: String, version: String = "1.0.0", game: String? = nil,
                author: String? = nil, language: String = "en-to-tr",
                description: String? = nil, terms: [TranslationTerm] = [],
                isEnabled: Bool = true) {
        self.name = name
        self.version = version
        self.game = game
        self.author = author
        self.language = language
        self.description = description
        self.terms = terms
        self.isEnabled = isEnabled
    }
}

/// Çeviri paketlerini yöneten, arama yapan ve diskte saklayan yönetici.
@MainActor
public final class CommunityPackManager: ObservableObject {
    public static let shared = CommunityPackManager()

    @Published public private(set) var packs: [CommunityPack] = []
    @Published public var enabledPackIDs: Set<String> = []

    /// Hızlı arama için küçük harfli ve temizlenmiş önbellek: normalizedEn -> (tr, packName)
    private var exactLookup: [String: (tr: String, packName: String)] = [:]

    private var storageDirectory: URL {
        let appSupport = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first!
        let dir = appSupport.appendingPathComponent("GameTranslator/packs", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }

    public init() {
        loadPacks()
    }

    /// Tüm paketleri diskten ve yerleşik dizinden yükler.
    public func loadPacks() {
        var loaded: [CommunityPack] = []

        // 1. Uygulama Destek klasöründeki kullanıcı paketleri
        let files = (try? FileManager.default.contentsOfDirectory(at: storageDirectory, includingPropertiesForKeys: nil)) ?? []
        for file in files where file.pathExtension.lowercased() == "json" || file.pathExtension.lowercased() == "gtpack" {
            if let pack = loadPack(from: file) {
                loaded.append(pack)
            }
        }

        // 2. Proje içindeki yerleşik sözlükler (varsa)
        let builtInFiles = ["dictionaries/gta6_community_pack.json", "dictionaries/gta6_slang.json"]
        for file in builtInFiles {
            let builtInURL = URL(fileURLWithPath: file)
            if FileManager.default.fileExists(atPath: builtInURL.path),
               let builtIn = loadPack(from: builtInURL),
               !loaded.contains(where: { $0.name == builtIn.name }) {
                loaded.append(builtIn)
                savePack(builtIn)
            }
        }

        // Varsayılan boş paket yoksa oluştur (Özel El Çevirileri)
        if loaded.isEmpty {
            let defaultPack = CommunityPack(
                name: "Özel El Çevirilerim",
                version: "1.0.0",
                game: "Tüm Oyunlar",
                author: "Kullanıcı",
                language: "en-to-tr",
                description: "Oyun esnasında eklediğiniz özel çeviri ve düzeltmeler.",
                terms: []
            )
            loaded.append(defaultPack)
            savePack(defaultPack)
        }

        self.packs = loaded
        self.enabledPackIDs = Set(loaded.map(\.id))
        rebuildLookup()
    }

    /// Bir dosyadaki paketi yükler.
    public func loadPack(from url: URL) -> CommunityPack? {
        guard let data = try? Data(contentsOf: url),
              let pack = try? JSONDecoder().decode(CommunityPack.self, from: data) else {
            return nil
        }
        return pack
    }

    /// Paketi diske kaydeder.
    public func savePack(_ pack: CommunityPack) {
        let safeName = pack.name.replacingOccurrences(of: "/", with: "-").replacingOccurrences(of: " ", with: "_")
        let destination = storageDirectory.appendingPathComponent("\(safeName).json")
        if let data = try? JSONEncoder().encode(pack) {
            try? data.write(to: destination, options: .atomic)
        }
    }

    /// Dışarıdan seçilen bir `.json` veya `.gtpack` dosyasını içe aktarır.
    @discardableResult
    public func importPack(from url: URL) throws -> CommunityPack {
        let data = try Data(contentsOf: url)
        let pack = try JSONDecoder().decode(CommunityPack.self, from: data)
        savePack(pack)
        if let idx = packs.firstIndex(where: { $0.id == pack.id }) {
            packs[idx] = pack
        } else {
            packs.append(pack)
        }
        enabledPackIDs.insert(pack.id)
        rebuildLookup()
        return pack
    }

    /// Belirtilen pakete yeni kelime veya cümle ekler/günceller.
    public func addOrUpdateTerm(en: String, tr: String, type: String = "dialogue", packName: String? = nil) {
        let targetPackName = packName ?? packs.first?.name ?? "Özel El Çevirilerim"
        guard let index = packs.firstIndex(where: { $0.name == targetPackName }) else { return }

        var pack = packs[index]
        let cleanEn = en.trimmingCharacters(in: .whitespacesAndNewlines)
        let cleanTr = tr.trimmingCharacters(in: .whitespacesAndNewlines)

        if let termIdx = pack.terms.firstIndex(where: { $0.en.caseInsensitiveCompare(cleanEn) == .orderedSame }) {
            pack.terms[termIdx].tr = cleanTr
            pack.terms[termIdx].type = type
        } else {
            pack.terms.append(TranslationTerm(en: cleanEn, tr: cleanTr, type: type))
        }

        packs[index] = pack
        savePack(pack)
        rebuildLookup()
    }

    /// Paketi siler.
    public func removePack(_ pack: CommunityPack) {
        let safeName = pack.name.replacingOccurrences(of: "/", with: "-").replacingOccurrences(of: " ", with: "_")
        let file = storageDirectory.appendingPathComponent("\(safeName).json")
        try? FileManager.default.removeItem(at: file)
        packs.removeAll { $0.id == pack.id }
        enabledPackIDs.remove(pack.id)
        rebuildLookup()
    }

    /// Paketi açıp kapatır.
    public func togglePack(_ pack: CommunityPack) {
        if enabledPackIDs.contains(pack.id) {
            enabledPackIDs.remove(pack.id)
        } else {
            enabledPackIDs.insert(pack.id)
        }
        rebuildLookup()
    }

    /// Bir cümlenin veya ifadenin el çevirisini arar.
    /// Tam eşleşme veya normalleştirilmiş eşleşme bulursa (çeviri, paketAdı) döner.
    public func lookup(source: String) -> (translation: String, packName: String)? {
        let normalized = normalizeKey(source)
        if let match = exactLookup[normalized] {
            return match
        }

        // Noktalama işaretleri atılmış arama
        let stripped = stripPunctuation(normalized)
        if let match = exactLookup[stripped] {
            return match
        }

        return nil
    }

    /// Hızlı arama indeksini yeniden derler.
    private func rebuildLookup() {
        var table: [String: (tr: String, packName: String)] = [:]
        for pack in packs where enabledPackIDs.contains(pack.id) {
            for term in pack.terms {
                let key = normalizeKey(term.en)
                let stripped = stripPunctuation(key)
                let entry = (tr: term.tr, packName: pack.name)
                table[key] = entry
                if !stripped.isEmpty {
                    table[stripped] = entry
                }
            }
        }
        self.exactLookup = table
    }

    private func normalizeKey(_ text: String) -> String {
        text.trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased()
            .components(separatedBy: .whitespacesAndNewlines)
            .filter { !$0.isEmpty }
            .joined(separator: " ")
    }

    private func stripPunctuation(_ text: String) -> String {
        text.components(separatedBy: CharacterSet.punctuationCharacters).joined()
            .trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
