// SPDX-License-Identifier: AGPL-3.0-only
// Geliştirme aracı: LG TV'yi bulur, eşleşir, GameTranslator TV uygulamasını başlatır ve öne getirmeyi dener.
// Kullanım: swift run tv-smoke <sunucu-adresi>   (ör. http://192.168.1.227:8787)

import GameTranslatorCore
import Foundation

setvbuf(stdout, nil, _IOLBF, 0)
let server = CommandLine.arguments.dropFirst().first ?? "http://127.0.0.1:8787"
let keyFile = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".gametranslator-tv-key")

print("TV aranıyor…")
let tvs = await LGDiscovery.search()
guard let tv = tvs.first else { print("LG TV bulunamadı."); exit(1) }
print("Bulundu: \(tv.name) (\(tv.host))")

let client = SSAPClient(tv: tv, clientKey: try? String(contentsOf: keyFile, encoding: .utf8))
print("Eşleşiliyor… (TV'de izin sorulursa onaylayın)")
let key = try await client.connect()
try key.write(to: keyFile, atomically: true, encoding: .utf8)
print("Eşleşildi.")

try await client.launch(appId: "com.gametranslator.tv", params: ["server": server])
print("TV uygulaması başlatıldı (\(server)). Önplandaki uygulama: \(try await client.foregroundAppId())")

let keeper = TVKeeper(connect: { client }, appId: "com.gametranslator.tv", params: ["server": server])
keeper.start()
print("Öne getirme açık. TV'yi kumandayla başka girişe geçirin; 5 sn içinde uygulama geri gelmeli. Çıkmak için Ctrl+C.")
while true { try await Task.sleep(for: .seconds(60)) }
