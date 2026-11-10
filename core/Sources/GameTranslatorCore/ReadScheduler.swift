// SPDX-License-Identifier: AGPL-3.0-only

import Foundation

/// Hangi karede yazı tanıma yapılacağını seçer. Oyun süresinin çoğunda bölge boştur ya da değişmez;
/// işlemcinin büyük kısmı bu karelerde boşa gider.
public struct ReadScheduler {
    public enum Plan: Equatable {
        /// Bölge değişmedi, son okuma geçerli.
        case reuse(String)
        /// Yazı tanıma yapılır; `deep` ise boş bölgede kısa satırlar için büyütülmüş okuma da denenir.
        case read(deep: Bool)
    }

    /// Video gürültüsü bu farkı aşmaz; bir harf eklenmesi aşar.
    private let changeThreshold: Int
    private let maxReuse: Int
    private let deepEvery: Int

    private var previous: [UInt8]?
    private var lastText = ""
    private var reused = 0
    private var emptyReads = 0

    public init(changeThreshold: Int = 16, maxReuse: Int = 20, deepEvery: Int = 3) {
        self.changeThreshold = changeThreshold
        self.maxReuse = maxReuse
        self.deepEvery = deepEvery
    }

    /// Her kare okunur ve boş kareler hep derin okunur.
    public static var always: ReadScheduler { ReadScheduler(changeThreshold: -1, maxReuse: 0, deepEvery: 1) }

    public mutating func plan(signature: [UInt8]) -> Plan {
        defer { previous = signature }
        if let previous, reused < maxReuse, !changed(previous, signature) {
            reused += 1
            return .reuse(lastText)
        }
        reused = 0
        return .read(deep: emptyReads % deepEvery == 0)
    }

    public mutating func record(_ text: String) {
        lastText = text
        // Yazı yeni kalktıysa derin okuma sırası hemen gelmez.
        emptyReads = text.isEmpty ? emptyReads + 1 : 1
    }

    public mutating func reset() {
        previous = nil
        lastText = ""
        reused = 0
        emptyReads = 0
    }

    private func changed(_ a: [UInt8], _ b: [UInt8]) -> Bool {
        guard a.count == b.count else { return true }
        return zip(a, b).contains { abs(Int($0) - Int($1)) > changeThreshold }
    }
}
