// SPDX-License-Identifier: AGPL-3.0-only

import Foundation

/// Kayıtlı TV'ye ulaşılamazsa ağda yeniden arar: modem TV'ye yeni adres verdiğinde eşleşme bozulmaz.
public final class TVLocator: @unchecked Sendable {
    private var tv: LGTV
    private let connectTo: (LGTV) async throws -> TVControlling
    private let search: () async -> [LGTV]
    private let onMoved: (LGTV) -> Void
    private let searchInterval: Duration
    private var lastSearch: ContinuousClock.Instant?

    public init(tv: LGTV, connect: @escaping (LGTV) async throws -> TVControlling, search: @escaping () async -> [LGTV],
                onMoved: @escaping (LGTV) -> Void, searchInterval: Duration = .seconds(30)) {
        self.tv = tv
        connectTo = connect
        self.search = search
        self.onMoved = onMoved
        self.searchInterval = searchInterval
    }

    public func connect() async throws -> TVControlling {
        do {
            return try await connectTo(tv)
        } catch {
            // TV kapalıyken de buraya gelinir; ağ her denemede taranmaz.
            let now = ContinuousClock.now
            if let lastSearch, lastSearch.duration(to: now) < searchInterval { throw error }
            lastSearch = now
            guard let moved = Self.moved(tv, among: await search()) else { throw error }
            let control = try await connectTo(moved)
            tv = moved
            onMoved(moved)
            return control
        }
    }

    /// Bulunanlar içinde kayıtlı TV'nin yeni adresi. Birden çok tanımadığı TV varsa tahmin etmez.
    static func moved(_ saved: LGTV, among all: [LGTV]) -> LGTV? {
        let found = all.filter { $0.brand == saved.brand }
        guard !found.contains(where: { $0.host == saved.host }) else { return nil }
        let sameName = found.filter { $0.name == saved.name }
        if sameName.count == 1 { return sameName[0] }
        return found.count == 1 ? found[0] : nil
    }
}
