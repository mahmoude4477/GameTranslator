// SPDX-License-Identifier: AGPL-3.0-only

import AppKit
import GameTranslatorCore
import Foundation

/// Uygulamanın içindeki pocket-tts ile Türkçe seslendirme (bkz. `PocketTTSServer`).
actor PocketTTSEngine {
    enum EngineError: LocalizedError {
        case missing, notReady, failed(String)

        var errorDescription: String? {
            switch self {
            case .missing: String(localized: "The voice engine is missing from the app. Download GameTranslator again.")
            case .notReady: String(localized: "The voice engine did not start.")
            case .failed(let detail): detail
            }
        }
    }

    private var server: PocketTTSServer?
    private var quitObserver: NSObjectProtocol?

    private func ready() throws -> PocketTTSServer {
        if let server { return server }
        guard let executable = Bundle.main.url(forAuxiliaryExecutable: "pocket-tts") else { throw EngineError.missing }
        guard DubbingModelStore.isReady else { throw EngineError.notReady }
        let server = PocketTTSServer(executable: executable, config: DubbingModelStore.config,
                                     defaultVoice: DubbingModelStore.voiceFile(.male1),
                                     pidFile: DubbingModelStore.root.appendingPathComponent("pocket-tts.pid"))
        // GameTranslator kapanınca ses motoru da kapanır (çökerse bir sonraki açılışta kapatılır).
        quitObserver = NotificationCenter.default.addObserver(forName: NSApplication.willTerminateNotification, object: nil,
                                                              queue: nil) { _ in server.terminate() }
        self.server = server
        return server
    }

    func start() async throws {
        do { try await ready().start() } catch let error as PocketTTSServer.ServerError { throw Self.map(error) }
    }

    func synthesize(_ text: String, voice: URL) async throws -> SpeechAudio {
        do { return try await ready().synthesize(text, voice: voice) } catch let error as PocketTTSServer.ServerError { throw Self.map(error) }
    }

    func stop() async {
        await server?.stop()
        server = nil
        if let quitObserver { NotificationCenter.default.removeObserver(quitObserver) }
        quitObserver = nil
    }

    private static func map(_ error: PocketTTSServer.ServerError) -> EngineError {
        switch error {
        case .notReady: .notReady
        case .failed(let detail): .failed(detail)
        }
    }
}
