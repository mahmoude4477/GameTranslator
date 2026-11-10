// SPDX-License-Identifier: AGPL-3.0-only

import GameTranslatorCore
import SwiftUI

struct TVStep: View {
    @Environment(AppModel.self) private var model
    @State private var tvs: [LGTV] = []
    @State private var searching = false
    @State private var working = false
    @State private var error: String?

    var body: some View {
        StepLayout(symbol: "tv.fill", title: "Connect your TV",
                   message: "Turn on the TV. When you press Connect, the TV asks for permission once — accept it with the remote. If the GameTranslator TV app is missing, this screen helps you install it.") {
            VStack(alignment: .leading, spacing: Theme.space("l")) {
                if let tv = model.tv {
                    DoneLabel(text: tv.name)
                }
                Card {
                    HStack {
                        Text(verbatim: "TV").font(.csHeadline)
                        if searching { ProgressView().controlSize(.small) }
                        Spacer()
                        Button("Search again") { Task { await search() } }.buttonStyle(.secondary).disabled(searching)
                    }
                    if tvs.isEmpty && !searching {
                        HintText(text: "No LG or Samsung TV found. Make sure the TV is on and on the same network.")
                    }
                    ForEach(tvs, id: \.host) { tv in
                        HStack {
                            Text(verbatim: "\(tv.name) — \(tv.host)")
                            Spacer()
                            Button("Connect") { Task { await pair(tv) } }.buttonStyle(.primary).disabled(working)
                        }
                    }
                }
                Card {
                    HStack {
                        Text(verbatim: "Android TV / Google TV")
                        Spacer()
                        Button("Select") { Task { await pair(LGTV(name: "Android TV", host: "", brand: .androidTV)) } }
                            .buttonStyle(.secondary).disabled(working)
                    }
                    HintText(text: "Vestel, Arçelik, Beko, Grundig, Philips and other TVs running Android TV.")
                }
                if working {
                    HStack(spacing: Theme.space("s")) {
                        ProgressView().controlSize(.small)
                        Text("Accept the request on the TV…").foregroundStyle(Color.csSecondary)
                    }
                }
                ErrorText(error: error)
                TVAppCard()
                // Uygulamayla TV paketi gelmediyse (geliştirici derlemesi) rehber gösterilir.
                if let tv = model.tv, tv.brand != .lg, !model.canInstallTVApp {
                    Card(title: "GameTranslator TV app") {
                        Text("Install the GameTranslator TV app on this TV using the guide and open it. It finds this computer by itself.")
                        Link("Open the guide", destination: AppIdentity.tvGuideURL)
                    }
                }
            }
        }
        .task { await search() }
    }

    private func search() async {
        searching = true
        tvs = await model.discoverTVs()
        searching = false
    }

    private func pair(_ tv: LGTV) async {
        working = true
        error = nil
        do { try await model.pair(tv) } catch { self.error = String(localized: "The TV did not respond. Make sure it is on and on the same network.") }
        working = false
    }
}
