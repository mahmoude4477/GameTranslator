// SPDX-License-Identifier: AGPL-3.0-only

import GameTranslatorCore
import SwiftUI

/// TV uygulamasını geliştirici moduyla kurma adımları (LG, Samsung, Android TV). Yapılacak bir şey yoksa görünmez.
struct TVAppCard: View {
    @Environment(AppModel.self) private var model
    @State private var passphrase = ""

    private var brand: TVBrand { model.tv?.brand ?? .lg }

    var body: some View {
        Group {
            switch model.tvApp {
            case .unknown:
                EmptyView()
            case .working:
                Card(title: "GameTranslator TV app") {
                    HStack(spacing: Theme.space("s")) {
                        ProgressView().controlSize(.small)
                        Text("Installing the TV app…").foregroundStyle(Color.csSecondary)
                    }
                }
            case .upToDate(let version):
                Card(title: "GameTranslator TV app") {
                    DoneLabel(text: String(format: String(localized: "The TV app is ready (version %@)."), version))
                    if brand == .lg {
                        if let hours = model.devModeHoursLeft {
                            HintText(text: "Developer Mode: \(hours) hours left. GameTranslator extends it by itself each time you use it.")
                        } else {
                            HintText(text: "GameTranslator extends Developer Mode by itself each time you use it.")
                        }
                    }
                }
            case .devModeOff:
                devModeSteps
                    // Kullanıcı TV'de geliştirici modunu açınca kendiliğinden devam eder.
                    .task {
                        while !Task.isCancelled, model.tvApp == .devModeOff {
                            try? await Task.sleep(for: .seconds(3))
                            await model.refreshTVApp()
                        }
                    }
            case .needsPassphrase(let error):
                passphraseCard(error: error)
            case .signIn(let url):
                Card(title: "GameTranslator TV app") {
                    Text("Samsung signs the app for your TV. Sign in with your Samsung account on the page that opened in your browser.")
                    HintText(text: "No account? You can create a free one on the same page. If the browser asks to allow access to devices on your network, allow it.")
                    HStack(spacing: Theme.space("s")) {
                        ProgressView().controlSize(.small)
                        Text("Waiting for sign-in…").foregroundStyle(Color.csSecondary)
                        Spacer()
                        Button("Open the page again") { model.openInBrowser(url) }.buttonStyle(.secondary)
                    }
                }
            case .approveOnTV:
                Card(title: "GameTranslator TV app") {
                    Text("Look at the TV: it asks whether to allow debugging from this computer.")
                    HintText(text: "Tick “Always allow from this computer” and choose Allow. The app then installs by itself.")
                    HStack(spacing: Theme.space("s")) {
                        ProgressView().controlSize(.small)
                        Text("Waiting for the TV…").foregroundStyle(Color.csSecondary)
                    }
                }
            case .failed(let error, let reason):
                Card(title: "GameTranslator TV app") {
                    ErrorText(error: Self.message(for: error, brand: brand, reason: reason))
                    Button("Try again") { Task { await model.refreshTVApp() } }.buttonStyle(.secondary)
                }
            }
        }
    }

    @ViewBuilder
    private var devModeSteps: some View {
        switch brand {
        case .lg:
            Card(title: "GameTranslator TV app") {
                Text("The TV app is not installed. GameTranslator can install it once Developer Mode is on.")
                HintText(text: "1. Create a free account at webostv.developer.lge.com.")
                HintText(text: "2. On the TV, install Developer Mode from the LG Content Store and sign in.")
                HintText(text: "3. Turn on Dev Mode Status. The TV restarts and this screen continues by itself.")
            }
        case .samsung:
            Card(title: "GameTranslator TV app") {
                Text("The TV app is not installed. GameTranslator installs it once Developer Mode is on.")
                HintText(text: "1. On the TV, open Apps.")
                HintText(text: "2. Press 1 2 3 4 5 on the remote (or type 12345 on the on-screen keypad). The Developer Mode window opens.")
                HintText(text: "3. Turn Developer mode On, type this computer's address as Host PC IP: \(model.computerAddress ?? "?") and press OK.")
                HintText(text: "4. Restart the TV. This screen continues by itself.")
                HintText(text: "Later you sign in once with a free Samsung account; Samsung needs it to sign the app for your TV.")
            }
        case .androidTV:
            Card(title: "GameTranslator TV app") {
                Text("The TV app is not installed. GameTranslator installs it over the network once debugging is on.")
                HintText(text: "1. On the TV, open Settings › Device Preferences › About (Google TV: Settings › System › About).")
                HintText(text: "2. Press OK on Build 7 times until it says you are a developer.")
                HintText(text: "3. Go back to Device Preferences (Google TV: System) › Developer options and turn on USB debugging. Turn on Network debugging too if you see it.")
                HStack(spacing: Theme.space("s")) {
                    ProgressView().controlSize(.small)
                    Text("Looking for the TV on the network…").foregroundStyle(Color.csSecondary)
                }
                HintText(text: "Menu names differ a little by brand. If the app is already on the TV, you can skip this step.")
                Link("Install with a USB stick instead", destination: AppIdentity.tvGuideURL)
            }
        }
    }

    private func passphraseCard(error: DevModeError?) -> some View {
        Card(title: "GameTranslator TV app") {
            Text("Developer Mode is on. One last step to install the TV app:")
            HintText(text: "Open the Developer Mode app on the TV, turn on Key Server and keep the app on screen. Type the Passphrase it shows.")
            HStack {
                TextField("Passphrase", text: $passphrase)
                    .textFieldStyle(CSTextFieldStyle())
                Button("Install on the TV") { Task { await model.installTVApp(passphrase: passphrase) } }
                    .buttonStyle(.primary)
                    .disabled(passphrase.trimmingCharacters(in: .whitespaces).isEmpty)
            }
            ErrorText(error: error.map { Self.message(for: $0, brand: .lg) })
        }
    }

    /// `reason`: TV'nin verdiği sebep; varsa mesajın altına eklenir.
    static func message(for error: DevModeError, brand: TVBrand, reason: String?) -> String {
        let text = message(for: error, brand: brand, hasReason: reason != nil)
        guard let reason else { return text }
        return text + "\n\n" + String(localized: "TV says:") + " " + reason
    }

    static func message(for error: DevModeError, brand: TVBrand, hasReason: Bool = false) -> String {
        switch error {
        case .keyServerOff: String(localized: "Key Server is off. Turn it on in the Developer Mode app and keep the app on screen.")
        case .wrongPassphrase: String(localized: "The passphrase is not correct. Check it on the TV screen.")
        case .keyRefused: String(localized: "The TV refused the key. Open Key Server and type the passphrase again.")
        case .unreachable where brand == .samsung:
            String(localized: "The TV did not accept this computer. In the TV's Developer Mode window, check that Host PC IP is this computer's address, then restart the TV.")
        case .unreachable: String(localized: "The TV did not respond. Make sure it is on and on the same network.")
        // Eskiden her hatada boş alan suçlanıyordu; oysa çoğu zaman sorun başkaydı (ares-install aynı TV'ye kurabiliyordu).
        case .installFailed where hasReason:
            String(localized: "The TV app could not be installed. Try again; if it fails again, send us the reason below.")
        case .installFailed: String(localized: "The TV app could not be installed. Try again.")
        case .notEnoughSpace: String(localized: "The TV is out of free space. Remove an app you do not use from the TV and try again.")
        case .tooOld: String(localized: "This Samsung TV is too old for the GameTranslator TV app (2019 models and later are supported).")
        case .signInFailed: String(localized: "Samsung sign-in did not complete. Try again and finish signing in on the page that opens.")
        case .certificateFailed: String(localized: "Samsung did not issue the signing certificate. Try again in a few minutes.")
        case .notApproved: String(localized: "Debugging was not allowed on the TV. Try again and choose Allow on the TV.")
        }
    }
}
