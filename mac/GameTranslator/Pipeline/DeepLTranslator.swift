// SPDX-License-Identifier: AGPL-3.0-only

import GameTranslatorCore
import Foundation

/// Çevrimiçi çeviri anahtarı (DeepL) denendiğinde ne çıktı.
enum OnlineKeyState: Equatable {
    case valid, invalid, quotaExceeded, unreachable
}

struct OnlineKeyCheck: Equatable {
    var state: OnlineKeyState
    /// Bu ay kullanılan karakter ve aylık sınır (geçerli anahtarda).
    var used: Int64 = 0
    var limit: Int64 = 0
}

/// İsteğe bağlı çevrimiçi çeviri: kullanıcının kendi DeepL anahtarıyla (Windows'taki `DeepLTranslator` ile aynı).
/// Altyazı cümle cümle, konuşmacı adları çevrilmeden gönderilir; önceki altyazı bağlam olarak eklenir. DeepL'e
/// ulaşılamazsa ya da kota biterse o satır Apple'ın cihaz üstü çevirisiyle çevrilir; oyun altyazısız kalmaz.
final class DeepLTranslator: TextTranslator {
    /// Hata sonrası bu süre DeepL denenmez; her satırda bağlantı zaman aşımını beklemeyelim.
    private static let retryAfter: TimeInterval = 60

    private let key: String
    private let fallback: TextTranslator
    private let session: URLSession
    private var cache: [String: String] = [:]
    private var speech: [String: [SpokenLine]] = [:]
    private let names = SpeakerNames()
    private var previous: String?
    private var pausedUntil = Date.distantPast

    init(key: String, fallback: TextTranslator, session: URLSession = DeepLTranslator.makeSession()) {
        self.key = key.trimmingCharacters(in: .whitespacesAndNewlines)
        self.fallback = fallback
        self.session = session
    }

    static func makeSession() -> URLSession {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 4
        return URLSession(configuration: configuration)
    }

    /// Ücretsiz anahtarlar ":fx" ile biter ve ayrı adrese gider.
    static func baseURL(for key: String) -> URL {
        URL(string: key.trimmingCharacters(in: .whitespacesAndNewlines).hasSuffix(":fx")
            ? "https://api-free.deepl.com/v2/" : "https://api.deepl.com/v2/")!
    }

    func translate(_ text: String) async throws -> String {
        if let cached = cache[text] { return cached }
        guard Date() >= pausedUntil else { return try await fallback.translate(text) }
        let pieces = SubtitleSegmenter.split(text)
        names.learn(from: pieces)
        let sources = pieces.filter(\.translate).map { SubtitleSegmenter.normalizeCase($0.text, names: names.all) }
        let translated: [String]
        do {
            translated = sources.isEmpty ? [] : try await request(sources)
        } catch is CancellationError {
            throw CancellationError()
        } catch {
            pausedUntil = Date().addingTimeInterval(Self.retryAfter)
            return try await fallback.translate(text)
        }
        var result = ""
        var translations: [String?] = []
        var next = 0
        for piece in pieces {
            let sentence: String? = piece.translate ? translated[next] : nil
            if piece.translate { next += 1 }
            translations.append(sentence)
            result += sentence ?? piece.text
        }
        if speech.count >= 512 { speech.removeAll() }
        speech[text] = SpeechScript.lines(pieces: pieces, translations: translations)
        if cache.count >= 4096 { cache.removeAll() }
        cache[text] = result
        previous = text
        return result
    }

    func spokenLines(for text: String) -> [SpokenLine] {
        if let lines = speech[text] { return lines }
        if let translation = cache[text] { return [SpokenLine(english: text, turkish: translation)] }
        return fallback.spokenLines(for: text)
    }

    private func request(_ sentences: [String]) async throws -> [String] {
        var body: [String: Any] = [
            "text": sentences, "source_lang": "EN", "target_lang": "TR",
            // Oyun konuşması: "siz" değil "sen" (DeepL Türkçede desteklemiyorsa yok sayar).
            "formality": "prefer_less", "preserve_formatting": true,
        ]
        if let previous { body["context"] = previous }
        var request = URLRequest(url: Self.baseURL(for: key).appending(path: "translate"))
        request.httpMethod = "POST"
        request.setValue("DeepL-Auth-Key \(key)", forHTTPHeaderField: "Authorization")
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        request.httpBody = try JSONSerialization.data(withJSONObject: body)
        let (data, response) = try await session.data(for: request)
        try Self.check(response)
        struct Reply: Decodable { struct Item: Decodable { let text: String }; let translations: [Item] }
        let reply = try JSONDecoder().decode(Reply.self, from: data)
        guard reply.translations.count == sentences.count else { throw URLError(.cannotParseResponse) }
        return reply.translations.map(\.text)
    }

    /// Ayarlardaki "Kaydet ve dene": anahtar geçerli mi, bu ay ne kadar kullanıldı.
    static func check(key: String, session: URLSession = makeSession()) async -> OnlineKeyCheck {
        let key = key.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !key.isEmpty else { return OnlineKeyCheck(state: .invalid) }
        var request = URLRequest(url: baseURL(for: key).appending(path: "usage"))
        request.setValue("DeepL-Auth-Key \(key)", forHTTPHeaderField: "Authorization")
        do {
            let (data, response) = try await session.data(for: request)
            try check(response)
            struct Usage: Decodable { let character_count: Int64; let character_limit: Int64 }
            let usage = try JSONDecoder().decode(Usage.self, from: data)
            let used = usage.character_limit > 0 && usage.character_count >= usage.character_limit
            return OnlineKeyCheck(state: used ? .quotaExceeded : .valid, used: usage.character_count, limit: usage.character_limit)
        } catch let error as DeepLError {
            return OnlineKeyCheck(state: error.state)
        } catch {
            return OnlineKeyCheck(state: .unreachable)
        }
    }

    private struct DeepLError: Error { let state: OnlineKeyState }

    private static func check(_ response: URLResponse) throws {
        guard let http = response as? HTTPURLResponse, !(200..<300).contains(http.statusCode) else { return }
        switch http.statusCode {
        case 401, 403: throw DeepLError(state: .invalid)
        case 456: throw DeepLError(state: .quotaExceeded)
        default: throw DeepLError(state: .unreachable)
        }
    }
}
