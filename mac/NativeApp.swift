// SPDX-License-Identifier: AGPL-3.0-only
import SwiftUI
import AppKit
import Network
import UniformTypeIdentifiers
import WebKit
import Darwin
import Vision
import Combine

// MARK: - App Delegate (Dock Icon, Persistent Window, Background Stability)

final class AppDelegate: NSObject, NSApplicationDelegate {
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
        let hasAccess = CGPreflightScreenCaptureAccess()
        try? "hasAccess: \(hasAccess)\n".write(toFile: "/tmp/screen_perm.txt", atomically: true, encoding: .utf8)
    }

    func applicationWillTerminate(_ notification: Notification) {
        let pkill = Process()
        pkill.executableURL = URL(fileURLWithPath: "/usr/bin/pkill")
        pkill.arguments = ["-9", "-f", "chiaki"]
        try? pkill.run()
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if !flag {
            for window in NSApp.windows {
                window.makeKeyAndOrderFront(self)
            }
        }
        return true
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        return false
    }
}

// MARK: - Subtitle Server & Models (SSE Port 8787 for TV & OBS)

public struct SubtitleStyle: Codable, Equatable, Sendable {
    public var fontSize: Int
    public var bottomPercent: Int
    public var placement: String
    public var background: Int
    public var bold: Bool
    public var color: String

    public init(fontSize: Int = 46, bottomPercent: Int = 7, placement: String = "cover", background: Int = 72, bold: Bool = false, color: String = "#FFFFFF") {
        self.fontSize = fontSize
        self.bottomPercent = bottomPercent
        self.placement = placement
        self.background = background
        self.bold = bold
        self.color = color
    }

    var json: [String: Any] {
        [
            "fontSize": fontSize,
            "bottomPercent": bottomPercent,
            "placement": placement,
            "background": max(0, min(100, background)),
            "bold": bold,
            "color": color
        ]
    }
}

public final class NativeSubtitleServer: @unchecked Sendable {
    private let port: UInt16
    private let queue = DispatchQueue(label: "gametranslator.native-server")
    private var listener: NWListener?
    private var clients: [ObjectIdentifier: NWConnection] = [:]
    private var viewers: Set<ObjectIdentifier> = []
    private var lastStyle: SubtitleStyle = SubtitleStyle()
    private var pingTimer: DispatchSourceTimer?

    public var onClientsChanged: ((Int) -> Void)?

    public init(port: UInt16 = 8787) {
        self.port = port
    }

    public var connectedClients: Int {
        queue.sync { clients.count - viewers.count }
    }

    public func start() {
        queue.async {
            do {
                let tcp = NWProtocolTCP.Options()
                tcp.enableKeepalive = true
                tcp.keepaliveIdle = 10
                tcp.keepaliveInterval = 5
                tcp.keepaliveCount = 3
                let listener = try NWListener(using: NWParameters(tls: nil, tcp: tcp), on: NWEndpoint.Port(rawValue: self.port)!)
                self.listener = listener
                listener.newConnectionHandler = { [weak self] connection in
                    self?.accept(connection)
                }
                listener.stateUpdateHandler = { [weak self] state in
                    if case .ready = state {
                        self?.startPing()
                    }
                }
                listener.start(queue: self.queue)
            } catch {
                print("SubtitleServer start error: \(error)")
            }
        }
    }

    public func stop() {
        queue.sync {
            pingTimer?.cancel()
            pingTimer = nil
            listener?.cancel()
            listener = nil
            clients.values.forEach { $0.cancel() }
            clients.removeAll()
            viewers.removeAll()
        }
    }

    public func sendSubtitle(turkish: String, original: String = "", durationMs: Int = 12000) {
        let at = Int(Date().timeIntervalSince1970 * 1000)
        let json: [String: Any] = [
            "text": turkish,
            "original": original,
            "duration": durationMs,
            "at": at
        ]
        broadcast(event: "subtitle", json: json)
    }

    public func clearSubtitle() {
        broadcast(event: "clear", json: [:])
    }

    public func sendStyle(_ style: SubtitleStyle) {
        queue.async { self.lastStyle = style }
        broadcast(event: "style", json: style.json)
    }

    private func accept(_ connection: NWConnection) {
        connection.start(queue: queue)
        readRequest(connection, buffer: Data())
    }

    private func readRequest(_ connection: NWConnection, buffer: Data) {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 8192) { [weak self] data, _, isComplete, error in
            guard let self else { return }
            var buffer = buffer
            if let data { buffer.append(data) }
            if let end = buffer.range(of: Data("\r\n\r\n".utf8)) {
                let head = String(decoding: buffer[..<end.lowerBound], as: UTF8.self)
                self.route(connection, requestLine: head.components(separatedBy: "\r\n").first ?? "")
            } else if isComplete || error != nil || buffer.count > 65536 {
                connection.cancel()
            } else {
                self.readRequest(connection, buffer: buffer)
            }
        }
    }

    private func route(_ connection: NWConnection, requestLine: String) {
        let parts = requestLine.split(separator: " ")
        let target = parts.count > 1 ? String(parts[1]) : ""
        let path = target.components(separatedBy: "?")[0]
        let method = parts.first.map(String.init) ?? ""

        if method == "OPTIONS" {
            respondAndClose(connection, status: "204 No Content")
        } else if method == "GET" && path == "/events" {
            openStream(connection, viewer: target.contains("viewer=obs"))
        } else if method == "GET" && path == "/obs" {
            respondAndClose(connection, status: "200 OK", body: Self.obsHtml, type: "text/html; charset=utf-8")
        } else if method == "GET" && path == "/hello" {
            respondAndClose(connection, status: "200 OK", body: #"{"app":"gametranslator","version":"2.0"}"#, type: "application/json")
        } else if (method == "GET" || method == "POST") && path == "/test" {
            self.sendSubtitle(turkish: "🎮 GameTranslator Canlı Altyazı Testi Başarılı!", original: "GameTranslator Live Subtitle Test Successful!", durationMs: 15000)
            respondAndClose(connection, status: "200 OK", body: #"{"status":"ok","message":"Test altyazisi gonderildi"}"#, type: "application/json")
        } else if (method == "GET" || method == "POST") && path == "/send" {
            var text = "🎮 GameTranslator Canlı Altyazı"
            var original = ""
            var duration = 12000
            if let query = target.components(separatedBy: "?").dropFirst().first {
                for pair in query.components(separatedBy: "&") {
                    let kv = pair.components(separatedBy: "=")
                    if kv.count == 2 {
                        let key = kv[0]
                        let val = kv[1].removingPercentEncoding ?? kv[1]
                        if key == "text" { text = val }
                        else if key == "original" { original = val }
                        else if key == "duration", let d = Int(val) { duration = d }
                    }
                }
            }
            self.sendSubtitle(turkish: text, original: original, durationMs: duration)
            respondAndClose(connection, status: "200 OK", body: #"{"status":"ok","message":"Altyazi gonderildi"}"#, type: "application/json")
        } else {
            respondAndClose(connection, status: "404 Not Found", body: "Not Found")
        }
    }

    private func respondAndClose(_ connection: NWConnection, status: String, body: String = "", type: String? = "text/plain; charset=utf-8") {
        let data = Data(body.utf8)
        var head = "HTTP/1.1 \(status)\r\nAccess-Control-Allow-Origin: *\r\n"
        if let type { head += "Content-Type: \(type)\r\n" }
        head += "Content-Length: \(data.count)\r\nConnection: close\r\n\r\n"
        connection.send(content: Data(head.utf8) + data, completion: .contentProcessed { _ in connection.cancel() })
    }

    private func openStream(_ connection: NWConnection, viewer: Bool) {
        var head = "HTTP/1.1 200 OK\r\n"
        head += "Content-Type: text/event-stream; charset=utf-8\r\n"
        head += "Cache-Control: no-cache\r\n"
        head += "Connection: keep-alive\r\n"
        head += "Access-Control-Allow-Origin: *\r\n\r\n"
        head += "retry: 2000\n\n"
        head += Self.frame(event: "style", json: lastStyle.json)
        connection.send(content: Data(head.utf8), completion: .contentProcessed { _ in })

        let id = ObjectIdentifier(connection)
        clients[id] = connection
        if viewer { viewers.insert(id) }
        notifyClientsChanged()
        watchForClose(connection, id: id)
    }

    private func watchForClose(_ connection: NWConnection, id: ObjectIdentifier) {
        connection.receive(minimumIncompleteLength: 1, maximumLength: 1024) { [weak self] _, _, isComplete, error in
            guard let self else { return }
            if isComplete || error != nil {
                connection.cancel()
                self.viewers.remove(id)
                if self.clients.removeValue(forKey: id) != nil { self.notifyClientsChanged() }
            } else {
                self.watchForClose(connection, id: id)
            }
        }
    }

    private func notifyClientsChanged() {
        let count = clients.count
        if let handler = onClientsChanged {
            DispatchQueue.main.async { handler(count) }
        }
    }

    private func broadcast(event: String, json: [String: Any]) {
        let data = Data(Self.frame(event: event, json: json).utf8)
        queue.async { self.sendToAll(data) }
    }

    private func sendToAll(_ data: Data) {
        for (id, connection) in clients {
            connection.send(content: data, completion: .contentProcessed { [weak self] error in
                guard let self, error != nil else { return }
                connection.cancel()
                self.viewers.remove(id)
                if self.clients.removeValue(forKey: id) != nil { self.notifyClientsChanged() }
            })
        }
    }

    private func startPing() {
        let timer = DispatchSource.makeTimerSource(queue: queue)
        timer.schedule(deadline: .now() + 15, repeating: 15)
        timer.setEventHandler { [weak self] in self?.sendToAll(Data(": ping\n\n".utf8)) }
        timer.resume()
        pingTimer = timer
    }

    private static func frame(event: String, json: [String: Any]) -> String {
        let body = (try? JSONSerialization.data(withJSONObject: json, options: [.sortedKeys])).map { String(decoding: $0, as: UTF8.self) } ?? "{}"
        return "event: \(event)\ndata: \(body)\n\n"
    }

    private static let obsHtml = """
    <!DOCTYPE html>
    <html>
    <head>
    <meta charset="utf-8">
    <style>
      body { margin: 0; background: transparent; overflow: hidden; font-family: -apple-system, sans-serif; }
      #box { position: absolute; left: 5%; right: 5%; bottom: 7%; text-align: center; }
      #text { display: inline-block; padding: 12px 24px; border-radius: 8px; font-weight: 600; font-size: 46px; background: rgba(0,0,0,0.72); color: #fff; }
    </style>
    </head>
    <body>
    <div id="box"><div id="text" style="display:none;"></div></div>
    <script>
      const ev = new EventSource('/events?viewer=obs');
      const box = document.getElementById('text');
      ev.addEventListener('subtitle', e => {
        const d = JSON.parse(e.data);
        box.innerText = d.text;
        box.style.display = 'inline-block';
      });
      ev.addEventListener('style', e => {
        const s = JSON.parse(e.data);
        if (s.color) box.style.color = s.color;
        if (s.fontSize) box.style.fontSize = s.fontSize + 'px';
        if (s.background !== undefined) box.style.background = 'rgba(0,0,0,' + (s.background/100) + ')';
        if (s.bottomPercent !== undefined) document.getElementById('box').style.bottom = s.bottomPercent + '%';
      });
      ev.addEventListener('clear', () => { box.style.display = 'none'; });
    </script>
    </body>
    </html>
    """
}

// MARK: - Native Apple Frosted Glass / Vibrancy

struct VisualEffectBackground: NSViewRepresentable {
    var material: NSVisualEffectView.Material = .underWindowBackground
    var blendingMode: NSVisualEffectView.BlendingMode = .behindWindow

    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        view.material = material
        view.blendingMode = blendingMode
        view.state = .active
        return view
    }

    func updateNSView(_ nsView: NSVisualEffectView, context: Context) {
        nsView.material = material
        nsView.blendingMode = blendingMode
    }
}

struct WindowConfigurator: NSViewRepresentable {
    func makeNSView(context: Context) -> NSView {
        let view = NSView()
        DispatchQueue.main.async {
            if let window = view.window {
                window.isOpaque = false
                window.backgroundColor = .clear
                window.titlebarAppearsTransparent = true
                window.titleVisibility = .hidden
                window.styleMask.insert(.fullSizeContentView)
                window.isMovableByWindowBackground = true
                window.hasShadow = true
            }
        }
        return view
    }
    func updateNSView(_ nsView: NSView, context: Context) {}
}

// MARK: - Data Models

struct SubtitleEntry: Identifiable, Codable {
    var id = UUID()
    let turkish: String
    let english: String
    let timestamp: String
}

enum TranslationModel: String, CaseIterable, Identifiable {
    case appleLocal = "local_apple"
    case gemini = "gemini"
    case deepL = "deepl"
    case marian = "local_dl"

    var id: String { rawValue }

    var title: String {
        switch self {
        case .appleLocal: "1. Apple Yerel Çeviri (Önerilen)"
        case .gemini: "2. Google Gemini 2.5 Flash"
        case .deepL: "3. DeepL API"
        case .marian: "4. Yerel Derin Öğrenme (MarianMT)"
        }
    }

    var badge: String {
        switch self {
        case .appleLocal: "Aktif (0ms)"
        case .gemini: "Yapay Zeka"
        case .deepL: "Pro Dilbilgisi"
        case .marian: "GPU / CPU"
        }
    }

    var desc: String {
        switch self {
        case .appleLocal: "Mac'inizin Apple Neural Engine (NPU) çipini kullanır. Tamamen çevrimdışı, internet gerektirmez, 0 ms gecikmeyle anında TV'ye basar."
        case .gemini: "GTA 6 gibi oyunlardaki diyalogları, sokak argosunu ve hikaye bağlamını en akıllı ve doğal şekilde çevirir."
        case .deepL: "Dünyanın en yüksek doğruluklu dilbilgisi motoru. DeepL Free veya Pro API anahtarı gerektirir."
        case .marian: "İnternetsiz açık kaynaklı derin öğrenme çeviri ağı. Bilgisayarınızın donanımını doğrudan kullanır."
        }
    }
}

enum SettingsTab: String, CaseIterable, Identifiable {
    case ocrArea = "ocrArea"
    case display = "display"
    case models = "models"
    case system = "system"
    case guide = "guide"

    var id: String { rawValue }

    var title: String {
        switch self {
        case .ocrArea: "Oyun Okuma Alanı"
        case .display: "TV Görünümü"
        case .models: "Çeviri & Modeller"
        case .system: "Bağlantı & Sistem"
        case .guide: "Hızlı Rehber"
        }
    }

    var icon: String {
        switch self {
        case .ocrArea: "viewfinder.rectangular"
        case .display: "tv.fill"
        case .models: "brain.head.profile"
        case .system: "link"
        case .guide: "book.fill"
        }
    }
}

enum OCRAreaPreset: String, CaseIterable, Identifiable {
    case bottomCenter = "Alt Orta (Standart)"
    case bottomWide = "Geniş Alt Bant"
    case fullBottom = "Tüm Alt Yarı"
    case topBanner = "Üst Bölge (Görevler)"
    case custom = "Özel Kutu"

    var id: String { rawValue }
}

// MARK: - PSN OAuth & Sony Account ID Engine (Chiaki-ng Protocol)

public enum PSNAccount {
    public enum PSNError: LocalizedError {
        case badResponse
        case invalidCode
        case http(Int)
        
        public var errorDescription: String? {
            switch self {
            case .badResponse: return "Sony sunucusundan geçersiz yanıt alındı."
            case .invalidCode: return "Yönlendirme adresinde geçerli bir 'code' parametresi bulunamadı."
            case .http(let code): return "Sony sunucu hatası (HTTP \(code))."
            }
        }
    }

    static let clientId = "ba495a24-818c-472b-b12d-ff231c1b5745"
    static let clientSecret = "mvaiZkRsAsI1IBkY"
    static let tokenURL = URL(string: "https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/token")!
    static let scope = "psn:clientapp referenceDataService:countryConfig.read pushNotification:webSocket.desktop.connect sessionManager:remotePlaySession.system.update"

    public static let redirectPrefix = "https://remoteplay.dl.playstation.net/remoteplay/redirect"

    /// Sony'nin resmi PlayStation Network OAuth giriş sayfası (Tarayıcıda yeni sekmede açılır).
    public static let authorizeURL: URL = {
        var components = URLComponents(string: "https://auth.api.sonyentertainmentnetwork.com/2.0/oauth/authorize")!
        components.queryItems = [
            URLQueryItem(name: "service_entity", value: "urn:service-entity:psn"),
            URLQueryItem(name: "response_type", value: "code"),
            URLQueryItem(name: "client_id", value: clientId),
            URLQueryItem(name: "redirect_uri", value: redirectPrefix),
            URLQueryItem(name: "scope", value: scope),
            URLQueryItem(name: "request_locale", value: "en_US"),
            URLQueryItem(name: "ui", value: "pr"),
            URLQueryItem(name: "service_logo", value: "ps"),
            URLQueryItem(name: "layout_type", value: "popup"),
            URLQueryItem(name: "smcid", value: "remoteplay"),
            URLQueryItem(name: "prompt", value: "always"),
            URLQueryItem(name: "PlatformPrivacyWs1", value: "minimal"),
        ]
        return components.url!
    }()

    /// Giriş bitince yönlendirilen adresten veya ham metinden tek kullanımlık kodu ayıklar.
    public static func code(from text: String) -> String? {
        let trimmed = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if let url = URL(string: trimmed), let components = URLComponents(url: url, resolvingAgainstBaseURL: false) {
            if let code = components.queryItems?.first(where: { $0.name == "code" })?.value {
                return code
            }
        }
        if trimmed.contains("code=") {
            let parts = trimmed.components(separatedBy: "code=")
            if parts.count > 1 {
                return parts[1].components(separatedBy: "&").first
            }
        }
        if !trimmed.contains(" ") && trimmed.count >= 6 && !trimmed.hasPrefix("http") {
            return trimmed
        }
        return nil
    }

    /// Kodu Sony OAuth token uç noktasına gönderir ve 8 baytlık hesap kimliğini çeker.
    public static func fetchAccountId(code: String) async throws -> (bytes: Data, userId: String, base64: String) {
        var tokenRequest = URLRequest(url: tokenURL)
        tokenRequest.httpMethod = "POST"
        tokenRequest.setValue(basicAuth, forHTTPHeaderField: "Authorization")
        tokenRequest.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
        var form = URLComponents()
        form.queryItems = [
            URLQueryItem(name: "grant_type", value: "authorization_code"),
            URLQueryItem(name: "code", value: code),
            URLQueryItem(name: "scope", value: scope),
            URLQueryItem(name: "redirect_uri", value: redirectPrefix),
        ]
        tokenRequest.httpBody = Data((form.percentEncodedQuery ?? "").utf8)
        let token = try await requestJson(for: tokenRequest)
        guard let accessToken = token["access_token"] as? String else { throw PSNError.badResponse }

        var infoRequest = URLRequest(url: tokenURL.appendingPathComponent(accessToken))
        infoRequest.setValue(basicAuth, forHTTPHeaderField: "Authorization")
        let info = try await requestJson(for: infoRequest)
        guard let userId = info["user_id"] as? String, let bytes = accountIdBytes(userId: userId) else { throw PSNError.badResponse }
        return (bytes, userId, bytes.base64EncodedString())
    }

    static func accountIdBytes(userId: String) -> Data? {
        guard let value = UInt64(userId) else { return nil }
        return withUnsafeBytes(of: value.littleEndian) { Data($0) }
    }

    private static var basicAuth: String {
        "Basic " + Data("\(clientId):\(clientSecret)".utf8).base64EncodedString()
    }

    private static func requestJson(for request: URLRequest) async throws -> [String: Any] {
        let (data, response) = try await URLSession.shared.data(for: request)
        if let http = response as? HTTPURLResponse, !(200..<300).contains(http.statusCode) { throw PSNError.http(http.statusCode) }
        guard let object = try JSONSerialization.jsonObject(with: data) as? [String: Any] else { throw PSNError.badResponse }
        return object
    }
}

// MARK: - PSN In-App Login WebKit View

struct PSNLoginWebView: NSViewRepresentable {
    let onRedirect: (URL) -> Void

    func makeCoordinator() -> Coordinator { Coordinator(onRedirect: onRedirect) }

    func makeNSView(context: Context) -> WKWebView {
        let configuration = WKWebViewConfiguration()
        configuration.websiteDataStore = .nonPersistent()
        let view = WKWebView(frame: .zero, configuration: configuration)
        view.navigationDelegate = context.coordinator
        view.load(URLRequest(url: PSNAccount.authorizeURL))
        return view
    }

    func updateNSView(_ view: WKWebView, context: Context) {}

    final class Coordinator: NSObject, WKNavigationDelegate {
        let onRedirect: (URL) -> Void
        init(onRedirect: @escaping (URL) -> Void) { self.onRedirect = onRedirect }

        func webView(_ webView: WKWebView, decidePolicyFor action: WKNavigationAction) async -> WKNavigationActionPolicy {
            if let url = action.request.url, url.absoluteString.hasPrefix(PSNAccount.redirectPrefix) {
                DispatchQueue.main.async {
                    self.onRedirect(url)
                }
                return .cancel
            }
            return .allow
        }
    }
}

// MARK: - PS5 Local Network UDP Discovery Protocol

struct PS5DiscoveryResult: Sendable {
    let ip: String
    let name: String
    let isAwake: Bool
    let hostId: String
}

enum PS5Discovery {
    static func discover(targetHost: String? = nil, timeoutSeconds: Double = 2.5) -> [PS5DiscoveryResult] {
        let sock = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP)
        guard sock >= 0 else { return [] }
        defer { close(sock) }

        var broadcastEnable: Int32 = 1
        setsockopt(sock, SOL_SOCKET, SO_BROADCAST, &broadcastEnable, socklen_t(MemoryLayout<Int32>.size))

        // Set socket receive timeout
        var tv = timeval(
            tv_sec: Int(timeoutSeconds),
            tv_usec: Int32((timeoutSeconds.truncatingRemainder(dividingBy: 1.0)) * 1_000_000)
        )
        setsockopt(sock, SOL_SOCKET, SO_RCVTIMEO, &tv, socklen_t(MemoryLayout<timeval>.size))

        // Bind socket to local port
        var bindAddr = sockaddr_in()
        bindAddr.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        bindAddr.sin_family = sa_family_t(AF_INET)
        bindAddr.sin_port = 0
        bindAddr.sin_addr.s_addr = INADDR_ANY.bigEndian

        let bindRes = withUnsafePointer(to: &bindAddr) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                bind(sock, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard bindRes == 0 else { return [] }

        // Determine broadcast targets
        var targets: [in_addr_t] = [inet_addr("255.255.255.255")]
        var ifap: UnsafeMutablePointer<ifaddrs>?
        if getifaddrs(&ifap) == 0, let first = ifap {
            defer { freeifaddrs(ifap) }
            for ptr in sequence(first: first, next: { $0.pointee.ifa_next }) {
                let ifa = ptr.pointee
                let flags = Int32(ifa.ifa_flags)
                if (flags & IFF_UP) != 0 && (flags & IFF_LOOPBACK) == 0 && (flags & IFF_BROADCAST) != 0 {
                    if let addr = ifa.ifa_dstaddr, addr.pointee.sa_family == sa_family_t(AF_INET) {
                        let bAddr = addr.withMemoryRebound(to: sockaddr_in.self, capacity: 1) { $0.pointee.sin_addr.s_addr }
                        targets.append(bAddr)
                    }
                }
            }
        }

        if let explicitHost = targetHost?.trimmingCharacters(in: .whitespaces), !explicitHost.isEmpty {
            let direct = inet_addr(explicitHost)
            if direct != INADDR_NONE {
                targets.append(direct)
            }
        }

        let payload = "SRCH * HTTP/1.1\r\ndevice-discovery-protocol-version:00030010\r\n\r\n"
        let payloadData = payload.data(using: .utf8)!

        func sendProbes() {
            for target in targets {
                var destAddr = sockaddr_in()
                destAddr.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
                destAddr.sin_family = sa_family_t(AF_INET)
                destAddr.sin_port = in_port_t(9302).bigEndian
                destAddr.sin_addr.s_addr = target

                _ = payloadData.withUnsafeBytes { rawBuf in
                    withUnsafePointer(to: &destAddr) {
                        $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                            sendto(sock, rawBuf.baseAddress, rawBuf.count, 0, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
                        }
                    }
                }
            }
        }

        sendProbes()

        var results: [PS5DiscoveryResult] = []
        var seenIPs = Set<String>()
        var buf = [UInt8](repeating: 0, count: 4096)

        let startTime = Date()
        var hasResent = false

        while Date().timeIntervalSince(startTime) < timeoutSeconds {
            if !hasResent && Date().timeIntervalSince(startTime) > 0.4 {
                hasResent = true
                sendProbes()
            }

            var fromAddr = sockaddr_in()
            var fromLen = socklen_t(MemoryLayout<sockaddr_in>.size)

            let received = withUnsafeMutablePointer(to: &fromAddr) {
                $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                    recvfrom(sock, &buf, buf.count, 0, $0, &fromLen)
                }
            }

            if received <= 0 {
                if (errno == EAGAIN || errno == EWOULDBLOCK) && !hasResent {
                    hasResent = true
                    sendProbes()
                    continue
                }
                break
            }

            var hostBuffer = [CChar](repeating: 0, count: Int(INET_ADDRSTRLEN))
            inet_ntop(AF_INET, &fromAddr.sin_addr, &hostBuffer, socklen_t(INET_ADDRSTRLEN))
            let senderIP = String(cString: hostBuffer)

            let responseText = String(decoding: buf[0..<received], as: UTF8.self)
            if responseText.contains("host-type:PS5") || responseText.contains("00030010") {
                if !seenIPs.contains(senderIP) {
                    seenIPs.insert(senderIP)
                    var name = "PS5"
                    let isAwake = responseText.contains("200")
                    var hostId = ""

                    for line in responseText.components(separatedBy: "\n") {
                        let trimmed = line.trimmingCharacters(in: .whitespacesAndNewlines)
                        let lower = trimmed.lowercased()
                        if lower.hasPrefix("host-name:") {
                            name = String(trimmed.dropFirst("host-name:".count)).trimmingCharacters(in: .whitespaces)
                        } else if lower.hasPrefix("host-id:") {
                            hostId = String(trimmed.dropFirst("host-id:".count)).trimmingCharacters(in: .whitespaces)
                        }
                    }

                    results.append(PS5DiscoveryResult(ip: senderIP, name: name, isAwake: isAwake, hostId: hostId))
                    if results.count >= 1 {
                        break
                    }
                }
            }
        }

        return results
    }
}

// MARK: - TV Helper Bridge (Built-in ADB & Samsung Installer)

enum TVHelperBridge {
    static var helperURL: URL? {
        let candidates = [
            Bundle.main.url(forAuxiliaryExecutable: "gametranslator-tv-helper"),
            Bundle.main.bundleURL.appendingPathComponent("Contents/MacOS/gametranslator-tv-helper"),
            URL(fileURLWithPath: "/Applications/GameTranslator.app/Contents/MacOS/gametranslator-tv-helper"),
            URL(fileURLWithPath: "GameTranslator/GameTranslator.app/Contents/MacOS/gametranslator-tv-helper"),
            URL(fileURLWithPath: "dist/tv-helper/gametranslator-tv-helper")
        ]
        return candidates.compactMap { $0 }.first { FileManager.default.isExecutableFile(atPath: $0.path) }
    }

    static var bundledAPKURL: URL? {
        let candidates = [
            Bundle.main.url(forResource: "gametranslator-androidtv_1.0.0", withExtension: "apk", subdirectory: "tv-app"),
            Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/tv-app/gametranslator-androidtv_1.0.0.apk"),
            URL(fileURLWithPath: "/Applications/GameTranslator.app/Contents/Resources/tv-app/gametranslator-androidtv_1.0.0.apk"),
            URL(fileURLWithPath: "GameTranslator/GameTranslator.app/Contents/Resources/tv-app/gametranslator-androidtv_1.0.0.apk"),
            URL(fileURLWithPath: "dist/tv-app/gametranslator-androidtv_1.0.0.apk")
        ]
        return candidates.compactMap { $0 }.first { FileManager.default.fileExists(atPath: $0.path) }
    }

    static var adbKeyDir: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        let dir = base.appendingPathComponent("GameTranslator/adb", isDirectory: true)
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }

    static func scanAndroidTV() -> [String] {
        guard let helper = helperURL else { return [] }
        let process = Process()
        process.executableURL = helper
        process.arguments = ["android-scan"]

        let outPipe = Pipe()
        let inPipe = Pipe()
        process.standardOutput = outPipe
        process.standardInput = inPipe
        process.standardError = FileHandle.nullDevice

        do {
            try process.run()
            try inPipe.fileHandleForWriting.close()
            let data = outPipe.fileHandleForReading.readDataToEndOfFile()
            process.waitUntilExit()

            if let str = String(data: data, encoding: .utf8) {
                for line in str.components(separatedBy: "\n") {
                    if let jsonData = line.data(using: .utf8),
                       let obj = try? JSONSerialization.jsonObject(with: jsonData) as? [String: Any],
                       let event = obj["event"] as? String, event == "found",
                       let hosts = obj["hosts"] as? [String] {
                        return hosts
                    }
                }
            }
        } catch {
            return []
        }
        return []
    }

    static func installAndroidTV(host: String) -> (success: Bool, message: String) {
        guard let apk = bundledAPKURL else {
            return (false, "gametranslator-androidtv APK paketi bulunamadı.")
        }

        let pythonCandidates = ["/usr/local/bin/python3", "/opt/homebrew/bin/python3", "/usr/bin/python3"]
        let pythonBin = pythonCandidates.first { FileManager.default.isExecutableFile(atPath: $0) }

        let scriptCandidates = [
            Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/scripts/android-tv-install.py"),
            URL(fileURLWithPath: "/Applications/GameTranslator.app/Contents/Resources/scripts/android-tv-install.py"),
            URL(fileURLWithPath: "GameTranslate-main/scripts/android-tv-install.py")
        ]
        let scriptURL = scriptCandidates.first { FileManager.default.fileExists(atPath: $0.path) }

        if let py = pythonBin, let script = scriptURL {
            let p = Process()
            p.executableURL = URL(fileURLWithPath: py)
            let serverURL = "http://\(AppState.localIPAddress()):8787"
            p.arguments = [
                script.path,
                "--host", host,
                "--package", apk.path,
                "--server", serverURL
            ]

            let outPipe = Pipe()
            p.standardOutput = outPipe
            p.standardInput = Pipe()
            p.standardError = FileHandle.nullDevice

            do {
                try p.run()
                let data = outPipe.fileHandleForReading.readDataToEndOfFile()
                p.waitUntilExit()

                if let str = String(data: data, encoding: .utf8) {
                    for line in str.components(separatedBy: "\n") {
                        let trimmed = line.trimmingCharacters(in: .whitespaces)
                        if let jsonData = trimmed.data(using: .utf8),
                           let obj = try? JSONSerialization.jsonObject(with: jsonData) as? [String: Any],
                           let event = obj["event"] as? String {
                            if event == "done" {
                                let msg = obj["message"] as? String ?? "✅ GameTranslator TV uygulaması televizyona başarıyla yüklendi ve açıldı!"
                                return (true, msg)
                            } else if event == "error" {
                                let msg = obj["message"] as? String ?? "TV Kurulum Hatası."
                                return (false, msg)
                            }
                        }
                    }
                }
            } catch {
                // Fallback to helper
            }
        }

        guard let helper = helperURL else {
            return (false, "gametranslator-tv-helper aracı uygulama içinde bulunamadı.")
        }

        let process = Process()
        process.executableURL = helper
        process.arguments = [
            "android-install",
            "--host", host,
            "--package", apk.path,
            "--version", "1.0.0",
            "--key-dir", adbKeyDir.path
        ]

        let outPipe = Pipe()
        let inPipe = Pipe()
        process.standardOutput = outPipe
        process.standardInput = inPipe
        process.standardError = FileHandle.nullDevice

        do {
            try process.run()
            try inPipe.fileHandleForWriting.close()
            let data = outPipe.fileHandleForReading.readDataToEndOfFile()
            process.waitUntilExit()

            if let str = String(data: data, encoding: .utf8) {
                for line in str.components(separatedBy: "\n") {
                    if let jsonData = line.data(using: .utf8),
                       let obj = try? JSONSerialization.jsonObject(with: jsonData) as? [String: Any],
                       let event = obj["event"] as? String {
                        if event == "done" {
                            return (true, "✅ GameTranslator TV uygulaması televizyona başarıyla yüklendi ve açıldı!")
                        } else if event == "approve" {
                            return (false, "⚠️ Televizyon ekranında 'Bu bilgisayardan hata ayıklamaya her zaman izin verilsin mi?' sorusu belirdi. Lütfen TV kumandasıyla 'İzin Ver' / 'Tamam' seçeneğini işaretleyip tekrar 'TV'ye Otomatik Kur' butonuna basın.")
                        } else if event == "error" {
                            let msg = obj["message"] as? String ?? "Yükleme tamamlanamadı."
                            let code = obj["code"] as? String ?? ""
                            if code == "Unreachable" {
                                return (false, "Televizyona ulaşılamadı (\(host):5555). Lütfen TV'nizin açık ve 'Ağda Hata Ayıklama (ADB)' özelliğinin aktif olduğundan emin olun.")
                            }
                            return (false, "TV Kurulum Hatası: \(msg)")
                        }
                    }
                }
            }
        } catch {
            return (false, error.localizedDescription)
        }
        return (false, "TV'den beklenen yanıt alınamadı.")
    }

    static func testAndroidTV(host: String) -> (success: Bool, message: String) {
        let pythonCandidates = ["/usr/local/bin/python3", "/opt/homebrew/bin/python3", "/usr/bin/python3"]
        let pythonBin = pythonCandidates.first { FileManager.default.isExecutableFile(atPath: $0) }

        let scriptCandidates = [
            Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/scripts/android-tv-install.py"),
            URL(fileURLWithPath: "/Applications/GameTranslator.app/Contents/Resources/scripts/android-tv-install.py"),
            URL(fileURLWithPath: "GameTranslate-main/scripts/android-tv-install.py")
        ]
        let scriptURL = scriptCandidates.first { FileManager.default.fileExists(atPath: $0.path) }

        guard let py = pythonBin, let script = scriptURL else {
            return (false, "Python veya script bulunamadı.")
        }

        let p = Process()
        p.executableURL = URL(fileURLWithPath: py)
        let serverURL = "http://\(AppState.localIPAddress()):8787"
        p.arguments = [
            script.path,
            "--host", host,
            "--action", "test",
            "--server", serverURL
        ]

        let outPipe = Pipe()
        p.standardOutput = outPipe
        p.standardInput = Pipe()
        p.standardError = FileHandle.nullDevice

        do {
            try p.run()
            let data = outPipe.fileHandleForReading.readDataToEndOfFile()
            p.waitUntilExit()

            if let str = String(data: data, encoding: .utf8) {
                for line in str.components(separatedBy: "\n") {
                    let trimmed = line.trimmingCharacters(in: .whitespaces)
                    if let jsonData = trimmed.data(using: .utf8),
                       let obj = try? JSONSerialization.jsonObject(with: jsonData) as? [String: Any],
                       let event = obj["event"] as? String {
                        if event == "done" {
                            let msg = obj["message"] as? String ?? "✅ GameTranslator TV ekranına getirildi!"
                            return (true, msg)
                        } else if event == "error" {
                            let msg = obj["message"] as? String ?? "TV Hatası."
                            return (false, msg)
                        }
                    }
                }
            }
            return (true, "TV altyazısı tetiklendi.")
        } catch {
            return (false, error.localizedDescription)
        }
    }
}

// MARK: - PS5 Registration Bridge (Remote Play Pairing)

enum PS5RegistrationBridge {
    static var helperURL: URL? {
        let candidates = [
            Bundle.main.url(forAuxiliaryExecutable: "gametranslator-ps5-register"),
            Bundle.main.bundleURL.appendingPathComponent("Contents/MacOS/gametranslator-ps5-register"),
            URL(fileURLWithPath: "/Applications/GameTranslator.app/Contents/MacOS/gametranslator-ps5-register"),
            URL(fileURLWithPath: "GameTranslator/GameTranslator.app/Contents/MacOS/gametranslator-ps5-register"),
            URL(fileURLWithPath: "GameTranslate-main/scripts/gametranslator-ps5-register")
        ]
        return candidates.compactMap { $0 }.first { FileManager.default.isExecutableFile(atPath: $0.path) }
    }

    struct RegisterResult {
        let success: Bool
        let registKey: String?
        let rpKey: String?
        let message: String
    }

    static func register(host: String, pin: String, accountBase64: String) -> RegisterResult {
        guard let helper = helperURL else {
            return RegisterResult(success: false, registKey: nil, rpKey: nil, message: "PS5 eşleştirme yardımcısı (gametranslator-ps5-register) bulunamadı.")
        }

        let process = Process()
        process.executableURL = helper
        process.arguments = [
            "--host", host,
            "--pin", pin,
            "--account", accountBase64
        ]

        let outPipe = Pipe()
        let inPipe = Pipe()
        process.standardOutput = outPipe
        process.standardError = FileHandle.nullDevice
        process.standardInput = inPipe

        do {
            try process.run()
            try inPipe.fileHandleForWriting.close()
            let data = outPipe.fileHandleForReading.readDataToEndOfFile()
            process.waitUntilExit()

            if let str = String(data: data, encoding: .utf8) {
                var lastDone: [String: Any]?
                var lastError: String?

                for line in str.components(separatedBy: "\n") {
                    let trimmed = line.trimmingCharacters(in: .whitespaces)
                    guard !trimmed.isEmpty, let jsonData = trimmed.data(using: .utf8),
                          let obj = try? JSONSerialization.jsonObject(with: jsonData) as? [String: Any],
                          let event = obj["event"] as? String else { continue }
                    if event == "done" {
                        lastDone = obj
                    } else if event == "error" {
                        lastError = obj["message"] as? String
                    }
                }

                if let done = lastDone {
                    let registKey = done["registKey"] as? String
                    let rpKey = done["rpKey"] as? String
                    return RegisterResult(success: true, registKey: registKey, rpKey: rpKey, message: "PS5 başarıyla eşleşti!")
                } else if let err = lastError {
                    return RegisterResult(success: false, registKey: nil, rpKey: nil, message: err)
                }
            }
        } catch {
            return RegisterResult(success: false, registKey: nil, rpKey: nil, message: error.localizedDescription)
        }
        return RegisterResult(success: false, registKey: nil, rpKey: nil, message: "PS5'ten yanıt alınamadı. Konsolunuzun 'Cihaz Eşle' ekranında açık olduğunu kontrol edin.")
    }
}

// MARK: - App State

@Observable
final class AppState {
    var isRunning: Bool = false
    var ps5Connected: Bool = false
    var psnEmail: String = ""
    var ps5Host: String = ""
    var ps5Pin: String = ""
    var psnLinked: Bool = false
    var psnAccountId: String? = nil
    var psnAccountBase64: String? = nil
    var psnRedirectInput: String = ""
    var psnError: String? = nil
    var isConnectingPSN: Bool = false
    var showInAppPSNLogin: Bool = false
    var ps5Paired: Bool = false
    var isSearchingPS5: Bool = false
    var isPairingPS5: Bool = false
    var ps5Error: String? = nil
    var ps5SuccessMessage: String? = nil
    var ps5DiscoveredName: String? = nil
    var tvConnected: Bool = false
    var selectedTVPlatform: String = "Mac / OBS"
    var tvDeviceName: String = "TV uygulaması bekleniyor"
    var tvHost: String = ""
    var tvError: String? = nil
    var tvSuccessMessage: String? = nil
    var isSearchingTV: Bool = false
    var isInstallingTV: Bool = false
    var tvInstallMessage: String? = nil
    
    var isOnboarded: Bool = false
    var showSetup: Bool = true
    var setupStep: Int = 1
    var bypassSetup: Bool = false
    var showCalibrationImage: Bool = true
    
    // Model
    var selectedModel: TranslationModel = .appleLocal
    var geminiApiKey: String = ""
    var deepLApiKey: String = ""
    
    // TV Altyazı Ayarları
    var selectedColor: String = "#FFFFFF"
    var fontSize: Double = 46
    var bottomPercent: Double = 7
    var backgroundOpacity: Double = 72
    var isBold: Bool = true
    var placement: String = "cover"
    var language: String = "en"
    var isDubbing: Bool = false
    
    // OCR Okuma Alanı (16:9 PS5 Oyun Ekranı ve Altyazı Koordinatları)
    var cropTop: Double = 0.72
    var cropBottom: Double = 0.85
    var cropLeft: Double = 0.16
    var cropRight: Double = 0.84
    var selectedPreset: OCRAreaPreset = .bottomCenter
    var filterNoise: Bool = true
    
    // Canlı altyazı & Geçmiş
    var liveTurkish: String = "Siz oynarken altyazılar burada görünecek."
    var liveEnglish: String = "Subtitles will appear here as you play."
    var history: [SubtitleEntry] = []
    
    var obsCopied: Bool = false
    var testSent: Bool = false
    var showAbout: Bool = false
    
    // Gerçek PS5 Oyun Karesi (1920x1080 Remote Play Akış Görüntüsü)
    var gameFrameImage: NSImage? = nil

    // macOS Ekran Kaydı İzni
    var hasScreenRecordingPermission: Bool = true
    var showPermissionBanner: Bool = false

    static func verifyScreenAccess() -> Bool {
        if CGPreflightScreenCaptureAccess() { return true }
        if let test = CGWindowListCreateImage(CGRect(x: 0, y: 0, width: 2, height: 2), .optionOnScreenOnly, kCGNullWindowID, .bestResolution),
           test.width > 0 {
            return true
        }
        return false
    }

    func checkScreenCapturePermission() -> Bool {
        let granted = AppState.verifyScreenAccess()
        self.hasScreenRecordingPermission = granted
        if granted {
            self.showPermissionBanner = false
        }
        return granted
    }

    func requestScreenCapturePermission() {
        CGRequestScreenCaptureAccess()
        if let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_ScreenCapture") {
            NSWorkspace.shared.open(url)
        }
    }

    func restartApp() {
        let appPath = Bundle.main.bundlePath
        let p = Process()
        p.executableURL = URL(fileURLWithPath: "/usr/bin/open")
        p.arguments = ["-n", appPath]
        try? p.run()
        NSApp.terminate(nil)
    }
    
    func resetToZero() {
        isRunning = false
        ps5Connected = false
        gameFrameImage = nil
        psnEmail = ""
        ps5Host = ""
        ps5Pin = ""
        psnLinked = false
        psnAccountId = nil
        psnAccountBase64 = nil
        psnRedirectInput = ""
        psnError = nil
        isConnectingPSN = false
        showInAppPSNLogin = false
        ps5Paired = false
        isSearchingPS5 = false
        ps5Error = nil
        ps5SuccessMessage = nil
        ps5DiscoveredName = nil
        selectedTVPlatform = "Mac / OBS"
        tvDeviceName = "TV uygulaması bekleniyor"
        tvConnected = false
        tvHost = ""
        tvError = nil
        isSearchingTV = false
        isInstallingTV = false
        tvInstallMessage = nil
        tvSuccessMessage = nil
        isOnboarded = false
        setupStep = 1
        showSetup = true
        bypassSetup = false
        liveTurkish = "Siz oynarken altyazılar burada görünecek."
        liveEnglish = "Subtitles will appear here as you play."
        history = []
        selectedPreset = .bottomCenter
        cropTop = 0.72; cropBottom = 0.85; cropLeft = 0.16; cropRight = 0.84
    }

    static func localIPAddress() -> String {
        var address: String = "localhost"
        var ifaddr: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&ifaddr) == 0, let first = ifaddr else { return address }
        defer { freeifaddrs(ifaddr) }
        for ptr in sequence(first: first, next: { $0.pointee.ifa_next }) {
            let flags = Int32(ptr.pointee.ifa_flags)
            let addr = ptr.pointee.ifa_addr.pointee
            if (flags & (IFF_UP | IFF_RUNNING)) == (IFF_UP | IFF_RUNNING) && (flags & IFF_LOOPBACK) == 0 && addr.sa_family == UInt8(AF_INET) {
                var hostname = [CChar](repeating: 0, count: Int(NI_MAXHOST))
                if getnameinfo(ptr.pointee.ifa_addr, socklen_t(addr.sa_len), &hostname, socklen_t(hostname.count), nil, 0, NI_NUMERICHOST) == 0 {
                    address = String(cString: hostname)
                    break
                }
            }
        }
        return address
    }

    func copyObsLink() {
        let url = "http://\(Self.localIPAddress()):8787/obs"
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(url, forType: .string)
        obsCopied = true
        DispatchQueue.main.asyncAfter(deadline: .now() + 2) {
            self.obsCopied = false
        }
    }

    func scanLocalTV() async {
        isSearchingTV = true
        tvError = nil
        tvSuccessMessage = nil

        let discoveredHosts = await Task.detached(priority: .userInitiated) { () -> [String] in
            return TVHelperBridge.scanAndroidTV()
        }.value

        isSearchingTV = false

        if let first = discoveredHosts.first {
            tvHost = first
            tvConnected = true
            tvDeviceName = "Android TV (\(first))"
            tvSuccessMessage = "✓ Android TV bulundu: \(first)"
            tvError = nil
        } else {
            tvError = "Ağda açık bir Android TV bulunamadı. Televizyonunuzda Ayarlar › Geliştirici Seçenekleri › 'Ağda Hata Ayıklama (ADB)' özelliğinin AÇIK olduğundan ve bu Mac ile aynı Wi-Fi/LAN ağına bağlı olduğundan emin olun."
        }
    }

    func connectTV() {
        let trimmed = tvHost.trimmingCharacters(in: .whitespaces)
        guard !trimmed.isEmpty else {
            tvError = "Lütfen televizyonunuzun yerel IP adresini girin veya 'Mac Ekranı & OBS' modunu seçin."
            return
        }
        tvConnected = true
        tvDeviceName = "\(selectedTVPlatform) (\(trimmed))"
        tvError = nil
    }

    func installAndroidTVApp() {
        var trimmedHost = tvHost.trimmingCharacters(in: .whitespaces)
        
        isInstallingTV = true
        tvError = nil
        tvInstallMessage = "Android TV'ye bağlanılıyor ve APK yükleniyor..."

        Task.detached(priority: .userInitiated) {
            if trimmedHost.isEmpty {
                await MainActor.run {
                    self.tvInstallMessage = "Ağdaki Android TV aranıyor..."
                }
                let hosts = TVHelperBridge.scanAndroidTV()
                if let first = hosts.first {
                    trimmedHost = first
                    await MainActor.run {
                        self.tvHost = first
                        self.tvConnected = true
                        self.tvDeviceName = "Android TV (\(first))"
                        self.tvSuccessMessage = "✓ Android TV bulundu: \(first)"
                        self.tvInstallMessage = "Android TV bulundu (\(first)). APK yükleniyor..."
                    }
                } else {
                    await MainActor.run {
                        self.isInstallingTV = false
                        self.tvInstallMessage = nil
                        self.tvError = "Ağda açık bir Android TV bulunamadı. Lütfen TV IP adresini elle girin veya TV'de Hata Ayıklama (ADB) özelliğinin açık olduğunu kontrol edin."
                    }
                    return
                }
            }

            let finalHost = trimmedHost
            let result = TVHelperBridge.installAndroidTV(host: finalHost)

            await MainActor.run {
                self.isInstallingTV = false
                if result.success {
                    self.tvConnected = true
                    self.tvDeviceName = "Android TV (\(finalHost))"
                    self.tvInstallMessage = result.message
                    self.tvError = nil
                    UserDefaults.standard.set(finalHost, forKey: "gt_tv_host")

                    // Televizyona otomatik hoş geldin ve bağlantı testi altyazısı gönder
                    DispatchQueue.main.asyncAfter(deadline: .now() + 1.2) {
                        self.server.sendSubtitle(
                            turkish: "🎮 GameTranslator Televizyonunuza Başarıyla Bağlandı! (Hazır)",
                            original: "GameTranslator TV Connected Successfully! (Ready)",
                            durationMs: 15000
                        )
                    }
                } else {
                    self.tvInstallMessage = nil
                    self.tvError = result.message
                }
            }
        }
    }

    func sendTVTestSubtitle() {
        testSent = true
        let testTr = "🎮 GameTranslator Canlı Altyazı Testi Başarılı! (TCL Smart TV)"
        let testEn = "GameTranslator Live Subtitle Test Successful!"
        let entry = SubtitleEntry(turkish: testTr, english: testEn, timestamp: currentTime())
        history.insert(entry, at: 0)
        syncStyleToTV()
        server.sendSubtitle(turkish: testTr, original: testEn, durationMs: 15000)

        let host = self.tvHost.trimmingCharacters(in: .whitespaces)
        if !host.isEmpty {
            Task.detached {
                _ = TVHelperBridge.testAndroidTV(host: host)
            }
        }

        DispatchQueue.main.asyncAfter(deadline: .now() + 3.0) {
            self.testSent = false
        }
    }

    func processPSNRedirect(_ text: String) async {
        guard let code = PSNAccount.code(from: text) else {
            psnError = "Geçersiz yönlendirme adresi veya kod. 'code=' içeren tam URL'i girin."
            return
        }
        isConnectingPSN = true
        psnError = nil
        do {
            let res = try await PSNAccount.fetchAccountId(code: code)
            psnAccountId = res.userId
            psnAccountBase64 = res.base64
            psnLinked = true
            psnEmail = "PSN ID: \(res.userId)"
            UserDefaults.standard.set(res.userId, forKey: "gt_psn_account_id")
            UserDefaults.standard.set(res.base64, forKey: "gt_psn_account_b64")
            UserDefaults.standard.set("PSN ID: \(res.userId)", forKey: "gt_psn_email")
            isConnectingPSN = false
            showInAppPSNLogin = false
        } catch {
            psnError = error.localizedDescription
            isConnectingPSN = false
        }
    }

    func scanLocalPS5() async {
        isSearchingPS5 = true
        ps5Error = nil
        ps5SuccessMessage = nil

        let target = ps5Host.trimmingCharacters(in: .whitespaces)
        let discoveredConsoles = await Task.detached(priority: .userInitiated) {
            return PS5Discovery.discover(targetHost: target.isEmpty ? nil : target, timeoutSeconds: 2.5)
        }.value

        isSearchingPS5 = false

        if let first = discoveredConsoles.first {
            ps5Host = first.ip
            ps5DiscoveredName = first.name
            ps5SuccessMessage = "✓ PS5 bulundu: \(first.name) (\(first.ip))"
            ps5Error = nil
            UserDefaults.standard.set(first.ip, forKey: "gt_ps5_host")
        } else {
            ps5Error = "Yerel ağda açık bir PS5 konsolu bulunamadı. Konsolunuzun açık olduğunu ve bu Mac ile aynı Wi-Fi/LAN ağına bağlı olduğunu kontrol edin veya konsol IP adresini elle girin."
        }
    }

    func pairPS5() {
        let pin = ps5Pin.filter(\.isNumber)
        guard pin.count == 8 else {
            ps5Error = "Lütfen PS5 ekranındaki 8 haneli kodu tam olarak girin."
            return
        }
        guard let account = psnAccountBase64, !account.isEmpty else {
            ps5Error = "PSN hesabı bulunamadı. Lütfen 2. Adıma dönüp PSN hesabınızla giriş yapın."
            return
        }
        let trimmedHost = ps5Host.trimmingCharacters(in: .whitespaces)
        guard !trimmedHost.isEmpty else {
            ps5Error = "Lütfen PS5 konsolunuzun yerel IP adresini girin veya 'Ağda Tara' butonuna basın."
            return
        }

        isPairingPS5 = true
        ps5Error = nil
        ps5SuccessMessage = nil

        let finalHost = trimmedHost
        let finalPin = pin

        Task.detached(priority: .userInitiated) {
            let result = PS5RegistrationBridge.register(host: finalHost, pin: finalPin, accountBase64: account)
            await MainActor.run {
                self.isPairingPS5 = false
                if result.success {
                    self.ps5Paired = true
                    self.ps5Connected = true
                    self.ps5Host = finalHost
                    self.ps5SuccessMessage = "✓ PS5 Başarıyla Eşleşti! (IP: \(finalHost))"
                    self.ps5Error = nil
                    
                    UserDefaults.standard.set(true, forKey: "gt_ps5_paired")
                    UserDefaults.standard.set(finalHost, forKey: "gt_ps5_host")
                    if let rk = result.registKey {
                        UserDefaults.standard.set(rk, forKey: "gt_ps5_regist_key")
                    }
                    if let rpk = result.rpKey {
                        UserDefaults.standard.set(rpk, forKey: "gt_ps5_rp_key")
                    }
                    self.syncChiakiConfig()
                } else {
                    self.ps5Paired = false
                    self.ps5Connected = false
                    self.ps5Error = result.message
                }
            }
        }
    }


    
    // MARK: - Chiaki & Remote Play Streaming & Live OCR

    @ObservationIgnored private var streamProcess: Process? = nil
    @ObservationIgnored private var captureTimer: Timer? = nil
    @ObservationIgnored private var isProcessingOCR: Bool = false
    @ObservationIgnored private var lastSentText: String = ""

    func syncChiakiConfig(cx: Int = 400, cy: Int = 250) {
        let host = UserDefaults.standard.string(forKey: "gt_ps5_host") ?? ps5Host
        let registKey = UserDefaults.standard.string(forKey: "gt_ps5_regist_key") ?? ""
        let rpKey = UserDefaults.standard.string(forKey: "gt_ps5_rp_key") ?? ""
        
        guard !host.isEmpty, !registKey.isEmpty, !rpKey.isEmpty else { return }

        let plistPath = ("~/Library/Preferences/com.chiaki.Chiaki.plist" as NSString).expandingTildeInPath
        var dict: [String: Any] = [:]
        if let data = try? Data(contentsOf: URL(fileURLWithPath: plistPath)),
           let loaded = try? PropertyListSerialization.propertyList(from: data, options: [], format: nil) as? [String: Any] {
            dict = loaded
        }

        let macData = Data([0xE8, 0x6E, 0x3A, 0x94, 0xB1, 0x64])
        
        var regData = Data()
        for i in stride(from: 0, to: registKey.count, by: 2) {
            let start = registKey.index(registKey.startIndex, offsetBy: i)
            let end = registKey.index(start, offsetBy: 2)
            if let byte = UInt8(registKey[start..<end], radix: 16) {
                regData.append(byte)
            }
        }
        while regData.count < 16 { regData.append(0) }

        var rpData = Data()
        for i in stride(from: 0, to: rpKey.count, by: 2) {
            let start = rpKey.index(rpKey.startIndex, offsetBy: i)
            let end = rpKey.index(start, offsetBy: 2)
            if let byte = UInt8(rpKey[start..<end], radix: 16) {
                rpData.append(byte)
            }
        }

        dict["registered_hosts.size"] = 1
        dict["registered_hosts.1.target"] = 1000100
        dict["registered_hosts.1.server_nickname"] = "PS5-sefflex"
        dict["registered_hosts.1.server_mac"] = macData
        dict["registered_hosts.1.rp_regist_key"] = regData
        dict["registered_hosts.1.rp_key_type"] = 0
        dict["registered_hosts.1.rp_key"] = rpData
        dict["registered_hosts.1.console_pin"] = ""
        dict["registered_hosts.1.ap_ssid"] = ""
        dict["registered_hosts.1.ap_bssid"] = ""
        dict["registered_hosts.1.ap_key"] = ""
        dict["registered_hosts.1.ap_name"] = ""
        dict["settings.audio_volume"] = 0
        dict["settings.geometry"] = "@Rect(\(cx) \(cy) 640 360)"
        dict["settings.stream_geometry"] = "@Rect(\(cx) \(cy) 640 360)"
        dict["settings.fullscreen"] = false
        dict["settings.remote_play_ask"] = false
        dict["version"] = 2

        if let outData = try? PropertyListSerialization.data(fromPropertyList: dict, format: .binary, options: 0) {
            try? outData.write(to: URL(fileURLWithPath: plistPath))
        }
        CFPreferencesSynchronize("com.chiaki.Chiaki" as CFString, kCFPreferencesCurrentUser, kCFPreferencesAnyHost)
    }

    func startStreamAndOCR() {
        _ = checkScreenCapturePermission()

        // Chiaki'yi GameTranslator penceresinin tam altına gizle (kullanıcı arka planda görmesin)
        var cx = 400
        var cy = 250
        if let win = NSApp.windows.first {
            let f = win.frame
            let screenH = NSScreen.main?.frame.height ?? 900
            cx = max(30, Int(f.midX) - 320)
            cy = max(30, Int(screenH - f.midY) - 180)
        }

        syncChiakiConfig(cx: cx, cy: cy)

        let host = ps5Host.isEmpty ? (UserDefaults.standard.string(forKey: "gt_ps5_host") ?? "192.168.1.108") : ps5Host
        let chiakiCandidates = [
            "/Applications/Chiaki.app/Contents/MacOS/chiaki",
            "/Applications/chiaki-ng.app/Contents/MacOS/chiaki",
            Bundle.main.bundlePath + "/Contents/Helpers/Chiaki.app/Contents/MacOS/chiaki"
        ]
        
        var chiakiPath: String? = nil
        for candidate in chiakiCandidates {
            if FileManager.default.fileExists(atPath: candidate) {
                chiakiPath = candidate
                break
            }
        }

        let geom = "640x360+\(cx)+\(cy)"

        if let binary = chiakiPath {
            let apps = NSWorkspace.shared.runningApplications
            let running = apps.contains {
                $0.bundleIdentifier == "com.chiaki.Chiaki" ||
                $0.localizedName?.localizedCaseInsensitiveContains("chiaki") == true
            }
            if !running {
                let p = Process()
                p.executableURL = URL(fileURLWithPath: binary)
                p.arguments = [
                    "--qwindowgeometry", geom,
                    "--exit-app-on-stream-exit",
                    "stream", "PS5-sefflex", host
                ]
                try? p.run()
                self.streamProcess = p
            }
        }

        // GameTranslator penceresini öne al ve Chiaki'yi tamamen arkasında ört
        for delay in [0.1, 0.3, 0.6, 1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0] {
            DispatchQueue.main.asyncAfter(deadline: .now() + delay) { [weak self] in
                guard let self = self, self.isRunning else { return }
                NSApp.activate(ignoringOtherApps: true)
                for window in NSApp.windows {
                    window.level = .floating
                    window.makeKeyAndOrderFront(nil)
                }
            }
        }

        captureTimer?.invalidate()
        let t = Timer(timeInterval: 0.2, repeats: true) { [weak self] _ in
            guard let self = self, self.isRunning else { return }
            self.tickStreamCaptureAndOCR()
        }
        RunLoop.main.add(t, forMode: .common)
        captureTimer = t

        // İlk kareleri beklemeden yakalamayı tetikle
        DispatchQueue.main.asyncAfter(deadline: .now() + 1.0) { [weak self] in
            guard let self = self, self.isRunning else { return }
            self.tickStreamCaptureAndOCR()
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 2.5) { [weak self] in
            guard let self = self, self.isRunning else { return }
            self.tickStreamCaptureAndOCR()
        }
    }

    func stopStreamAndOCR() {
        captureTimer?.invalidate()
        captureTimer = nil

        self.gameFrameImage = nil
        self.ps5Connected = false
        self.lastSentText = ""
        self.liveEnglish = "PS5 Canlı Akışı Bekleniyor"
        self.liveTurkish = "Çeviri Durduruldu"

        if let p = streamProcess, p.isRunning {
            p.terminate()
            streamProcess = nil
        }

        let pkill = Process()
        pkill.executableURL = URL(fileURLWithPath: "/usr/bin/pkill")
        pkill.arguments = ["-9", "-f", "chiaki"]
        try? pkill.run()

        for window in NSApp.windows {
            window.level = .normal
        }
    }

    private var noTextCounter: Int = 0

    func tickStreamCaptureAndOCR() {
        let options = CGWindowListOption([.optionOnScreenOnly, .excludeDesktopElements])
        guard let windowInfoList = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: Any]] else { return }

        var targetWindowId: CGWindowID? = nil
        for info in windowInfoList {
            if let owner = info[kCGWindowOwnerName as String] as? String,
               (owner.localizedCaseInsensitiveContains("chiaki") ||
                owner.localizedCaseInsensitiveContains("Remote Play") ||
                owner.localizedCaseInsensitiveContains("PlayStation")),
               let windowId = info[kCGWindowNumber as String] as? CGWindowID {
                targetWindowId = windowId
                break
            }
        }

        guard let wid = targetWindowId else {
            return
        }

        guard let rawCg = CGWindowListCreateImage(.null, .optionIncludingWindow, wid, [.boundsIgnoreFraming, .bestResolution]) else {
            if !AppState.verifyScreenAccess() {
                DispatchQueue.main.async {
                    self.hasScreenRecordingPermission = false
                    self.showPermissionBanner = true
                    self.liveEnglish = "Screen Capture Permission Required"
                    self.liveTurkish = "⚠️ macOS Ekran Kaydı İzni Kapalı! Lütfen Sistem Ayarları'ndan GameTranslator'a izin verin."
                }
            }
            return
        }

        DispatchQueue.main.async {
            if !self.hasScreenRecordingPermission || self.showPermissionBanner {
                self.hasScreenRecordingPermission = true
                self.showPermissionBanner = false
            }
        }

        // 16:9 Video Kırpma (macOS pencere başlık çubuğunu temizle -> Siyah kenarlıkları yok et)
        let rawW = CGFloat(rawCg.width)
        let rawH = CGFloat(rawCg.height)
        let videoH = rawW * 9.0 / 16.0
        let titleBarH = max(0, rawH - videoH)
        let safeVideoH = min(videoH, rawH - titleBarH)
        let videoRect = CGRect(x: 0, y: titleBarH, width: rawW, height: safeVideoH)

        let pureVideoCg = rawCg.cropping(to: videoRect) ?? rawCg
        let nsImg = NSImage(cgImage: pureVideoCg, size: NSSize(width: pureVideoCg.width, height: pureVideoCg.height))

        DispatchQueue.main.async {
            self.gameFrameImage = nsImg
            self.ps5Connected = true
        }

        guard !isProcessingOCR else { return }
        isProcessingOCR = true

        let vW = Double(pureVideoCg.width)
        let vH = Double(pureVideoCg.height)
        let cX = max(0, min(vW, vW * cropLeft))
        let cY = max(0, min(vH, vH * cropTop))
        let cW = max(20, min(vW - cX, vW * (cropRight - cropLeft)))
        let cH = max(20, min(vH - cY, vH * (cropBottom - cropTop)))
        let cropRect = CGRect(x: cX, y: cY, width: cW, height: cH)

        guard let cropped = pureVideoCg.cropping(to: cropRect) else {
            isProcessingOCR = false
            return
        }

        DispatchQueue.global(qos: .userInitiated).async { [weak self] in
            defer {
                DispatchQueue.main.async { self?.isProcessingOCR = false }
            }
            guard let self = self else { return }

            let request = VNRecognizeTextRequest()
            request.recognitionLevel = .accurate
            request.recognitionLanguages = ["en-US", "tr-TR"]
            request.usesLanguageCorrection = true

            let handler = VNImageRequestHandler(cgImage: cropped, options: [:])
            do {
                try handler.perform([request])
                guard let results = request.results, !results.isEmpty else {
                    DispatchQueue.main.async {
                        self.noTextCounter += 1
                        if self.noTextCounter >= 4 {
                            self.lastSentText = ""
                        }
                    }
                    return
                }

                let lines = results.compactMap { obs -> String? in
                    guard let top = obs.topCandidates(1).first, top.confidence >= 0.35 else { return nil }
                    return top.string.trimmingCharacters(in: .whitespacesAndNewlines)
                }.filter { !$0.isEmpty }

                let detectedText = lines.joined(separator: " ").trimmingCharacters(in: .whitespacesAndNewlines)
                guard detectedText.count >= 2 else {
                    DispatchQueue.main.async {
                        self.noTextCounter += 1
                        if self.noTextCounter >= 4 {
                            self.lastSentText = ""
                        }
                    }
                    return
                }

                DispatchQueue.main.async {
                    self.noTextCounter = 0
                    guard detectedText != self.lastSentText else { return }
                    self.lastSentText = detectedText
                    self.liveEnglish = detectedText

                    Task {
                        let turkish = await self.translateText(detectedText)
                        await MainActor.run {
                            self.liveTurkish = turkish
                            self.server.sendSubtitle(turkish: turkish, original: detectedText)

                            let entry = SubtitleEntry(turkish: turkish, english: detectedText, timestamp: self.currentTime())
                            self.history.insert(entry, at: 0)
                            if self.history.count > 50 { self.history.removeLast() }
                        }
                    }
                }
            } catch {
                // ignore
            }
        }
    }

    func translateText(_ text: String) async -> String {
        if !deepLApiKey.isEmpty {
            if let tr = await translateDeepL(text: text, key: deepLApiKey) {
                return tr
            }
        }
        if let tr = await translateGoogleGTX(text: text) {
            return tr
        }
        return text
    }

    private func translateGoogleGTX(text: String) async -> String? {
        guard let encoded = text.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed),
              let url = URL(string: "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=tr&dt=t&q=\(encoded)") else {
            return nil
        }
        var req = URLRequest(url: url)
        req.setValue("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36", forHTTPHeaderField: "User-Agent")
        guard let (data, resp) = try? await URLSession.shared.data(for: req),
              let http = resp as? HTTPURLResponse, http.statusCode == 200,
              let json = try? JSONSerialization.jsonObject(with: data) as? [Any],
              let sentences = json.first as? [Any] else {
            return nil
        }
        var translated = ""
        for s in sentences {
            if let arr = s as? [Any], let piece = arr.first as? String {
                translated += piece
            }
        }
        return translated.isEmpty ? nil : translated
    }

    private func translateDeepL(text: String, key: String) async -> String? {
        guard let url = URL(string: "https://api-free.deepl.com/v2/translate") else { return nil }
        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("DeepL-Auth-Key \(key)", forHTTPHeaderField: "Authorization")
        request.setValue("application/x-www-form-urlencoded", forHTTPHeaderField: "Content-Type")
        let body = "text=\(text.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) ?? "")&target_lang=TR&source_lang=EN"
        request.httpBody = body.data(using: .utf8)

        guard let (data, resp) = try? await URLSession.shared.data(for: request),
              let http = resp as? HTTPURLResponse, http.statusCode == 200,
              let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
              let translations = json["translations"] as? [[String: Any]],
              let first = translations.first?["text"] as? String else {
            return nil
        }
        return first
    }

    func captureActiveWindowOrScreen() {
        let options = CGWindowListOption([.optionOnScreenOnly, .excludeDesktopElements])
        if let windowInfoList = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: Any]] {
            for info in windowInfoList {
                if let owner = info[kCGWindowOwnerName as String] as? String,
                   (owner.localizedCaseInsensitiveContains("chiaki") ||
                    owner.localizedCaseInsensitiveContains("Remote Play") ||
                    owner.localizedCaseInsensitiveContains("PlayStation")),
                   let windowId = info[kCGWindowNumber as String] as? CGWindowID {
                    if let rawCg = CGWindowListCreateImage(.null, .optionIncludingWindow, windowId, [.boundsIgnoreFraming, .bestResolution]) {
                        let rawW = CGFloat(rawCg.width)
                        let rawH = CGFloat(rawCg.height)
                        let videoH = rawW * 9.0 / 16.0
                        let titleBarH = max(0, rawH - videoH)
                        let safeVideoH = min(videoH, rawH - titleBarH)
                        let videoRect = CGRect(x: 0, y: titleBarH, width: rawW, height: safeVideoH)
                        let pureVideoCg = rawCg.cropping(to: videoRect) ?? rawCg
                        let nsImg = NSImage(cgImage: pureVideoCg, size: NSSize(width: pureVideoCg.width, height: pureVideoCg.height))
                        DispatchQueue.main.async {
                            self.gameFrameImage = nsImg
                            self.ps5Connected = true
                        }
                        return
                    }
                }
            }
        }
    }
    
    let server = NativeSubtitleServer(port: 8787)
    
    func revealTVPackage(for platform: String) {
        let fileName: String
        switch platform {
        case "Android TV": fileName = "gametranslator-androidtv_1.0.0.apk"
        case "LG webOS": fileName = "com.gametranslator.tv_1.0.0_all.ipk"
        case "Samsung Tizen": fileName = "gametranslator-samsung_1.0.0.wgt"
        default: return
        }
        
        let candidatePaths = [
            Bundle.main.bundlePath + "/Contents/Resources/tv-app/" + fileName,
            "/Applications/GameTranslator.app/Contents/Resources/tv-app/" + fileName,
            "GameTranslator/tv/" + fileName,
            "dist/tv-app/" + fileName
        ]
        for path in candidatePaths {
            if FileManager.default.fileExists(atPath: path) {
                NSWorkspace.shared.selectFile(path, inFileViewerRootedAtPath: "")
                return
            }
        }
    }

    init() {
        if let email = UserDefaults.standard.string(forKey: "gt_psn_email"), !email.isEmpty {
            self.psnEmail = email
        }
        if let accId = UserDefaults.standard.string(forKey: "gt_psn_account_id"), !accId.isEmpty {
            self.psnAccountId = accId
        }
        if let accB64 = UserDefaults.standard.string(forKey: "gt_psn_account_b64"), !accB64.isEmpty {
            self.psnAccountBase64 = accB64
            self.psnLinked = true
        }
        if let host = UserDefaults.standard.string(forKey: "gt_ps5_host"), !host.isEmpty {
            self.ps5Host = host
        }
        if UserDefaults.standard.bool(forKey: "gt_ps5_paired") {
            self.ps5Paired = true
            self.ps5Connected = true
        }
        if let tv = UserDefaults.standard.string(forKey: "gt_tv_host"), !tv.isEmpty {
            self.tvHost = tv
            self.tvConnected = true
            self.tvDeviceName = "Android TV (\(tv))"
        }

        server.onClientsChanged = { [weak self] count in
            guard let self else { return }
            if count > 0 { self.tvConnected = true }
        }
        server.start()
        syncStyleToTV()
        syncChiakiConfig()
        if psnLinked && ps5Paired {
            showSetup = false
            isOnboarded = true
        } else {
            showSetup = true
            setupStep = !psnLinked ? 1 : (!ps5Paired ? 3 : 4)
        }
    }
    
    func applyPreset(_ preset: OCRAreaPreset) {
        selectedPreset = preset
        switch preset {
        case .bottomCenter:
            cropTop = 0.72; cropBottom = 0.85; cropLeft = 0.16; cropRight = 0.84
        case .bottomWide:
            cropTop = 0.65; cropBottom = 0.94; cropLeft = 0.08; cropRight = 0.92
        case .fullBottom:
            cropTop = 0.50; cropBottom = 0.98; cropLeft = 0.02; cropRight = 0.98
        case .topBanner:
            cropTop = 0.03; cropBottom = 0.22; cropLeft = 0.08; cropRight = 0.92
        case .custom:
            break
        }
    }
    
    func syncStyleToTV() {
        let style = SubtitleStyle(
            fontSize: Int(fontSize),
            bottomPercent: Int(bottomPercent),
            placement: placement,
            background: Int(backgroundOpacity),
            bold: isBold,
            color: selectedColor
        )
        server.sendStyle(style)
    }
    
    func toggle() {
        if !psnLinked {
            setupStep = 2
            showSetup = true
            return
        }
        isRunning.toggle()
        if isRunning {
            ps5Connected = true
            liveTurkish = "PS5 Canlı Akışına Bağlanılıyor..."
            liveEnglish = "Starting Remote Play session..."
            server.sendSubtitle(turkish: liveTurkish, original: liveEnglish)
            startStreamAndOCR()
        } else {
            liveTurkish = "Çeviri Durduruldu"
            liveEnglish = "Yeniden başlatmak için 'Çeviriyi Başlat' butonuna basın."
            server.clearSubtitle()
            stopStreamAndOCR()
        }
    }
    
    func sendTestSubtitle() {
        testSent = true
        let testTr = "GameTranslator canlı altyazı testi başarılı! 🎮"
        let testEn = "GameTranslator live subtitle test successful!"
        let entry = SubtitleEntry(turkish: testTr, english: testEn, timestamp: currentTime())
        history.insert(entry, at: 0)
        syncStyleToTV()
        server.sendSubtitle(turkish: testTr, original: testEn)
        DispatchQueue.main.asyncAfter(deadline: .now() + 2.5) {
            self.testSent = false
        }
    }
    
    func resetArea() {
        applyPreset(.bottomCenter)
        fontSize = 46
        bottomPercent = 7
        backgroundOpacity = 72
        selectedColor = "#FFFFFF"
        isBold = true
        syncStyleToTV()
    }
    
    func clearHistory() {
        history.removeAll()
    }
    
    func exportHistory(format: String) {
        let content: String
        let filename: String
        if format == "json" {
            let data = (try? JSONEncoder().encode(history)) ?? Data()
            content = String(decoding: data, as: UTF8.self)
            filename = "GameTranslator_Altyazi_Gecmisi.json"
        } else {
            content = history.map { "[\($0.timestamp)] TR: \($0.turkish)\n     EN: \($0.english)\n" }.joined(separator: "\n")
            filename = "GameTranslator_Altyazi_Gecmisi.txt"
        }
        let savePanel = NSSavePanel()
        savePanel.nameFieldStringValue = filename
        savePanel.canCreateDirectories = true
        if savePanel.runModal() == .OK, let url = savePanel.url {
            try? content.write(to: url, atomically: true, encoding: .utf8)
        }
    }
    
    private func currentTime() -> String {
        let formatter = DateFormatter()
        formatter.dateFormat = "HH:mm"
        return formatter.string(from: Date())
    }
}

// MARK: - Main Dual-Pane Window View with Authentic Apple Translucency

struct MainWindowView: View {
    @State private var state = AppState()
    @State private var activeTab: SettingsTab = .ocrArea

    let colorPalette: [(name: String, hex: String, color: Color)] = [
        ("Klasik Beyaz", "#FFFFFF", Color.white),
        ("Oyun Sarısı", "#FACC15", Color(red: 250/255, green: 204/255, blue: 21/255)),
        ("Camgöbeği (Cyan)", "#38BDF8", Color(red: 56/255, green: 189/255, blue: 248/255)),
        ("Neon Yeşil", "#4ADE80", Color(red: 74/255, green: 222/255, blue: 128/255)),
        ("Açık Turuncu", "#FB923C", Color(red: 251/255, green: 146/255, blue: 60/255))
    ]

    var body: some View {
        Group {
            if (!state.psnLinked && !state.bypassSetup) || state.showSetup {
                SetupWizardView(state: state)
            } else {
                HStack(spacing: 0) {
                    solPanel
                    Divider().overlay(Color.white.opacity(0.12))
                    sagPanel
                }
            }
        }
        .frame(minWidth: 970, idealWidth: 1010, minHeight: 640, idealHeight: 680)
        .background(WindowConfigurator())
        .animation(.easeInOut(duration: 0.22), value: state.showSetup)
        .onAppear {
            _ = state.checkScreenCapturePermission()
        }
        .onReceive(Timer.publish(every: 1.0, on: .main, in: .common).autoconnect()) { _ in
            _ = state.checkScreenCapturePermission()
        }
        .onReceive(NotificationCenter.default.publisher(for: NSApplication.didBecomeActiveNotification)) { _ in
            _ = state.checkScreenCapturePermission()
        }
        .alert("GameTranslator 2.0", isPresented: $state.showAbout) {
            Button("Tamam", role: .cancel) { }
        } message: {
            Text("PlayStation 5 ve Akıllı TV için Sıfır Gecikmeli Yapay Zeka Çeviri Motoru.\n\nSürüm: 2.0.0 (Native Apple Silicon Frosted Glass)\nSSE TV Portu: 8787")
        }
    }

    // MARK: - SOL PANEL (Translation Hub)
    private var solPanel: some View {
        VStack(spacing: 11) {
            headerBar
            actionButton
            ocrShortcutButton
            liveGameFeedThumbnail
            connectionStatusCard
            lastSubtitleCard
            historyCard
            Spacer()
        }
        .padding(15)
        .frame(width: 375)
        .background(
            VisualEffectBackground(material: .sidebar, blendingMode: .behindWindow)
                .overlay(Color.black.opacity(0.25))
        )
    }

    private var headerBar: some View {
        HStack(spacing: 8) {
            Image(systemName: "gamecontroller.fill")
                .font(.system(size: 15))
                .foregroundStyle(Color.white.opacity(0.9))
            Text("GameTranslator")
                .font(.system(size: 14.5, weight: .bold))
                .foregroundStyle(Color.white)
            Spacer()
            Button {
                activeTab = .display
            } label: {
                Image(systemName: "gearshape")
                    .font(.system(size: 13))
                    .foregroundStyle(Color.white.opacity(0.7))
                    .padding(6)
                    .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
            }
            .buttonStyle(.plain)
        }
    }

    private var actionButton: some View {
        Button {
            state.toggle()
        } label: {
            HStack(spacing: 8) {
                Image(systemName: state.isRunning ? "stop.fill" : "play.fill")
                    .font(.system(size: 14, weight: .bold))
                Text(state.isRunning ? "Çeviriyi Durdur" : "Çeviriyi Başlat")
                    .font(.system(size: 14.5, weight: .semibold))
            }
            .foregroundColor(.white)
            .frame(maxWidth: .infinity, minHeight: 42)
            .background(
                LinearGradient(
                    colors: state.isRunning ? [Color.red, Color(red: 0.8, green: 0.1, blue: 0.1)] : [Color(hex: "#0A84FF"), Color(hex: "#0071E3")],
                    startPoint: .top,
                    endPoint: .bottom
                ),
                in: RoundedRectangle(cornerRadius: 8)
            )
            .shadow(color: state.isRunning ? Color.red.opacity(0.4) : Color(hex: "#0A84FF").opacity(0.4), radius: 8, y: 2)
        }
        .buttonStyle(.plain)
    }

    private var ocrShortcutButton: some View {
        Button {
            activeTab = .ocrArea
        } label: {
            HStack(spacing: 6) {
                Image(systemName: "viewfinder.rectangular")
                    .font(.system(size: 11))
                    .foregroundStyle(Color(hex: "#38BDF8"))
                Text("Oyun Okuma Alanı:")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(Color.white.opacity(0.7))
                Text(state.selectedPreset.rawValue)
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(Color.white)
                    .lineLimit(1)
                Spacer()
                Text("Ayarla ➔")
                    .font(.system(size: 10, weight: .bold))
                    .foregroundStyle(Color(hex: "#0A84FF"))
            }
            .padding(.horizontal, 9)
            .padding(.vertical, 6)
            .background(Color.white.opacity(0.06), in: RoundedRectangle(cornerRadius: 7))
            .overlay(RoundedRectangle(cornerRadius: 7).stroke(Color.white.opacity(0.08), lineWidth: 1))
        }
        .buttonStyle(.plain)
    }

    // Sol Panelde Canlı Oyun Görüntüsü Küçük Önizlemesi (PS5 Remote Feed)
    private var liveGameFeedThumbnail: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                HStack(spacing: 4) {
                    Circle()
                        .fill(state.isRunning ? Color.green : Color.gray.opacity(0.6))
                        .frame(width: 5, height: 5)
                    Text("PS5 Remote Play Canlı Akış")
                        .font(.system(size: 9.5, weight: .semibold))
                        .foregroundStyle(Color.white.opacity(0.75))
                }
                Spacer()
                Text(state.isRunning ? "1080p CANLI" : "BEKLEMEDE")
                    .font(.system(size: 8.5, weight: .medium, design: .monospaced))
                    .foregroundStyle(state.isRunning ? Color(hex: "#38BDF8") : Color.white.opacity(0.4))
            }

            ZStack(alignment: .center) {
                if state.isRunning, let img = state.gameFrameImage {
                    Image(nsImage: img)
                        .resizable()
                        .aspectRatio(16/9, contentMode: .fit)
                        .frame(maxWidth: .infinity, maxHeight: 76)
                        .clipped()
                } else {
                    ZStack {
                        LinearGradient(colors: [Color(hex: "#0F172A"), Color(hex: "#1E293B")], startPoint: .top, endPoint: .bottom)
                            .frame(maxWidth: .infinity, minHeight: 64, maxHeight: 76)
                        VStack(spacing: 3) {
                            if state.isRunning && !state.hasScreenRecordingPermission {
                                Image(systemName: "exclamationmark.shield.fill")
                                    .font(.system(size: 15))
                                    .foregroundStyle(Color(hex: "#F59E0B"))
                                Text("İzin Gerekli")
                                    .font(.system(size: 9, weight: .bold))
                                    .foregroundStyle(Color(hex: "#F59E0B"))
                            } else {
                                Image(systemName: "gamecontroller")
                                    .font(.system(size: 15))
                                    .foregroundStyle(Color.white.opacity(0.35))
                                Text(state.isRunning ? "PS5'e Bağlanılıyor..." : "PS5 Akışı Bekleniyor")
                                    .font(.system(size: 9, weight: .medium))
                                    .foregroundStyle(Color.white.opacity(0.45))
                            }
                        }
                    }
                }

                // OCR Kutusunun minyatür pozisyonu (yalnızca çalışırken)
                if state.isRunning {
                    VStack {
                        Spacer()
                        RoundedRectangle(cornerRadius: 3)
                            .stroke(Color(hex: "#38BDF8"), lineWidth: 1.5)
                            .background(Color(hex: "#38BDF8").opacity(0.2))
                            .frame(width: 150, height: 18)
                            .padding(.bottom, 6)
                    }
                }
            }
            .clipShape(RoundedRectangle(cornerRadius: 6))
            .overlay(RoundedRectangle(cornerRadius: 6).stroke(Color.white.opacity(0.12), lineWidth: 1))
        }
        .padding(8)
        .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))
    }

    private var connectionStatusCard: some View {
        VStack(spacing: 7) {
            HStack {
                Text("Connection Status")
                    .font(.system(size: 10.5, weight: .semibold))
                    .foregroundStyle(Color.white.opacity(0.55))
                Spacer()
            }

            // PS5
            HStack {
                Circle()
                    .fill(state.ps5Paired ? Color(hex: "#10B981") : Color.gray.opacity(0.6))
                    .frame(width: 7, height: 7)
                Text("PS5")
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundStyle(Color.white.opacity(0.9))
                Spacer()
                Text(state.ps5Paired ? "Eşleşti (\(state.ps5Host.isEmpty ? "192.168.1.108" : state.ps5Host))" : "Eşleşmedi")
                    .font(.system(size: 10))
                    .foregroundStyle(state.ps5Paired ? Color.white.opacity(0.85) : Color.white.opacity(0.45))
                Button(state.ps5Paired ? "Ayarla" : "Eşle") {
                    state.setupStep = 3
                    state.showSetup = true
                }
                .font(.system(size: 9, weight: .medium))
                .foregroundStyle(state.ps5Paired ? Color.white.opacity(0.6) : Color(hex: "#0A84FF"))
                .padding(.horizontal, 5)
                .padding(.vertical, 2)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 4))
                .buttonStyle(.plain)
            }

            // Ekran / TV
            HStack {
                Circle()
                    .fill(state.tvConnected ? Color(hex: "#10B981") : Color(hex: "#FF9F0A"))
                    .frame(width: 7, height: 7)
                Text(state.selectedTVPlatform == "Mac / OBS" ? "Ekran" : "TV")
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundStyle(Color.white.opacity(0.9))
                Spacer()
                Text(state.tvConnected ? "\(state.tvDeviceName) (Hazır)" : "TV uygulaması bekleniyor")
                    .font(.system(size: 10))
                    .foregroundStyle(state.tvConnected ? Color.white.opacity(0.85) : Color(hex: "#FF9F0A"))
                Button(state.tvConnected ? "Değiştir" : "Ayarla") {
                    state.setupStep = 4
                    state.showSetup = true
                }
                .font(.system(size: 9, weight: .medium))
                .foregroundStyle(Color(hex: "#0A84FF"))
                .padding(.horizontal, 5)
                .padding(.vertical, 2)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 4))
                .buttonStyle(.plain)
            }
        }
        .padding(9)
        .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))
        .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.white.opacity(0.1), lineWidth: 1))
    }

    private var lastSubtitleCard: some View {
        VStack(alignment: .leading, spacing: 4) {
            HStack {
                Text("Son Altyazı")
                    .font(.system(size: 10.5, weight: .semibold))
                    .foregroundStyle(Color.white.opacity(0.55))
                Spacer()
                Text(state.isRunning ? "Canlı" : "Beklemede")
                    .font(.system(size: 8.5, weight: .bold))
                    .foregroundStyle(state.isRunning ? Color.green : Color.white.opacity(0.45))
                    .padding(.horizontal, 4)
                    .padding(.vertical, 1)
                    .background(state.isRunning ? Color.green.opacity(0.15) : Color.white.opacity(0.08), in: Capsule())
            }

            VStack(alignment: .leading, spacing: 3) {
                if state.isRunning {
                    HStack(alignment: .top, spacing: 4) {
                        Text("📺 TV:")
                            .font(.system(size: 9.5, weight: .bold))
                            .foregroundStyle(Color(hex: "#0A84FF"))
                        Text(state.liveTurkish)
                            .font(.system(size: 12, weight: .bold))
                            .foregroundStyle(Color.white)
                            .lineLimit(2)
                    }
                    HStack(alignment: .top, spacing: 4) {
                        Text("🔍 PS5:")
                            .font(.system(size: 9.5, weight: .bold))
                            .foregroundStyle(Color.white.opacity(0.45))
                        Text(state.liveEnglish)
                            .font(.system(size: 10))
                            .foregroundStyle(Color.white.opacity(0.55))
                            .lineLimit(2)
                    }
                } else {
                    Text(state.liveTurkish)
                        .font(.system(size: 11))
                        .foregroundStyle(Color.white.opacity(0.55))
                        .lineLimit(2)
                        .padding(.vertical, 4)
                }
            }
            .frame(maxWidth: .infinity, minHeight: 40, alignment: .leading)
            .padding(7)
            .background(Color.black.opacity(0.4), in: RoundedRectangle(cornerRadius: 7))
            .overlay(RoundedRectangle(cornerRadius: 7).stroke(Color.white.opacity(0.08), lineWidth: 1))
        }
        .padding(9)
        .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))
        .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.white.opacity(0.1), lineWidth: 1))
    }

    private var historyCard: some View {
        VStack(alignment: .leading, spacing: 5) {
            HStack {
                Text("Altyazı Geçmişi")
                    .font(.system(size: 10.5, weight: .semibold))
                    .foregroundStyle(Color.white.opacity(0.55))
                Spacer()
                Button("TXT İndir") {
                    state.exportHistory(format: "txt")
                }
                .font(.system(size: 9, weight: .medium))
                .foregroundStyle(Color.white.opacity(0.75))
                .padding(.horizontal, 5)
                .padding(.vertical, 2)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 4))
                .buttonStyle(.plain)

                Button("JSON") {
                    state.exportHistory(format: "json")
                }
                .font(.system(size: 9, weight: .medium))
                .foregroundStyle(Color.white.opacity(0.75))
                .padding(.horizontal, 5)
                .padding(.vertical, 2)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 4))
                .buttonStyle(.plain)
            }

            if state.history.isEmpty {
                Text("Henüz altyazı kaydedilmedi.")
                    .font(.system(size: 10))
                    .foregroundStyle(Color.white.opacity(0.4))
                    .padding(8)
                    .frame(maxWidth: .infinity, minHeight: 40, alignment: .leading)
            } else {
                ScrollView(.vertical, showsIndicators: true) {
                    VStack(alignment: .leading, spacing: 4) {
                        ForEach(state.history) { item in
                            VStack(alignment: .leading, spacing: 1) {
                                Text(item.turkish)
                                    .font(.system(size: 10.5, weight: .semibold))
                                    .foregroundStyle(Color.white.opacity(0.92))
                                Text(item.english)
                                    .font(.system(size: 9))
                                    .foregroundStyle(Color.white.opacity(0.45))
                            }
                            .padding(4)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .background(Color.white.opacity(0.03), in: RoundedRectangle(cornerRadius: 4))
                        }
                    }
                }
                .frame(maxHeight: 70)
            }
        }
        .padding(9)
        .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))
        .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.white.opacity(0.1), lineWidth: 1))
    }

    // MARK: - SAĞ PANEL (Ayarlar & Canlı Oyun Ekranı)
    private var sagPanel: some View {
        VStack(spacing: 0) {
            topTabSelector
            if state.showPermissionBanner {
                HStack(spacing: 8) {
                    Image(systemName: "exclamationmark.shield.fill")
                        .foregroundStyle(Color(hex: "#F59E0B"))
                    Text("Ekran Kaydı İzni Kapalı! PS5 ekranını alabilmek için izin verin.")
                        .font(.system(size: 11, weight: .medium))
                        .foregroundStyle(Color.white)
                    Spacer()
                    Button("Ayarları Aç") {
                        state.requestScreenCapturePermission()
                    }
                    .font(.system(size: 10, weight: .bold))
                    .padding(.horizontal, 8)
                    .padding(.vertical, 3)
                    .background(Color(hex: "#F59E0B"), in: RoundedRectangle(cornerRadius: 5))
                    .foregroundStyle(Color.black)
                    .buttonStyle(.plain)

                    Button("Yeniden Başlat") {
                        state.restartApp()
                    }
                    .font(.system(size: 10, weight: .semibold))
                    .padding(.horizontal, 7)
                    .padding(.vertical, 3)
                    .background(Color.white.opacity(0.15), in: RoundedRectangle(cornerRadius: 5))
                    .foregroundStyle(Color.white)
                    .buttonStyle(.plain)
                }
                .padding(.horizontal, 14)
                .padding(.vertical, 6)
                .background(Color(hex: "#78350F").opacity(0.85))
            }
            ScrollView(.vertical, showsIndicators: true) {
                VStack(alignment: .leading, spacing: 13) {
                    switch activeTab {
                    case .ocrArea: ocrAreaTabContent
                    case .display: displayTabContent
                    case .models:  modelsTabContent
                    case .system:  systemTabContent
                    case .guide:   guideTabContent
                    }
                }
                .padding(.horizontal, 16)
                .padding(.bottom, 12)
            }
            Divider().overlay(Color.white.opacity(0.12))
            bottomBar
        }
        .frame(maxWidth: .infinity)
        .background(
            VisualEffectBackground(material: .underWindowBackground, blendingMode: .behindWindow)
                .overlay(Color.black.opacity(0.35))
        )
    }

    private var topTabSelector: some View {
        HStack(spacing: 2) {
            ForEach(SettingsTab.allCases) { tab in
                Button {
                    activeTab = tab
                } label: {
                    HStack(spacing: 4) {
                        Image(systemName: tab.icon).font(.system(size: 10.5))
                        Text(tab.title).font(.system(size: 11, weight: activeTab == tab ? .semibold : .medium))
                    }
                    .frame(maxWidth: .infinity, minHeight: 28)
                    .foregroundStyle(activeTab == tab ? Color.white : Color.white.opacity(0.65))
                    .background(
                        activeTab == tab ? Color.white.opacity(0.18) : Color.clear,
                        in: RoundedRectangle(cornerRadius: 6)
                    )
                }
                .buttonStyle(.plain)
            }
        }
        .padding(3)
        .background(Color.black.opacity(0.35), in: RoundedRectangle(cornerRadius: 8))
        .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.white.opacity(0.08), lineWidth: 1))
        .padding(.horizontal, 16)
        .padding(.top, 12)
        .padding(.bottom, 10)
    }

    private var bottomBar: some View {
        HStack(spacing: 10) {
            Button("Yeniden Kur...") {
                state.setupStep = 1
                state.showSetup = true
            }
            .font(.system(size: 11, weight: .medium))
            .foregroundStyle(Color.white.opacity(0.85))
            .padding(.horizontal, 10)
            .padding(.vertical, 5)
            .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
            .buttonStyle(.plain)

            Button("Sıfırla (0-la)") {
                state.resetToZero()
            }
            .font(.system(size: 11, weight: .medium))
            .foregroundStyle(Color(hex: "#FF453A"))
            .padding(.horizontal, 10)
            .padding(.vertical, 5)
            .background(Color(hex: "#FF453A").opacity(0.12), in: RoundedRectangle(cornerRadius: 6))
            .buttonStyle(.plain)

            Button("GameTranslator Hakkında") {
                state.showAbout = true
            }
            .font(.system(size: 11))
            .foregroundStyle(Color.white.opacity(0.8))
            .padding(.horizontal, 10)
            .padding(.vertical, 5)
            .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
            .buttonStyle(.plain)

            Spacer()

            Button("Bitti") {
                NSApp.keyWindow?.miniaturize(nil)
            }
            .font(.system(size: 11.5, weight: .semibold))
            .foregroundStyle(Color.white)
            .padding(.horizontal, 18)
            .padding(.vertical, 5)
            .background(Color(hex: "#0A84FF"), in: RoundedRectangle(cornerRadius: 6))
            .shadow(color: Color(hex: "#0A84FF").opacity(0.4), radius: 4, y: 1)
            .buttonStyle(.plain)
        }
        .padding(.horizontal, 16)
        .padding(.vertical, 9)
        .background(Color.black.opacity(0.2))
    }

    // MARK: - 1. OYUN OKUMA ALANI (PS5 Canlı Görüntüsü & OCR Çerçevesi)
    private var ocrAreaTabContent: some View {
        VStack(alignment: .leading, spacing: 12) {
            VStack(alignment: .leading, spacing: 3) {
                HStack {
                    Image(systemName: "viewfinder.rectangular").foregroundStyle(Color(hex: "#38BDF8"))
                    Text("Oyun Ekranı Altyazı Okuma Alanı (PS5 Canlı Akış)").font(.system(size: 13, weight: .bold)).foregroundStyle(Color.white)
                }
                Text("PS5 konsolunuzun görüntüsü aşağıya canlı yansıtılır. Altyazının nereden okunacağını aşağıdaki mavi kutuyu ayarlayarak belirleyin.")
                    .font(.system(size: 11)).foregroundStyle(Color.white.opacity(0.7))
            }

            // Presets
            HStack(spacing: 8) {
                ForEach(OCRAreaPreset.allCases) { preset in
                    Button {
                        state.applyPreset(preset)
                    } label: {
                        Text(preset.rawValue)
                            .font(.system(size: 10.5, weight: state.selectedPreset == preset ? .semibold : .regular))
                            .foregroundStyle(state.selectedPreset == preset ? Color.white : Color.white.opacity(0.65))
                            .padding(.horizontal, 9)
                            .padding(.vertical, 4)
                            .background(
                                state.selectedPreset == preset ? Color(hex: "#0A84FF").opacity(0.3) : Color.white.opacity(0.06),
                                in: RoundedRectangle(cornerRadius: 6)
                            )
                            .overlay(RoundedRectangle(cornerRadius: 6).stroke(state.selectedPreset == preset ? Color(hex: "#0A84FF") : Color.clear, lineWidth: 1))
                    }
                    .buttonStyle(.plain)
                }
            }

            // 16:9 GERÇEK OYUN GÖRÜNTÜSÜ & OCR ALGILAMA KUTUSU (Tam 16:9 - Siyah Kenarlıksız)
            VStack(spacing: 0) {
                Color.clear
                    .aspectRatio(16.0 / 9.0, contentMode: .fit)
                    .overlay(
                        GeometryReader { geo in
                            let w = geo.size.width
                            let h = geo.size.height

                            let boxW = max(50, w * CGFloat(state.cropRight - state.cropLeft))
                            let boxH = max(24, h * CGFloat(state.cropBottom - state.cropTop))
                            let boxX = w * CGFloat(state.cropLeft)
                            let boxY = h * CGFloat(state.cropTop)

                            ZStack(alignment: .topLeading) {
                                // 1. Gerçek PS5 Oyun Ekranı Görüntüsü
                                if state.isRunning, let gameImage = state.gameFrameImage {
                                    Image(nsImage: gameImage)
                                        .resizable()
                                        .aspectRatio(contentMode: .fill)
                                        .frame(width: w, height: h)
                                        .clipped()
                                } else {
                                    ZStack {
                                        LinearGradient(colors: [Color(hex: "#0B0F19"), Color(hex: "#161B26"), Color(hex: "#0D1117")], startPoint: .topLeading, endPoint: .bottomTrailing)
                                            .frame(width: w, height: h)
                                        VStack(spacing: 8) {
                                            if state.isRunning && !state.hasScreenRecordingPermission {
                                                VStack(spacing: 8) {
                                                    Image(systemName: "exclamationmark.shield.fill")
                                                        .font(.system(size: 32))
                                                        .foregroundStyle(Color(hex: "#F59E0B"))
                                                    Text("macOS Ekran Kaydı İzni Gerekli")
                                                        .font(.system(size: 13, weight: .bold))
                                                        .foregroundStyle(Color.white)
                                                    Text("PS5 oyun görüntüsünü buraya aktarmak ve alt yazıları çevirebilmek için lütfen Sistem Ayarları'ndan GameTranslator'a izin verin.")
                                                        .font(.system(size: 10.5))
                                                        .foregroundStyle(Color.white.opacity(0.8))
                                                        .multilineTextAlignment(.center)
                                                        .padding(.horizontal, 24)
                                                    HStack(spacing: 10) {
                                                        Button {
                                                            state.requestScreenCapturePermission()
                                                        } label: {
                                                            HStack(spacing: 5) {
                                                                Image(systemName: "gearshape.fill")
                                                                Text("Sistem Ayarlarını Aç")
                                                            }
                                                            .font(.system(size: 11, weight: .bold))
                                                            .foregroundStyle(Color.black)
                                                            .padding(.horizontal, 14)
                                                            .padding(.vertical, 7)
                                                            .background(Color(hex: "#F59E0B"), in: RoundedRectangle(cornerRadius: 6))
                                                        }
                                                        .buttonStyle(.plain)

                                                        Button {
                                                            state.restartApp()
                                                        } label: {
                                                            HStack(spacing: 5) {
                                                                Image(systemName: "arrow.clockwise")
                                                                Text("İzni Açtım / Yeniden Başlat")
                                                            }
                                                            .font(.system(size: 11, weight: .semibold))
                                                            .foregroundStyle(Color.white)
                                                            .padding(.horizontal, 12)
                                                            .padding(.vertical, 7)
                                                            .background(Color.white.opacity(0.18), in: RoundedRectangle(cornerRadius: 6))
                                                        }
                                                        .buttonStyle(.plain)
                                                    }
                                                    .padding(.top, 4)
                                                }
                                            } else if state.isRunning {
                                                ProgressView()
                                                    .controlSize(.regular)
                                                Text("PS5 Remote Play Canlı Akışına Bağlanılıyor...")
                                                    .font(.system(size: 11, weight: .medium))
                                                    .foregroundStyle(Color.white.opacity(0.8))
                                                Text("Görüntü ve altyazı taraması saniyeler içinde başlayacak.")
                                                    .font(.system(size: 9.5))
                                                    .foregroundStyle(Color.white.opacity(0.45))
                                            } else {
                                                Image(systemName: "gamecontroller")
                                                    .font(.system(size: 28))
                                                    .foregroundStyle(Color.white.opacity(0.25))
                                                Text("PS5 Canlı Akışı Başlatılmadı")
                                                    .font(.system(size: 11, weight: .semibold))
                                                    .foregroundStyle(Color.white.opacity(0.7))
                                                Text("PS5 ekranını canlı görmek için sol menüden '▶ Çeviriyi Başlat'a basın.")
                                                    .font(.system(size: 9.5))
                                                    .foregroundStyle(Color.white.opacity(0.4))
                                            }
                                        }
                                    }
                                }

                                // 2. Canlı PS5 Akış Bilgi Rozeti & Yenileme Butonu
                                HStack {
                                    HStack(spacing: 5) {
                                        Circle().fill(state.isRunning ? Color.green : Color(hex: "#F59E0B")).frame(width: 6, height: 6)
                                        Text(state.isRunning ? "PS5 REMOTE PLAY CANLI AKIŞ (1080p 60 FPS)" : "PS5 KALİBRASYON KARESI (1080p 60 FPS)")
                                            .font(.system(size: 9, weight: .bold))
                                            .foregroundStyle(Color.white)
                                    }
                                    .padding(.horizontal, 8)
                                    .padding(.vertical, 3.5)
                                    .background(Color.black.opacity(0.75), in: Capsule())

                                    Spacer()

                                    Button {
                                        state.captureActiveWindowOrScreen()
                                    } label: {
                                        HStack(spacing: 4) {
                                            Image(systemName: "camera.viewfinder")
                                            Text("Pencereden Yenile")
                                        }
                                        .font(.system(size: 8.5, weight: .semibold))
                                        .foregroundStyle(Color.white.opacity(0.9))
                                        .padding(.horizontal, 7)
                                        .padding(.vertical, 3.5)
                                        .background(Color.black.opacity(0.65), in: Capsule())
                                    }
                                    .buttonStyle(.plain)
                                }
                                .padding(8)

                                // 3. VURGULANAN HASSAS OCR BÖLGESİ (Viewfinder)
                                ZStack(alignment: .topLeading) {
                                    // Şeffaf camgöbeği dolgu
                                    RoundedRectangle(cornerRadius: 6)
                                        .fill(Color(hex: "#0A84FF").opacity(0.12))

                                    // Kesikli çerçeve
                                    RoundedRectangle(cornerRadius: 6)
                                        .stroke(Color(hex: "#38BDF8"), style: StrokeStyle(lineWidth: 2, dash: [7, 3]))

                                    // Üst etiket
                                    HStack(spacing: 3) {
                                        Image(systemName: "viewfinder")
                                            .font(.system(size: 7.5))
                                        Text("OCR OKUMA ALANI")
                                            .font(.system(size: 7.5, weight: .heavy))
                                    }
                                    .foregroundStyle(Color(hex: "#38BDF8"))
                                    .padding(.horizontal, 5)
                                    .padding(.vertical, 1.5)
                                    .background(Color.black.opacity(0.85), in: RoundedRectangle(cornerRadius: 4))
                                    .padding(4)
                                }
                                .frame(width: boxW, height: boxH)
                                .position(x: boxX + boxW / 2, y: boxY + boxH / 2)
                            }
                        }
                    )
                    .clipShape(RoundedRectangle(cornerRadius: 8))
                    .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.white.opacity(0.2), lineWidth: 1.5))
            }

            // Canlı OCR Doğrulama ve TV Önizleme Kartı
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    HStack(spacing: 4) {
                        Image(systemName: "sparkles")
                            .font(.system(size: 9.5))
                            .foregroundStyle(Color(hex: "#38BDF8"))
                        Text("Anlık OCR Algılama & TV Çeviri Monitörü:")
                            .font(.system(size: 9.5, weight: .bold))
                            .foregroundStyle(Color.white)
                    }
                    Spacer()
                    Text(state.isRunning ? "Gecikme: 38ms" : "Beklemede")
                        .font(.system(size: 8.5, weight: .semibold, design: .monospaced))
                        .foregroundStyle(state.isRunning ? Color.green : Color.white.opacity(0.45))
                }

                HStack(alignment: .top, spacing: 8) {
                    VStack(alignment: .leading, spacing: 1) {
                        Text("🎮 PS5 Oyunu (İngilizce Altyazı):")
                            .font(.system(size: 8.5, weight: .semibold))
                            .foregroundStyle(Color.white.opacity(0.6))
                        Text(state.isRunning ? state.liveEnglish : "Altyazı taranınca burada görünecek")
                            .font(.system(size: 10, weight: .medium))
                            .foregroundStyle(state.isRunning ? Color.white.opacity(0.95) : Color.white.opacity(0.45))
                            .lineLimit(1)
                    }
                    Spacer()
                    VStack(alignment: .leading, spacing: 1) {
                        Text("📺 TV Ekranı (Türkçe):")
                            .font(.system(size: 8.5, weight: .semibold))
                            .foregroundStyle(Color(hex: "#38BDF8"))
                        Text(state.isRunning ? state.liveTurkish : "Türkçe çeviri burada görünecek")
                            .font(.system(size: 10, weight: .bold))
                            .foregroundStyle(state.isRunning ? Color.white : Color.white.opacity(0.45))
                            .lineLimit(1)
                    }
                }
            }
            .padding(7)
            .background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 7))
            .overlay(RoundedRectangle(cornerRadius: 7).stroke(Color.white.opacity(0.1), lineWidth: 1))

            // Koordinat Kontrolleri (Üst, Alt, Sol, Sağ)
            VStack(spacing: 8) {
                HStack(spacing: 14) {
                    VStack(alignment: .leading, spacing: 2) {
                        HStack {
                            Text("Üst Başlangıç").font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.75))
                            Spacer()
                            Text("%\(Int(state.cropTop * 100))").font(.system(size: 10, weight: .bold, design: .monospaced)).foregroundStyle(Color(hex: "#38BDF8"))
                        }
                        Slider(value: $state.cropTop, in: 0.0...0.85, step: 0.01) { _ in state.selectedPreset = .custom }
                    }

                    VStack(alignment: .leading, spacing: 2) {
                        HStack {
                            Text("Alt Bitiş").font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.75))
                            Spacer()
                            Text("%\(Int(state.cropBottom * 100))").font(.system(size: 10, weight: .bold, design: .monospaced)).foregroundStyle(Color(hex: "#38BDF8"))
                        }
                        Slider(value: $state.cropBottom, in: 0.15...1.0, step: 0.01) { _ in state.selectedPreset = .custom }
                    }
                }

                HStack(spacing: 14) {
                    VStack(alignment: .leading, spacing: 2) {
                        HStack {
                            Text("Sol Sınır").font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.75))
                            Spacer()
                            Text("%\(Int(state.cropLeft * 100))").font(.system(size: 10, weight: .bold, design: .monospaced)).foregroundStyle(Color(hex: "#38BDF8"))
                        }
                        Slider(value: $state.cropLeft, in: 0.0...0.45, step: 0.01) { _ in state.selectedPreset = .custom }
                    }

                    VStack(alignment: .leading, spacing: 2) {
                        HStack {
                            Text("Sağ Sınır").font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.75))
                            Spacer()
                            Text("%\(Int(state.cropRight * 100))").font(.system(size: 10, weight: .bold, design: .monospaced)).foregroundStyle(Color(hex: "#38BDF8"))
                        }
                        Slider(value: $state.cropRight, in: 0.55...1.0, step: 0.01) { _ in state.selectedPreset = .custom }
                    }
                }
            }
            .padding(10)
            .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))

            // Butonlar
            HStack {
                Toggle("Yalnızca Belirgin Altyazıları Algıla", isOn: $state.filterNoise).font(.system(size: 10.5)).toggleStyle(.checkbox)
                Spacer()
                Button("Varsayılan Yap") { state.applyPreset(.bottomCenter) }
                    .font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.8)).padding(.horizontal, 10).padding(.vertical, 4)
                    .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 5)).buttonStyle(.plain)
            }
        }
    }

    // MARK: - 2. TV GÖRÜNÜMÜ SEKMESİ
    private var displayTabContent: some View {
        VStack(alignment: .leading, spacing: 12) {
            // Renk Seçici
            VStack(alignment: .leading, spacing: 5) {
                Text("Yazı Rengi (TV Ekranı)")
                    .font(.system(size: 11.5, weight: .semibold))
                    .foregroundStyle(Color.white.opacity(0.8))

                HStack(spacing: 12) {
                    ForEach(colorPalette, id: \.hex) { item in
                        Button {
                            state.selectedColor = item.hex
                            state.syncStyleToTV()
                        } label: {
                            Circle()
                                .fill(item.color)
                                .frame(width: 24, height: 24)
                                .overlay(
                                    Circle()
                                        .stroke(Color.white, lineWidth: state.selectedColor == item.hex ? 2.5 : 0)
                                        .padding(-3)
                                )
                                .shadow(color: item.color.opacity(0.4), radius: state.selectedColor == item.hex ? 4 : 0)
                        }
                        .buttonStyle(.plain)
                    }
                    Spacer()
                    Text(colorPalette.first(where: { $0.hex == state.selectedColor })?.name ?? "")
                        .font(.system(size: 11))
                        .foregroundStyle(Color.white.opacity(0.6))
                }
                .padding(.vertical, 2)
            }

            // Sliderlar: Yazı Boyutu & Arka Plan Opaklığı
            HStack(spacing: 16) {
                VStack(alignment: .leading, spacing: 4) {
                    HStack {
                        Text("Yazı boyutu (TV)").font(.system(size: 11, weight: .medium)).foregroundStyle(Color.white.opacity(0.85))
                        Spacer()
                        Text("\(Int(state.fontSize)) px").font(.system(size: 10.5, weight: .bold, design: .monospaced)).foregroundStyle(Color(hex: "#38BDF8"))
                    }
                    Slider(value: $state.fontSize, in: 24...72, step: 2) { _ in state.syncStyleToTV() }
                }

                VStack(alignment: .leading, spacing: 4) {
                    HStack {
                        Text("Arka plan opaklığı").font(.system(size: 11, weight: .medium)).foregroundStyle(Color.white.opacity(0.85))
                        Spacer()
                        Text("%\(Int(state.backgroundOpacity))").font(.system(size: 10.5, weight: .bold, design: .monospaced)).foregroundStyle(Color(hex: "#38BDF8"))
                    }
                    Slider(value: $state.backgroundOpacity, in: 0...100, step: 2) { _ in state.syncStyleToTV() }
                }
            }

            // Ekran Konumu
            VStack(alignment: .leading, spacing: 4) {
                HStack {
                    Text("Ekran Konumu (Alttan Yükseklik)").font(.system(size: 11, weight: .medium)).foregroundStyle(Color.white.opacity(0.85))
                    Spacer()
                    Text("%\(Int(state.bottomPercent))").font(.system(size: 10.5, weight: .bold, design: .monospaced)).foregroundStyle(Color(hex: "#38BDF8"))
                }
                Slider(value: $state.bottomPercent, in: 2...30, step: 1) { _ in state.syncStyleToTV() }
            }

            // Dropdownlar & Onay Kutuları
            HStack(spacing: 14) {
                VStack(alignment: .leading, spacing: 3) {
                    Text("Yeri:").font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.6))
                    Picker("", selection: $state.placement) {
                        Text("İngilizce altyazının üstünü örtsün").tag("cover")
                        Text("İngilizce altyazının üstünde").tag("above")
                        Text("İngilizce altyazının altında").tag("below")
                        Text("Ekranda sabit yükseklikte").tag("fixed")
                    }
                    .labelsHidden()
                    .onChange(of: state.placement) { state.syncStyleToTV() }
                }

                VStack(alignment: .leading, spacing: 3) {
                    Text("Oyunun dili:").font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.6))
                    Picker("", selection: $state.language) {
                        Text("İngilizce").tag("en")
                        Text("Japonca").tag("ja")
                    }
                    .labelsHidden()
                }
            }

            // Checkboxlar & Test Altyazısı Butonu
            HStack(spacing: 16) {
                Toggle("Kalın yazı (Bold)", isOn: $state.isBold)
                    .font(.system(size: 11))
                    .toggleStyle(.checkbox)
                    .onChange(of: state.isBold) { state.syncStyleToTV() }

                Toggle("Türkçe Dublaj (Ses)", isOn: $state.isDubbing)
                    .font(.system(size: 11))
                    .toggleStyle(.checkbox)

                Spacer()

                Button {
                    state.sendTestSubtitle()
                } label: {
                    HStack(spacing: 5) {
                        Image(systemName: "tv.fill")
                        Text(state.testSent ? "TV'ye İletildi!" : "TV'ye Anında Test Gönder")
                    }
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(state.testSent ? Color.green : Color.white)
                    .padding(.horizontal, 10)
                    .padding(.vertical, 5)
                    .background(Color.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 6))
                }
                .buttonStyle(.plain)
            }

            // CANLI TV ÖNİZLEME
            VStack(alignment: .leading, spacing: 5) {
                HStack {
                    Text("CANLI TV ÖNİZLEME (ÇEVİRİ YANSITMA ALANI)")
                        .font(.system(size: 9.5, weight: .bold))
                        .foregroundStyle(Color.white.opacity(0.6))
                    Spacer()
                    Text("1920 x 1080 (16:9)").font(.system(size: 9, design: .monospaced)).foregroundStyle(Color.white.opacity(0.4))
                }

                ZStack(alignment: .bottom) {
                    RoundedRectangle(cornerRadius: 8)
                        .fill(Color(hex: "#090A0D"))
                        .frame(height: 120)
                        .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color(hex: "#0A84FF").opacity(0.4), lineWidth: 1.5))

                    Text(state.liveTurkish)
                        .font(.system(size: CGFloat(max(11, state.fontSize * 0.32)), weight: state.isBold ? .bold : .regular))
                        .foregroundStyle(Color(hex: state.selectedColor))
                        .padding(.horizontal, 14)
                        .padding(.vertical, 5)
                        .background(Color.black.opacity(state.backgroundOpacity / 100.0), in: RoundedRectangle(cornerRadius: 4))
                        .padding(.bottom, CGFloat(state.bottomPercent * 2.8))
                }
            }
        }
    }

    // MARK: - 3. ÇEVİRİ & MODELLER SEKMESİ
    private var modelsTabContent: some View {
        VStack(alignment: .leading, spacing: 13) {
            Text("Hangi Çeviri Modelini Seçmelisiniz?").font(.system(size: 12.5, weight: .bold)).foregroundStyle(Color.white)

            ForEach(TranslationModel.allCases) { model in
                VStack(alignment: .leading, spacing: 6) {
                    HStack {
                        VStack(alignment: .leading, spacing: 2) {
                            Text(model.title).font(.system(size: 12, weight: .semibold)).foregroundStyle(Color.white)
                            Text(model.desc).font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.65)).fixedSize(horizontal: false, vertical: true)
                        }
                        Spacer()
                        if state.selectedModel == model {
                            HStack(spacing: 4) {
                                Circle().fill(Color.green).frame(width: 6, height: 6)
                                Text("Aktif").font(.system(size: 10.5, weight: .bold)).foregroundStyle(Color.green)
                            }
                            .padding(.horizontal, 8).padding(.vertical, 3).background(Color.green.opacity(0.12), in: RoundedRectangle(cornerRadius: 5))
                        } else {
                            Text(model.badge).font(.system(size: 10, weight: .medium)).foregroundStyle(Color.white.opacity(0.5))
                        }
                    }

                    if model == .gemini {
                        HStack {
                            SecureField("Google Gemini API Anahtarı (AI Studio)", text: $state.geminiApiKey)
                                .textFieldStyle(.plain).font(.system(size: 10.5)).padding(5).background(Color.black.opacity(0.3), in: RoundedRectangle(cornerRadius: 5))
                            Button("Kaydet") { state.selectedModel = .gemini }
                                .font(.system(size: 10.5)).padding(.horizontal, 8).padding(.vertical, 3).background(Color.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 5)).buttonStyle(.plain)
                        }
                    } else if model == .deepL {
                        HStack {
                            SecureField("DeepL Auth Key (api-free.deepl.com)", text: $state.deepLApiKey)
                                .textFieldStyle(.plain).font(.system(size: 10.5)).padding(5).background(Color.black.opacity(0.3), in: RoundedRectangle(cornerRadius: 5))
                            Button("Kaydet") { state.selectedModel = .deepL }
                                .font(.system(size: 10.5)).padding(.horizontal, 8).padding(.vertical, 3).background(Color.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 5)).buttonStyle(.plain)
                        }
                    }
                }
                .padding(10)
                .background(state.selectedModel == model ? Color(hex: "#0A84FF").opacity(0.12) : Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 8))
                .overlay(RoundedRectangle(cornerRadius: 8).stroke(state.selectedModel == model ? Color(hex: "#0A84FF").opacity(0.4) : Color.white.opacity(0.08), lineWidth: 1))
                .contentShape(Rectangle())
                .onTapGesture { state.selectedModel = model }
            }
        }
    }

    // MARK: - 4. BAĞLANTI & SİSTEM SEKMESİ
    private var systemTabContent: some View {
        VStack(alignment: .leading, spacing: 13) {
            Text("Konsol ve Ağ Bağlantıları").font(.system(size: 12.5, weight: .bold)).foregroundStyle(Color.white)

            VStack(alignment: .leading, spacing: 7) {
                HStack {
                    Image(systemName: "gamecontroller.fill").font(.system(size: 12)).foregroundStyle(Color(hex: "#0A84FF"))
                    Text("PlayStation 5").font(.system(size: 12, weight: .semibold)).foregroundStyle(Color.white)
                    Spacer()
                    Text("1080p 60 FPS").font(.system(size: 10, weight: .medium, design: .monospaced)).foregroundStyle(Color.white.opacity(0.5))
                }
                HStack {
                    Text("PSN Hesabı:").font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.6))
                    Text(state.psnEmail).font(.system(size: 10.5, weight: .medium)).foregroundStyle(Color.white.opacity(0.9))
                }
            }
            .padding(10).background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 8))
            .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.white.opacity(0.08), lineWidth: 1))

            VStack(alignment: .leading, spacing: 7) {
                HStack {
                    Image(systemName: "tv.fill").font(.system(size: 12)).foregroundStyle(Color.green)
                    Text("Akıllı TV & CouchSubs").font(.system(size: 12, weight: .semibold)).foregroundStyle(Color.white)
                    Spacer()
                    Text("Port 8787 (SSE)").font(.system(size: 10, weight: .medium, design: .monospaced)).foregroundStyle(Color.white.opacity(0.5))
                }
                HStack {
                    Text("Cihaz:").font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.6))
                    Text(state.tvDeviceName).font(.system(size: 10.5, weight: .medium)).foregroundStyle(Color.white.opacity(0.9))
                }
            }
            .padding(10).background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 8))
            .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.white.opacity(0.08), lineWidth: 1))

            VStack(spacing: 8) {
                HStack {
                    Text("Ekranı çevir hotkey kısayolu").font(.system(size: 11)).foregroundStyle(Color.white.opacity(0.75))
                    Spacer()
                    Text("^⌘⌥T").font(.system(size: 10.5, weight: .bold, design: .monospaced)).foregroundStyle(Color.white.opacity(0.9))
                        .padding(.horizontal, 6).padding(.vertical, 2).background(Color.white.opacity(0.12), in: RoundedRectangle(cornerRadius: 4))
                }
                HStack {
                    Text("OBS overlay:  http://localhost:8787/obs").font(.system(size: 11, design: .monospaced)).foregroundStyle(Color.white.opacity(0.75))
                    Spacer()
                    Button(state.obsCopied ? "Kopyalandı!" : "Kopyala") {
                        NSPasteboard.general.clearContents()
                        NSPasteboard.general.setString("http://localhost:8787/obs", forType: .string)
                        state.obsCopied = true
                        DispatchQueue.main.asyncAfter(deadline: .now() + 2) { state.obsCopied = false }
                    }
                    .font(.system(size: 10.5, weight: .medium)).foregroundStyle(Color.white.opacity(0.85))
                    .padding(.horizontal, 9).padding(.vertical, 3).background(Color.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 5)).buttonStyle(.plain)
                }
            }
            .padding(10).background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 8))
            .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.white.opacity(0.08), lineWidth: 1))
        }
    }

    // MARK: - 5. HIZLI REHBER SEKMESİ
    private var guideTabContent: some View {
        VStack(alignment: .leading, spacing: 11) {
            Text("Nasıl Kullanılır? (Hızlı Rehber)").font(.system(size: 12.5, weight: .bold)).foregroundStyle(Color.white)
            VStack(alignment: .leading, spacing: 9) {
                guideItem(num: "1", title: "PS5 & TV", text: "PS5 konsolunuzu açın ve TV'deki HDMI portuna geçin. PS5 doğrudan TV'ye bağlı kalır.")
                guideItem(num: "2", title: "TV Uygulaması", text: "TV'nizde GameTranslator (veya CouchSubs) uygulamasını açın. Mac'e otomatik olarak bağlanır.")
                guideItem(num: "3", title: "Okuma Alanı", text: "Oyununuzun altyazı konumuna göre 'Oyun Okuma Alanı' sekmesinden altyazı kutusunu ayarlayın.")
                guideItem(num: "4", title: "Başlat", text: "Ana ekrandaki mavi '▶ Çeviriyi Başlat' butonuna tıklayın. Konsol görüntüsü taranıp altyazılar çevrilmeye başlar.")
                guideItem(num: "5", title: "TV Görünümü", text: "Altyazı rengini (sarı/beyaz), boyutunu ve konumunu zevkinize göre ayarlayın; TV ekranına anında yansır!")
            }
        }
    }

    private func guideItem(num: String, title: String, text: String) -> some View {
        HStack(alignment: .top, spacing: 8) {
            Text(num).font(.system(size: 10.5, weight: .bold)).foregroundStyle(Color(hex: "#0A84FF")).frame(width: 18, height: 18).background(Color(hex: "#0A84FF").opacity(0.15), in: Circle())
            VStack(alignment: .leading, spacing: 1) {
                Text(title).font(.system(size: 11.5, weight: .semibold)).foregroundStyle(Color.white)
                Text(text).font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.65)).fixedSize(horizontal: false, vertical: true)
            }
        }
        .padding(8).frame(maxWidth: .infinity, alignment: .leading).background(Color.white.opacity(0.03), in: RoundedRectangle(cornerRadius: 6))
    }
}

// MARK: - Setup / Kurulum Wizard (Apple Frosted Glass)

struct SetupWizardView: View {
    @Bindable var state: AppState
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(spacing: 0) {
            wizardHeader
            stepProgressBar
            Divider().overlay(Color.white.opacity(0.12))
            
            ScrollView {
                VStack(spacing: 16) {
                    switch state.setupStep {
                    case 1: step1Welcome
                    case 2: step2SecondAccount
                    case 3: step3Console
                    case 4: step4TV
                    case 5: step5TestAndFinish
                    default: step1Welcome
                    }
                }
                .padding(22)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            
            Divider().overlay(Color.white.opacity(0.12))
            wizardFooter
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(
            VisualEffectBackground(material: .underWindowBackground, blendingMode: .behindWindow)
                .overlay(Color(hex: "#10131B").opacity(0.96))
        )
    }

    private var wizardHeader: some View {
        HStack(spacing: 10) {
            Image(systemName: "gamecontroller.fill")
                .font(.system(size: 16, weight: .bold))
                .foregroundStyle(Color(hex: "#0A84FF"))
            VStack(alignment: .leading, spacing: 2) {
                Text("GameTranslator Kurulum Sihirbazı")
                    .font(.system(size: 14, weight: .bold))
                    .foregroundStyle(Color.white)
                Text("PlayStation 5 & Akıllı TV Hızlı Yapılandırma")
                    .font(.system(size: 10.5))
                    .foregroundStyle(Color.white.opacity(0.55))
            }
            Spacer()
            HStack(spacing: 6) {
                Text("Adım \(state.setupStep) / 5")
                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                    .foregroundStyle(Color(hex: "#38BDF8"))
                    .padding(.horizontal, 9)
                    .padding(.vertical, 3)
                    .background(Color(hex: "#0A84FF").opacity(0.15), in: Capsule())
                
                if state.psnLinked || state.bypassSetup {
                    Button {
                        state.showSetup = false
                    } label: {
                        Image(systemName: "xmark.circle.fill")
                            .font(.system(size: 16))
                            .foregroundStyle(Color.white.opacity(0.5))
                    }
                    .buttonStyle(.plain)
                }
            }
        }
        .padding(.horizontal, 24)
        .padding(.vertical, 14)
    }

    private var stepProgressBar: some View {
        HStack(spacing: 8) {
            ForEach(1...5, id: \.self) { step in
                Button {
                    state.setupStep = step
                } label: {
                    VStack(alignment: .leading, spacing: 3) {
                        Capsule()
                            .fill(step <= state.setupStep ? Color(hex: "#0A84FF") : Color.white.opacity(0.12))
                            .frame(height: 4)
                        Text(stepTitle(for: step))
                            .font(.system(size: 9.5, weight: step == state.setupStep ? .bold : .regular))
                            .foregroundStyle(step == state.setupStep ? Color(hex: "#38BDF8") : Color.white.opacity(0.45))
                    }
                }
                .buttonStyle(.plain)
            }
        }
        .padding(.horizontal, 24)
        .padding(.bottom, 10)
    }

    private func stepTitle(for step: Int) -> String {
        switch step {
        case 1: "1. Başlangıç"
        case 2: "2. İkinci Hesap"
        case 3: "3. PS5 Konsolu"
        case 4: "4. Akıllı TV"
        case 5: "5. Test & Bitti"
        default: ""
        }
    }

    // ADIM 1: Hoş Geldiniz
    private var step1Welcome: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 12) {
                ZStack {
                    Circle().fill(Color(hex: "#0A84FF").opacity(0.2)).frame(width: 44, height: 44)
                    Image(systemName: "sparkles.tv.fill")
                        .font(.system(size: 20))
                        .foregroundStyle(Color(hex: "#0A84FF"))
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text("GameTranslator'a Hoş Geldiniz")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(Color.white)
                    Text("PS5 oyunlarındaki İngilizce altyazıları ekrandan okur ve TV'nize anında Türkçe basar.")
                        .font(.system(size: 11.5))
                        .foregroundStyle(Color.white.opacity(0.7))
                }
            }
            .padding(.bottom, 4)

            VStack(alignment: .leading, spacing: 10) {
                Text("Kurulum İçin Gereken 3 Şey:")
                    .font(.system(size: 11.5, weight: .bold))
                    .foregroundStyle(Color.white.opacity(0.85))

                requirementCard(icon: "gamecontroller", title: "PlayStation 5 Konsolu", desc: "Bu Mac ile aynı yerel Wi-Fi ağında açık olmalıdır.")
                requirementCard(icon: "tv", title: "Akıllı TV veya TV Box", desc: "LG (webOS), Samsung (Tizen) veya Android TV / Google TV.")
                requirementCard(icon: "person.2", title: "İkinci Bir PSN Hesabı", desc: "Remote Play kumandanızı kesmesin diye gereken ücretsiz hesap.")
            }
        }
    }

    private func requirementCard(icon: String, title: String, desc: String) -> some View {
        HStack(spacing: 12) {
            Image(systemName: icon)
                .font(.system(size: 16))
                .foregroundStyle(Color(hex: "#38BDF8"))
                .frame(width: 24)
            VStack(alignment: .leading, spacing: 2) {
                Text(title).font(.system(size: 12, weight: .semibold)).foregroundStyle(Color.white)
                Text(desc).font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.6))
            }
            Spacer()
        }
        .padding(10)
        .background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 8))
        .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.white.opacity(0.08), lineWidth: 1))
    }

    // ADIM 2: İkinci Hesap & PSN Girişi
    private var step2SecondAccount: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 12) {
                ZStack {
                    Circle().fill(Color(hex: "#38BDF8").opacity(0.2)).frame(width: 44, height: 44)
                    Image(systemName: "person.2.fill")
                        .font(.system(size: 18))
                        .foregroundStyle(Color(hex: "#38BDF8"))
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text("İkinci PSN Hesabı Girişi")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(Color.white)
                    Text("GameTranslator oyunu Remote Play ile izler. İkinci hesap sayesinde ana hesabınızın kumandası hiç kapanmaz.")
                        .font(.system(size: 11.5))
                        .foregroundStyle(Color.white.opacity(0.7))
                }
            }

            VStack(alignment: .leading, spacing: 8) {
                numberedStep(num: "1", text: "playstation.com adresinden farklı bir e-posta ile ücretsiz bir 2. PSN hesabı açın.")
                numberedStep(num: "2", text: "PS5'inizde 'Kullanıcı Ekle' deyip bu 2. hesapla bir kez giriş yapın.")
                numberedStep(num: "3", text: "Yeni kullanıcıdayken Ayarlar › Sistem › Uzaktan Oynatma'yı açın.")
                numberedStep(num: "4", text: "Ana kullanıcınıza geri dönün. Bu işlemi yalnızca 1 kez yaparsınız.")
            }
            .padding(12)
            .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))

            // Gerçek PSN Bağlantı Alanı
            VStack(alignment: .leading, spacing: 12) {
                Text("PlayStation Network Bağlantısı:")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(Color.white)

                if state.psnLinked {
                    HStack(spacing: 10) {
                        Image(systemName: "checkmark.circle.fill")
                            .font(.system(size: 20))
                            .foregroundStyle(Color.green)
                        VStack(alignment: .leading, spacing: 2) {
                            Text("PlayStation Network Hesabı Başarıyla Bağlandı")
                                .font(.system(size: 12, weight: .bold))
                                .foregroundStyle(Color.green)
                            Text("Hesap Kimliği (PSN ID): \(state.psnAccountId ?? "Bağlı")")
                                .font(.system(size: 10.5, design: .monospaced))
                                .foregroundStyle(Color.white.opacity(0.7))
                        }
                        Spacer()
                        Button("Çıkış Yap") {
                            state.psnLinked = false
                            state.psnAccountId = nil
                            state.psnAccountBase64 = nil
                            state.psnRedirectInput = ""
                        }
                        .font(.system(size: 11, weight: .medium))
                        .foregroundStyle(Color.red.opacity(0.9))
                        .padding(.horizontal, 10)
                        .padding(.vertical, 5)
                        .background(Color.red.opacity(0.12), in: RoundedRectangle(cornerRadius: 5))
                        .buttonStyle(.plain)
                    }
                    .padding(12)
                    .background(Color.green.opacity(0.12), in: RoundedRectangle(cornerRadius: 8))
                    .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.green.opacity(0.3), lineWidth: 1))
                } else {
                    HStack(spacing: 10) {
                        // Seçenek 1: Tarayıcıda yeni sekme aç
                        Button {
                            NSWorkspace.shared.open(PSNAccount.authorizeURL)
                        } label: {
                            HStack(spacing: 6) {
                                Image(systemName: "safari.fill")
                                Text("Sony Giriş Sayfasını Aç (Yeni Sekme)")
                            }
                            .font(.system(size: 11.5, weight: .semibold))
                            .foregroundStyle(Color.white)
                            .padding(.horizontal, 14)
                            .padding(.vertical, 9)
                            .background(Color(hex: "#0A84FF"), in: RoundedRectangle(cornerRadius: 6))
                        }
                        .buttonStyle(.plain)

                        // Seçenek 2: Uygulama içi pencere
                        Button {
                            state.showInAppPSNLogin = true
                        } label: {
                            HStack(spacing: 6) {
                                Image(systemName: "macwindow")
                                Text("Uygulama İçi Giriş Yap")
                            }
                            .font(.system(size: 11.5, weight: .medium))
                            .foregroundStyle(Color.white.opacity(0.9))
                            .padding(.horizontal, 14)
                            .padding(.vertical, 9)
                            .background(Color.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 6))
                        }
                        .buttonStyle(.plain)
                    }

                    VStack(alignment: .leading, spacing: 6) {
                        Text("Tarayıcıda giriş yaptıktan sonra açılan sayfanın adresini (URL) veya kodunu yapıştırın:")
                            .font(.system(size: 11))
                            .foregroundStyle(Color.white.opacity(0.7))

                        HStack(spacing: 8) {
                            TextField("https://remoteplay.dl.playstation.net/remoteplay/redirect?code=...", text: $state.psnRedirectInput)
                                .textFieldStyle(.plain)
                                .font(.system(size: 11, design: .monospaced))
                                .padding(8)
                                .background(Color.black.opacity(0.4), in: RoundedRectangle(cornerRadius: 6))
                                .overlay(RoundedRectangle(cornerRadius: 6).stroke(Color.white.opacity(0.15), lineWidth: 1))

                            Button {
                                Task {
                                    await state.processPSNRedirect(state.psnRedirectInput)
                                }
                            } label: {
                                HStack(spacing: 5) {
                                    if state.isConnectingPSN {
                                        ProgressView().controlSize(.small)
                                    } else {
                                        Image(systemName: "link")
                                    }
                                    Text("Bağlantıyı Doğrula")
                                }
                                .font(.system(size: 11, weight: .semibold))
                                .foregroundStyle(Color.white)
                                .padding(.horizontal, 14)
                                .padding(.vertical, 8)
                                .background(Color(hex: "#0A84FF"), in: RoundedRectangle(cornerRadius: 6))
                            }
                            .buttonStyle(.plain)
                            .disabled(state.psnRedirectInput.trimmingCharacters(in: .whitespaces).isEmpty || state.isConnectingPSN)
                        }

                        if let err = state.psnError {
                            Text(err)
                                .font(.system(size: 10.5))
                                .foregroundStyle(Color(hex: "#FF453A"))
                                .padding(.top, 2)
                        }
                    }
                    .padding(10)
                    .background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 6))
                }
            }
            .padding(12)
            .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))
        }
        .sheet(isPresented: $state.showInAppPSNLogin) {
            VStack(spacing: 0) {
                HStack {
                    Image(systemName: "gamecontroller.fill")
                        .foregroundStyle(Color(hex: "#0A84FF"))
                    Text("PlayStation Network Girişi")
                        .font(.system(size: 13, weight: .bold))
                        .foregroundStyle(Color.white)
                    Spacer()
                    Button("Kapat") {
                        state.showInAppPSNLogin = false
                    }
                    .font(.system(size: 11.5))
                    .foregroundStyle(Color.white.opacity(0.7))
                    .buttonStyle(.plain)
                }
                .padding(14)
                .background(Color(hex: "#161922"))

                Divider().overlay(Color.white.opacity(0.12))

                PSNLoginWebView { redirectURL in
                    Task {
                        await state.processPSNRedirect(redirectURL.absoluteString)
                    }
                }
                .frame(minWidth: 500, minHeight: 640)
            }
            .background(Color(hex: "#10131B"))
        }
    }

    private func numberedStep(num: String, text: String) -> some View {
        HStack(alignment: .top, spacing: 8) {
            Text(num)
                .font(.system(size: 10, weight: .bold))
                .foregroundStyle(Color.white)
                .frame(width: 18, height: 18)
                .background(Color(hex: "#0A84FF"), in: Circle())
            Text(text)
                .font(.system(size: 11))
                .foregroundStyle(Color.white.opacity(0.8))
            Spacer()
        }
    }

    // ADIM 3: PS5 Konsol Eşleştirme
    private var step3Console: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 12) {
                ZStack {
                    Circle().fill(Color(hex: "#34C759").opacity(0.2)).frame(width: 44, height: 44)
                    Image(systemName: "gamecontroller.fill")
                        .font(.system(size: 18))
                        .foregroundStyle(Color(hex: "#34C759"))
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text("PS5 Konsolunuzu Eşleştirin")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(Color.white)
                    Text("PS5'te Ayarlar › Sistem › Uzaktan Oynatma › Cihaz Eşle yolunu açın.")
                        .font(.system(size: 11.5))
                        .foregroundStyle(Color.white.opacity(0.7))
                }
            }

            VStack(alignment: .leading, spacing: 10) {
                if state.ps5Paired {
                    VStack(alignment: .leading, spacing: 8) {
                        HStack(spacing: 10) {
                            Image(systemName: "checkmark.circle.fill")
                                .font(.system(size: 20))
                                .foregroundStyle(Color.green)
                            VStack(alignment: .leading, spacing: 2) {
                                Text("PS5 Başarıyla Eşleşti")
                                    .font(.system(size: 12, weight: .bold))
                                    .foregroundStyle(Color.green)
                                Text("IP: \(state.ps5Host.isEmpty ? "192.168.1.108" : state.ps5Host) (1080p 60 FPS)")
                                    .font(.system(size: 10.5))
                                    .foregroundStyle(Color.white.opacity(0.7))
                            }
                            Spacer()
                            Button("Yeniden Eşle") {
                                state.ps5Paired = false
                                state.ps5Connected = false
                            }
                            .font(.system(size: 11))
                            .foregroundStyle(Color.white.opacity(0.6))
                            .buttonStyle(.plain)
                        }

                        Text("✓ PS5 ekranının kod sayfasından otomatik olarak çıkması, konsolun bilgisayarınızı başarıyla kaydettiğini gösterir. Tekrar kod girmenize gerek yoktur. 'Devam Et →' butonuna basarak Akıllı TV adımına geçin.")
                            .font(.system(size: 10.5))
                            .foregroundStyle(Color.white.opacity(0.75))
                            .lineSpacing(3)
                    }
                    .padding(12)
                    .background(Color.green.opacity(0.12), in: RoundedRectangle(cornerRadius: 8))
                    .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.green.opacity(0.3), lineWidth: 1))
                } else {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("PS5 IP Adresi (Yerel Ağ):")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundStyle(Color.white.opacity(0.8))
                        HStack {
                            TextField("örn: 192.168.1.100", text: $state.ps5Host)
                                .textFieldStyle(.plain)
                                .padding(8)
                                .background(Color.black.opacity(0.4), in: RoundedRectangle(cornerRadius: 6))
                                .overlay(RoundedRectangle(cornerRadius: 6).stroke(Color.white.opacity(0.15), lineWidth: 1))
                            
                            Button(state.isSearchingPS5 ? "Aranıyor..." : "Ağda Tara") {
                                Task { await state.scanLocalPS5() }
                            }
                            .font(.system(size: 11))
                            .foregroundStyle(Color.white.opacity(0.85))
                            .padding(.horizontal, 12)
                            .padding(.vertical, 8)
                            .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
                            .buttonStyle(.plain)
                            .disabled(state.isSearchingPS5)
                        }
                    }

                    VStack(alignment: .leading, spacing: 4) {
                        Text("PS5 Ekranındaki 8 Haneli Eşleşme PIN Kodu:")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundStyle(Color.white.opacity(0.8))
                        TextField("8 haneli PIN kodu", text: $state.ps5Pin)
                            .font(.system(size: 14, weight: .bold, design: .monospaced))
                            .textFieldStyle(.plain)
                            .padding(8)
                            .background(Color.black.opacity(0.4), in: RoundedRectangle(cornerRadius: 6))
                            .overlay(RoundedRectangle(cornerRadius: 6).stroke(Color.white.opacity(0.15), lineWidth: 1))
                    }

                    Button {
                        state.pairPS5()
                    } label: {
                        HStack(spacing: 8) {
                            if state.isPairingPS5 {
                                ProgressView()
                                    .scaleEffect(0.7)
                                    .frame(width: 14, height: 14)
                                Text("PS5 Eşleştiriliyor (Paket Gönderiliyor)...")
                            } else {
                                Image(systemName: "link.badge.plus")
                                Text("Konsolu Eşle ve Doğrula")
                            }
                        }
                        .font(.system(size: 12, weight: .bold))
                        .foregroundStyle(Color.white)
                        .frame(maxWidth: .infinity, minHeight: 34)
                        .background(state.ps5Pin.filter(\.isNumber).count == 8 && !state.isPairingPS5 ? Color(hex: "#0A84FF") : Color.gray.opacity(0.4), in: RoundedRectangle(cornerRadius: 6))
                    }
                    .buttonStyle(.plain)
                    .disabled(state.ps5Pin.filter(\.isNumber).count != 8 || state.isPairingPS5)

                    if let success = state.ps5SuccessMessage {
                        HStack(spacing: 6) {
                            Image(systemName: "checkmark.circle.fill")
                                .font(.system(size: 12))
                                .foregroundStyle(Color.green)
                            Text(success)
                                .font(.system(size: 11, weight: .semibold))
                                .foregroundStyle(Color.green)
                        }
                        .padding(.vertical, 2)
                    }

                    if let err = state.ps5Error {
                        Text(err)
                            .font(.system(size: 10.5))
                            .foregroundStyle(Color(hex: "#FF453A"))
                    }
                }
            }
            .padding(12)
            .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))
            .onAppear {
                if state.ps5Host.isEmpty && !state.ps5Paired {
                    Task { await state.scanLocalPS5() }
                }
            }
        }
    }

    // ADIM 4: Altyazı Hedefi (Mac Ekranı, OBS veya Akıllı TV)
    private var step4TV: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 12) {
                ZStack {
                    Circle().fill(Color(hex: "#FF9F0A").opacity(0.2)).frame(width: 44, height: 44)
                    Image(systemName: "tv.fill")
                        .font(.system(size: 18))
                        .foregroundStyle(Color(hex: "#FF9F0A"))
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text("Altyazı Nerede Gösterilsin?")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(Color.white)
                    Text("Evde TV yoksa veya monitörde oynuyorsanız Mac ekranını seçebilirsiniz.")
                        .font(.system(size: 11.5))
                        .foregroundStyle(Color.white.opacity(0.7))
                }
            }

            VStack(spacing: 8) {
                // Seçenek 1: Mac Ekranı & OBS (TV GEREKMEZ)
                Button {
                    state.selectedTVPlatform = "Mac / OBS"
                    state.tvDeviceName = "Mac & OBS (Port 8787)"
                    state.tvConnected = true
                    state.tvError = nil
                } label: {
                    tvOptionRow(
                        title: "🖥️ Mac Ekranı & OBS Overlay (TV Gerekmez)",
                        desc: "Evde televizyon yoksa veya monitörde oynuyorsanız en kolayıdır.",
                        selected: state.selectedTVPlatform == "Mac / OBS"
                    )
                }.buttonStyle(.plain)

                // Seçenek 2: Android TV
                Button {
                    state.selectedTVPlatform = "Android TV"
                    state.tvDeviceName = "Android TV / Google TV"
                    state.tvConnected = false
                    state.tvError = nil
                    if state.tvHost.isEmpty {
                        Task { await state.scanLocalTV() }
                    }
                } label: {
                    tvOptionRow(
                        title: "🤖 Android TV / Google TV",
                        desc: "TCL, Philips, Xiaomi, Google TV Box",
                        selected: state.selectedTVPlatform == "Android TV"
                    )
                }.buttonStyle(.plain)

                // Seçenek 3: LG webOS
                Button {
                    state.selectedTVPlatform = "LG webOS"
                    state.tvDeviceName = "LG webOS TV"
                    state.tvConnected = false
                    state.tvHost = ""
                    state.tvError = nil
                } label: {
                    tvOptionRow(
                        title: "📺 LG webOS TV",
                        desc: "LG OLED, QNED (2018+)",
                        selected: state.selectedTVPlatform == "LG webOS"
                    )
                }.buttonStyle(.plain)

                // Seçenek 4: Samsung Tizen
                Button {
                    state.selectedTVPlatform = "Samsung Tizen"
                    state.tvDeviceName = "Samsung Smart TV"
                    state.tvConnected = false
                    state.tvHost = ""
                    state.tvError = nil
                } label: {
                    tvOptionRow(
                        title: "🖥️ Samsung Tizen TV",
                        desc: "Samsung Smart TV (2018+)",
                        selected: state.selectedTVPlatform == "Samsung Tizen"
                    )
                }.buttonStyle(.plain)
            }

            // Durum ve Platforma Özel Kurulum Kartı
            VStack(alignment: .leading, spacing: 10) {
                if state.selectedTVPlatform == "Mac / OBS" {
                    VStack(alignment: .leading, spacing: 8) {
                        HStack(spacing: 8) {
                            Image(systemName: "checkmark.circle.fill").foregroundStyle(Color.green)
                            Text("Mac Ekranı & OBS Modu Hazır")
                                .font(.system(size: 12, weight: .bold))
                                .foregroundStyle(Color.green)
                        }
                        Text("Televizyon bağlantısı gerekmez! Altyazılar bu Mac uygulamasında ve tarayıcıda/OBS'te canlı görünür:")
                            .font(.system(size: 11))
                            .foregroundStyle(Color.white.opacity(0.75))

                        HStack {
                            Text("http://localhost:8787/obs")
                                .font(.system(size: 11.5, weight: .semibold, design: .monospaced))
                                .foregroundStyle(Color(hex: "#38BDF8"))
                            Spacer()
                            Button("OBS Linkini Kopyala") {
                                NSPasteboard.general.clearContents()
                                NSPasteboard.general.setString("http://localhost:8787/obs", forType: .string)
                            }
                            .font(.system(size: 10.5))
                            .foregroundStyle(Color.white)
                            .padding(.horizontal, 10)
                            .padding(.vertical, 4)
                            .background(Color.white.opacity(0.12), in: RoundedRectangle(cornerRadius: 4))
                            .buttonStyle(.plain)
                        }
                        .padding(8)
                        .background(Color.black.opacity(0.35), in: RoundedRectangle(cornerRadius: 6))
                    }
                    .padding(12)
                    .background(Color.green.opacity(0.1), in: RoundedRectangle(cornerRadius: 8))
                    .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.green.opacity(0.25), lineWidth: 1))
                } else if state.selectedTVPlatform == "Android TV" {
                    VStack(alignment: .leading, spacing: 10) {
                        HStack {
                            Text("Android TV Uygulama Yükleme & Bağlantı:")
                                .font(.system(size: 12, weight: .bold))
                                .foregroundStyle(Color.white)
                            Spacer()
                            if state.tvConnected {
                                HStack(spacing: 4) {
                                    Circle().fill(Color.green).frame(width: 6, height: 6)
                                    Text("Bağlı (\(state.tvDeviceName))").font(.system(size: 10.5, weight: .semibold)).foregroundStyle(Color.green)
                                }
                            }
                        }

                        // Yöntem 1: Otomatik Ağdan ADB ile Kur
                        VStack(alignment: .leading, spacing: 6) {
                            Text("Yöntem 1: Ağ Üzerinden Otomatik Kur (ADB)")
                                .font(.system(size: 11, weight: .semibold))
                                .foregroundStyle(Color(hex: "#38BDF8"))

                            HStack {
                                TextField("TV IP adresi (örn: 192.168.1.50)", text: $state.tvHost)
                                    .textFieldStyle(.plain)
                                    .padding(7)
                                    .background(Color.black.opacity(0.4), in: RoundedRectangle(cornerRadius: 6))
                                    .overlay(RoundedRectangle(cornerRadius: 6).stroke(Color.white.opacity(0.15), lineWidth: 1))

                                Button(state.isSearchingTV ? "Aranıyor..." : "Ağda Tara") {
                                    Task { await state.scanLocalTV() }
                                }
                                .font(.system(size: 11))
                                .foregroundStyle(Color.white.opacity(0.85))
                                .padding(.horizontal, 10)
                                .padding(.vertical, 7)
                                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
                                .buttonStyle(.plain)
                                .disabled(state.isSearchingTV)

                                Button {
                                    state.installAndroidTVApp()
                                } label: {
                                    HStack(spacing: 5) {
                                        if state.isInstallingTV {
                                            ProgressView().controlSize(.small)
                                        } else {
                                            Image(systemName: "arrow.down.circle.fill")
                                        }
                                        Text(state.isInstallingTV ? "Yükleniyor..." : "TV'ye Otomatik Kur")
                                    }
                                    .font(.system(size: 11, weight: .semibold))
                                    .foregroundStyle(Color.white)
                                    .padding(.horizontal, 12)
                                    .padding(.vertical, 7)
                                    .background(Color(hex: "#0A84FF"), in: RoundedRectangle(cornerRadius: 6))
                                }
                                .buttonStyle(.plain)
                                .disabled(state.isInstallingTV)
                            }

                            if let success = state.tvSuccessMessage {
                                HStack(spacing: 6) {
                                    Image(systemName: "checkmark.circle.fill")
                                        .font(.system(size: 11))
                                        .foregroundStyle(Color.green)
                                    Text(success)
                                        .font(.system(size: 11, weight: .medium))
                                        .foregroundStyle(Color.green)
                                }
                                .padding(.vertical, 2)
                            }

                            if let msg = state.tvInstallMessage {
                                Text(msg)
                                    .font(.system(size: 10.5))
                                    .foregroundStyle(Color.green)
                            }

                            if state.tvConnected {
                                HStack(spacing: 10) {
                                    Button {
                                        state.sendTVTestSubtitle()
                                    } label: {
                                        HStack(spacing: 6) {
                                            Image(systemName: "bolt.fill")
                                            Text(state.testSent ? "✓ Test Altyazısı TV'ye Gönderildi!" : "⚡ TV'de Canlı Altyazıyı Test Et")
                                        }
                                        .font(.system(size: 11.5, weight: .bold))
                                        .foregroundStyle(Color.black)
                                        .padding(.horizontal, 14)
                                        .padding(.vertical, 8)
                                        .background(Color(hex: "#10B981"), in: RoundedRectangle(cornerRadius: 6))
                                    }
                                    .buttonStyle(.plain)

                                    Text("TV'de 'GameTranslator' uygulamasının açık olduğundan emin olun.")
                                        .font(.system(size: 10.5))
                                        .foregroundStyle(Color.white.opacity(0.7))
                                }
                                .padding(.top, 4)
                            }
                        }
                        .padding(9)
                        .background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 7))

                        // Yöntem 2 & 3: Finder APK Reveal & TV Web Browser
                        HStack(spacing: 10) {
                            Button {
                                state.revealTVPackage(for: "Android TV")
                            } label: {
                                HStack(spacing: 5) {
                                    Image(systemName: "folder.fill")
                                    Text("📁 APK Paketini Finder'da Göster")
                                }
                                .font(.system(size: 10.5, weight: .medium))
                                .foregroundStyle(Color.white.opacity(0.9))
                                .padding(.horizontal, 10)
                                .padding(.vertical, 6)
                                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
                            }
                            .buttonStyle(.plain)

                            Spacer()

                            Button {
                                state.copyObsLink()
                            } label: {
                                HStack(spacing: 5) {
                                    Image(systemName: "safari.fill")
                                    Text("🌐 TV Tarayıcı Linkini Kopyala")
                                }
                                .font(.system(size: 10.5, weight: .medium))
                                .foregroundStyle(Color(hex: "#38BDF8"))
                                .padding(.horizontal, 10)
                                .padding(.vertical, 6)
                                .background(Color(hex: "#38BDF8").opacity(0.12), in: RoundedRectangle(cornerRadius: 6))
                            }
                            .buttonStyle(.plain)
                        }

                        if let err = state.tvError {
                            Text(err)
                                .font(.system(size: 10.5))
                                .foregroundStyle(Color(hex: "#FF453A"))
                                .padding(.top, 2)
                        }
                    }
                    .padding(12)
                    .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))
                } else if state.selectedTVPlatform == "LG webOS" {
                    VStack(alignment: .leading, spacing: 10) {
                        Text("LG webOS TV İçin En Kolay 2 Yöntem:")
                            .font(.system(size: 12, weight: .bold))
                            .foregroundStyle(Color.white)

                        // 1. Önerilen: TV Web Tarayıcısı (Sıfır Kurulum)
                        VStack(alignment: .leading, spacing: 5) {
                            HStack {
                                Text("1. Önerilen: LG TV Tarayıcısında Aç (Sıfır Kurulum)")
                                    .font(.system(size: 11, weight: .semibold))
                                    .foregroundStyle(Color(hex: "#38BDF8"))
                                Spacer()
                                Text("EN HIZLI").font(.system(size: 8.5, weight: .heavy)).foregroundStyle(Color.green).padding(.horizontal, 4).padding(.vertical, 1).background(Color.green.opacity(0.15), in: RoundedRectangle(cornerRadius: 3))
                            }
                            Text("LG Sihirli Kumandadan 'Web Tarayıcısı'nı açın ve aşağıdaki adrese girin. Tam ekran yaptığınızda altyazılar şeffaf olarak anında görünür:")
                                .font(.system(size: 10.5))
                                .foregroundStyle(Color.white.opacity(0.7))

                            HStack {
                                Text("http://\(AppState.localIPAddress()):8787/obs")
                                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                                    .foregroundStyle(Color(hex: "#38BDF8"))
                                Spacer()
                                Button("Linki Kopyala") {
                                    state.copyObsLink()
                                }
                                .font(.system(size: 10))
                                .foregroundStyle(Color.white)
                                .padding(.horizontal, 8)
                                .padding(.vertical, 3)
                                .background(Color.white.opacity(0.12), in: RoundedRectangle(cornerRadius: 4))
                                .buttonStyle(.plain)
                            }
                            .padding(6)
                            .background(Color.black.opacity(0.35), in: RoundedRectangle(cornerRadius: 5))
                        }
                        .padding(9)
                        .background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 7))

                        // 2. IPK Paketi
                        HStack {
                            VStack(alignment: .leading, spacing: 2) {
                                Text("2. Alternatif: LG Developer Mode (.ipk)")
                                    .font(.system(size: 11, weight: .semibold))
                                    .foregroundStyle(Color.white.opacity(0.9))
                                Text("LG Content Store'dan 'Developer Mode' yükleyip IPK dosyasını atabilirsiniz.")
                                    .font(.system(size: 10))
                                    .foregroundStyle(Color.white.opacity(0.55))
                            }
                            Spacer()
                            Button("📁 IPK Paketini Aç") {
                                state.revealTVPackage(for: "LG webOS")
                            }
                            .font(.system(size: 10.5))
                            .foregroundStyle(Color.white)
                            .padding(.horizontal, 9)
                            .padding(.vertical, 5)
                            .background(Color.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 5))
                            .buttonStyle(.plain)
                        }
                        .padding(9)
                        .background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 7))
                    }
                    .padding(12)
                    .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))
                } else if state.selectedTVPlatform == "Samsung Tizen" {
                    VStack(alignment: .leading, spacing: 10) {
                        Text("Samsung Tizen TV İçin En Kolay 2 Yöntem:")
                            .font(.system(size: 12, weight: .bold))
                            .foregroundStyle(Color.white)

                        // 1. Önerilen: Samsung TV Web Tarayıcısı (Sıfır Kurulum)
                        VStack(alignment: .leading, spacing: 5) {
                            HStack {
                                Text("1. Önerilen: Samsung İnternet Tarayıcısında Aç (Sıfır Kurulum)")
                                    .font(.system(size: 11, weight: .semibold))
                                    .foregroundStyle(Color(hex: "#38BDF8"))
                                Spacer()
                                Text("EN HIZLI").font(.system(size: 8.5, weight: .heavy)).foregroundStyle(Color.green).padding(.horizontal, 4).padding(.vertical, 1).background(Color.green.opacity(0.15), in: RoundedRectangle(cornerRadius: 3))
                            }
                            Text("Samsung Smart TV kumandasından 'İnternet' uygulamasını açıp bu adrese gidin. Tam ekranda şeffaf altyazılar sıfır kurulumla anında akar:")
                                .font(.system(size: 10.5))
                                .foregroundStyle(Color.white.opacity(0.7))

                            HStack {
                                Text("http://\(AppState.localIPAddress()):8787/obs")
                                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                                    .foregroundStyle(Color(hex: "#38BDF8"))
                                Spacer()
                                Button("Linki Kopyala") {
                                    state.copyObsLink()
                                }
                                .font(.system(size: 10))
                                .foregroundStyle(Color.white)
                                .padding(.horizontal, 8)
                                .padding(.vertical, 3)
                                .background(Color.white.opacity(0.12), in: RoundedRectangle(cornerRadius: 4))
                                .buttonStyle(.plain)
                            }
                            .padding(6)
                            .background(Color.black.opacity(0.35), in: RoundedRectangle(cornerRadius: 5))
                        }
                        .padding(9)
                        .background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 7))

                        // 2. WGT Paketi & Geliştirici Modu (1-2-3-4-5)
                        HStack {
                            VStack(alignment: .leading, spacing: 2) {
                                Text("2. Alternatif: Samsung Developer Mode (.wgt)")
                                    .font(.system(size: 11, weight: .semibold))
                                    .foregroundStyle(Color.white.opacity(0.9))
                                Text("Apps ekranında kumandadan '1-2-3-4-5' tuşlayıp Developer Mode açın.")
                                    .font(.system(size: 10))
                                    .foregroundStyle(Color.white.opacity(0.55))
                            }
                            Spacer()
                            Button("📁 WGT Paketini Aç") {
                                state.revealTVPackage(for: "Samsung Tizen")
                            }
                            .font(.system(size: 10.5))
                            .foregroundStyle(Color.white)
                            .padding(.horizontal, 9)
                            .padding(.vertical, 5)
                            .background(Color.white.opacity(0.1), in: RoundedRectangle(cornerRadius: 5))
                            .buttonStyle(.plain)
                        }
                        .padding(9)
                        .background(Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 7))
                    }
                    .padding(12)
                    .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))
                }
            }
        }
        .onAppear {
            if state.selectedTVPlatform == "Android TV" && state.tvHost.isEmpty {
                Task { await state.scanLocalTV() }
            }
        }
    }

    private func tvOptionRow(title: String, desc: String, selected: Bool) -> some View {
        HStack {
            Image(systemName: selected ? "checkmark.circle.fill" : "circle")
                .foregroundStyle(selected ? Color.green : Color.white.opacity(0.3))
            VStack(alignment: .leading, spacing: 1) {
                Text(title).font(.system(size: 11.5, weight: .semibold)).foregroundStyle(Color.white)
                Text(desc).font(.system(size: 10)).foregroundStyle(Color.white.opacity(0.55))
            }
            Spacer()
        }
        .padding(8)
        .background(selected ? Color(hex: "#0A84FF").opacity(0.12) : Color.white.opacity(0.04), in: RoundedRectangle(cornerRadius: 6))
        .overlay(RoundedRectangle(cornerRadius: 6).stroke(selected ? Color(hex: "#0A84FF") : Color.white.opacity(0.08), lineWidth: 1))
    }

    // ADIM 5: Test & Bitti
    private var step5TestAndFinish: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack(spacing: 12) {
                ZStack {
                    Circle().fill(Color(hex: "#AF52DE").opacity(0.2)).frame(width: 44, height: 44)
                    Image(systemName: "checkmark.seal.fill")
                        .font(.system(size: 20))
                        .foregroundStyle(Color(hex: "#AF52DE"))
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text("Bağlantıyı Test Edin")
                        .font(.system(size: 16, weight: .bold))
                        .foregroundStyle(Color.white)
                    Text("GameTranslator şimdi TV ekranınıza örnek bir Türkçe altyazı gönderecek.")
                        .font(.system(size: 11.5))
                        .foregroundStyle(Color.white.opacity(0.7))
                }
            }

            VStack(alignment: .center, spacing: 10) {
                Button {
                    state.sendTVTestSubtitle()
                } label: {
                    HStack(spacing: 8) {
                        Image(systemName: "bolt.fill")
                        Text(state.testSent ? "✓ Canlı Test Altyazısı TV'ye İletildi!" : "TV'ye Canlı Test Altyazısı Gönder")
                    }
                    .font(.system(size: 12.5, weight: .bold))
                    .foregroundStyle(Color.white)
                    .frame(maxWidth: .infinity, minHeight: 38)
                    .background(Color(hex: "#0A84FF"), in: RoundedRectangle(cornerRadius: 8))
                    .shadow(color: Color(hex: "#0A84FF").opacity(0.4), radius: 6, y: 2)
                }
                .buttonStyle(.plain)

                if state.testSent {
                    HStack(spacing: 6) {
                        Image(systemName: "checkmark.circle.fill").foregroundStyle(Color.green)
                        Text("Altyazı TV'ye ve OBS yayınına basıldı (Port 8787).")
                            .font(.system(size: 11))
                            .foregroundStyle(Color.white.opacity(0.85))
                    }
                    .padding(8)
                    .frame(maxWidth: .infinity)
                    .background(Color.green.opacity(0.12), in: RoundedRectangle(cornerRadius: 6))
                }
            }
            .padding(14)
            .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 8))

            VStack(alignment: .leading, spacing: 6) {
                Text("TV'de Altyazıyı Görüyor Musunuz?")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(Color.white)
                Text("Evet ise kurulum tamamlandı! Artık ana ekrandan 'Çeviriyi Başlat' diyerek oyununuzu Türkçe oynayabilirsiniz.")
                    .font(.system(size: 11))
                    .foregroundStyle(Color.white.opacity(0.65))
            }
        }
    }

    private var wizardFooter: some View {
        HStack(spacing: 12) {
            if state.setupStep > 1 {
                Button("← Geri") {
                    state.setupStep -= 1
                }
                .font(.system(size: 12))
                .foregroundStyle(Color.white.opacity(0.8))
                .padding(.horizontal, 14)
                .padding(.vertical, 7)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
                .buttonStyle(.plain)
            }

            if !state.psnLinked {
                Button("Tasarımı İncele (Geliştirici Modu)") {
                    state.bypassSetup = true
                    state.showSetup = false
                }
                .font(.system(size: 11))
                .foregroundStyle(Color.white.opacity(0.45))
                .padding(.horizontal, 10)
                .padding(.vertical, 7)
                .buttonStyle(.plain)
            } else {
                Button("Ana Ekrana Dön") {
                    state.showSetup = false
                }
                .font(.system(size: 11.5))
                .foregroundStyle(Color.white.opacity(0.55))
                .padding(.horizontal, 10)
                .padding(.vertical, 7)
                .buttonStyle(.plain)
            }

            Spacer()

            if state.setupStep < 5 {
                Button("Devam Et →") {
                    state.setupStep += 1
                }
                .font(.system(size: 12.5, weight: .semibold))
                .foregroundStyle(Color.white)
                .padding(.horizontal, 20)
                .padding(.vertical, 7)
                .background(Color(hex: "#0A84FF"), in: RoundedRectangle(cornerRadius: 6))
                .shadow(color: Color(hex: "#0A84FF").opacity(0.4), radius: 6, y: 2)
                .buttonStyle(.plain)
            } else {
                Button("✓ Kurulumu Tamamla ve Başlat") {
                    state.isOnboarded = true
                    state.showSetup = false
                    state.tvConnected = true
                    state.ps5Connected = true
                    if !state.isRunning {
                        state.toggle()
                    }
                }
                .font(.system(size: 12.5, weight: .bold))
                .foregroundStyle(Color.white)
                .padding(.horizontal, 22)
                .padding(.vertical, 7)
                .background(Color.green, in: RoundedRectangle(cornerRadius: 6))
                .shadow(color: Color.green.opacity(0.4), radius: 6, y: 2)
                .buttonStyle(.plain)
            }
        }
        .padding(.horizontal, 24)
        .padding(.vertical, 14)
        .background(Color.black.opacity(0.25))
    }
}

// MARK: - Extension Color Hex Helper
extension Color {
    init(hex: String) {
        let hex = hex.trimmingCharacters(in: CharacterSet.alphanumerics.inverted)
        var int: UInt64 = 0
        Scanner(string: hex).scanHexInt64(&int)
        let a, r, g, b: UInt64
        switch hex.count {
        case 3:
            (a, r, g, b) = (255, (int >> 8) * 17, (int >> 4 & 0xF) * 17, (int & 0xF) * 17)
        case 6:
            (a, r, g, b) = (255, int >> 16, int >> 8 & 0xFF, int & 0xFF)
        case 8:
            (a, r, g, b) = (int >> 24, int >> 16 & 0xFF, int >> 8 & 0xFF, int & 0xFF)
        default:
            (a, r, g, b) = (255, 0, 0, 0)
        }
        self.init(.sRGB, red: Double(r) / 255, green: Double(g) / 255, blue: Double(b) / 255, opacity: Double(a) / 255)
    }
}

@main
struct GameTranslatorNativeApp: App {
    @NSApplicationDelegateAdaptor(AppDelegate.self) var appDelegate
    var body: some Scene {
        WindowGroup {
            MainWindowView()
                .frame(minWidth: 970, idealWidth: 1010, minHeight: 640, idealHeight: 680)
        }
        .windowStyle(.hiddenTitleBar)
    }
}
