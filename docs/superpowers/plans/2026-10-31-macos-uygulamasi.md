# macOS Uygulaması (Adım 0–1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** PS5 oyunlarındaki İngilizce altyazıyı ikinci bir PSN hesabıyla kurulan Remote Play bağlantısından okuyup Türkçesini LG TV'de oyunun üstünde gösteren, kurulum sihirbazlı, TR/EN arayüzlü macOS uygulamasının ilk beta sürümü.

**Architecture:** Platformdan bağımsız mantık (metin eşleştirme, altyazı kararlılığı, yerel SSE sunucusu, LG eşleme) `core/` altındaki bir Swift paketinde; Remote Play bağlantısı chiaki-ng'nin C kütüphanesi (`libchiaki`) üstüne ince bir Swift sarmalayıcı; macOS uygulaması (SwiftUI) bunları birleştirir. TV tarafı mevcut webOS uygulamasıdır; uygulama onu LG'nin SSAP arayüzüyle kendi adresini vererek başlatır.

**Tech Stack:** Swift 6 araç zinciri (dil modu 5), SwiftUI, Network.framework, Vision, Translation, VideoToolbox, WebKit, XcodeGen; chiaki-ng v1.10.0 `libchiaki` (C, CMake, OpenSSL); webOS web uygulaması (ES5).

**Spec:** `docs/superpowers/specs/2026-10-31-oyun-altyazi-uygulamasi-design.md`

## Global Constraints

- macOS 26.0 ve üstü (arayüzsüz `TranslationSession(installedSource:target:)` için). Apple Silicon öncelikli.
- Okuma ve çeviri tamamen cihaz üstü; ağ trafiği yalnız yerel ağ (PS5, TV) ve PSN giriş/token uçları.
- Arayüz Türkçe ve İngilizce (String Catalog `Localizable.xcstrings`); TV uygulaması da iki dilli.
- Uygulama adında "PlayStation" / "PS5" geçmez. Ad `GameTranslator` (kullanıcı onayladı); macOS paket kimliği `com.gametranslator.mac`, TV `com.gametranslator.tv`; GitHub `Sefflex/gametranslator`; ad tek bir yerde tanımlanır (`AppIdentity.swift`, `tv-webos/appinfo.json`).
- Lisans AGPL-3.0; her yeni kaynak dosyada SPDX başlığı `// SPDX-License-Identifier: AGPL-3.0-only`. chiaki-ng'nin `COPYING` ve telif bildirimleri korunur; "Hakkında" ekranında atıf.
- Kod tanımlayıcıları İngilizce, kullanıcıya görünen metinler TR/EN katalogda (açık kaynak katkıcılar için; kararım).
- Remote Play oturumu: ses kapalı (`CHIAKI_AUDIO_DISABLED`), kol girdisi gönderilmez, H.264 1080p 30 fps.
- Okuma: ~4 kare/sn; varsayılan bölge dikey 0.62–0.97, yatay 0.14–0.86; iki ardışık benzer okuma = kararlı; benzerlik eşiği 0.85; "zaten hedef dilde" eşiği 0.90; 2 boş kare = altyazı kalktı. TV'de altyazı süresi 15 sn (kalkma mesajı gelmezse).
- Hedef gecikme: oyundaki altyazı belirdikten sonra ~1,5 sn içinde TV'de.

## Review Focus

1. **PS5 kapalı ya da dinlenme modunda** → uygulama uyandırmayı dener (`chiaki_discovery_wakeup`), olmazsa "PS5'i açın" der; çökmez, sonsuz döngüye girmez. (Task 8)
2. **Ağ kopması / Mac uykudan uyanma / PS5 oturumu düşmesi** → oturum kendiliğinden yeniden kurulur, TV'deki altyazı temizlenir. (Task 8)
3. **TV kendi HDMI girişine geçer (SimpLink/CEC)** → uygulama bunu fark edip TV uygulamasını yeniden öne getirir. (Task 5)
4. **Çeviri dil paketi yüklü değil** → kullanıcıya indirme yolu gösterilir; okuma hattı çökmez, metin gönderilmez. (Task 9)
5. **Yanıp sönen HUD yazısı / OCR gürültüsü** → aynı altyazı tekrar tekrar gönderilmez, TV'de titreme olmaz. (Task 2)

---

### Task 0: Kapı testi — ikinci hesap ve TV gecikmesi (kod yok)

**Files:**
- Modify: `BENIOKU.md` (sonuçlar "Doğrulananlar" altına)

- [ ] **Step 1:** Kullanıcı ücretsiz ikinci PSN hesabı açar, PS5'e kullanıcı olarak ekler, o kullanıcı için Uzaktan Oynatma'yı açar.
- [ ] **Step 2:** chiaki-ng'de PS5 ikinci hesapla yeniden kaydedilir (PSN Login + PS5'ten yeni 8 haneli kod).
- [ ] **Step 3:** Kullanıcı kendi hesabı ve koluyla oyundayken chiaki-ng ile bağlanılır. Geçme ölçütü: kol bağlı kalır, oyun duraklamaz, TV'de kalıcı ekran kalmaz, chiaki-ng'de görüntü engelsiz akar. Kısa bildirim kabul.
- [ ] **Step 4:** Aynı oturumda TV uygulaması (HDMI uygulama içinde) ile normal HDMI girişi karşılaştırılır: tuş gecikmesi, HDR/oyun modu, ses. Kullanıcı belirgin fark hissetmemeli.
- [ ] **Step 5:** Sonuçlar `BENIOKU.md`'ye yazılır. **Herhangi biri kalırsa plan durur** ve tasarım kullanıcıyla yeniden konuşulur.

### Task 1: Depo iskeleti ve metin eşleştirme

**Files:**
- Create: `LICENSE` (AGPL-3.0 tam metni), `README.md` (TR/EN kısa tanıtım), `.gitignore` (mevcut + `build/`, `*.xcodeproj/xcuserdata`, `third-party/build/`)
- Move: prototip dosyaları `prototip/` altına (`okuyucu/`, `sunucu/`, `araclar/`, `kur.sh`, `ornekler/`, `BENIOKU.md`); `tv-webos/` yerinde kalır
- Create: `core/Package.swift` (paket `GameTranslatorCore`, platformlar `.macOS(.v26), .iOS(.v26)`)
- Create: `core/Sources/GameTranslatorCore/TextMatch.swift`
- Test: `core/Tests/GameTranslatorCoreTests/TextMatchTests.swift`

**Interfaces:**
- Produces: `enum TextMatch { static func normalize(_ s: String) -> String; static func isSimilar(_ a: String, _ b: String, threshold: Double = 0.85) -> Bool }` — davranış prototipteki `sade` / `benzer` ile aynı (küçük harf, yalnız harf/rakam, tek boşluk; Levenshtein oranı).

- [ ] **Step 1:** `git init`, prototipi taşı, `LICENSE`/`README.md`/`.gitignore` ekle.
- [ ] **Step 2: Failing tests**

```swift
func testNormalizeStripsPunctuationAndCase() {
    XCTAssertEqual(TextMatch.normalize("Kratos: We do NOT have time, boy."), "kratos we do not have time boy")
}
func testSimilarToleratesOcrNoise() {
    XCTAssertTrue(TextMatch.isSimilar("the gate will not hold", "the gale will not hold"))
}
func testDifferentLinesAreNotSimilar() {
    XCTAssertFalse(TextMatch.isSimilar("we do not have time", "the gate will not hold"))
}
func testEmptyIsNeverSimilarToText() {
    XCTAssertFalse(TextMatch.isSimilar("", "abc"))
}
```

- [ ] **Step 3:** `cd core && swift test` → FAIL (tanımsız).
- [ ] **Step 4:** `TextMatch` yaz (prototip `okuyucu/main.swift` içindeki `sade`/`benzer` taşınır).
- [ ] **Step 5:** `swift test` → PASS.
- [ ] **Step 6:** Commit: `chore: depo iskeleti, AGPL lisansı, TextMatch`

### Task 2: Altyazı kararlılık makinesi

**Files:**
- Create: `core/Sources/GameTranslatorCore/SubtitleStabilizer.swift`
- Test: `core/Tests/GameTranslatorCoreTests/SubtitleStabilizerTests.swift`

**Interfaces:**
- Consumes: `TextMatch`
- Produces:

```swift
public enum StabilizerAction: Equatable { case none, translate(String), clear }
public struct SubtitleStabilizer {
    public init(similarity: Double = 0.85, stableFrames: Int = 2, emptyFramesToClear: Int = 2)
    /// Her OCR karesi için çağrılır; okunan ham metni alır.
    public mutating func feed(_ rawText: String) -> StabilizerAction
    /// Çeviri bittiğinde çağrılır; çeviri kaynağıyla aynıysa false döner (gönderme).
    public mutating func shouldSend(source: String, translation: String) -> Bool  // eşik 0.90
    public mutating func reset() -> StabilizerAction   // gönderilmiş bir şey varsa .clear
}
```

- [ ] **Step 1: Failing tests**

```swift
func testEmitsAfterTwoStableFrames() {
    var s = SubtitleStabilizer()
    XCTAssertEqual(s.feed("We do not have time."), .none)
    XCTAssertEqual(s.feed("We do not have time."), .translate("We do not have time."))
    XCTAssertEqual(s.feed("We do not have time."), .none)
}
func testOcrNoiseDoesNotReemit() {
    var s = SubtitleStabilizer()
    _ = s.feed("The gate will not hold"); _ = s.feed("The gate will not hold")
    XCTAssertEqual(s.feed("The gale will not hold"), .none)
    XCTAssertEqual(s.feed("The gale will not hold"), .none)
}
func testFlickeringHudDoesNotSpam() {           // Review Focus 5
    var s = SubtitleStabilizer()
    var sent = 0
    for i in 0..<20 { if case .translate = s.feed(i % 2 == 0 ? "24 / 120" : "") { sent += 1 } }
    XCTAssertEqual(sent, 0)
}
func testTypewriterSendsCompletedLine() {
    var s = SubtitleStabilizer()
    _ = s.feed("We do"); _ = s.feed("We do not have")
    _ = s.feed("We do not have time for this, boy.")
    XCTAssertEqual(s.feed("We do not have time for this, boy."), .translate("We do not have time for this, boy."))
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
```

Not: "24 / 120" normalize edilince 3 karakterden kısa değil; boş karelerle dönüşümlü geldiğinde iki ardışık eşit okuma oluşmadığı için gönderilmez. Metin < 3 normalize karakter ise boş sayılır.

- [ ] **Step 2:** `swift test --filter SubtitleStabilizerTests` → FAIL.
- [ ] **Step 3:** Uygula (prototipteki ana döngü mantığı: aday/tekrar sayacı, gönderilen, boş kare sayacı).
- [ ] **Step 4:** → PASS.
- [ ] **Step 5:** Commit: `feat(core): altyazı kararlılık makinesi`

### Task 3: Yerel altyazı sunucusu (SSE)

**Files:**
- Create: `core/Sources/GameTranslatorCore/SubtitleServer.swift`
- Test: `core/Tests/GameTranslatorCoreTests/SubtitleServerTests.swift`

**Interfaces:**
- Produces:

```swift
public struct SubtitleStyle: Codable, Equatable { public var fontSize: Int /*36|46|58*/; public var bottomPercent: Int /*varsayılan 7*/ }
public final class SubtitleServer {
    public init(port: UInt16 = 8787)                 // 0 = rastgele (testler)
    public func start() async throws -> UInt16       // dinlenen port
    public func stop()
    public var connectedClients: Int { get }
    public func sendSubtitle(_ text: String, durationMs: Int = 15000)
    public func clear()
    public func sendStyle(_ style: SubtitleStyle)
    public var onClientsChanged: ((Int) -> Void)?
}
```

- Tel formatı prototip `sunucu/server.mjs` ile aynı: `GET /events` → `text/event-stream`, `Access-Control-Allow-Origin: *`, olaylar `subtitle {text,duration,at}`, `clear {}`, `style {fontSize,bottomPercent}`, 15 sn'de bir `: ping`. Yeni bağlanan istemciye son `style` hemen gönderilir. Network.framework `NWListener` ile.

- [ ] **Step 1: Failing tests** — rastgele portta başlat; `URLSession.bytes(from: http://127.0.0.1:<port>/events)` ile bağlan; `connectedClients == 1` olana kadar bekle; `sendSubtitle("Merhaba", durationMs: 4000)` → akıştan `event: subtitle` ve `"text":"Merhaba"` içeren `data:` satırı okunur; `clear()` → `event: clear`; yanıt başlığında `Access-Control-Allow-Origin: *`; istemci kapanınca `connectedClients == 0`.
- [ ] **Step 2:** → FAIL. **Step 3:** Uygula. **Step 4:** → PASS.
- [ ] **Step 5:** Commit: `feat(core): yerel SSE altyazı sunucusu`

### Task 4: TV uygulaması — başlatma parametreleri, stil, iki dil

**Files:**
- Modify: `tv-webos/app.js`, `tv-webos/index.html`, `tv-webos/style.css`, `tv-webos/appinfo.json`
- Create: `tv-webos/i18n.js` (TR/EN metinleri; dil `navigator.language` ile)

**Interfaces:**
- Consumes: Task 3 olayları.
- Produces: TV uygulama kimliği `com.gametranslator.tv` (`appinfo.json` `id`, `title: "GameTranslator"`); başlatma parametresi şeması `{"server":"http://<ip>:<port>","hdmi":<1-4, opsiyonel>}`.

- [ ] **Step 1:** Sunucu adresi önceliği: `PalmSystem.launchParams` (JSON) → `webOSRelaunch` olayının `detail`'i (yeniden başlatmada sunucuyu değiştirip yeniden bağlanır) → `config.js` (geliştirme). Adres yoksa ekranda i18n'li "Bilgisayardaki/telefondaki uygulamayı açın" mesajı.
- [ ] **Step 2:** `style` olayı → `#subtitle-text` yazı boyutu ve `#subtitle` alt boşluğu.
- [ ] **Step 3:** Geliştirme panelindeki tüm metinler `i18n.js`'den.
- [ ] **Step 4: Doğrulama (TV'de):** `ares-package` + `ares-install` + `ares-launch -d tv com.gametranslator.tv --params '{"server":"http://<mac-ip>:8787"}'`; prototip sunucusu (`prototip/sunucu/server.mjs`) ile altyazı gönder → TV'de görünür; `prototip/araclar/tv-eval.mjs "PalmSystem.launchParams"` parametreyi gösterir. Parametresiz başlatmada mesaj görünür.
- [ ] **Step 5:** Commit: `feat(tv): başlatma parametresiyle sunucu, stil olayı, TR/EN`

### Task 5: LG eşleme ve öne getirme (SSDP + SSAP)

**Files:**
- Create: `core/Sources/GameTranslatorCore/LGTV/LGDiscovery.swift`, `core/Sources/GameTranslatorCore/LGTV/SSAPClient.swift`, `core/Sources/GameTranslatorCore/LGTV/SSAPMessages.swift`
- Test: `core/Tests/GameTranslatorCoreTests/SSAPMessagesTests.swift`, `core/Tests/GameTranslatorCoreTests/SSAPClientTests.swift`

**Interfaces:**
- Produces:

```swift
public struct LGTV: Codable, Hashable { public let name: String; public let host: String }
public enum LGDiscovery { public static func search(timeout: Duration = .seconds(3)) async -> [LGTV] }
// SSDP M-SEARCH ST: "urn:lge-com:service:webos-second-screen:1", 239.255.255.250:1900
public final class SSAPClient {
    public init(tv: LGTV, clientKey: String?)          // önce wss://host:3001 (self-signed kabul), olmazsa ws://host:3000
    public func connect() async throws -> String         // register; TV'de onay istenir; client-key döner
    public func launch(appId: String, params: [String: Any]) async throws
    public func foregroundAppId() async throws -> String // ssap://com.webos.applicationManager/getForegroundAppInfo
}
enum SSAPMessages {
    static func register(clientKey: String?) -> Data     // pairingType "PROMPT", manifest izinleri: LAUNCH, READ_RUNNING_APPS, APP_TO_APP
    static func request(id: String, uri: String, payload: [String: Any]?) -> Data
}
```

- [ ] **Step 1: Failing tests** — `SSAPMessagesTests`: `register(clientKey: nil)` JSON'unda `type == "register"`, `payload.pairingType == "PROMPT"`, izinler `LAUNCH` ve `READ_RUNNING_APPS` içerir; `clientKey` verilince `payload["client-key"]` dolu; `request(..., uri: "ssap://system.launcher/launch", payload: ["id": "com.gametranslator.tv", "params": ["server": "http://1.2.3.4:8787"]])` doğru alanları içerir. `SSAPClientTests`: yerel bir `NWListener` WebSocket sahte TV'si `registered` + `client-key` yanıtı verir → `connect()` anahtarı döner; `launch` sahte TV'ye beklenen URI'yi yollar.
- [ ] **Step 2:** → FAIL. **Step 3:** Uygula (`URLSessionWebSocketTask`; 3001'de sertifika doğrulaması yalnız bu bağlantı için atlanır).
- [ ] **Step 4:** → PASS.
- [ ] **Step 5: Öne getirme (Review Focus 3)** — `public final class TVKeeper { init(client: SSAPClient, appId: String, params: [String: Any]); func start(interval: Duration = .seconds(5)); func stop() }`: önplandaki uygulama `appId` değilse `launch` eder. Test: sahte TV önce `com.webos.app.hdmi2` sonra `com.gametranslator.tv` döndürür → tam bir `launch` çağrısı yapılır.
- [ ] **Step 6: Doğrulama (TV'de):** küçük bir test hedefiyle LG 55UK7550'yi bul, eşle (TV'de onay), `com.gametranslator.tv`'yi parametreyle başlat; TV'yi kumandayla HDMI 2'ye geçir → ≤ 5 sn içinde uygulama yeniden öne gelir.
- [ ] **Step 7:** Commit: `feat(core): LG TV bulma, eşleme, başlatma, öne getirme`

### Task 6: libchiaki'yi macOS için derleme

**Files:**
- Create: `third-party/chiaki-ng` (git submodule, etiket `v1.10.0`, commit `0c4a45df0cae2af2ba2daef84e881850b07038a3`)
- Create: `scripts/build-chiaki-macos.sh` → çıktı `third-party/build/macos/{include,lib}` (statik `libchiaki.a` + bağımlılıklar)
- Create: `remoteplay/Package.swift` (paket `RemotePlay`; `CChiaki` system/binary hedefi `module.modulemap` ile, `RemotePlay` Swift hedefi)
- Test: `remoteplay/Tests/RemotePlayTests/LinkTests.swift`

**Interfaces:**
- Produces: Swift'ten `import CChiaki` ile `chiaki_*` fonksiyonları.

- [ ] **Step 1:** CMake seçenekleri: `CHIAKI_ENABLE_GUI=OFF CHIAKI_ENABLE_CLI=OFF CHIAKI_ENABLE_TESTS=OFF CHIAKI_ENABLE_FFMPEG_DECODER=OFF CHIAKI_ENABLE_PI_DECODER=OFF CHIAKI_LIB_ENABLE_OPUS=OFF CHIAKI_ENABLE_SPEEX=OFF CHIAKI_ENABLE_RUDP=OFF CHIAKI_ENABLE_SETSU=OFF CHIAKI_ENABLE_STEAMDECK_NATIVE=OFF CHIAKI_ENABLE_STEAM_SHORTCUT=OFF`, `CMAKE_OSX_DEPLOYMENT_TARGET=26.0`, `arm64`. OpenSSL statik bağlanır (Homebrew'ünkü değil, betik içinde derlenen ya da `CHIAKI_LIB_OPENSSL_EXTERNAL_PROJECT=ON`). Seçeneklerden biri çekirdeği kırıyorsa açık bırakılır ve nedeni betikte yorum olarak yazılır.
- [ ] **Step 2: Failing test** — `XCTAssertEqual(String(cString: chiaki_rp_version_string(CHIAKI_TARGET_PS5_1)), <beklenen sürüm dizgesi>)` (beklenen değer `lib/src/session.c` içinden okunur).
- [ ] **Step 3:** Betiği çalıştır, `cd remoteplay && swift test` → PASS.
- [ ] **Step 4:** Commit: `build: libchiaki macOS statik derleme ve CChiaki modülü`

### Task 7: PSN hesap kimliği

**Files:**
- Create: `remoteplay/Sources/RemotePlay/PSNAccount.swift`
- Test: `remoteplay/Tests/RemotePlayTests/PSNAccountTests.swift`

**Interfaces:**
- Produces:

```swift
public enum PSNAccount {
    public static let authorizeURL: URL   // chiaki-ng gui/include/psnaccountid.h'deki adres, CLIENT_ID ile
    public static let redirectPrefix = "https://remoteplay.dl.playstation.net/remoteplay/redirect"
    public static func code(fromRedirect url: URL) -> String?        // "code" sorgu parametresi
    public static func accountId(code: String) async throws -> Data  // 8 bayt
    static func accountIdBytes(userId: String) -> Data?              // ondalık user_id → 8 bayt little-endian
}
```

- Token: `POST https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/token`, Basic auth (chiaki-ng'deki CLIENT_ID/CLIENT_SECRET), gövde chiaki-ng `psnaccountid.cpp` ile aynı. Kimlik: `GET <TOKEN_URL>/<access_token>` → `user_id`. Token saklanmaz.

- [ ] **Step 1: Failing tests** — `code(fromRedirect: URL(string: redirectPrefix + "?code=AbC123&cid=x")!) == "AbC123"`; redirect olmayan URL → `nil`; `accountIdBytes(userId: "1")!.base64EncodedString() == "AQAAAAAAAAA="`; geçersiz `user_id` → `nil`.
- [ ] **Step 2:** → FAIL. **Step 3:** Uygula. **Step 4:** → PASS.
- [ ] **Step 5:** Commit: `feat(remoteplay): PSN hesap kimliği`

### Task 8: PS5 bulma, kayıt, oturum ve görüntü çözme

**Files:**
- Create: `remoteplay/Sources/RemotePlay/Console.swift` (bulma + uyandırma), `Registration.swift`, `StreamSession.swift`, `H264FrameDecoder.swift`
- Test: `remoteplay/Tests/RemotePlayTests/H264FrameDecoderTests.swift`, `remoteplay/Tests/RemotePlayTests/StreamSessionTests.swift`
- Create: `remoteplay/Tests/RemotePlayTests/Fixtures/sample-1080p.h264` (birkaç karelik Annex-B örnek; `ffmpeg` ile üretilen, depoya giren küçük dosya)

**Interfaces:**
- Consumes: `CChiaki`, `PSNAccount`
- Produces:

```swift
public struct ConsoleInfo: Codable, Hashable { public let host: String; public let name: String; public let isAwake: Bool }
public enum Console {
    public static func discover(timeout: Duration = .seconds(3)) async -> [ConsoleInfo]
    public static func wake(_ console: ConsoleInfo, credential: UInt64) async throws
}
public struct RegisteredConsole: Codable { public let host: String; public let name: String; public let registKey: Data; public let morning: Data /*16*/; public let credential: UInt64 }
public enum Registration { public static func register(host: String, accountId: Data, pin: UInt32) async throws -> RegisteredConsole }
public enum SessionState: Equatable { case connecting, streaming, disconnected(reason: String) }
public final class StreamSession {
    public init(console: RegisteredConsole, accountId: Data)
    public var onFrame: ((CGImage) -> Void)?           // en fazla ~4/sn iletilir (arada kalanlar atılır)
    public var onState: ((SessionState) -> Void)?
    public func start() throws
    public func stop()
}
final class H264FrameDecoder { func decode(annexB: Data) -> CGImage? }   // VideoToolbox; SPS/PPS'ten format
```

- `ChiakiConnectInfo`: `ps5 = true`, `audio_video_disabled = CHIAKI_AUDIO_DISABLED`, `chiaki_connect_video_profile_preset(..., 1080p, 30fps)`, `codec = CHIAKI_CODEC_H264`, `enable_keyboard = false`, `enable_dualsense = false`, `video_profile_auto_downgrade = true`. Kayıt bilgisi Anahtarlık'ta saklanır (Task 9 yapar).
- Yeniden bağlanma (Review Focus 2): `disconnected` sonrası 2, 4, 8… en çok 30 sn aralıkla yeniden dener; Mac uykudan uyanınca (`NSWorkspace.didWakeNotification`) hemen dener.
- PS5 kapalı (Review Focus 1): `discover` PS5'i uyuyor görürse `wake` çağrılır, 20 sn içinde uyanmazsa `disconnected(reason: "console-asleep")`.

- [ ] **Step 1: Failing tests** — `H264FrameDecoderTests`: fixture'ın ilk IDR'ı → `CGImage` 1920×1080; bozuk veri → `nil`, çökme yok. `StreamSessionTests`: sahte bir video kaynağıyla (iç `ingest(sample:)` kancası) 30 kare/sn beslenince `onFrame` 1 sn'de ≤ 5 kez çağrılır; `stop()` sonrası çağrı yok; `disconnected` durumunda yeniden deneme zamanlayıcısı 2 sn ile başlar (zaman enjekte edilir).
- [ ] **Step 2:** → FAIL. **Step 3:** Uygula. **Step 4:** → PASS.
- [ ] **Step 5: Doğrulama (gerçek PS5):** küçük bir komut satırı hedefi (`remoteplay/Sources/rp-smoke/main.swift`) ikinci hesapla bağlanır, 10 sn'de alınan kare sayısını ve bir kareyi PNG olarak yazar. Kullanıcı kendi koluyla oynamaya devam edebilmeli.
- [ ] **Step 6:** Commit: `feat(remoteplay): PS5 bulma, kayıt, oturum, H.264 çözme`

### Task 9: Okuma hattı (OCR + çeviri) ve uygulama iskeleti

**Files:**
- Create: `mac/project.yml` (XcodeGen; hedef `GameTranslator`, macOS 26, `GameTranslatorCore` ve `RemotePlay` yerel paket bağımlılıkları, Ekran Kaydı gerekmez — görüntü Remote Play'den gelir)
- Create: `mac/GameTranslator/AppIdentity.swift` (ad, TV app id `com.gametranslator.tv`, sürüm)
- Create: `mac/GameTranslator/Pipeline/RegionCropper.swift`, `VisionReader.swift`, `Translator.swift`, `SubtitlePipeline.swift`
- Create: `mac/GameTranslator/Storage/SecureStore.swift` (Anahtarlık: `RegisteredConsole`, accountId, TV client-key)
- Test: `mac/GameTranslatorTests/SubtitlePipelineTests.swift`, `mac/GameTranslatorTests/RegionCropperTests.swift`
- Fixture: `prototip/ornekler/oyun-karesi.png`

**Interfaces:**
- Consumes: `SubtitleStabilizer`, `SubtitleServer`, `StreamSession.onFrame`
- Produces:

```swift
struct CropRegion: Codable, Equatable { var top = 0.62, bottom = 0.97, left = 0.14, right = 0.86 }
enum RegionCropper { static func crop(_ image: CGImage, to region: CropRegion) -> CGImage }
protocol TextReader { func read(_ image: CGImage) throws -> String }           // VisionReader: .accurate, dil "en"
protocol TextTranslator { func translate(_ text: String) async throws -> String }
enum TranslatorStatus { case ready, needsDownload, unsupported }
final class Translator: TextTranslator { init(source: String = "en", target: String = "tr"); static func status(source: String, target: String) async -> TranslatorStatus }
@MainActor final class SubtitlePipeline {
    init(reader: TextReader, translator: TextTranslator, server: SubtitleServer)
    func process(_ frame: CGImage, region: CropRegion) async
    var lastPair: (source: String, translation: String)? { get }
}
```

- [ ] **Step 1: Failing tests** — `RegionCropperTests`: 1600×1600 görüntü varsayılan bölgeyle → 1152×560 (±1). `SubtitlePipelineTests` (sahte okuyucu/çevirmen/sunucu): aynı metin iki kare → sunucuya tam bir `sendSubtitle` (çevirisiyle); çevirmen `needsDownload` hatası atınca hiçbir şey gönderilmez ve çökme olmaz (Review Focus 4); iki boş kare → `clear`. Gerçek Vision ile: fixture → okunan metin "We do not have time for this, boy" içerir, "24 / 120" içermez.
- [ ] **Step 2:** → FAIL. **Step 3:** Uygula (prototip `okuyucu/main.swift` mantığı; çeviri önbelleği `Translator` içinde). **Step 4:** → PASS (`xcodegen generate && xcodebuild test -scheme GameTranslator`).
- [ ] **Step 5:** Commit: `feat(mac): okuma hattı ve uygulama iskeleti`

### Task 10: Kurulum sihirbazı ve ana ekran (TR/EN)

**Files:**
- Create: `mac/GameTranslator/UI/Onboarding/{WelcomeStep,SecondAccountStep,PSNLoginStep,ConsoleStep,TVStep,TestStep}.swift`, `mac/GameTranslator/UI/OnboardingView.swift`
- Create: `mac/GameTranslator/UI/MainView.swift`, `mac/GameTranslator/UI/SettingsView.swift`, `mac/GameTranslator/UI/RegionPicker.swift`, `mac/GameTranslator/UI/AboutView.swift`
- Create: `mac/GameTranslator/AppModel.swift` (`@Observable`; durum: onboarding adımı, PS5/TV bağlantısı, son çeviri, ayarlar)
- Create: `mac/GameTranslator/Resources/Localizable.xcstrings` (tr, en), `mac/GameTranslator/Resources/Guides/` (ikinci hesap adımlarının ekran görüntüleri; kullanıcıdan alınır)
- Test: `mac/GameTranslatorTests/AppModelTests.swift`, `mac/GameTranslatorUITests/OnboardingFlowTests.swift`

**Interfaces:**
- Consumes: Task 5, 7, 8, 9'un tümü.
- Produces: `AppModel.Step` = `.welcome, .secondAccount, .psnLogin, .console, .tv, .test, .done`; `AppModel.advance()` ve her adımın ön koşulu.

Adımlar (spec §4):
1. Hoş geldin — gerekenler listesi.
2. İkinci hesap — tek cümle neden + ekran görüntülü adımlar + "Hazırım".
3. PSN girişi — `WKWebView` `PSNAccount.authorizeURL`'i açar; `redirectPrefix`'e gidiş yakalanır, kod alınır, kimlik çekilir; kullanıcı hiçbir adres kopyalamaz.
4. PS5 — `Console.discover`; tek PS5 varsa otomatik seçilir; 8 haneli kod alanı → `Registration.register`.
5. TV — `LGDiscovery.search`; tek TV otomatik; "TV'de çıkan izni onaylayın" → `SSAPClient.connect` → `launch(appId, params: ["server": ...])`.
6. Deneme — `SubtitleServer.sendSubtitle(<yerelleştirilmiş örnek cümle>)` + "TV'de görüyor musunuz?" Evet/Hayır (Hayır → TV adımına dön, ipucu).
Ana ekran: büyük Başlat/Durdur; PS5 ve TV durum noktaları; son İngilizce + Türkçe; ayarlar (bölge seçici — son kare üstünde sürüklenebilir çerçeve, yazı boyutu S/M/L, alt boşluk, kaynak dil). Hakkında: sürüm, AGPL, chiaki-ng atfı, kaynak kodu bağlantısı.

- [ ] **Step 1: Failing tests** — `AppModelTests`: kayıtlı PS5 + accountId + TV anahtarı varken açılış `.done`'a gider; `.psnLogin` accountId olmadan ilerlemez; `.console` kayıt olmadan ilerlemez; dil `tr`/`en` iki katalogda her anahtar için dolu (katalog tamlık testi). `OnboardingFlowTests`: sahte servislerle 6 adım baştan sona tıklanabilir.
- [ ] **Step 2:** → FAIL. **Step 3:** Uygula. **Step 4:** → PASS.
- [ ] **Step 5: Doğrulama (gerçek kurulum):** Anahtarlık temizlenmiş Mac'te kullanıcıyla birlikte sihirbaz baştan sona; ölçüt: hiçbir adımda adres kopyalama, IP yazma, terminal yok. Ardından gerçek oyunda bir konuşma sahnesi: Türkçe altyazı TV'de ≤ ~1,5 sn içinde.
- [ ] **Step 6:** Commit: `feat(mac): kurulum sihirbazı, ana ekran, TR/EN`

### Task 11: Beta paketleme ve belgeler

**Files:**
- Create: `scripts/package-mac.sh` (Release derleme → `dist/GameTranslator-<sürüm>-macos.zip`; `DEVELOPER_ID` ortam değişkeni varsa imzalar ve notarize eder, yoksa imzasız paketler ve uyarır)
- Create: `docs/tv-kurulum.md` (TR) ve `docs/tv-setup.md` (EN): LG geliştirici modu ile TV uygulamasını kurma (prototipte öğrenilenler: Key Server'ın kısa açık kalması, `webos_rsa` yolu), mağaza onayına kadar geçerli
- Modify: `README.md` (TR/EN: ne yapar, gereksinimler, kurulum, ikinci hesap neden, lisans/atıf, Sony ile ilişki yok)

- [ ] **Step 1:** `scripts/package-mac.sh` çalıştırılır → zip oluşur; zip açılıp uygulama başka bir kullanıcı hesabında açılır (imzasızsa "Yine de Aç" yolu README'de yazılı).
- [ ] **Step 2:** README ve TV rehberi bir kez baştan sona izlenerek doğrulanır.
- [ ] **Step 3:** Commit: `chore: beta paketleme ve kurulum belgeleri`

**Bu planın dışında (sonraki planlar):** Samsung Tizen TV uygulaması, LG/Samsung mağaza başvuruları (hesap kararı sonrası), Android ve iOS uygulamaları, GitHub'a yayın (hesap kararı sonrası).
