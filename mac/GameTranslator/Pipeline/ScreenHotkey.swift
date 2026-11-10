// SPDX-License-Identifier: AGPL-3.0-only

import Carbon.HIToolbox
import Foundation

/// "Ekranı çevir" genel kısayolu (⌥⌘T): kullanıcı kumandayla oynarken Mac'teki pencere arka planda olsa da çalışır.
/// Carbon kısayolu erişilebilirlik izni istemez. Kısayol yalnız `enabled` iken tutulur; böylece çeviri kapalıyken
/// ⌥⌘T diğer uygulamalarda olağan işini görür. Başka bir uygulama kısayolu tutuyorsa sessizce atlanır.
@MainActor
final class ScreenHotkey {
    private let pressed: () -> Void
    private var hotKey: EventHotKeyRef?
    private var handler: EventHandlerRef?

    init(pressed: @escaping () -> Void) {
        self.pressed = pressed
    }

    var enabled = false {
        didSet {
            guard enabled != oldValue else { return }
            if enabled { register() } else { unregister() }
        }
    }

    private func register() {
        var spec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        let me = Unmanaged.passUnretained(self).toOpaque()
        InstallEventHandler(GetApplicationEventTarget(), { _, _, userData in
            guard let userData else { return OSStatus(eventNotHandledErr) }
            let hotkey = Unmanaged<ScreenHotkey>.fromOpaque(userData).takeUnretainedValue()
            DispatchQueue.main.async { MainActor.assumeIsolated { hotkey.pressed() } }
            return noErr
        }, 1, &spec, me, &handler)
        let id = EventHotKeyID(signature: OSType(0x4353_5354), id: 1) // "CSST"
        RegisterEventHotKey(UInt32(kVK_ANSI_T), UInt32(optionKey | cmdKey), id, GetApplicationEventTarget(), 0, &hotKey)
    }

    private func unregister() {
        if let hotKey { UnregisterEventHotKey(hotKey) }
        hotKey = nil
        if let handler { RemoveEventHandler(handler) }
        handler = nil
    }
}
