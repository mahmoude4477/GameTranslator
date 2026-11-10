// SPDX-License-Identifier: AGPL-3.0-only

import RemotePlay
import SwiftUI
import WebKit

struct PSNLoginStep: View {
    @Environment(AppModel.self) private var model
    @State private var showLogin = false
    @State private var working = false
    @State private var error: String?

    var body: some View {
        StepLayout(symbol: "person.crop.circle.badge.checkmark", title: "Sign in with the second account",
                   message: "Sign in to PlayStation Network with the second account you just created. Your password goes directly to Sony; GameTranslator only keeps the account ID.") {
            VStack(alignment: .leading, spacing: Theme.space("m")) {
                if model.accountId != nil {
                    DoneLabel(text: String(localized: "Signed in"))
                }
                Button(model.accountId == nil ? "Sign in" : "Sign in again") { showLogin = true }
                    .buttonStyle(CSButtonStyle(kind: model.accountId == nil ? .primary : .secondary))
                    .disabled(working)
                if working { ProgressView().controlSize(.small) }
                ErrorText(error: error)
            }
        }
        .sheet(isPresented: $showLogin) {
            PSNLoginSheet { url in
                showLogin = false
                working = true
                error = nil
                Task {
                    do { try await model.completePSNLogin(redirect: url) } catch { self.error = error.localizedDescription }
                    working = false
                }
            }
        }
    }
}

/// Sony'nin giriş sayfasını açar; giriş bitince yönlendirme adresini yakalar.
struct PSNLoginSheet: View {
    let onRedirect: (URL) -> Void
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text(verbatim: "PlayStation Network").font(.csHeadline)
                Spacer()
                Button("Cancel") { dismiss() }.buttonStyle(.secondary)
            }
            .padding(Theme.space("m"))
            Divider()
            LoginWebView(onRedirect: onRedirect)
        }
        .frame(width: 520, height: 680)
    }
}

struct LoginWebView: NSViewRepresentable {
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
            if let url = action.request.url, PSNAccount.code(fromRedirect: url) != nil {
                onRedirect(url)
                return .cancel
            }
            return .allow
        }
    }
}
