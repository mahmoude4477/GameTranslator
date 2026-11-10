// SPDX-License-Identifier: AGPL-3.0-only

@testable import GameTranslatorCore
import XCTest

/// Oyun ifadeleri çeviriden önce düz İngilizceye çevrilir (Windows: GameEnglishTests).
final class GameEnglishTests: XCTestCase {
    func testGameIdiomsBecomePlainEnglish() {
        XCTAssertEqual(GameEnglish.plain("Hold your fire! They're friendlies!"), "Hold your fire! They're allies!")
        XCTAssertEqual(GameEnglish.plain("I got your six."), "I've got your back.")
        XCTAssertEqual(GameEnglish.plain("Heads up! Incoming!"), "Watch out! Incoming!")
        XCTAssertEqual(GameEnglish.plain("Took you long enough."), "You're late.")
        XCTAssertEqual(GameEnglish.plain("Careful, we've got company."), "Careful, enemies are coming.")
        XCTAssertEqual(GameEnglish.plain("Copy that. Moving in."), "Copy that. Going in.")
    }

    func testOtherTextIsUnchanged() {
        XCTAssertEqual(GameEnglish.plain("The friendliest town in the north."), "The friendliest town in the north.")
        XCTAssertEqual(GameEnglish.plain("Stand down!"), "Stand down!")
    }
}
