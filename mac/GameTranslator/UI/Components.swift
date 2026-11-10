// SPDX-License-Identifier: AGPL-3.0-only

import SwiftUI

// Ortak bileşenler. Ölçü ve renkler Theme'den (design/tokens.json) gelir; ekranlar sabit değer kullanmaz.
// Bileşenlerin tarifi docs/TASARIM.md'de; Windows sürümü aynı bileşenleri aynı adlarla kurar.

/// Uygulamanın logosu (kanepe + altyazı).
struct LogoView: View {
    var size: CGFloat = 40

    var body: some View {
        // Simge dosyası macOS ızgarasına göre kenar boşluğu içerir (1024'te 824'lük kare); uygulama içinde kırpılır.
        Image("Logo")
            .resizable()
            .interpolation(.high)
            .frame(width: size * 1024 / 824, height: size * 1024 / 824)
            .frame(width: size, height: size)
            .clipped()
            // Koyu temada logonun kenarı zeminden ayrılsın.
            .overlay(RoundedRectangle(cornerRadius: size * 186 / 824, style: .continuous).strokeBorder(Color.csBorder))
            .accessibilityHidden(true)
    }
}

/// Düğme türleri: birincil (sarı), ikincil (çerçeveli), tehlikeli (kırmızı).
struct CSButtonStyle: ButtonStyle {
    enum Kind { case primary, secondary, danger }
    var kind: Kind = .secondary
    var fullWidth = false
    /// Ana ekrandaki büyük düğme için "primaryActionHeight".
    var height = "buttonHeight"
    @Environment(\.isEnabled) private var isEnabled

    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.csButton)
            .foregroundStyle(foreground)
            .padding(.horizontal, Theme.space("xl"))
            .frame(maxWidth: fullWidth ? .infinity : nil, minHeight: Theme.size(height))
            .background(background, in: RoundedRectangle(cornerRadius: Theme.radius("control")))
            .overlay {
                if kind == .secondary {
                    RoundedRectangle(cornerRadius: Theme.radius("control")).strokeBorder(Color.csBorder)
                }
            }
            .opacity(isEnabled ? (configuration.isPressed ? 0.8 : 1) : 0.45)
            .contentShape(RoundedRectangle(cornerRadius: Theme.radius("control")))
    }

    private var foreground: Color {
        switch kind {
        case .primary: .csOnAccent
        case .secondary: .csText
        case .danger: .csOnDanger
        }
    }

    private var background: Color {
        switch kind {
        case .primary: .csAccent
        case .secondary: .csSurface
        case .danger: .csDanger
        }
    }
}

extension ButtonStyle where Self == CSButtonStyle {
    static var primary: CSButtonStyle { CSButtonStyle(kind: .primary) }
    static var secondary: CSButtonStyle { CSButtonStyle(kind: .secondary) }
    static var danger: CSButtonStyle { CSButtonStyle(kind: .danger) }
}

/// Kartlarla aynı dilde metin alanı: yüzey zemin, ince çerçeve, yuvarlak köşe.
struct CSTextFieldStyle: TextFieldStyle {
    var font: Font = .csBody
    var height: CGFloat = 40

    func _body(configuration: TextField<Self._Label>) -> some View {
        configuration
            .textFieldStyle(.plain)
            .font(font)
            .padding(.horizontal, Theme.space("m"))
            .frame(minHeight: height)
            .background(Color.csSurface, in: RoundedRectangle(cornerRadius: Theme.radius("control")))
            .overlay(RoundedRectangle(cornerRadius: Theme.radius("control")).strokeBorder(Color.csBorder))
    }
}

/// Başlıklı ya da başlıksız içerik kartı.
struct Card<Content: View>: View {
    var title: LocalizedStringKey?
    @ViewBuilder var content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: Theme.space("m")) {
            if let title {
                Text(title).font(.csHeadline)
            }
            content
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Theme.space("xl"))
        .background(Color.csSurface, in: RoundedRectangle(cornerRadius: Theme.radius("card")))
        .overlay(RoundedRectangle(cornerRadius: Theme.radius("card")).strokeBorder(Color.csBorder))
    }
}

/// Yumuşak sarı zeminde simge.
struct IconBadge: View {
    let symbol: String

    var body: some View {
        Image(systemName: symbol)
            .font(.system(size: Theme.size("iconBadge") * 0.46, weight: .semibold))
            .foregroundStyle(Color.csAccentText)
            .frame(width: Theme.size("iconBadge"), height: Theme.size("iconBadge"))
            .background(Color.csAccentSoft, in: RoundedRectangle(cornerRadius: Theme.radius("control")))
            .accessibilityHidden(true)
    }
}

/// Sihirbaz adımlarının ortak düzeni: simge, başlık, açıklama, içerik.
struct StepLayout<Content: View>: View {
    let symbol: String
    let title: LocalizedStringKey
    let message: LocalizedStringKey
    @ViewBuilder var content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: Theme.space("l")) {
            IconBadge(symbol: symbol)
            Text(title)
                .font(.csDisplay)
                .padding(.top, Theme.space("xs"))
            Text(message)
                .font(.csBody)
                .foregroundStyle(Color.csSecondary)
                .lineSpacing(3)
                .fixedSize(horizontal: false, vertical: true)
            content
                .padding(.top, Theme.space("s"))
            Spacer(minLength: 0)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

/// Numaralı talimat satırı.
struct NumberedRow: View {
    let number: Int
    let text: LocalizedStringKey

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: Theme.space("m")) {
            Text("\(number)")
                .font(.csCallout.monospacedDigit().weight(.semibold))
                .foregroundStyle(Color.csAccentText)
                .frame(width: 26, height: 26)
                .background(Circle().fill(Color.csAccentSoft))
            Text(text)
                .font(.csBody)
                .lineSpacing(3)
                .fixedSize(horizontal: false, vertical: true)
        }
    }
}

/// Durum noktasının rengi: bağlı (yeşil), sorun var (turuncu), boşta (gri).
enum StatusTone: Equatable {
    case idle, warning, good
}

/// Renkli durum noktası, adı ve değeri.
struct StatusRow: View {
    let title: LocalizedStringKey
    let value: String
    let tone: StatusTone

    var body: some View {
        HStack(spacing: Theme.space("m")) {
            Circle()
                .fill(dotColor)
                .frame(width: Theme.size("statusDot"), height: Theme.size("statusDot"))
            Text(title).font(.csBody.weight(.semibold))
            Spacer()
            Text(value)
                .font(.csBody)
                .foregroundStyle(tone == .good ? Color.csText : Color.csSecondary)
                .lineLimit(1)
        }
    }

    private var dotColor: Color {
        switch tone {
        case .good: .csSuccess
        case .warning: .csWarning
        case .idle: .csSecondary
        }
    }
}

/// Tamamlanan bir adımın onayı.
struct DoneLabel: View {
    let text: String

    var body: some View {
        Label(text, systemImage: "checkmark.circle.fill")
            .font(.csBody.weight(.semibold))
            .foregroundStyle(Color.csSuccess)
    }
}

/// İkincil açıklama metni.
struct HintText: View {
    let text: LocalizedStringKey

    var body: some View {
        Text(text)
            .font(.csCallout)
            .foregroundStyle(Color.csSecondary)
            .lineSpacing(2)
            .fixedSize(horizontal: false, vertical: true)
    }
}

/// Bir işlem başarısız olunca gösterilen hata metni.
struct ErrorText: View {
    let error: String?

    var body: some View {
        if let error {
            Label(error, systemImage: "exclamationmark.triangle.fill")
                .font(.csCallout)
                .foregroundStyle(Color.csWarning)
                .fixedSize(horizontal: false, vertical: true)
        }
    }
}

/// Uygulamanın tamamına zemin rengi, yazı rengi ve temel yazı boyutunu verir.
struct AppSurface: ViewModifier {
    func body(content: Content) -> some View {
        content
            .font(.csBody)
            .foregroundStyle(Color.csText)
            .tint(Color.csAccentText)
            .background(Color.csBackground)
    }
}

extension View {
    func appSurface() -> some View { modifier(AppSurface()) }
}

// MARK: - Modern Gaming UI Components

/// Çevirinin kaynağını gösteren rozet (Topluluk El Çevirisi, Apple Yerel, DeepL).
struct OriginBadge: View {
    let origin: String

    private var isCommunity: Bool {
        origin.localizedCaseInsensitiveContains("topluluk") || origin.localizedCaseInsensitiveContains("el çevirisi")
    }

    private var isDeepL: Bool {
        origin.localizedCaseInsensitiveContains("deepl")
    }

    var body: some View {
        HStack(spacing: 5) {
            Image(systemName: isCommunity ? "checkmark.seal.fill" : (isDeepL ? "network" : "bolt.fill"))
                .font(.system(size: 10, weight: .bold))
            Text(origin)
                .font(.system(size: 11, weight: .semibold))
        }
        .padding(.horizontal, 8)
        .padding(.vertical, 4)
        .background(
            isCommunity ? Color.purple.opacity(0.2) : (isDeepL ? Color.blue.opacity(0.2) : Color.green.opacity(0.18)),
            in: Capsule()
        )
        .overlay(
            Capsule()
                .strokeBorder(isCommunity ? Color.purple.opacity(0.5) : (isDeepL ? Color.blue.opacity(0.5) : Color.green.opacity(0.4)), lineWidth: 1)
        )
        .foregroundStyle(isCommunity ? Color.purple : (isDeepL ? Color.blue : Color.green))
    }
}

/// TV'de altyazının nasıl görüneceğini gerçek zamanlı gösteren mini TV önizleme ekranı.
struct MiniTVPreview: View {
    let text: String
    let fontSize: Int
    let bottomPercent: Int
    let backgroundOpacity: Int
    let isBold: Bool

    var body: some View {
        VStack(spacing: 6) {
            // TV Ekranı Çerçevesi (16:9 Oranında)
            ZStack(alignment: .bottom) {
                // TV Arka Planı (Oyun ekranını simüle eden koyu degrade)
                RoundedRectangle(cornerRadius: 10)
                    .fill(
                        LinearGradient(
                            colors: [Color(red: 0.08, green: 0.10, blue: 0.14), Color(red: 0.04, green: 0.05, blue: 0.08)],
                            startPoint: .topLeading,
                            endPoint: .bottomTrailing
                        )
                    )
                    .overlay(
                        RoundedRectangle(cornerRadius: 10)
                            .strokeBorder(Color.white.opacity(0.12), lineWidth: 1.5)
                    )

                // Simüle edilmiş oyun ortamı / HUD izleri
                VStack {
                    HStack {
                        Circle().fill(Color.red.opacity(0.4)).frame(width: 8, height: 8)
                        Circle().fill(Color.green.opacity(0.4)).frame(width: 8, height: 8)
                        Spacer()
                        Text("1080p • 60 FPS")
                            .font(.system(size: 9, weight: .medium, design: .monospaced))
                            .foregroundStyle(Color.white.opacity(0.35))
                    }
                    .padding(8)
                    Spacer()
                }

                // Gerçek Altyazı Önizlemesi (Seçilen parametrelerle)
                VStack {
                    Spacer()
                    Text(text.isEmpty ? "Örnek Türkçe Altyazı" : text)
                        .font(.system(size: max(11, CGFloat(fontSize) * 0.28), weight: isBold ? .bold : .medium))
                        .foregroundStyle(Color.white)
                        .padding(.horizontal, 12)
                        .padding(.vertical, 4)
                        .background(
                            Color.black.opacity(Double(backgroundOpacity) / 100.0),
                            in: RoundedRectangle(cornerRadius: 4)
                        )
                        .padding(.bottom, max(6, CGFloat(bottomPercent) * 1.5))
                        .animation(.easeInOut(duration: 0.2), value: bottomPercent)
                        .animation(.easeInOut(duration: 0.2), value: fontSize)
                        .animation(.easeInOut(duration: 0.2), value: backgroundOpacity)
                }
            }
            .frame(height: 160)
            .shadow(color: Color.black.opacity(0.4), radius: 8, y: 4)

            // TV Alt Standı & Bilgi
            HStack {
                Label("Canlı TV Önizleme", systemImage: "tv")
                    .font(.system(size: 11, weight: .medium))
                    .foregroundStyle(Color.csSecondary)
                Spacer()
                Text("TV Boyutu: \(fontSize)px • Konum: %\(bottomPercent)")
                    .font(.system(size: 11, design: .monospaced))
                    .foregroundStyle(Color.csSecondary)
            }
            .padding(.horizontal, 4)
        }
    }
}

/// Modern Sekmeli Gezinme Butonu.
struct ModernTabItem: View {
    let title: String
    let icon: String
    let isSelected: Bool
    var badge: Int? = nil
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: 7) {
                Image(systemName: icon)
                    .font(.system(size: 13, weight: isSelected ? .bold : .medium))
                Text(title)
                    .font(.system(size: 13, weight: isSelected ? .semibold : .regular))
                if let badge, badge > 0 {
                    Text("\(badge)")
                        .font(.system(size: 10, weight: .bold))
                        .padding(.horizontal, 6)
                        .padding(.vertical, 2)
                        .background(isSelected ? Color.white.opacity(0.25) : Color.blue.opacity(0.3), in: Capsule())
                }
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 9)
            .foregroundStyle(isSelected ? Color.white : Color.csSecondary)
            .background(
                isSelected ?
                    LinearGradient(colors: [Color.blue, Color(red: 0.15, green: 0.4, blue: 0.9)], startPoint: .top, endPoint: .bottom) :
                    LinearGradient(colors: [Color.clear, Color.clear], startPoint: .top, endPoint: .bottom),
                in: RoundedRectangle(cornerRadius: 9)
            )
            .overlay(
                RoundedRectangle(cornerRadius: 9)
                    .strokeBorder(isSelected ? Color.blue.opacity(0.6) : Color.clear, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }
}

