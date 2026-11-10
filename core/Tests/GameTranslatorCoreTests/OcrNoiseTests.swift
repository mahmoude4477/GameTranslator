// SPDX-License-Identifier: AGPL-3.0-only

@testable import GameTranslatorCore
import XCTest

final class OcrNoiseTests: XCTestCase {
    /// Anıl'ın videosu: süslü yazıda bir sözcük her karede başka çöpe dönüşüyordu.
    func testGarbledWordsAreRemoved() {
        XCTAssertEqual(OcrNoise.clean("Across the deep waters, Pbo ou$3e went, hoping a boat would carry him home."),
                       "Across the deep waters, Pbo went, hoping a boat would carry him home.")
        XCTAssertEqual(OcrNoise.clean("Across the deep waters2aljd O went, Hoping a boat."),
                       "Across the deep waters O went, Hoping a boat.")
        XCTAssertEqual(OcrNoise.clean("He left 3e, at dawn."), "He left, at dawn.")
    }

    func testUsualTokensAreKept() {
        for line in ["Turn on the PS5 now.", "Hold R2-D2 still.", "It costs $500.", "Meet me at 3:00pm.",
                     "The 2nd gate is 100m away.", "Run at 60fps, 4K.", "Score x2!", "Year 1984.", "I can't — wait…",
                     "\"Kratos!\" she said.", "Mk2 rifle, 9.5kg.", "L1 + R1 Spartan Rage"] {
            XCTAssertEqual(OcrNoise.clean(line), line, line)
        }
    }

    func testReadingDropsLinesThatWereOnlyNoise() {
        let reading = Reading(lines: [TextLine("We go now."), TextLine("ou$3e 2aljd")]).withoutOcrNoise()
        XCTAssertEqual(reading.text, "We go now.")
    }
}
