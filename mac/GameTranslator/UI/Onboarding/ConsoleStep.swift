// SPDX-License-Identifier: AGPL-3.0-only

import RemotePlay
import SwiftUI

struct ConsoleStep: View {
    @Environment(AppModel.self) private var model
    @State private var consoles: [ConsoleInfo] = []
    @State private var selected: String?
    @State private var manualHost = ""
    @State private var pin = ""
    @State private var searching = false
    @State private var working = false
    @State private var error: String?

    private var host: String { selected ?? manualHost.trimmingCharacters(in: .whitespaces) }

    var body: some View {
        StepLayout(symbol: "gamecontroller.fill", title: "Connect your PS5",
                   message: "Turn on the PS5. As the second user, open Settings › System › Remote Play › Pair Device and enter the 8-digit code shown there.") {
            VStack(alignment: .leading, spacing: Theme.space("l")) {
                if let console = model.console {
                    DoneLabel(text: console.name)
                }
                Card {
                HStack {
                    Text(verbatim: "PS5").font(.csHeadline)
                    if searching { ProgressView().controlSize(.small) }
                    Spacer()
                    Button("Search again") { Task { await search() } }.buttonStyle(.secondary).disabled(searching)
                }
                if consoles.isEmpty && !searching {
                    HintText(text: "No PS5 found. Make sure it is on and on the same network, or enter its IP address.")
                    TextField("IP address, e.g. 192.168.1.20", text: $manualHost).textFieldStyle(CSTextFieldStyle())
                } else {
                    Picker("PS5", selection: $selected) {
                        ForEach(consoles, id: \.host) { Text(verbatim: "\($0.name) — \($0.host)").tag(Optional($0.host)) }
                    }
                    .labelsHidden()
                    .pickerStyle(.radioGroup)
                    .font(.csBody)
                }
                }
                VStack(alignment: .leading, spacing: Theme.space("s")) {
                    Text("8-digit code").font(.csCallout).foregroundStyle(Color.csSecondary)
                    TextField("", text: $pin)
                        .textFieldStyle(CSTextFieldStyle(font: .csCode, height: 52))
                        .frame(maxWidth: 240)
                }
                HStack(spacing: Theme.space("m")) {
                    Button("Connect") { Task { await register() } }
                        .buttonStyle(.primary)
                        .disabled(working || host.isEmpty || pin.filter(\.isNumber).count != 8)
                    if working { ProgressView().controlSize(.small) }
                }
                ErrorText(error: error)
            }
        }
        .task { await search() }
    }

    private func search() async {
        searching = true
        consoles = await model.discoverConsoles()
        if selected == nil { selected = consoles.first?.host }
        searching = false
    }

    private func register() async {
        working = true
        error = nil
        do { try await model.register(host: host, pin: pin) } catch { self.error = error.localizedDescription }
        working = false
    }
}
