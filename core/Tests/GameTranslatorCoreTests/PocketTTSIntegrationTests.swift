// SPDX-License-Identifier: AGPL-3.0-only

import Foundation
import XCTest
@testable import GameTranslatorCore

/// Gerçek pocket-tts ve Pocket TTS Turkish ile uçtan uca deneme (.github/workflows/tts-samples.yml çalıştırır).
/// GAMETRANSLATOR_TTS_BIN: pocket-tts programı, GAMETRANSLATOR_TTS_MODEL: indirilen model klasörü (DubbingModel.files),
/// GAMETRANSLATOR_TTS_OUT: üretilen seslerin yazılacağı klasör. Verilmezse atlanır.
final class PocketTTSIntegrationTests: XCTestCase {
    func testDubbingLinesAreVoicedWithEveryVoice() async throws {
        let environment = ProcessInfo.processInfo.environment
        guard let binary = environment["GAMETRANSLATOR_TTS_BIN"], let modelPath = environment["GAMETRANSLATOR_TTS_MODEL"],
              let outPath = environment["GAMETRANSLATOR_TTS_OUT"] else { throw XCTSkip("Gerçek model verilmedi") }
        let model = URL(fileURLWithPath: modelPath), out = URL(fileURLWithPath: outPath)
        try FileManager.default.createDirectory(at: out, withIntermediateDirectories: true)

        let original = try String(contentsOf: model.appendingPathComponent("config.yaml"), encoding: .utf8)
        let config = model.appendingPathComponent("gametranslator.yaml")
        try DubbingModel.localConfig(from: original, directory: model).write(to: config, atomically: true, encoding: .utf8)

        // Sesler uygulamadaki gibi altyazı sunucusundan verilir.
        let subtitles = SubtitleServer(port: 0)
        let port = try await subtitles.start()
        defer { subtitles.stop() }
        for voice in DubbingVoice.allCases {
            subtitles.publish(try Data(contentsOf: model.appendingPathComponent("voices/\(voice.rawValue).wav")),
                              at: "/voices/\(voice.rawValue).wav")
        }

        let server = PocketTTSServer(executable: URL(fileURLWithPath: binary), config: config,
                                     defaultVoice: model.appendingPathComponent("voices/male_1.wav"))
        let started = Date()
        try await server.start()
        var report = "Model yükleme: \(String(format: "%.2f", Date().timeIntervalSince(started))) sn\n"
        report += "dosya | ham (sn) | akıcı (sn) | üretim (sn)\n"

        let lines: [(String, SpokenLine)] = [
            ("notr", SpokenLine(english: "We need to cross the river, the bridge is gone.",
                                turkish: "Nehrin karşı tarafına geçmemiz gerek, köprü yıkılmış.")),
            ("kizgin", SpokenLine(english: "Get back! This is not your fight.", turkish: "Geri çekil! Bu savaş seninle ilgili değil.")),
            ("uzgun", SpokenLine(english: "I'm sorry. Your mother would be proud of you.",
                                 turkish: "Üzgünüm. Annen seninle gurur duyardı.")),
            ("saskin", SpokenLine(english: "What?! The door opened by itself!", turkish: "Ne?! Kapı kendi kendine açıldı!")),
            ("sakin", SpokenLine(english: "Breathe, boy. Take your time.", turkish: "Nefes al, evlat. Acele etme.")),
            ("mutlu", SpokenLine(english: "We did it! We're going home.", turkish: "Başardık! Eve dönüyoruz.")),
            ("sayi", SpokenLine(english: "We have 3 days and 1,250 coins.", turkish: "3 günümüz ve 1.250 altınımız var.")),
        ]
        for voice in DubbingVoice.allCases {
            let url = URL(string: "http://127.0.0.1:\(port)/voices/\(voice.rawValue).wav")!
            for (name, line) in lines {
                let text = SpeechText.prepare(line.turkish, emotion: SpeechEmotion.guess(english: line.english))
                let begin = Date()
                let raw = try await server.synthesize(text, voice: url)
                let took = Date().timeIntervalSince(begin)
                let smooth = raw.tightened()
                XCTAssertGreaterThan(raw.duration, 0.5, "\(voice) \(name)")
                XCTAssertEqual(raw.sampleRate, 24000)
                try raw.wav.write(to: out.appendingPathComponent("ham-\(voice.rawValue)-\(name).wav"))
                try smooth.wav.write(to: out.appendingPathComponent("akici-\(voice.rawValue)-\(name).wav"))
                report += "\(voice.rawValue)-\(name) | \(String(format: "%.2f", raw.duration)) | "
                    + "\(String(format: "%.2f", smooth.duration)) | \(String(format: "%.2f", took))  [\(text)]\n"
            }
        }
        await server.stop()
        try report.write(to: out.appendingPathComponent("rapor.txt"), atomically: true, encoding: .utf8)
        print(report)
    }
}
