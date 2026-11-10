// SPDX-License-Identifier: AGPL-3.0-only

import XCTest
@testable import GameTranslatorCore

final class SubtitleStabilizerTests: XCTestCase {
    /// Noktalamayla bitmeyen satır yazılmakta olabilir; iki kare aynı okununca gönderilir.
    func testUnfinishedLineIsSentAfterTwoStableFrames() {
        var s = SubtitleStabilizer()
        XCTAssertEqual(s.feed("We do not have time"), .none)
        XCTAssertEqual(s.feed("We do not have time"), .translate("We do not have time"))
        XCTAssertEqual(s.feed("We do not have time"), .none)
    }

    /// Cümle bitmişse beklemeye gerek yok; hızlı konuşmada her kare önemli.
    func testFinishedSentenceIsSentOnFirstFrame() {
        for line in ["We do not have time.", "Wait!", "Who are you?", "I know…", "\"Run.\""] {
            var s = SubtitleStabilizer()
            XCTAssertEqual(s.feed(line), .translate(line), line)
            XCTAssertEqual(s.feed(line), .none, line)
        }
    }

    func testOcrNoiseDoesNotReemit() {
        var s = SubtitleStabilizer()
        _ = s.feed("The gate will not hold"); _ = s.feed("The gate will not hold")
        XCTAssertEqual(s.feed("The gale will not hold"), .none)
        XCTAssertEqual(s.feed("The gale will not hold"), .none)
    }

    /// Süslü yazıda aynı cümle her karede başka çöple okunur; TV'de iki çeviri olarak görünmemeli.
    func testGarbledRereadOfSentLineDoesNotReemit() {
        var s = SubtitleStabilizer()
        let first = "Across the deep waters, Pbo ou$3e went, hoping a boat would carry him home."
        XCTAssertEqual(s.feed(first), .translate(first))
        XCTAssertEqual(s.feed("Across the deep waters2aljd O went, Hoping a boat would carry him home."), .none)
        XCTAssertEqual(s.feed("Across the deep waters2aljd O went, Hoping a boat would carry him home."), .none)
        XCTAssertEqual(s.feed("Across the deep waters, he went."), .translate("Across the deep waters, he went."))
    }

    func testFlickeringHudDoesNotSpam() {
        var s = SubtitleStabilizer()
        var sent = 0
        for i in 0..<20 { if case .translate = s.feed(i % 2 == 0 ? "24 / 120" : "") { sent += 1 } }
        XCTAssertEqual(sent, 0)
    }

    func testTypewriterSendsCompletedLine() {
        var s = SubtitleStabilizer()
        _ = s.feed("We do"); _ = s.feed("We do not have")
        XCTAssertEqual(s.feed("We do not have time for this, boy."), .translate("We do not have time for this, boy."))
        XCTAssertEqual(s.feed("We do not have time for this, boy."), .none)
    }

    func testTwoEmptyFramesClearAfterSend() {
        var s = SubtitleStabilizer()
        _ = s.feed("Hello there"); _ = s.feed("Hello there")
        XCTAssertEqual(s.feed(""), .none)
        XCTAssertEqual(s.feed(""), .clear)
    }

    func testAlreadyTranslatedIsSkipped() {
        var s = SubtitleStabilizer()
        XCTAssertFalse(s.shouldSend(source: "Kullanıcı Ekle Oturum Açtı", translation: "Kullanıcı Ekle Oturum Açtı"))
        XCTAssertTrue(s.shouldSend(source: "Add user", translation: "Kullanıcı ekle"))
    }

    func testResetClearsOnlyAfterSomethingWasSent() {
        var s = SubtitleStabilizer()
        XCTAssertEqual(s.reset(), .none)
        _ = s.feed("Hello there"); _ = s.feed("Hello there")
        XCTAssertEqual(s.reset(), .clear)
        XCTAssertEqual(s.reset(), .none)
    }
}

final class SubtitleStabilizerRetryTests: XCTestCase {
    /// Çevirisi başarısız olan satır ekranda kaldıkça yeniden denenebilmeli.
    func testForgottenLineIsTranslatedAgain() {
        var s = SubtitleStabilizer()
        _ = s.feed("Hold the gate")
        XCTAssertEqual(s.feed("Hold the gate"), .translate("Hold the gate"))
        s.forgetSent()
        XCTAssertEqual(s.feed("Hold the gate"), .none)
        XCTAssertEqual(s.feed("Hold the gate"), .translate("Hold the gate"))
    }
}
