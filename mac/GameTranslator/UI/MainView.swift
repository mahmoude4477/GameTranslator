// SPDX-License-Identifier: AGPL-3.0-only
import SwiftUI
import AppKit
import Network
import UniformTypeIdentifiers

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
        let count = clients.count - viewers.count
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

public struct VisualEffectBackground: NSViewRepresentable {
    public var material: NSVisualEffectView.Material = .underWindowBackground
    public var blendingMode: NSVisualEffectView.BlendingMode = .behindWindow

    public init(material: NSVisualEffectView.Material = .underWindowBackground, blendingMode: NSVisualEffectView.BlendingMode = .behindWindow) {
        self.material = material
        self.blendingMode = blendingMode
    }

    public func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        view.material = material
        view.blendingMode = blendingMode
        view.state = .active
        return view
    }

    public func updateNSView(_ nsView: NSVisualEffectView, context: Context) {
        nsView.material = material
        nsView.blendingMode = blendingMode
    }
}

public struct WindowConfigurator: NSViewRepresentable {
    public init() {}

    public func makeNSView(context: Context) -> NSView {
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
    public func updateNSView(_ nsView: NSView, context: Context) {}
}

// MARK: - Data Models

public struct SubtitleEntry: Identifiable, Codable {
    public var id = UUID()
    public let turkish: String
    public let english: String
    public let timestamp: String
}

public enum TranslationModel: String, CaseIterable, Identifiable {
    case appleLocal = "local_apple"
    case gemini = "gemini"
    case deepL = "deepl"
    case marian = "local_dl"

    public var id: String { rawValue }

    public var title: String {
        switch self {
        case .appleLocal: "1. Apple Yerel Çeviri (Önerilen)"
        case .gemini: "2. Google Gemini 2.5 Flash"
        case .deepL: "3. DeepL API"
        case .marian: "4. Yerel Derin Öğrenme (MarianMT)"
        }
    }

    public var badge: String {
        switch self {
        case .appleLocal: "Aktif (0ms)"
        case .gemini: "Yapay Zeka"
        case .deepL: "Pro Dilbilgisi"
        case .marian: "GPU / CPU"
        }
    }

    public var desc: String {
        switch self {
        case .appleLocal: "Mac'inizin Apple Neural Engine (NPU) çipini kullanır. Tamamen çevrimdışı, internet gerektirmez, 0 ms gecikmeyle anında TV'ye basar."
        case .gemini: "GTA 6 gibi oyunlardaki diyalogları, sokak argosunu ve hikaye bağlamını en akıllı ve doğal şekilde çevirir."
        case .deepL: "Dünyanın en yüksek doğruluklu dilbilgisi motoru. DeepL Free veya Pro API anahtarı gerektirir."
        case .marian: "İnternetsiz açık kaynaklı derin öğrenme çeviri ağı. Bilgisayarınızın donanımını doğrudan kullanır."
        }
    }
}

public enum SettingsTab: String, CaseIterable, Identifiable {
    case display = "display"
    case ocrArea = "ocrArea"
    case models = "models"
    case system = "system"
    case guide = "guide"

    public var id: String { rawValue }

    public var title: String {
        switch self {
        case .display: "TV & Görünüm"
        case .ocrArea: "Okuma Alanı (OCR)"
        case .models: "Çeviri & Modeller"
        case .system: "Bağlantı & Sistem"
        case .guide: "Hızlı Rehber"
        }
    }

    public var icon: String {
        switch self {
        case .display: "tv.fill"
        case .ocrArea: "viewfinder.rectangular"
        case .models: "brain.head.profile"
        case .system: "link"
        case .guide: "book.fill"
        }
    }
}

public enum OCRAreaPreset: String, CaseIterable, Identifiable {
    case bottomCenter = "Alt Orta (Standart Oyun)"
    case bottomWide = "Geniş Alt Bölge"
    case fullBottom = "Tüm Alt Yarı"
    case topBanner = "Üst Bölge (Görevler)"
    case custom = "Özel Kutu"

    public var id: String { rawValue }
}

// MARK: - App State

@Observable
public final class AppState {
    public var isRunning: Bool = false
    public var ps5Connected: Bool = true
    public var psnEmail: String = "sefflexpsplus@gmail.com"
    public var tvConnected: Bool = true
    public var tvDeviceName: String = "TCL Android TV (192.168.1.105:5555)"
    
    // Model
    public var selectedModel: TranslationModel = .appleLocal
    public var geminiApiKey: String = ""
    public var deepLApiKey: String = ""
    
    // TV Altyazı Ayarları
    public var selectedColor: String = "#FFFFFF"
    public var fontSize: Double = 46
    public var bottomPercent: Double = 7
    public var backgroundOpacity: Double = 72
    public var isBold: Bool = true
    public var placement: String = "cover"
    public var language: String = "en"
    public var isDubbing: Bool = false
    
    // OCR Okuma Alanı
    public var cropTop: Double = 0.70
    public var cropBottom: Double = 0.94
    public var cropLeft: Double = 0.15
    public var cropRight: Double = 0.85
    public var selectedPreset: OCRAreaPreset = .bottomCenter
    public var filterNoise: Bool = true
    
    // Canlı altyazı & Geçmiş
    public var liveTurkish: String = "muhafız"
    public var liveEnglish: String = "Guard"
    public var history: [SubtitleEntry] = [
        SubtitleEntry(turkish: "muhafız", english: "Guard", timestamp: "23:00"),
        SubtitleEntry(turkish: "İyi olmalı. Topografyanın değiştiğini söyledi, bu yüzden bir şeylerin farklı olup olmadığını fark", english: "about the giant bird he mentioned. I wo", timestamp: "22:58"),
        SubtitleEntry(turkish: "Bahsettiği dev kuş hakkında. Ben wo", english: "about the giant bird he mentioned. I wo", timestamp: "22:57"),
        SubtitleEntry(turkish: "Şimdi bunun için endişelenmek biraz geç, değil mi?", english: "It's a bit late to be worrying about that now, isn't it?", timestamp: "22:55")
    ]
    
    public var obsCopied: Bool = false
    public var testSent: Bool = false
    public var showAbout: Bool = false
    
    public let server = NativeSubtitleServer(port: 8787)
    
    public init() {
        server.onClientsChanged = { [weak self] count in
            guard let self else { return }
            if count > 0 { self.tvConnected = true }
        }
        server.start()
        syncStyleToTV()
    }
    
    public func applyPreset(_ preset: OCRAreaPreset) {
        selectedPreset = preset
        switch preset {
        case .bottomCenter:
            cropTop = 0.70; cropBottom = 0.94; cropLeft = 0.15; cropRight = 0.85
        case .bottomWide:
            cropTop = 0.60; cropBottom = 0.98; cropLeft = 0.05; cropRight = 0.95
        case .fullBottom:
            cropTop = 0.50; cropBottom = 0.98; cropLeft = 0.02; cropRight = 0.98
        case .topBanner:
            cropTop = 0.04; cropBottom = 0.25; cropLeft = 0.08; cropRight = 0.92
        case .custom:
            break
        }
    }
    
    public func syncStyleToTV() {
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
    
    public func toggle() {
        isRunning.toggle()
        if isRunning {
            liveTurkish = "GameTranslator Aktif"
            liveEnglish = "Çeviri başlatıldı, PS5 ve TV senkronize çalışıyor."
            server.sendSubtitle(turkish: liveTurkish, original: liveEnglish)
        } else {
            liveTurkish = "Çeviri Durduruldu"
            liveEnglish = "Yeniden başlatmak için 'Çeviriyi Başlat' butonuna basın."
            server.clearSubtitle()
        }
    }
    
    public func sendTestSubtitle() {
        testSent = true
        liveTurkish = "muhafız"
        liveEnglish = "Guard"
        let entry = SubtitleEntry(turkish: liveTurkish, english: liveEnglish, timestamp: currentTime())
        history.insert(entry, at: 0)
        syncStyleToTV()
        server.sendSubtitle(turkish: liveTurkish, original: liveEnglish)
        DispatchQueue.main.asyncAfter(deadline: .now() + 2.5) {
            self.testSent = false
        }
    }
    
    public func resetArea() {
        applyPreset(.bottomCenter)
        fontSize = 46
        bottomPercent = 7
        backgroundOpacity = 72
        selectedColor = "#FFFFFF"
        isBold = true
        syncStyleToTV()
    }
    
    public func clearHistory() {
        history.removeAll()
    }
    
    public func exportHistory(format: String) {
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

// MARK: - Main Dual-Pane Window View

public struct MainView: View {
    @State private var state = AppState()
    @State private var activeTab: SettingsTab = .display

    public init() {}

    let colorPalette: [(name: String, hex: String, color: Color)] = [
        ("Klasik Beyaz", "#FFFFFF", Color.white),
        ("Oyun Sarısı", "#FACC15", Color(red: 250/255, green: 204/255, blue: 21/255)),
        ("Camgöbeği (Cyan)", "#38BDF8", Color(red: 56/255, green: 189/255, blue: 248/255)),
        ("Neon Yeşil", "#4ADE80", Color(red: 74/255, green: 222/255, blue: 128/255)),
        ("Açık Turuncu", "#FB923C", Color(red: 251/255, green: 146/255, blue: 60/255))
    ]

    public var body: some View {
        HStack(spacing: 0) {
            solPanel
            Divider().overlay(Color.white.opacity(0.12))
            sagPanel
        }
        .frame(minWidth: 960, idealWidth: 1000, minHeight: 630, idealHeight: 670)
        .background(WindowConfigurator())
        .alert("GameTranslator 2.0", isPresented: $state.showAbout) {
            Button("Tamam", role: .cancel) { }
        } message: {
            Text("PlayStation 5 ve Akıllı TV için Sıfır Gecikmeli Yapay Zeka Çeviri Motoru.\n\nSürüm: 2.0.0 (Native Frosted Glass)\nSSE TV Portu: 8787")
        }
    }

    // MARK: - SOL PANEL
    private var solPanel: some View {
        VStack(spacing: 12) {
            headerBar
            actionButton
            ocrShortcutButton
            connectionStatusCard
            lastSubtitleCard
            historyCard
            Spacer()
        }
        .padding(16)
        .frame(width: 370)
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
            .frame(maxWidth: .infinity, minHeight: 44)
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
            .padding(.horizontal, 10)
            .padding(.vertical, 7)
            .background(Color.white.opacity(0.06), in: RoundedRectangle(cornerRadius: 7))
            .overlay(RoundedRectangle(cornerRadius: 7).stroke(Color.white.opacity(0.08), lineWidth: 1))
        }
        .buttonStyle(.plain)
    }

    private var connectionStatusCard: some View {
        VStack(spacing: 8) {
            HStack {
                Text("Connection Status")
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(Color.white.opacity(0.55))
                Spacer()
            }

            // PS5
            HStack {
                Circle()
                    .fill(state.ps5Connected ? Color(hex: "#10B981") : Color.gray.opacity(0.6))
                    .frame(width: 7, height: 7)
                Text("PS5")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(Color.white.opacity(0.9))
                Spacer()
                Text(state.ps5Connected ? "Bağlı (\(state.psnEmail))" : "Aktif değil")
                    .font(.system(size: 10.5))
                    .foregroundStyle(state.ps5Connected ? Color.white.opacity(0.85) : Color.white.opacity(0.45))
                Button(state.ps5Connected ? "Çıkış" : "Giriş") {
                    state.ps5Connected.toggle()
                }
                .font(.system(size: 9.5, weight: .medium))
                .foregroundStyle(state.ps5Connected ? Color.red.opacity(0.9) : Color.blue)
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 4))
                .buttonStyle(.plain)
            }

            // TV
            HStack {
                Circle()
                    .fill(state.tvConnected ? Color(hex: "#10B981") : Color(hex: "#F59E0B"))
                    .frame(width: 7, height: 7)
                Text("TV")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(Color.white.opacity(0.9))
                Spacer()
                Text(state.tvConnected ? "\(state.tvDeviceName) (Bağlı)" : "TV uygulaması bekleniyor")
                    .font(.system(size: 10.5))
                    .foregroundStyle(state.tvConnected ? Color.white.opacity(0.85) : Color(hex: "#F59E0B"))
                Button(state.tvConnected ? "Kes" : "Bağlan") {
                    state.tvConnected.toggle()
                }
                .font(.system(size: 9.5, weight: .medium))
                .foregroundStyle(state.tvConnected ? Color.red.opacity(0.9) : Color.green)
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 4))
                .buttonStyle(.plain)
            }

            // Model
            HStack {
                Image(systemName: "sparkles")
                    .font(.system(size: 10))
                    .foregroundStyle(Color.yellow)
                Text("Model:")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(Color.white.opacity(0.6))
                Text(state.selectedModel.title)
                    .font(.system(size: 10.5, weight: .semibold))
                    .foregroundStyle(Color(hex: "#38BDF8"))
                    .lineLimit(1)
                Spacer()
                Button("Değiştir") {
                    activeTab = .models
                }
                .font(.system(size: 9.5))
                .foregroundStyle(Color.white.opacity(0.6))
                .buttonStyle(.plain)
            }
        }
        .padding(10)
        .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 9))
        .overlay(RoundedRectangle(cornerRadius: 9).stroke(Color.white.opacity(0.1), lineWidth: 1))
    }

    private var lastSubtitleCard: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text("Son Altyazı Akışı")
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(Color.white.opacity(0.55))
                Spacer()
                Text("Canlı")
                    .font(.system(size: 9, weight: .bold))
                    .foregroundStyle(Color.green)
                    .padding(.horizontal, 5)
                    .padding(.vertical, 1)
                    .background(Color.green.opacity(0.15), in: Capsule())
            }

            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 4) {
                    Text("📺 TV:")
                        .font(.system(size: 10, weight: .bold))
                        .foregroundStyle(Color(hex: "#0A84FF"))
                    Text(state.liveTurkish)
                        .font(.system(size: 14.5, weight: .bold))
                        .foregroundStyle(Color.white)
                        .lineLimit(2)
                }
                HStack(spacing: 4) {
                    Text("🔍 PS5:")
                        .font(.system(size: 10, weight: .bold))
                        .foregroundStyle(Color.white.opacity(0.45))
                    Text(state.liveEnglish)
                        .font(.system(size: 11))
                        .foregroundStyle(Color.white.opacity(0.55))
                        .lineLimit(2)
                }
            }
            .frame(maxWidth: .infinity, minHeight: 46, alignment: .leading)
            .padding(9)
            .background(Color.black.opacity(0.4), in: RoundedRectangle(cornerRadius: 8))
            .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.white.opacity(0.08), lineWidth: 1))
        }
        .padding(10)
        .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 9))
        .overlay(RoundedRectangle(cornerRadius: 9).stroke(Color.white.opacity(0.1), lineWidth: 1))
    }

    private var historyCard: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text("Altyazı Geçmişi")
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundStyle(Color.white.opacity(0.55))
                Spacer()
                Button("TXT İndir") {
                    state.exportHistory(format: "txt")
                }
                .font(.system(size: 9.5, weight: .medium))
                .foregroundStyle(Color.white.opacity(0.75))
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 4))
                .buttonStyle(.plain)

                Button("JSON") {
                    state.exportHistory(format: "json")
                }
                .font(.system(size: 9.5, weight: .medium))
                .foregroundStyle(Color.white.opacity(0.75))
                .padding(.horizontal, 6)
                .padding(.vertical, 2)
                .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 4))
                .buttonStyle(.plain)
            }

            ScrollView(.vertical, showsIndicators: true) {
                VStack(alignment: .leading, spacing: 5) {
                    ForEach(state.history) { item in
                        VStack(alignment: .leading, spacing: 1) {
                            Text(item.turkish)
                                .font(.system(size: 11, weight: .semibold))
                                .foregroundStyle(Color.white.opacity(0.92))
                            Text(item.english)
                                .font(.system(size: 9.5))
                                .foregroundStyle(Color.white.opacity(0.45))
                        }
                        .padding(5)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(Color.white.opacity(0.03), in: RoundedRectangle(cornerRadius: 5))
                    }
                }
            }
            .frame(maxHeight: 95)
        }
        .padding(10)
        .background(Color.black.opacity(0.25), in: RoundedRectangle(cornerRadius: 9))
        .overlay(RoundedRectangle(cornerRadius: 9).stroke(Color.white.opacity(0.1), lineWidth: 1))
    }

    // MARK: - SAĞ PANEL
    private var sagPanel: some View {
        VStack(spacing: 0) {
            topTabSelector
            ScrollView(.vertical, showsIndicators: true) {
                VStack(alignment: .leading, spacing: 13) {
                    switch activeTab {
                    case .display: displayTabContent
                    case .ocrArea: ocrAreaTabContent
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
                        activeTab == tab ? Color.white.opacity(0.16) : Color.clear,
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
                state.resetArea()
            }
            .font(.system(size: 11))
            .foregroundStyle(Color.white.opacity(0.8))
            .padding(.horizontal, 10)
            .padding(.vertical, 5)
            .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 6))
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

    // MARK: - 1. TV & GÖRÜNÜM SEKMESİ
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

    // MARK: - 2. OKUMA & OCR ALANI SEKMESİ
    private var ocrAreaTabContent: some View {
        VStack(alignment: .leading, spacing: 12) {
            VStack(alignment: .leading, spacing: 3) {
                HStack {
                    Image(systemName: "viewfinder.rectangular").foregroundStyle(Color(hex: "#38BDF8"))
                    Text("Oyun Ekranı Altyazı Okuma Alanı (OCR Tarama Bölgesi)").font(.system(size: 12.5, weight: .bold)).foregroundStyle(Color.white)
                }
                Text("Oyun ekranında altyazının nerede olduğunu seçin. OCR motoru yalnızca bu kutu içindeki altyazıları tarar.")
                    .font(.system(size: 11)).foregroundStyle(Color.white.opacity(0.65))
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
                                state.selectedPreset == preset ? Color(hex: "#0A84FF").opacity(0.25) : Color.white.opacity(0.06),
                                in: RoundedRectangle(cornerRadius: 6)
                            )
                            .overlay(RoundedRectangle(cornerRadius: 6).stroke(state.selectedPreset == preset ? Color(hex: "#0A84FF") : Color.clear, lineWidth: 1))
                    }
                    .buttonStyle(.plain)
                }
            }

            // 16:9 Canvas
            GeometryReader { geo in
                ZStack(alignment: .topLeading) {
                    RoundedRectangle(cornerRadius: 8)
                        .fill(LinearGradient(colors: [Color(hex: "#0B0F19"), Color(hex: "#161B26"), Color(hex: "#0D1117")], startPoint: .topLeading, endPoint: .bottomTrailing))
                        .overlay(VStack { HStack { Text("🎮 PS5 Görüntüsü (1080p 60 FPS)").font(.system(size: 9, weight: .medium)).foregroundStyle(Color.white.opacity(0.3)).padding(6); Spacer() }; Spacer() })

                    RoundedRectangle(cornerRadius: 8).fill(Color.black.opacity(0.45))

                    let boxWidth = geo.size.width * CGFloat(max(0.1, state.cropRight - state.cropLeft))
                    let boxHeight = geo.size.height * CGFloat(max(0.08, state.cropBottom - state.cropTop))
                    let boxX = geo.size.width * CGFloat(state.cropLeft)
                    let boxY = geo.size.height * CGFloat(state.cropTop)

                    ZStack {
                        RoundedRectangle(cornerRadius: 5).fill(Color(hex: "#38BDF8").opacity(0.15))
                        RoundedRectangle(cornerRadius: 5).stroke(Color(hex: "#38BDF8"), style: StrokeStyle(lineWidth: 1.8, dash: [4, 2]))

                        VStack(spacing: 2) {
                            Text("OCR OKUMA ALANI").font(.system(size: 8, weight: .bold)).foregroundStyle(Color(hex: "#38BDF8"))
                                .padding(.horizontal, 5).padding(.vertical, 1).background(Color.black.opacity(0.6), in: Capsule())
                            Text("\"\(state.liveEnglish)\"").font(.system(size: 10, weight: .semibold)).foregroundStyle(Color.white).lineLimit(1)
                        }
                        .padding(4)
                    }
                    .frame(width: boxWidth, height: boxHeight)
                    .position(x: boxX + boxWidth / 2, y: boxY + boxHeight / 2)
                }
                .clipShape(RoundedRectangle(cornerRadius: 8))
                .overlay(RoundedRectangle(cornerRadius: 8).stroke(Color.white.opacity(0.12), lineWidth: 1))
            }
            .frame(height: 140)

            // Koordinat Kontrolleri
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
                Toggle("Gürültü Filtresi", isOn: $state.filterNoise).font(.system(size: 10.5)).toggleStyle(.checkbox)
                Spacer()
                Button("Varsayılan Yap") { state.applyPreset(.bottomCenter) }
                    .font(.system(size: 10.5)).foregroundStyle(Color.white.opacity(0.8)).padding(.horizontal, 10).padding(.vertical, 4)
                    .background(Color.white.opacity(0.08), in: RoundedRectangle(cornerRadius: 5)).buttonStyle(.plain)
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
                guideItem(num: "3", title: "Okuma Alanı", text: "Oyununuzun altyazı konumuna göre 'Okuma Alanı (OCR)' sekmesinden altyazı kutusunu ayarlayın.")
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
