// SPDX-License-Identifier: AGPL-3.0-only

import SwiftUI

struct WelcomeStep: View {
    var body: some View {
        StepLayout(symbol: "captions.bubble.fill", title: "Welcome to GameTranslator",
                   message: "GameTranslator reads the English subtitles of your PS5 games and shows a Turkish translation on your TV, on top of the game. You keep playing on the TV with your own controller.") {
            Card(title: "You will need:") {
                Label("A PS5 on the same Wi-Fi network as this Mac", systemImage: "gamecontroller")
                Label("A compatible TV (LG, Samsung, or Android TV) with the GameTranslator TV app", systemImage: "tv")
                Label("A free second PlayStation Network account", systemImage: "person.2")
            }
        }
    }
}
