// swift-tools-version: 6.2
// SPDX-License-Identifier: AGPL-3.0-only

import PackageDescription

let package = Package(
    name: "GameTranslatorCore",
    platforms: [.macOS(.v26), .iOS(.v26)],
    products: [
        .library(name: "GameTranslatorCore", targets: ["GameTranslatorCore"]),
        .executable(name: "tv-smoke", targets: ["tv-smoke"]),
    ],
    targets: [
        .target(
            name: "GameTranslatorCore",
            resources: [.copy("Resources/obs-overlay.html")],
            swiftSettings: [.swiftLanguageMode(.v5)]
        ),
        .executableTarget(
            name: "tv-smoke",
            dependencies: ["GameTranslatorCore"],
            swiftSettings: [.swiftLanguageMode(.v5)]
        ),
        .testTarget(
            name: "GameTranslatorCoreTests",
            dependencies: ["GameTranslatorCore"],
            swiftSettings: [.swiftLanguageMode(.v5)]
        ),
    ]
)
