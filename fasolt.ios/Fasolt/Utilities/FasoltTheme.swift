import SwiftUI
import UIKit

// MARK: - Design tokens
//
// Paper-and-ink palette with a vermilion accent. Light/dark adaptive via
// dynamic UIColors so SwiftUI picks the right value automatically.

enum FasoltTheme {

    // MARK: Surfaces

    /// Page background — warm off-white in light, ink in dark.
    static let paper0 = Color(
        light: UIColor(red: 0.961, green: 0.953, blue: 0.933, alpha: 1.0),
        dark:  UIColor(red: 0.085, green: 0.082, blue: 0.078, alpha: 1.0)
    )

    /// Card / inset surface.
    static let paper1 = Color(
        light: UIColor.white,
        dark:  UIColor(red: 0.117, green: 0.114, blue: 0.106, alpha: 1.0)
    )

    /// Sunken / striped surface.
    static let paper2 = Color(
        light: UIColor(red: 0.925, green: 0.918, blue: 0.898, alpha: 1.0),
        dark:  UIColor(red: 0.152, green: 0.149, blue: 0.137, alpha: 1.0)
    )

    /// Hairline rule (0.5pt borders).
    static let rule1 = Color(
        light: UIColor(red: 0.235, green: 0.235, blue: 0.263, alpha: 0.18),
        dark:  UIColor(white: 1.0, alpha: 0.12)
    )

    /// Fainter inset separator.
    static let rule2 = Color(
        light: UIColor(red: 0.235, green: 0.235, blue: 0.263, alpha: 0.12),
        dark:  UIColor(white: 1.0, alpha: 0.07)
    )

    // MARK: Ink (text)

    static let ink0 = Color(
        light: UIColor(red: 0.110, green: 0.110, blue: 0.118, alpha: 1.0),
        dark:  UIColor(white: 0.96, alpha: 1.0)
    )

    static let ink1 = Color(
        light: UIColor(red: 0.235, green: 0.235, blue: 0.263, alpha: 0.78),
        dark:  UIColor(white: 0.78, alpha: 1.0)
    )

    static let ink2 = Color(
        light: UIColor(red: 0.235, green: 0.235, blue: 0.263, alpha: 0.58),
        dark:  UIColor(white: 0.62, alpha: 1.0)
    )

    static let ink3 = Color(
        light: UIColor(red: 0.235, green: 0.235, blue: 0.263, alpha: 0.36),
        dark:  UIColor(white: 0.42, alpha: 1.0)
    )

    // MARK: Accent (vermilion)

    /// Primary accent — vermilion.
    static let accent = Color(
        light: UIColor(red: 0.815, green: 0.330, blue: 0.180, alpha: 1.0),
        dark:  UIColor(red: 0.952, green: 0.462, blue: 0.282, alpha: 1.0)
    )

    /// Hover / pressed variant.
    static let accentHi = Color(
        light: UIColor(red: 0.733, green: 0.290, blue: 0.155, alpha: 1.0),
        dark:  UIColor(red: 1.000, green: 0.560, blue: 0.380, alpha: 1.0)
    )

    /// Soft tint for chips & subtle backgrounds.
    static let accentSoft = Color(
        light: UIColor(red: 0.980, green: 0.918, blue: 0.882, alpha: 1.0),
        dark:  UIColor(red: 0.290, green: 0.150, blue: 0.092, alpha: 1.0)
    )

    /// Foreground when on accent surfaces.
    static let accentOn = Color.white

    // MARK: Status colors (rating buttons / state chips)

    static let again = Color(
        light: UIColor(red: 0.812, green: 0.300, blue: 0.235, alpha: 1.0),
        dark:  UIColor(red: 0.952, green: 0.420, blue: 0.330, alpha: 1.0)
    )

    static let hard = Color(
        light: UIColor(red: 0.795, green: 0.502, blue: 0.110, alpha: 1.0),
        dark:  UIColor(red: 0.972, green: 0.685, blue: 0.220, alpha: 1.0)
    )

    static let good = Color(
        light: UIColor(red: 0.227, green: 0.560, blue: 0.345, alpha: 1.0),
        dark:  UIColor(red: 0.388, green: 0.760, blue: 0.500, alpha: 1.0)
    )

    static let easy = Color(
        light: UIColor(red: 0.220, green: 0.476, blue: 0.770, alpha: 1.0),
        dark:  UIColor(red: 0.400, green: 0.660, blue: 0.950, alpha: 1.0)
    )

    // MARK: Spacing & radius

    static let pagePadding: CGFloat = 16
    static let groupRadius: CGFloat = 16
    static let cardRadius: CGFloat = 20
    static let pillRadius: CGFloat = 999
    static let hairline: CGFloat = 0.5

    // MARK: Deck color palette (deterministic per id)
    //
    // Returns a vivid but tasteful color for a given deck id, so the UI can show
    // a per-deck tag without requiring the server to persist a color.

    static let deckPalette: [Color] = [
        Color(red: 0.875, green: 0.470, blue: 0.250),   // 35 (warm)
        Color(red: 0.540, green: 0.410, blue: 0.870),   // 270 (violet)
        Color(red: 0.220, green: 0.640, blue: 0.610),   // 175 (teal)
        Color(red: 0.930, green: 0.360, blue: 0.330),   // 25 (red-orange)
        Color(red: 0.310, green: 0.490, blue: 0.880),   // 245 (blue)
        Color(red: 0.330, green: 0.620, blue: 0.450),   // 155 (green)
        Color(red: 0.610, green: 0.500, blue: 0.300),   // 80 (gold)
        Color(red: 0.760, green: 0.420, blue: 0.620),   // 320 (rose)
    ]

    static func deckColor(for id: String) -> Color {
        var hash: UInt = 5381
        for byte in id.utf8 {
            hash = ((hash << 5) &+ hash) &+ UInt(byte)
        }
        return deckPalette[Int(hash % UInt(deckPalette.count))]
    }

    static func deckInitials(_ name: String) -> String {
        let words = name.split(separator: " ", omittingEmptySubsequences: true)
        let first = words.prefix(2).compactMap { $0.first }
        if first.isEmpty {
            return String(name.prefix(2)).uppercased()
        }
        return String(first).uppercased()
    }
}

// MARK: - Color (light/dark dynamic)

extension Color {
    init(light: UIColor, dark: UIColor) {
        self.init(uiColor: UIColor { trait in
            trait.userInterfaceStyle == .dark ? dark : light
        })
    }
}

// MARK: - Reusable styling primitives

extension View {
    /// Inset paper surface (matches the design's white card sections).
    func paperCard(
        radius: CGFloat = FasoltTheme.groupRadius,
        padding: EdgeInsets = EdgeInsets(top: 0, leading: 0, bottom: 0, trailing: 0)
    ) -> some View {
        self
            .padding(padding)
            .background(FasoltTheme.paper1)
            .clipShape(RoundedRectangle(cornerRadius: radius, style: .continuous))
            .overlay(
                RoundedRectangle(cornerRadius: radius, style: .continuous)
                    .strokeBorder(FasoltTheme.rule2, lineWidth: FasoltTheme.hairline)
            )
    }

    /// Section header style — small all-caps mono-ish label.
    func sectionLabel() -> some View {
        self
            .font(.system(size: 12, weight: .medium, design: .default))
            .textCase(.uppercase)
            .tracking(0.6)
            .foregroundStyle(FasoltTheme.ink2)
    }
}

// MARK: - Small components

/// Tiny coloured square used as a deck tag.
struct DeckTag: View {
    let color: Color
    var size: CGFloat = 8
    var body: some View {
        RoundedRectangle(cornerRadius: 2, style: .continuous)
            .fill(color)
            .frame(width: size, height: size)
    }
}

/// Rounded coloured icon square (Linear/Things-style settings icon).
struct ColorIconBadge: View {
    let systemName: String
    let tint: Color
    var size: CGFloat = 28
    var body: some View {
        ZStack {
            RoundedRectangle(cornerRadius: 7, style: .continuous)
                .fill(tint)
            Image(systemName: systemName)
                .font(.system(size: size * 0.5, weight: .semibold))
                .foregroundStyle(.white)
        }
        .frame(width: size, height: size)
    }
}

/// Block initials avatar for a deck.
struct DeckInitialsBadge: View {
    let name: String
    let color: Color
    var size: CGFloat = 36
    var body: some View {
        ZStack {
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .fill(color)
            Text(FasoltTheme.deckInitials(name))
                .font(.system(size: size * 0.36, weight: .semibold))
                .foregroundStyle(.white)
        }
        .frame(width: size, height: size)
    }
}

/// Vermilion underline used on hero cards.
struct AccentStripe: View {
    var horizontalInset: CGFloat = 24
    var body: some View {
        GeometryReader { geo in
            Rectangle()
                .fill(FasoltTheme.accent)
                .frame(width: max(0, geo.size.width - horizontalInset * 2), height: 3)
                .clipShape(RoundedRectangle(cornerRadius: 1.5, style: .continuous))
                .position(x: geo.size.width / 2, y: 1.5)
        }
        .frame(height: 3)
    }
}

/// Small uppercase mono caption used throughout the design.
struct CapsLabel: View {
    let text: String
    var color: Color = FasoltTheme.ink2
    var size: CGFloat = 11
    var body: some View {
        Text(text)
            .font(.system(size: size, weight: .medium))
            .tracking(0.6)
            .textCase(.uppercase)
            .foregroundStyle(color)
    }
}

/// Primary accent button used as a hero CTA.
struct AccentButtonStyle: ButtonStyle {
    var height: CGFloat = 50
    var radius: CGFloat = 14
    var fontSize: CGFloat = 17
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: fontSize, weight: .semibold))
            .foregroundStyle(.white)
            .frame(maxWidth: .infinity)
            .frame(height: height)
            .background(
                RoundedRectangle(cornerRadius: radius, style: .continuous)
                    .fill(FasoltTheme.accent)
            )
            .overlay(
                RoundedRectangle(cornerRadius: radius, style: .continuous)
                    .stroke(.white.opacity(0.12), lineWidth: 1)
                    .blendMode(.overlay)
            )
            .opacity(configuration.isPressed ? 0.85 : 1.0)
            .scaleEffect(configuration.isPressed ? 0.985 : 1.0)
            .animation(.easeOut(duration: 0.12), value: configuration.isPressed)
    }
}

/// Soft accent button (tinted background, accent text).
struct SoftAccentButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 14, weight: .semibold))
            .foregroundStyle(FasoltTheme.accentHi)
            .padding(.horizontal, 16)
            .padding(.vertical, 8)
            .background(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .fill(FasoltTheme.accentSoft)
            )
            .opacity(configuration.isPressed ? 0.85 : 1.0)
    }
}
