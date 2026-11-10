// SPDX-License-Identifier: AGPL-3.0-only

import Foundation

/// Pocket TTS Turkish (wite-tech, CC-BY-4.0): ilk kullanımda indirilen dosyalar. Sürüm sabittir, özetler doğrulanır.
public enum DubbingModel {
    public static let revision = "fcbd37c74dc127be846face0ff36062e9a939f7b"
    public static let repository = "wite-tech/pocket-tts-turkish"

    public struct File: Equatable, Sendable {
        public var name: String
        public var bytes: Int64
        public var sha256: String
        public var url: URL { URL(string: "https://huggingface.co/\(repository)/resolve/\(revision)/\(name)")! }
    }

    public static let files: [File] = [
        File(name: "config.yaml", bytes: 1212, sha256: "fa60a7799ac09778360ce1aa5c2216fa2ce6f1be06aabb2e0dd4f6ea63a6d3d7"),
        File(name: "model.safetensors", bytes: 438_033_312, sha256: "c43b72ed336b64c66adceba8fbbb059e869f753b60de086e3f32bf1b118d5a77"),
        File(name: "tokenizer.model", bytes: 303_502, sha256: "a6c3a7ca48ecf3c9254bf72dff58e3f09f2798f49effcbd7debcf6f7a87f3568"),
        File(name: "voices/female_1.wav", bytes: 188_684, sha256: "82be41762735f5004cec992badc67813a02206fe53888ba7342a589197850225"),
        File(name: "voices/female_2.wav", bytes: 229_964, sha256: "98cd697c9dd6f77f253bd1233675496b6d4882cd9c0ef72434b6a12e06564aa7"),
        File(name: "voices/female_3.wav", bytes: 212_684, sha256: "555eab5c0640649c1323e6e51c2fae10a334ac3988e6ba0052822147fd9bc40c"),
        File(name: "voices/male_1.wav", bytes: 235_724, sha256: "e6538a1f0ce60d08994c3a733946589d327cf9256b35b77e4155c0baefadc81a"),
        File(name: "voices/male_2.wav", bytes: 195_884, sha256: "d5585323c634900b054ea20d1c8c9e39d35516b89dba693eaabf8587143dd6d8"),
        File(name: "voices/male_3.wav", bytes: 237_644, sha256: "b5bbbca120837cca1b93c3b8d740e70bcc3f32f53c557af04b207fc69a31545e"),
    ]

    public static var totalBytes: Int64 { files.reduce(0) { $0 + $1.bytes } }

    /// İndirilen ayar dosyasından yerel çalışma ayarı: ağırlık ve sözlük yolları klasördeki dosyalara çevrilir, model
    /// küçük harfle başlayan cümlelerle eğitildiği için ilk harf büyütülmez.
    public static func localConfig(from original: String, directory: URL) -> String {
        var lines: [String] = []
        for line in original.components(separatedBy: "\n") {
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            let indent = String(line.prefix(while: { $0 == " " }))
            if trimmed.hasPrefix("weights_path:") {
                lines.append(indent + "weights_path: " + quoted(directory.appendingPathComponent("model.safetensors").path))
            } else if trimmed.hasPrefix("tokenizer_path:") {
                lines.append(indent + "tokenizer_path: " + quoted(directory.appendingPathComponent("tokenizer.model").path))
            } else if trimmed.hasPrefix("capitalize_first_letter:") {
                continue
            } else {
                lines.append(line)
            }
        }
        while lines.last?.isEmpty == true { lines.removeLast() }
        return lines.joined(separator: "\n") + "\ncapitalize_first_letter: false\n"
    }

    /// YAML'da tek tırnaklı dize: Windows yollarındaki ters bölü olduğu gibi kalır.
    private static func quoted(_ path: String) -> String { "'" + path.replacingOccurrences(of: "'", with: "''") + "'" }
}
