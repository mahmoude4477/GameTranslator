// SPDX-License-Identifier: AGPL-3.0-only

import CoreGraphics
import GameTranslatorCore
import ImageIO
import XCTest
@testable import GameTranslator

private final class FakeReader: TextReader, @unchecked Sendable {
    var texts: [String]
    var readOnMainThread: [Bool] = []
    init(_ texts: [String]) { self.texts = texts }
    func read(_ image: CGImage) throws -> String {
        readOnMainThread.append(Thread.isMainThread)
        return texts.isEmpty ? "" : texts.removeFirst()
    }
}

/// Satırları yerleriyle döndüren okuyucu.
private struct LinesReader: TextReader {
    let reading: Reading
    init(_ reading: Reading) { self.reading = reading }
    func read(_ image: CGImage) throws -> String { reading.text }
    func readLines(_ image: CGImage, deep: Bool) throws -> Reading { reading }
}

private final class FakeTranslator: TextTranslator {
    var error: Error?
    var failuresLeft = 0
    func translate(_ text: String) async throws -> String {
        if let error { throw error }
        if failuresLeft > 0 { failuresLeft -= 1; throw URLError(.timedOut) }
        return "TR: " + text
    }
}

private final class SpeakingTranslator: TextTranslator {
    func translate(_ text: String) async throws -> String { "TR: " + text }
    func spokenLines(for text: String) -> [SpokenLine] { [SpokenLine(english: text, turkish: "TR: " + text)] }
}

/// Çevirileri test kapıyı açana kadar bekletir; kapı açıldıktan sonra gelenler beklemez.
private final class GatedTranslator: TextTranslator {
    private(set) var requested: [String] = []
    private var isOpen = false
    private var waiting: [(text: String, continuation: CheckedContinuation<Void, Never>)] = []
    func translate(_ text: String) async throws -> String {
        requested.append(text)
        if !isOpen { await withCheckedContinuation { waiting.append((text, $0)) } }
        return "TR: " + text
    }
    func open() { isOpen = true; waiting.forEach { $0.continuation.resume() }; waiting.removeAll() }
    /// Yalnız bu satırın çevirisini bitirir (sırayı test belirler).
    func release(_ text: String) async {
        while !waiting.contains(where: { $0.text == text }) { await Task.yield() }
        let index = waiting.firstIndex { $0.text == text }!
        waiting.remove(at: index).continuation.resume()
    }
}

private final class Counter: @unchecked Sendable {
    private let lock = NSLock()
    private var count = 0
    var value: Int { lock.withLock { count } }
    func increment() { lock.withLock { count += 1 } }
}

private final class FakeSender: SubtitleSending {
    var sent: [String] = []
    var durations: [Int] = []
    var boxes: [TextBox?] = []
    var clears = 0
    func sendSubtitle(_ text: String, durationMs: Int, box: TextBox?) { sent.append(text); durations.append(durationMs); boxes.append(box) }
    func clear() { clears += 1 }
}

@MainActor
final class SubtitlePipelineTests: XCTestCase {
    private let frame: CGImage = {
        let context = CGContext(data: nil, width: 64, height: 64, bitsPerComponent: 8, bytesPerRow: 0,
                                space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
        return context.makeImage()!
    }()

    func testStableTextIsTranslatedAndSentOnce() async {
        let sender = FakeSender()
        let pipeline = SubtitlePipeline(reader: FakeReader(["Hold the gate", "Hold the gate", "Hold the gate"]),
                                        translator: FakeTranslator(), server: sender)
        for _ in 0..<3 { await pipeline.process(frame, region: CropRegion()); await pipeline.waitForTranslations() }
        XCTAssertEqual(sender.sent, ["TR: Hold the gate"])
        XCTAssertEqual(pipeline.lastPair?.source, "Hold the gate")
    }

    func testEachSentLineIsHandedToDubbingOnce() async {
        let sender = FakeSender()
        let translator = SpeakingTranslator()
        let pipeline = SubtitlePipeline(reader: FakeReader(["Hold the gate", "Hold the gate", "Hold the gate"]),
                                        translator: translator, server: sender)
        var spoken: [[SpokenLine]] = []
        pipeline.onSpeech = { spoken.append($0) }
        for _ in 0..<3 { await pipeline.process(frame, region: CropRegion()); await pipeline.waitForTranslations() }
        XCTAssertEqual(spoken, [[SpokenLine(english: "Hold the gate", turkish: "TR: Hold the gate")]])
    }

    func testTtsFormEncodesSpecialCharacters() {
        XCTAssertEqual(PocketTTSServer.form(["text": "[kızgın] dur & bekle = 1+1", "voice_url": "http://127.0.0.1:8787/voices/male_1.wav"]),
                       "text=%5Bk%C4%B1zg%C4%B1n%5D%20dur%20%26%20bekle%20%3D%201%2B1&voice_url=http%3A%2F%2F127.0.0.1%3A8787%2Fvoices%2Fmale_1.wav")
    }

    func testButtonPromptsAreNotTranslated() async {
        let sender = FakeSender()
        let pipeline = SubtitlePipeline(reader: FakeReader(["R2 Swing", "R2 Swing", "Hold the gate\nR2 Swing", "Hold the gate\nR2 Swing"]),
                                        translator: FakeTranslator(), server: sender)
        for _ in 0..<4 { await pipeline.process(frame, region: CropRegion()); await pipeline.waitForTranslations() }
        XCTAssertEqual(sender.sent, ["TR: Hold the gate"])
    }

    func testSubtitleBoxIsSentInFrameCoordinates() async {
        let sender = FakeSender()
        let reading = Reading(lines: [TextLine("Hold the gate.", box: TextBox(left: 0.25, top: 0.5, right: 0.75, bottom: 1))])
        let pipeline = SubtitlePipeline(reader: LinesReader(reading), translator: FakeTranslator(), server: sender)
        await pipeline.process(frame, region: CropRegion(top: 0.6, bottom: 1.0, left: 0.1, right: 0.9))
        await pipeline.waitForTranslations()
        let box = try? XCTUnwrap(sender.boxes.first ?? nil)
        XCTAssertEqual(box?.left ?? 0, 0.3, accuracy: 1e-9)
        XCTAssertEqual(box?.top ?? 0, 0.8, accuracy: 1e-9)
        XCTAssertEqual(box?.right ?? 0, 0.7, accuracy: 1e-9)
        XCTAssertEqual(box?.bottom ?? 0, 1.0, accuracy: 1e-9)
    }

    func testTextRecognitionRunsOffTheMainThread() async {
        let reader = FakeReader(["Hold the gate"])
        let pipeline = SubtitlePipeline(reader: reader, translator: FakeTranslator(), server: FakeSender())
        await pipeline.process(frame, region: CropRegion())
        XCTAssertEqual(reader.readOnMainThread, [false])
    }

    func testMissingLanguagePackSendsNothing() async {
        let sender = FakeSender()
        let translator = FakeTranslator()
        translator.error = TranslatorError.needsDownload
        let pipeline = SubtitlePipeline(reader: FakeReader(["Hold the gate", "Hold the gate"]), translator: translator, server: sender)
        for _ in 0..<2 { await pipeline.process(frame, region: CropRegion()); await pipeline.waitForTranslations() }
        XCTAssertTrue(sender.sent.isEmpty)
        XCTAssertEqual(pipeline.lastError, .needsDownload)
    }

    func testFailedTranslationIsRetriedWhileLineStaysOnScreen() async {
        let sender = FakeSender()
        let translator = FakeTranslator()
        translator.failuresLeft = 1
        let pipeline = SubtitlePipeline(reader: FakeReader(Array(repeating: "Hold the gate", count: 4)), translator: translator, server: sender)
        for _ in 0..<4 { await pipeline.process(frame, region: CropRegion()); await pipeline.waitForTranslations() }
        XCTAssertEqual(sender.sent, ["TR: Hold the gate"])
    }

    /// Kareler saniyede ~20 kez okunur; tek tük okunamayan kare altyazıyı TV'den silmemeli.
    func testShortReadingGapDoesNotClearTheTV() async {
        let sender = FakeSender()
        let pipeline = SubtitlePipeline(reader: FakeReader(["Hold the gate", "Hold the gate", "", "", "Hold the gate"]),
                                        translator: FakeTranslator(), server: sender)
        for _ in 0..<5 { await pipeline.process(frame, region: CropRegion()); await pipeline.waitForTranslations() }
        XCTAssertEqual(sender.clears, 0)
        XCTAssertEqual(sender.sent, ["TR: Hold the gate"])
    }

    func testSubtitleGoneForSixFramesClearsTheTV() async {
        let sender = FakeSender()
        let pipeline = SubtitlePipeline(reader: FakeReader(["Hold the gate", "Hold the gate", "", "", "", "", "", ""]),
                                        translator: FakeTranslator(), server: sender)
        for _ in 0..<8 { await pipeline.process(frame, region: CropRegion()); await pipeline.waitForTranslations() }
        XCTAssertEqual(sender.clears, 1)
    }

    /// Bölge değişmiyorsa yazı tanıma çalışmaz; işlemci boşa harcanmaz.
    func testUnchangedRegionIsNotReadAgain() async {
        let reader = FakeReader(Array(repeating: "Hold the gate", count: 8))
        let pipeline = SubtitlePipeline(reader: reader, translator: FakeTranslator(), server: FakeSender(), scheduler: ReadScheduler())
        for _ in 0..<5 { await pipeline.process(frame, region: CropRegion()); await pipeline.waitForTranslations() }
        XCTAssertEqual(reader.readOnMainThread.count, 1)
    }

    func testSignatureNoticesAChangedRegion() throws {
        func image(_ gray: CGFloat) -> CGImage {
            let context = CGContext(data: nil, width: 640, height: 120, bitsPerComponent: 8, bytesPerRow: 0,
                                    space: CGColorSpaceCreateDeviceGray(), bitmapInfo: CGImageAlphaInfo.none.rawValue)!
            context.setFillColor(gray: gray, alpha: 1)
            context.fill(CGRect(x: 0, y: 0, width: 640, height: 120))
            return context.makeImage()!
        }
        XCTAssertEqual(RegionCropper.signature(of: image(0.2)), RegionCropper.signature(of: image(0.2)))
        XCTAssertNotEqual(RegionCropper.signature(of: image(0.2)), RegionCropper.signature(of: image(0.8)))
        XCTAssertEqual(RegionCropper.signature(of: image(0.2)).count, 64 * 12)
    }

    /// Çeviri sürerken okuma durmamalı; yoksa kısa satırlar kaçar.
    func testReadingContinuesWhileTranslating() async {
        let reader = FakeReader(["Hold the gate", "Hold the gate", "Run", "Run"])
        let translator = GatedTranslator()
        let pipeline = SubtitlePipeline(reader: reader, translator: translator, server: FakeSender())
        // İlk satırın çevirisi kapıda beklerken ikinci satır okunup çeviriye gönderilmiş olmalı.
        for _ in 0..<4 { await pipeline.process(frame, region: CropRegion()) }
        translator.open()
        await pipeline.waitForTranslations()
        XCTAssertEqual(translator.requested, ["Hold the gate", "Run"])
    }

    /// Hızlı konuşmada yeni satır gelse de önceki satırın çevirisi gösterilir; satır kaybolmaz.
    func testEarlierLineIsShownWhenItsTranslationArrivesFirst() async {
        let sender = FakeSender()
        let translator = GatedTranslator()
        let pipeline = SubtitlePipeline(reader: FakeReader(["Hold the gate.", "Run!"]), translator: translator, server: sender)
        for _ in 0..<2 { await pipeline.process(frame, region: CropRegion()) }
        await translator.release("Hold the gate.")
        await translator.release("Run!")
        await pipeline.waitForTranslations()
        XCTAssertEqual(sender.sent, ["TR: Hold the gate.", "TR: Run!"])
    }

    /// Yeni satırın çevirisi zaten gösterildiyse eski satırınki onun üstüne yazılmaz.
    func testOlderTranslationArrivingAfterNewerIsDropped() async {
        let sender = FakeSender()
        let translator = GatedTranslator()
        let pipeline = SubtitlePipeline(reader: FakeReader(["Hold the gate.", "Run!"]), translator: translator, server: sender)
        for _ in 0..<2 { await pipeline.process(frame, region: CropRegion()) }
        await translator.release("Run!")
        await translator.release("Hold the gate.")
        await pipeline.waitForTranslations()
        XCTAssertEqual(sender.sent, ["TR: Run!"])
    }

    /// Satır ekrandan kalktıktan sonra çevirisi gelirse yine de okunacak kadar kısa süre gösterilir.
    func testTranslationArrivingAfterLineLeftIsShownBriefly() async {
        let sender = FakeSender()
        let translator = GatedTranslator()
        let pipeline = SubtitlePipeline(reader: FakeReader(["Hold the gate.", "", "", "", "", "", ""]), translator: translator, server: sender)
        for _ in 0..<7 { await pipeline.process(frame, region: CropRegion()) }
        XCTAssertEqual(sender.clears, 1)
        translator.open()
        await pipeline.waitForTranslations()
        XCTAssertEqual(sender.sent, ["TR: Hold the gate."])
        XCTAssertEqual(sender.durations.first ?? 0, 2000 + "TR: Hold the gate.".count * 60)
    }

    /// Çeviri durdurulduysa geç gelen çeviri TV'ye gönderilmez.
    func testTranslationArrivingAfterStopIsDropped() async {
        let sender = FakeSender()
        let translator = GatedTranslator()
        let pipeline = SubtitlePipeline(reader: FakeReader(["Hold the gate", "Hold the gate"]), translator: translator, server: sender)
        for _ in 0..<2 { await pipeline.process(frame, region: CropRegion()) }
        pipeline.reset()
        translator.open()
        await pipeline.waitForTranslations()
        XCTAssertTrue(sender.sent.isEmpty)
    }

    func testShortLinesGetAZoomedSecondReading() throws {
        let image = frame
        let stripWidth = Int((Double(image.width) * 0.4).rounded()) * 2
        let text = try VisionReader.firstReading(of: image) { candidate in
            // Yalnız ortası büyütülmüş görüntüde okunabilen kısa satır.
            candidate.width == stripWidth ? "Go!" : ""
        }
        XCTAssertEqual(text, "Go!")
    }

    /// İki yedek okuma da yazı bulursa ortadaki şeridinki (daha güvenilir olanı) seçilir.
    func testCenterStripReadingWinsOverZoomedRegion() throws {
        let image = frame
        let text = try VisionReader.firstReading(of: image) { candidate in
            switch candidate.width {
            case image.width: ""
            case image.width * 2: "G0!"
            default: "Go!"
            }
        }
        XCTAssertEqual(text, "Go!")
    }

    func testFullReadingIsUsedWhenItFindsText() throws {
        let calls = Counter()
        let text = try VisionReader.firstReading(of: frame) { _ in calls.increment(); return "Hold the gate" }
        XCTAssertEqual(text, "Hold the gate")
        XCTAssertEqual(calls.value, 1)
    }

    /// Bölge boşken iki yedek okuma art arda değil aynı anda yapılır; yeni satır daha erken fark edilir.
    func testFallbackReadingsRunAtTheSameTime() throws {
        let image = frame
        let start = Date()
        _ = try VisionReader.firstReading(of: image) { candidate in
            if candidate.width != image.width { Thread.sleep(forTimeInterval: 0.2) }
            return ""
        }
        XCTAssertLessThan(Date().timeIntervalSince(start), 0.35)
    }

    func testVisionReadsSubtitleButNotHud() throws {
        let url = try XCTUnwrap(Bundle(for: Self.self).url(forResource: "oyun-karesi", withExtension: "png"))
        let source = try XCTUnwrap(CGImageSourceCreateWithURL(url as CFURL, nil))
        let image = try XCTUnwrap(CGImageSourceCreateImageAtIndex(source, 0, nil))
        let text = try VisionReader().read(RegionCropper.crop(image, to: CropRegion(top: 0.6, bottom: 0.99)))
        XCTAssertTrue(text.contains("We do not have time for this, boy"), text)
        XCTAssertFalse(text.contains("24 / 120"), text)
    }
}
