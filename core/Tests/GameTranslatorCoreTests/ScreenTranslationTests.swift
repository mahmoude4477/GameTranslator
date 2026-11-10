// SPDX-License-Identifier: AGPL-3.0-only

import XCTest
@testable import GameTranslatorCore

/// "Ekranı çevir": ekranın tamamındaki yazılar yerlerine göre gruplanır (Windows: ScreenTranslationTests.cs).
final class ScreenTranslationTests: XCTestCase {
    private func line(_ text: String, _ left: Double, _ top: Double, _ right: Double, height: Double = 0.03) -> TextLine {
        TextLine(text, box: TextBox(left: left, top: top, right: right, bottom: top + height))
    }

    func testLinesFarApartBecomeSeparateGroups() {
        let groups = ScreenTranslation.groups(Reading(lines: [
            line("Find the blacksmith in", 0.05, 0.10, 0.30),
            line("the village square", 0.05, 0.135, 0.25),
            line("Press the lever to open the gate.", 0.40, 0.85, 0.60),
            line("New item unlocked", 0.75, 0.60, 0.95),
        ]))
        XCTAssertEqual(groups.map(\.paragraphs), [["Find the blacksmith in the village square"], ["New item unlocked"], ["Press the lever to open the gate."]])
        XCTAssertEqual(groups[0].box.bottom, 0.165, accuracy: 1e-6)
        XCTAssertEqual(groups[0].lineHeight, 0.03, accuracy: 1e-6)
    }

    func testLinesOnTheSameRowButFarApartAreSeparate() {
        XCTAssertEqual(ScreenTranslation.groups(Reading(lines: [line("Settings", 0.05, 0.05, 0.15), line("Back to menu", 0.80, 0.05, 0.95)])).count, 2)
    }

    func testMenuLinesAreTranslatedOneByOne() {
        let groups = ScreenTranslation.groups(Reading(lines: [
            line("Map", 0.05, 0.30, 0.15), line("Inventory", 0.05, 0.34, 0.20), line("Settings", 0.05, 0.38, 0.18),
        ]))
        XCTAssertEqual(groups.map(\.paragraphs), [["Map", "Inventory", "Settings"]])
    }

    func testTitleInBigLettersIsNotMergedWithTheSmallTextBelow() {
        let groups = ScreenTranslation.groups(Reading(lines: [
            line("Chapter One", 0.30, 0.20, 0.70, height: 0.08),
            line("The journey begins in the north.", 0.30, 0.29, 0.70),
        ]))
        XCTAssertEqual(groups.count, 2)
    }

    func testNumbersAndPromptsAreNotTranslated() {
        let groups = ScreenTranslation.groups(Reading(lines: [
            line("23m", 0.50, 0.05, 0.55), line("9", 0.90, 0.05, 0.92), line("R2 Swing", 0.80, 0.90, 0.95), line("HP 120/300", 0.05, 0.90, 0.20),
        ]))
        XCTAssertEqual(groups.map(\.paragraphs), [["HP 120/300"]])
    }

    func testEveryGroupIsTranslatedAndProgressIsReported() async throws {
        var reported: [Int] = []
        let items = try await ScreenTranslation.translate(Reading(lines: [
            line("Find the blacksmith.", 0.05, 0.10, 0.30),
            line("Bring him three iron bars.", 0.05, 0.135, 0.30),
            line("KRATOS", 0.60, 0.50, 0.70),
            line("Leave the camp", 0.60, 0.80, 0.90),
        ]), using: { $0 == "KRATOS" ? $0 : "TR: " + $0 }, progress: { reported.append($0.count) })
        XCTAssertEqual(items.map(\.translation), ["TR: Find the blacksmith.\nTR: Bring him three iron bars.", "TR: Leave the camp"])
        XCTAssertTrue(items[0].paragraphs)
        XCTAssertFalse(items[1].paragraphs)
        XCTAssertEqual(reported, [1, 2])
    }

    func testDisplayTimeGrowsWithTextButIsBounded() {
        let item = { (length: Int) in ScreenTranslation.Item(source: "a", translation: String(repeating: "a", count: length), box: TextBox(left: 0, top: 0, right: 1, bottom: 1), lineHeight: 0.03) }
        XCTAssertEqual(ScreenTranslation.displayMilliseconds([item(0)]), 12000)
        XCTAssertEqual(ScreenTranslation.displayMilliseconds([item(200)]), 18000)
        XCTAssertEqual(ScreenTranslation.displayMilliseconds([item(5000)]), 60000)
    }
}
