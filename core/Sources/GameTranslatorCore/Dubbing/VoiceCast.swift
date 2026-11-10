// SPDX-License-Identifier: AGPL-3.0-only

import Foundation

/// Dublaj sesleri: Pocket TTS Turkish'in hazır sesleri (yapay, gerçek kişi değil).
public enum DubbingVoice: String, CaseIterable, Codable, Sendable {
    case female1 = "female_1", female2 = "female_2", female3 = "female_3"
    case male1 = "male_1", male2 = "male_2", male3 = "male_3"

    public enum Gender: Sendable { case female, male }

    public var gender: Gender { rawValue.hasPrefix("female") ? .female : .male }

    public static func voices(_ gender: Gender) -> [DubbingVoice] { allCases.filter { $0.gender == gender } }
}

/// Karakterlere ses dağıtır. Her konuşmacı adı kalıcı olarak bir sese bağlanır (Kratos oyun boyunca aynı sesle konuşur);
/// etiketsiz satırlar anlatıcı sesiyle, tireyle değişen konuşmalar iki ses arasında sırayla okunur.
public struct VoiceCast: Codable, Equatable, Sendable {
    /// Adın büyük harfli biçimi → ses. Ayarlardan değiştirilebilir.
    public private(set) var assignments: [String: DubbingVoice] = [:]
    public var narrator: DubbingVoice = .male1
    /// Tireli konuşmada sıradaki kişinin sesi.
    public var partner: DubbingVoice = .female1
    private var lastWasPartner = false

    private enum CodingKeys: String, CodingKey { case assignments, narrator, partner }

    public init() {}

    public static func key(_ name: String) -> String { name.uppercased(with: Locale(identifier: "en_US")) }

    /// Satırın sesi; yeni bir ad ilk kez görülünce ona bir ses ayrılır.
    public mutating func voice(for line: SpokenLine) -> DubbingVoice {
        if let speaker = line.speaker {
            lastWasPartner = false
            let key = Self.key(speaker)
            if let voice = assignments[key] { return voice }
            let voice = pick(for: NameGender.guess(speaker))
            assignments[key] = voice
            return voice
        }
        if line.newTurn { lastWasPartner.toggle() }
        return lastWasPartner ? partner : narrator
    }

    public mutating func assign(_ voice: DubbingVoice, to name: String) { assignments[Self.key(name)] = voice }

    /// Yeni karakter için en az kullanılan ses; cinsiyet bilinmiyorsa iki cinsiyetten de seçilir.
    private func pick(for gender: DubbingVoice.Gender?) -> DubbingVoice {
        let candidates = gender.map(DubbingVoice.voices) ?? DubbingVoice.allCases
        var uses: [DubbingVoice: Int] = [:]
        for voice in assignments.values { uses[voice, default: 0] += 1 }
        // Anlatıcı sesi en sona kalsın: etiketsiz satırlarla karışmasın.
        uses[narrator, default: 0] += 1
        return candidates.min { (uses[$0] ?? 0, $0.order) < (uses[$1] ?? 0, $1.order) }!
    }
}

private extension DubbingVoice {
    var order: Int { Self.allCases.firstIndex(of: self)! }
}

/// Addan cinsiyet tahmini: oyun karakterleri ve yaygın İngilizce adlar. Bilinmeyen ad nil döner.
public enum NameGender {
    private static let female: Set<String> = [
        "freya", "faye", "angrboda", "sif", "thrud", "ellie", "abby", "dina", "tess", "marlene", "riley", "aloy",
        "elisabet", "beta", "mary", "jane", "mj", "may", "rio", "hailey", "lara", "jill", "claire", "ada", "ashley",
        "tifa", "aerith", "yuffie", "jessie", "yuna", "lulu", "rikku", "ciri", "yennefer", "triss", "kassandra",
        "aya", "evie", "lydia", "anna", "emma", "olivia", "sophia", "isabella", "mia", "amelia", "harper", "evelyn",
        "abigail", "emily", "ella", "elizabeth", "sofia", "avery", "scarlett", "grace", "chloe", "victoria", "lily",
        "hannah", "zoe", "nora", "sarah", "rachel", "rebecca", "laura", "julia", "maria", "kate", "katie", "lucy",
        "alice", "eve", "helen", "linda", "susan", "karen", "nancy", "lisa", "betty", "margaret", "sandra", "kim",
        "mother", "mom", "mum", "queen", "princess", "lady", "girl", "woman", "sister", "aunt", "grandma", "witch",
        "goddess", "daughter", "wife",
    ]
    private static let male: Set<String> = [
        "kratos", "atreus", "mimir", "baldur", "thor", "odin", "brok", "sindri", "tyr", "heimdall", "freyr",
        "durlin", "joel", "tommy", "bill", "henry", "jesse", "lev", "isaac", "owen", "manny", "jerry", "rost",
        "erend", "varl", "sylens", "kotallo", "peter", "miles", "harry", "otto", "kraven", "venom", "norman",
        "aaron", "ganke", "leon", "chris", "albert", "wesker", "ethan", "cloud", "barret", "zack", "sephiroth",
        "noctis", "tidus", "auron", "geralt", "jaskier", "alexios", "bayek", "ezio", "altair", "connor", "edward",
        "arthur", "dutch", "john", "micah", "hosea", "nathan", "nate", "drake", "sully", "jin", "khotun", "deacon",
        "boozer", "james", "robert", "michael", "william", "david", "richard", "joseph", "thomas", "charles",
        "daniel", "matthew", "anthony", "mark", "paul", "steven", "andrew", "kenneth", "kevin", "brian", "george",
        "jack", "jacob", "luke", "max", "father", "dad", "boy", "king", "prince", "lord", "sir", "man", "brother",
        "uncle", "grandpa", "son", "husband", "soldier", "guard", "captain", "commander", "general",
    ]

    public static func guess(_ name: String) -> DubbingVoice.Gender? {
        let lower = name.lowercased()
        if female.contains(lower) { return .female }
        if male.contains(lower) { return .male }
        // Çok sözcüklü adda sözcüklere bakılır ("OLD MAN", "Mary Jane", "Guard Captain").
        for word in lower.split(separator: " ").map(String.init) {
            if female.contains(word) { return .female }
            if male.contains(word) { return .male }
        }
        return nil
    }
}
