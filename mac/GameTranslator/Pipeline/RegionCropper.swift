// SPDX-License-Identifier: AGPL-3.0-only

import CoreGraphics

/// Görüntüde altyazının arandığı bölge, kenarlara oranla (0–1, üstten ve soldan).
struct CropRegion: Codable, Equatable {
    var top = 0.62
    var bottom = 0.97
    var left = 0.14
    var right = 0.86
}

enum RegionCropper {
    static func crop(_ image: CGImage, to region: CropRegion) -> CGImage {
        let width = Double(image.width), height = Double(image.height)
        let rect = CGRect(x: width * region.left, y: height * region.top,
                          width: width * (region.right - region.left), height: height * (region.bottom - region.top))
        return image.cropping(to: rect.integral) ?? image
    }

    /// Bölgenin küçültülmüş gri kopyası: iki kareyi yazı tanımaya gerek kalmadan karşılaştırmaya yeter.
    static func signature(of image: CGImage) -> [UInt8] {
        let width = 64, height = 12
        var pixels = [UInt8](repeating: 0, count: width * height)
        pixels.withUnsafeMutableBytes { buffer in
            guard let context = CGContext(data: buffer.baseAddress, width: width, height: height, bitsPerComponent: 8, bytesPerRow: width,
                                          space: CGColorSpaceCreateDeviceGray(), bitmapInfo: CGImageAlphaInfo.none.rawValue) else { return }
            context.interpolationQuality = .low
            context.draw(image, in: CGRect(x: 0, y: 0, width: width, height: height))
        }
        return pixels
    }
}
