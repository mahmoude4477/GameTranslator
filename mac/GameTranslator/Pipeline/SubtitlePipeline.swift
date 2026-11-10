// SPDX-License-Identifier: AGPL-3.0-only

import CoreGraphics
import GameTranslatorCore
import Foundation

/// Altyazıları TV'ye ileten taraf (gerçekte `SubtitleServer`).
protocol SubtitleSending: AnyObject {
    /// `box`: oyundaki altyazının karedeki yeri (kenarlara oranla), bilinmiyorsa nil.
    func sendSubtitle(_ text: String, durationMs: Int, box: TextBox?)
    func clear()
}

extension SubtitleServer: SubtitleSending {}

/// Kare → bölge → yazı tanıma → kararlılık → çeviri → TV.
@MainActor
final class SubtitlePipeline {
    private let reader: TextReader
    private let translator: TextTranslator
    private let server: SubtitleSending
    /// Kareler saniyede ~20 kez okunur; yaklaşık 0,3 sn boş kalan bölge altyazının kalktığı anlamına gelir.
    private var stabilizer = SubtitleStabilizer(emptyFramesToClear: 6)
    private var scheduler: ReadScheduler
    /// Her çeviri isteği sıra numarası alır. Hızlı konuşmada çeviriler sırasız dönebilir:
    /// daha yeni bir satırın çevirisi gösterilmediyse eski satırınki yine gösterilir.
    private var lastRequested = 0
    private var lastShown = 0
    /// Şu an oyun ekranında duran satırın numarası (bölge boşalınca nil).
    private var lineOnScreen: Int?
    /// Okuma durdurulunca artar; önceki oturumdan dönen çeviriler gösterilmez.
    private var session = 0
    private var translations: [Int: Task<Void, Never>] = [:]
    /// Son okunan satırlar; kare değişmediyse yeniden okunmaz, bunlar kullanılır.
    private var lastReading = Reading.empty

    private(set) var lastPair: (source: String, translation: String)?
    private(set) var lastOrigin: String = "Makine Çevirisi"
    /// Son gösterilen çevirinin oyundaki altyazının yeri.
    private(set) var lastBox: TextBox?
    private(set) var lastError: TranslatorError?
    var onPair: ((String, String) -> Void)?
    var onPairWithOrigin: ((String, String, String) -> Void)?
    /// TV'ye giden her yeni altyazının seslendirilecek cümleleri (dublaj).
    var onSpeech: (([SpokenLine]) -> Void)?
    /// Her yazı tanıma denemesinden sonra çağrılır (başarılıysa nil). Disk dolunca macOS okumayı hep hata ile bitirir.
    var onReadResult: ((Error?) -> Void)?
    /// Her çeviri denemesinden sonra çağrılır (başarılıysa nil).
    var onTranslationResult: ((TranslatorError?) -> Void)?

    /// `scheduler`: varsayılanı her kareyi okur; uygulama gereksiz okumaları atlayan planı verir.
    init(reader: TextReader, translator: TextTranslator, server: SubtitleSending, scheduler: ReadScheduler = .always) {
        self.reader = reader
        self.translator = translator
        self.server = server
        self.scheduler = scheduler
    }

    /// Okuma çeviriyi beklemez; böylece çeviri sürerken gelen satırlar da yakalanır.
    func process(_ frame: CGImage, region: CropRegion) async {
        // Yazı tanıma uzun sürebilir; arayüz takılmasın diye ana iş parçacığının dışında yapılır.
        let reader = self.reader
        let (area, signature) = await Task.detached(priority: .userInitiated) {
            let area = RegionCropper.crop(frame, to: region)
            return (area, RegionCropper.signature(of: area))
        }.value
        switch scheduler.plan(signature: signature) {
        case .reuse:
            break
        case .read(let deep):
            let result = await Task.detached(priority: .userInitiated) {
                Result { try reader.readLines(area, deep: deep) }
            }.value
            lastReading = (try? result.get()) ?? .empty
            onReadResult?(result.failure)
            scheduler.record(lastReading.text)
        }
        // Kumanda ipuçları ("R2 Swing") altyazı değildir; çevrilmez, yeri de altyazının yerine katılmaz.
        // Yazı tanımanın uydurduğu sözcük çöpleri ("ou$3e") de çevirmene gitmez.
        // Alandaki menü ve köşe göstergeleri de altyazı değildir; yalnız altyazı bloğu çevrilir ve kutusu ona göre olur.
        let place = TextBox(left: region.left, top: region.top, right: region.right, bottom: region.bottom)
        let reading = lastReading.withoutButtonPrompts().withoutOcrNoise().subtitleBlock(in: place)
        switch stabilizer.feed(reading.text) {
        case .none:
            break
        case .clear:
            lineOnScreen = nil
            server.clear()
        case .translate(let source):
            lastRequested += 1
            let number = lastRequested, current = session
            lineOnScreen = number
            let box = reading.box?.placed(in: place).centeredOnScreen()
            translations[number] = Task { [weak self] in
                await self?.translate(source, box: box, number: number, session: current)
                self?.translations[number] = nil
            }
        }
    }

    /// Süren bütün çevirilerin bitmesini bekler.
    func waitForTranslations() async {
        while !translations.isEmpty {
            for task in Array(translations.values) { await task.value }
        }
    }

    /// Satır ekrandan kalktıktan sonra gelen çevirinin TV'de kalacağı süre (TV uygulamasıyla aynı kural).
    static func readingTime(for text: String) -> Int {
        min(8000, 2000 + text.count * 60)
    }

    private func translate(_ source: String, box: TextBox?, number: Int, session current: Int) async {
        do {
            let translated = try await translator.translate(source)
            guard current == session else { return }
            lastError = nil
            onTranslationResult?(nil)
            guard number > lastShown, stabilizer.shouldSend(source: source, translation: translated) else { return }
            lastShown = number
            // Satır hâlâ ekrandaysa kalkınca silinir; kalktıysa okunacak kadar gösterilir.
            server.sendSubtitle(translated, durationMs: number == lineOnScreen ? 15000 : Self.readingTime(for: translated), box: box)
            lastPair = (source, translated)
            lastBox = box
            let origin = CommunityPackManager.shared.lookup(source: source).map { "Topluluk (\($0.packName))" } ?? "Makine Çevirisi"
            lastOrigin = origin
            onPair?(source, translated)
            onPairWithOrigin?(source, translated, origin)
            if let onSpeech { onSpeech(translator.spokenLines(for: source)) }
        } catch {
            guard current == session else { return }
            // Dil paketi eksikse ya da geçici bir hata olduysa satır ekranda kaldıkça yeniden denenir.
            lastError = error as? TranslatorError
            onTranslationResult?(lastError)
            if number == lineOnScreen { stabilizer.forgetSent() }
        }
    }

    /// Okumanın hemen başlaması için modelleri önceden yükler; ilk okuma yoksa saniyelerce sürer.
    func warmUp() {
        let reader = self.reader
        Task.detached(priority: .utility) {
            let context = CGContext(data: nil, width: 64, height: 32, bitsPerComponent: 8, bytesPerRow: 0,
                                    space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
            if let image = context?.makeImage() { _ = try? reader.read(image) }
        }
        let translator = self.translator
        Task { _ = try? await translator.translate("Hello") }
    }

    /// Okuma durunca TV'deki son altyazıyı kaldırır.
    func reset() {
        session += 1
        lineOnScreen = nil
        lastReading = .empty
        scheduler.reset()
        if stabilizer.reset() == .clear { server.clear() }
    }
}

private extension Result {
    var failure: Failure? {
        if case .failure(let error) = self { return error }
        return nil
    }
}
