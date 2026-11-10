// SPDX-License-Identifier: AGPL-3.0-only

import XCTest
@testable import GameTranslatorCore

final class TVLocatorTests: XCTestCase {
    struct Unreachable: Error {}
    final class Control: TVControlling {
        func launch(appId: String, params: [String: Any]) async throws {}
        func foregroundAppId() async throws -> String { "" }
        func close() {}
    }

    private let saved = LGTV(name: "Salon", host: "192.168.1.153")

    func testKeepsSavedAddressWhileItWorks() async throws {
        var searches = 0
        let locator = TVLocator(tv: saved, connect: { _ in Control() }, search: { searches += 1; return [] }, onMoved: { _ in })
        _ = try await locator.connect()
        XCTAssertEqual(searches, 0)
    }

    func testFollowsTVToItsNewAddressAndReportsIt() async throws {
        let moved = LGTV(name: "Salon", host: "192.168.1.230")
        var tried: [String] = []
        var reported: LGTV?
        let locator = TVLocator(tv: saved, connect: { tv in
            tried.append(tv.host)
            if tv.host == "192.168.1.153" { throw Unreachable() }
            return Control()
        }, search: { [moved] }, onMoved: { reported = $0 })

        _ = try await locator.connect()
        XCTAssertEqual(tried, ["192.168.1.153", "192.168.1.230"])
        XCTAssertEqual(reported, moved)

        _ = try await locator.connect()   // sonraki bağlantılar yeni adrese gider
        XCTAssertEqual(tried.last, "192.168.1.230")
        XCTAssertEqual(tried.count, 3)
    }

    func testDoesNotSearchTheNetworkOnEveryFailure() async {
        var searches = 0
        let locator = TVLocator(tv: saved, connect: { _ in throw Unreachable() },
                                search: { searches += 1; return [] }, onMoved: { _ in }, searchInterval: .seconds(30))
        for _ in 0..<5 { _ = try? await locator.connect() }
        XCTAssertEqual(searches, 1)
    }

    func testPicksTheTVWithTheSameNameAmongSeveral() {
        let other = LGTV(name: "Yatak odası", host: "192.168.1.40")
        let moved = LGTV(name: "Salon", host: "192.168.1.230")
        XCTAssertEqual(TVLocator.moved(saved, among: [other, moved]), moved)
    }

    func testAcceptsTheOnlyTVWhenItsNameDiffers() {
        let renamed = LGTV(name: "LG TV", host: "192.168.1.230")
        XCTAssertEqual(TVLocator.moved(saved, among: [renamed]), renamed)
    }

    func testDoesNotGuessAmongSeveralUnknownTVs() {
        let a = LGTV(name: "A", host: "192.168.1.40"), b = LGTV(name: "B", host: "192.168.1.41")
        XCTAssertNil(TVLocator.moved(saved, among: [a, b]))
    }

    func testStaysPutWhenSavedAddressStillAnswersDiscovery() {
        XCTAssertNil(TVLocator.moved(saved, among: [LGTV(name: "Salon", host: saved.host)]))
    }
}
