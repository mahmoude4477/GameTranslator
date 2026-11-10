// SPDX-License-Identifier: AGPL-3.0-only

import GameTranslatorCore
import Foundation

/// Samsung ve Android TV kurulumunu yapan gömülü yardımcıdan gelen olay.
enum TVInstallEvent: Equatable, Sendable {
    /// Samsung hesabıyla giriş gerekiyor; sayfa tarayıcıda açılmalı.
    case signIn(URL)
    /// Android TV ekranında hata ayıklama izni soruluyor.
    case approveOnTV
    case progress(String)
}

/// Uygulamanın içindeki `gametranslator-tv-helper` aracını çalıştırır (kaynağı windows/src/GameTranslator.TvHelper; Windows
/// uygulaması aynı kitaplığı doğrudan kullanır). Her çıktı satırı bir JSON olaydır.
enum TVHelper {
    static var executable: URL? { Bundle.main.url(forAuxiliaryExecutable: "gametranslator-tv-helper") }

    /// Yardımcının kalıcı dosyaları (ADB anahtarı, Samsung sertifikaları).
    static var dataDirectory: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        return base.appendingPathComponent(AppIdentity.name, isDirectory: true)
    }

    /// Komutun sonucu: `done` olayında sürüm, `found` olayında bulunan TV adresleri.
    struct Result: Equatable, Sendable {
        var version: String?
        var installed = false
        var hosts: [String] = []
    }

    /// Komutu çalıştırır; `done` olayına kadar bekler, `error` olayını `DevModeError`'a çevirir.
    static func run(_ arguments: [String], onEvent: @escaping @Sendable (TVInstallEvent) -> Void = { _ in }) async throws -> Result {
        guard let executable else { throw DevModeError.installFailed }
        let process = Process()
        process.executableURL = executable
        process.arguments = arguments + ["--exit-with-input"]
        let output = Pipe()
        let input = Pipe()
        process.standardOutput = output
        process.standardError = FileHandle.nullDevice
        process.standardInput = input
        let reader = LineReader()

        return try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Result, Error>) in
                let once = Resumer(continuation)
                output.fileHandleForReading.readabilityHandler = { handle in
                    let data = handle.availableData
                    if data.isEmpty {
                        handle.readabilityHandler = nil
                        return
                    }
                    for line in reader.append(data) {
                        guard let event = try? JSONSerialization.jsonObject(with: Data(line.utf8)) as? [String: Any] else { continue }
                        switch event["event"] as? String {
                        case "login": if let url = (event["url"] as? String).flatMap(URL.init(string:)) { onEvent(.signIn(url)) }
                        case "approve": onEvent(.approveOnTV)
                        case "progress": onEvent(.progress(event["step"] as? String ?? ""))
                        case "done", "found":
                            once.resume(.success(Result(version: event["version"] as? String,
                                                        installed: event["installed"] as? Bool ?? false,
                                                        hosts: event["hosts"] as? [String] ?? [])))
                        case "error": once.resume(.failure(Self.error(code: event["code"] as? String)))
                        default: break
                        }
                    }
                }
                process.terminationHandler = { _ in
                    // Olay yazmadan kapandıysa (çöktüyse) kurulum başarısız sayılır.
                    once.resume(.failure(DevModeError.installFailed))
                }
                do {
                    try process.run()
                } catch {
                    once.resume(.failure(DevModeError.installFailed))
                }
            }
        } onCancel: {
            try? input.fileHandleForWriting.close()
            process.terminate()
        }
    }

    static func error(code: String?) -> DevModeError {
        switch code {
        case "Unreachable": .unreachable
        case "NotApproved": .notApproved
        case "LoginFailed": .signInFailed
        case "CertificateFailed": .certificateFailed
        case "TooOld": .tooOld
        default: .installFailed
        }
    }
}

/// Parça parça gelen çıktıyı satırlara böler.
final class LineReader: @unchecked Sendable {
    private let lock = NSLock()
    private var buffer = Data()

    func append(_ data: Data) -> [String] {
        lock.withLock {
            buffer.append(data)
            var lines: [String] = []
            while let newline = buffer.firstIndex(of: 0x0A) {
                lines.append(String(decoding: buffer[buffer.startIndex..<newline], as: UTF8.self))
                buffer.removeSubrange(buffer.startIndex...newline)
            }
            return lines
        }
    }
}

/// Devamı yalnız bir kez sürdürür.
private final class Resumer: @unchecked Sendable {
    private let lock = NSLock()
    private var continuation: CheckedContinuation<TVHelper.Result, Error>?

    init(_ continuation: CheckedContinuation<TVHelper.Result, Error>) { self.continuation = continuation }

    func resume(_ result: Swift.Result<TVHelper.Result, Error>) {
        let current = lock.withLock { () -> CheckedContinuation<TVHelper.Result, Error>? in
            defer { continuation = nil }
            return continuation
        }
        current?.resume(with: result)
    }
}
