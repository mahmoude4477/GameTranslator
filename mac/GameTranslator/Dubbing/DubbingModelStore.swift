// SPDX-License-Identifier: AGPL-3.0-only

import GameTranslatorCore
import CryptoKit
import Foundation

/// Dublaj modelini (Pocket TTS Turkish, ~440 MB) ilk kullanımda indirir ve doğrular.
/// Dosyalar Application Support/GameTranslator/dubbing/<sürüm>/ altında durur; sürüm değişince eski klasör silinir.
enum DubbingModelStore {
    enum StoreError: LocalizedError {
        case download(String), corrupt(String)

        var errorDescription: String? {
            switch self {
            case .download(let name): String(localized: "Could not download \(name). Check the internet connection and try again.")
            case .corrupt(let name): String(localized: "\(name) was damaged while downloading. Try again.")
            }
        }
    }

    static var root: URL { TVHelper.dataDirectory.appendingPathComponent("dubbing", isDirectory: true) }
    static var directory: URL { root.appendingPathComponent(DubbingModel.revision, isDirectory: true) }
    static var config: URL { directory.appendingPathComponent("gametranslator.yaml") }
    private static var marker: URL { directory.appendingPathComponent(".ready") }

    static func voiceFile(_ voice: DubbingVoice) -> URL {
        directory.appendingPathComponent("voices/\(voice.rawValue).wav")
    }

    static var isReady: Bool { FileManager.default.fileExists(atPath: marker.path) }

    /// Eksik dosyaları indirir; `progress` 0…1 arası ana iş parçacığında çağrılır.
    static func download(progress: @escaping @MainActor (Double) -> Void) async throws {
        let manager = FileManager.default
        try manager.createDirectory(at: directory.appendingPathComponent("voices"), withIntermediateDirectories: true)
        // Önceki sürümlerin modelleri yer kaplamasın.
        for old in (try? manager.contentsOfDirectory(at: root, includingPropertiesForKeys: nil)) ?? []
        where old.lastPathComponent != DubbingModel.revision {
            try? manager.removeItem(at: old)
        }

        let total = Double(DubbingModel.totalBytes)
        var done: Int64 = 0
        for file in DubbingModel.files {
            let target = directory.appendingPathComponent(file.name)
            if let size = try? manager.attributesOfItem(atPath: target.path)[.size] as? Int64, size == file.bytes,
               try sha256(of: target) == file.sha256 {
                done += file.bytes
                await progress(Double(done) / total)
                continue
            }
            let base = done
            let temporary = try await Downloader.download(file.url) { written in
                Task { @MainActor in progress(Double(base + written) / total) }
            }
            guard try sha256(of: temporary) == file.sha256 else {
                try? manager.removeItem(at: temporary)
                throw StoreError.corrupt(file.name)
            }
            try? manager.removeItem(at: target)
            try manager.moveItem(at: temporary, to: target)
            done += file.bytes
        }

        let original = try String(contentsOf: directory.appendingPathComponent("config.yaml"), encoding: .utf8)
        try DubbingModel.localConfig(from: original, directory: directory).write(to: config, atomically: true, encoding: .utf8)
        try Data().write(to: marker)
        await progress(1)
    }

    static func remove() {
        try? FileManager.default.removeItem(at: root)
    }

    private static func sha256(of url: URL) throws -> String {
        let handle = try FileHandle(forReadingFrom: url)
        defer { try? handle.close() }
        var hasher = SHA256()
        while let chunk = try handle.read(upToCount: 1 << 20), !chunk.isEmpty { hasher.update(data: chunk) }
        return hasher.finalize().map { String(format: "%02x", $0) }.joined()
    }
}

/// İlerlemesi izlenebilen tek dosya indirmesi.
private final class Downloader: NSObject, URLSessionDownloadDelegate, @unchecked Sendable {
    private let onProgress: @Sendable (Int64) -> Void
    private var continuation: CheckedContinuation<URL, Error>?
    /// İlerleme her 2 MB'de bir bildirilir; her küçük parçada arayüz güncellenmesin.
    private var reported: Int64 = 0

    private init(onProgress: @escaping @Sendable (Int64) -> Void) {
        self.onProgress = onProgress
    }

    static func download(_ url: URL, onProgress: @escaping @Sendable (Int64) -> Void) async throws -> URL {
        let downloader = Downloader(onProgress: onProgress)
        let session = URLSession(configuration: .default, delegate: downloader, delegateQueue: nil)
        defer { session.finishTasksAndInvalidate() }
        return try await withTaskCancellationHandler {
            try await withCheckedThrowingContinuation { continuation in
                downloader.continuation = continuation
                session.downloadTask(with: url).resume()
            }
        } onCancel: {
            session.invalidateAndCancel()
        }
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didWriteData bytesWritten: Int64,
                    totalBytesWritten: Int64, totalBytesExpectedToWrite: Int64) {
        guard totalBytesWritten - reported >= 2 << 20 || totalBytesWritten == totalBytesExpectedToWrite else { return }
        reported = totalBytesWritten
        onProgress(totalBytesWritten)
    }

    func urlSession(_ session: URLSession, downloadTask: URLSessionDownloadTask, didFinishDownloadingTo location: URL) {
        // Geçici dosya bu çağrıdan sonra silinir; hemen taşınır.
        let kept = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let status = (downloadTask.response as? HTTPURLResponse)?.statusCode ?? 0
        do {
            guard (200..<300).contains(status) else {
                throw DubbingModelStore.StoreError.download(downloadTask.originalRequest?.url?.lastPathComponent ?? "")
            }
            try FileManager.default.moveItem(at: location, to: kept)
            continuation?.resume(returning: kept)
        } catch {
            continuation?.resume(throwing: error)
        }
        continuation = nil
    }

    func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        guard error != nil else { return }
        continuation?.resume(throwing: DubbingModelStore.StoreError.download(task.originalRequest?.url?.lastPathComponent ?? ""))
        continuation = nil
    }
}
