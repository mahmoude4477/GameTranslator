// SPDX-License-Identifier: AGPL-3.0-only

import Foundation

/// Tek kanallı ses: örnekler -1…1 arası.
public struct SpeechAudio: Equatable, Sendable {
    public var samples: [Float]
    public var sampleRate: Int

    public init(samples: [Float], sampleRate: Int) {
        self.samples = samples
        self.sampleRate = sampleRate
    }

    public var duration: Double { Double(samples.count) / Double(sampleRate) }

    // MARK: - WAV

    public enum WAVError: Error { case notWAV, unsupportedFormat }

    /// 16 bit tam sayı ya da 32 bit kayan noktalı PCM WAV okur. Akış hâlinde yazılmış dosyada (boyut alanları 0 ya da
    /// en büyük değer) veri dosyanın sonuna kadar sürer. Çok kanallıysa ilk kanal alınır.
    public init(wav data: Data) throws {
        let bytes = [UInt8](data)
        guard bytes.count >= 12, String(decoding: bytes[0..<4], as: UTF8.self) == "RIFF",
              String(decoding: bytes[8..<12], as: UTF8.self) == "WAVE" else { throw WAVError.notWAV }
        func u16(_ at: Int) -> Int { Int(bytes[at]) | Int(bytes[at + 1]) << 8 }
        func u32(_ at: Int) -> Int { u16(at) | u16(at + 2) << 16 }

        var format = 0, channels = 1, rate = 24000, bits = 16
        var offset = 12
        while offset + 8 <= bytes.count {
            let id = String(decoding: bytes[offset..<offset + 4], as: UTF8.self)
            let size = u32(offset + 4)
            let body = offset + 8
            if id == "fmt ", body + 16 <= bytes.count {
                format = u16(body)
                channels = max(1, u16(body + 2))
                rate = u32(body + 4)
                bits = u16(body + 14)
                // WAVE_FORMAT_EXTENSIBLE: asıl biçim alt türün ilk iki baytında.
                if format == 0xFFFE, body + 26 <= bytes.count { format = u16(body + 24) }
            } else if id == "data" {
                let end = size == 0 || size == 0xFFFF_FFFF || body + size > bytes.count ? bytes.count : body + size
                let frame = channels * bits / 8
                guard frame > 0 else { throw WAVError.unsupportedFormat }
                var samples: [Float] = []
                samples.reserveCapacity((end - body) / frame)
                var at = body
                switch (format, bits) {
                case (1, 16):
                    while at + frame <= end {
                        samples.append(Float(Int16(bitPattern: UInt16(u16(at)))) / 32768)
                        at += frame
                    }
                case (3, 32):
                    while at + frame <= end {
                        samples.append(Float(bitPattern: UInt32(u32(at))))
                        at += frame
                    }
                default:
                    throw WAVError.unsupportedFormat
                }
                self.init(samples: samples, sampleRate: rate)
                return
            }
            offset = body + size + (size & 1)
        }
        throw WAVError.notWAV
    }

    /// 16 bit PCM WAV (TV'deki tarayıcıların hepsi çalar).
    public var wav: Data {
        var data = Data()
        func append(_ text: String) { data.append(contentsOf: Array(text.utf8)) }
        func append32(_ value: Int) { withUnsafeBytes(of: UInt32(value).littleEndian) { data.append(contentsOf: $0) } }
        func append16(_ value: Int) { withUnsafeBytes(of: UInt16(value).littleEndian) { data.append(contentsOf: $0) } }
        let payload = samples.count * 2
        append("RIFF"); append32(36 + payload); append("WAVE")
        append("fmt "); append32(16); append16(1); append16(1); append32(sampleRate); append32(sampleRate * 2); append16(2); append16(16)
        append("data"); append32(payload)
        data.reserveCapacity(44 + payload)
        for sample in samples {
            let value = Int16(max(-1, min(1, sample)) * 32767)
            withUnsafeBytes(of: value.littleEndian) { data.append(contentsOf: $0) }
        }
        return data
    }

    // MARK: - Duraklamalar

    /// Akıcılık: model virgül ve ünlemden sonra 200–400 ms, cümle başında ve sonunda daha uzun susuyor. Cümle içindeki
    /// her sessizlik en çok `maxPause`'a, baştaki `lead`'e, sondaki `tail`'e iner; kesilen yerler `fade` ile yumuşatılır.
    public func tightened(maxPause: Double = 0.2, lead: Double = 0.03, tail: Double = 0.08, fade: Double = 0.01) -> SpeechAudio {
        let hop = max(1, sampleRate / 100) // 10 ms
        let frames = samples.count / hop
        guard frames > 2 else { return self }
        var levels = [Float](repeating: 0, count: frames)
        for frame in 0..<frames {
            var sum: Float = 0
            for index in frame * hop..<(frame + 1) * hop { sum += samples[index] * samples[index] }
            levels[frame] = (sum / Float(hop)).squareRoot()
        }
        let threshold = (levels.max() ?? 0) * 0.03
        guard threshold > 0, let first = levels.firstIndex(where: { $0 >= threshold }),
              let last = levels.lastIndex(where: { $0 >= threshold }) else { return self }

        let maxGap = Int((maxPause * 100).rounded())
        var keep: [Range<Int>] = []
        var segmentStart = first
        var frame = first
        while frame <= last {
            guard levels[frame] < threshold else { frame += 1; continue }
            var end = frame
            while end <= last, levels[end] < threshold { end += 1 }
            let gap = end - frame
            if gap > maxGap {
                // Sessizliğin ortası kesilir; iki yanda konuşmanın sönümü kalır.
                let cut = gap - maxGap
                let middle = frame + (gap - cut) / 2
                keep.append(segmentStart * hop..<middle * hop)
                segmentStart = middle + cut
            }
            frame = end
        }
        keep.append(segmentStart * hop..<min(samples.count, (last + 1) * hop))
        let leadSamples = Int(lead * Double(sampleRate)), tailSamples = Int(tail * Double(sampleRate))
        keep[0] = max(0, keep[0].lowerBound - leadSamples)..<keep[0].upperBound
        keep[keep.count - 1] = keep[keep.count - 1].lowerBound..<min(samples.count, keep[keep.count - 1].upperBound + tailSamples)

        let fadeSamples = max(1, Int(fade * Double(sampleRate)))
        var output: [Float] = []
        output.reserveCapacity(samples.count)
        for range in keep where !range.isEmpty {
            var piece = Array(samples[range])
            let count = min(fadeSamples, piece.count / 2)
            for index in 0..<count {
                let gain = Float(index) / Float(fadeSamples)
                piece[index] *= gain
                piece[piece.count - 1 - index] *= gain
            }
            output += piece
        }
        return SpeechAudio(samples: output, sampleRate: sampleRate)
    }
}
