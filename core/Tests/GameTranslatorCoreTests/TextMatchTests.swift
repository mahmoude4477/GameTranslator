// SPDX-License-Identifier: AGPL-3.0-only

import XCTest
@testable import GameTranslatorCore

final class TextMatchTests: XCTestCase {
    func testNormalizeStripsPunctuationAndCase() {
        XCTAssertEqual(TextMatch.normalize("Kratos: We do NOT have time, boy."), "kratos we do not have time boy")
    }

    func testSimilarToleratesOcrNoise() {
        XCTAssertTrue(TextMatch.isSimilar("the gate will not hold", "the gale will not hold"))
    }

    func testDifferentLinesAreNotSimilar() {
        XCTAssertFalse(TextMatch.isSimilar("we do not have time", "the gate will not hold"))
    }

    func testSameSubtitleIgnoresGarbledWords() {
        let a = TextMatch.normalize("Across the deep waters, Pbo ou$3e went, hoping a boat would carry him home.")
        let b = TextMatch.normalize("Across the deep waters2aljd O went, Hoping a boat would carry him home.")
        XCTAssertTrue(TextMatch.isSameSubtitle(b, a))
        let wordSwap = TextMatch.normalize("Across the deep waters, 2aljd Pbo went, Hoping 3e boat would carry him home now.")
        XCTAssertFalse(TextMatch.isSimilar(wordSwap, a))
        XCTAssertTrue(TextMatch.isSameSubtitle(wordSwap, a))
    }

    /// İki satırlık altyazı solarken alt satır kaçırıldı: yeni satır değil.
    func testRereadMissingALineIsSameSubtitle() {
        let sent = TextMatch.normalize("Across the deep waters, Pbo went,\nhoping a boat would carry him home.")
        XCTAssertTrue(TextMatch.isSameSubtitle(TextMatch.normalize(OcrNoise.clean("Across the deep waters2aljd O went,")), sent))
        // Daktilo efektinde uzayan metin ise yenidir.
        XCTAssertFalse(TextMatch.isSameSubtitle(sent, TextMatch.normalize("Across the deep waters, Pbo went,")))
    }

    func testSameSubtitleKeepsDistinctLinesApart() {
        let pairs = [
            ("we do not have time", "the gate will not hold"),
            // Daktilo efektiyle uzayan satır yeni sayılır.
            ("we do not have time for this boy and you know it", "we do not have time"),
            ("where is the boy", "where is the ship"),
            ("you should not be here", "i told you to stay with your mother"),
            ("we have to cross the river before the sun goes down", "the river is too deep for us to cross today"),
        ]
        for (a, b) in pairs { XCTAssertFalse(TextMatch.isSameSubtitle(a, b), "\(a) / \(b)") }
    }

    func testEmptyIsNeverSimilarToText() {
        XCTAssertFalse(TextMatch.isSimilar("", "abc"))
    }
}

final class CenteredBoxTests: XCTestCase {
    func testPartlyReadCenteredSubtitleIsWidenedSymmetrically() {
        let box = TextBox(left: 0.45, top: 0.85, right: 0.75, bottom: 0.9).centeredOnScreen()
        XCTAssertEqual(box.left, 0.25, accuracy: 0.001)
        XCTAssertEqual(box.right, 0.75, accuracy: 0.001)
        XCTAssertEqual(box.top, 0.85)
    }

    func testCornerTextIsLeftAlone() {
        let box = TextBox(left: 0.02, top: 0.1, right: 0.2, bottom: 0.15)
        XCTAssertEqual(box.centeredOnScreen(), box)
    }
}
