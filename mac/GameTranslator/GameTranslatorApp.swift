// SPDX-License-Identifier: AGPL-3.0-only

import Sparkle
import SwiftUI

@main
struct GameTranslatorApp: App {
    @State private var model = Self.makeModel()
    /// Sparkle: açılışta ve günde bir GitHub'daki son sürüme bakar; testlerde başlamaz.
    private let updater = SPUStandardUpdaterController(
        startingUpdater: ProcessInfo.processInfo.environment["XCTestConfigurationFilePath"] == nil,
        updaterDelegate: nil, userDriverDelegate: nil)

    /// Testler uygulamanın içinde çalışır. Gerçek Anahtarlığa dokunmazlar: imza farklı olunca macOS parola sorar ve test takılır.
    private static func makeModel() -> AppModel {
        ProcessInfo.processInfo.environment["XCTestConfigurationFilePath"] == nil ? AppModel() : AppModel(store: EphemeralStore())
    }

    var body: some Scene {
        WindowGroup {
            RootView()
                .environment(model)
                .appSurface()
                .frame(minWidth: 880, idealWidth: Theme.size("windowWidth"), maxWidth: 1440,
                       minHeight: 650, idealHeight: Theme.size("windowHeight"), maxHeight: 1080)
                // Başlık çubuğu da pencere zemininin rengini alır.
                .containerBackground(Color.csBackground, for: .window)
        }
        .windowResizability(.contentSize)
        .commands {
            CommandGroup(replacing: .appInfo) {
                Button("About GameTranslator") { model.showAbout = true }
                Button("Check for Updates…") { updater.checkForUpdates(nil) }
            }
            CommandGroup(replacing: .appSettings) {
                Button("Settings") { model.showSettings = true }
                    .keyboardShortcut(",")
                    .disabled(model.step != .done)
            }
        }
    }
}

struct RootView: View {
    @Environment(AppModel.self) private var model

    var body: some View {
        @Bindable var model = model
        Group {
            if model.step == .done { MainView() } else { OnboardingView() }
        }
        .sheet(isPresented: $model.showAbout) { AboutView().appSurface() }
    }
}
