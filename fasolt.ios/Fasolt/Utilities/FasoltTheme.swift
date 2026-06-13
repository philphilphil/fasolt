import SwiftUI
import UIKit

// MARK: - Design tokens
//
// "Quiet & Functional" palette, anchored on Things — a warm marigold accent on
// white / warm-charcoal paper. System font (SF) throughout, whitespace over
// borders, no gloss. Mirrors the web design language; see
// docs/superpowers/specs/2026-06-13-things-design-language.md.
// Light/dark adaptive via dynamic UIColors so SwiftUI picks the right value.

enum FasoltTheme {

    // MARK: Surfaces

    /// Page background — white in light, warm charcoal in dark.
    static let paper0 = Color(
        light: UIColor.white,
        dark:  UIColor(red: 0.106, green: 0.106, blue: 0.110, alpha: 1.0)
    )

    /// Card / inset surface.
    static let paper1 = Color(
        light: UIColor.white,
        dark:  UIColor(red: 0.141, green: 0.141, blue: 0.153, alpha: 1.0)
    )

    /// Sunken / striped surface / hover fill.
    static let paper2 = Color(
        light: UIColor(red: 0.957, green: 0.957, blue: 0.953, alpha: 1.0),
        dark:  UIColor(red: 0.184, green: 0.184, blue: 0.200, alpha: 1.0)
    )

    /// Hairline rule (0.5pt borders).
    static let rule1 = Color(
        light: UIColor(red: 0.925, green: 0.925, blue: 0.925, alpha: 1.0),
        dark:  UIColor(red: 0.204, green: 0.204, blue: 0.227, alpha: 1.0)
    )

    /// Fainter inset separator.
    static let rule2 = Color(
        light: UIColor(red: 0.949, green: 0.949, blue: 0.949, alpha: 1.0),
        dark:  UIColor(red: 0.173, green: 0.173, blue: 0.188, alpha: 1.0)
    )

    // MARK: Ink (text)

    static let ink0 = Color(
        light: UIColor(red: 0.110, green: 0.110, blue: 0.118, alpha: 1.0),
        dark:  UIColor(red: 0.929, green: 0.929, blue: 0.941, alpha: 1.0)
    )

    static let ink1 = Color(
        light: UIColor(red: 0.420, green: 0.420, blue: 0.439, alpha: 1.0),
        dark:  UIColor(red: 0.659, green: 0.659, blue: 0.686, alpha: 1.0)
    )

    static let ink2 = Color(
        light: UIColor(red: 0.557, green: 0.557, blue: 0.576, alpha: 1.0),
        dark:  UIColor(red: 0.541, green: 0.541, blue: 0.569, alpha: 1.0)
    )

    static let ink3 = Color(
        light: UIColor(red: 0.761, green: 0.761, blue: 0.780, alpha: 1.0),
        dark:  UIColor(red: 0.361, green: 0.361, blue: 0.388, alpha: 1.0)
    )

    // MARK: Accent (warm marigold)

    /// Primary accent — marigold. Used only for action / active / today / progress.
    static let accent = Color(
        light: UIColor(red: 0.933, green: 0.608, blue: 0.259, alpha: 1.0),
        dark:  UIColor(red: 0.949, green: 0.639, blue: 0.322, alpha: 1.0)
    )

    /// Pressed / stronger variant.
    static let accentHi = Color(
        light: UIColor(red: 0.878, green: 0.561, blue: 0.192, alpha: 1.0),
        dark:  UIColor(red: 0.957, green: 0.678, blue: 0.388, alpha: 1.0)
    )

    /// Accent as text/links on light surfaces — deeper, for legibility (AA).
    static let accentText = Color(
        light: UIColor(red: 0.788, green: 0.471, blue: 0.102, alpha: 1.0),
        dark:  UIColor(red: 0.949, green: 0.639, blue: 0.322, alpha: 1.0)
    )

    /// Soft tint for chips & subtle backgrounds.
    static let accentSoft = Color(
        light: UIColor(red: 0.984, green: 0.933, blue: 0.871, alpha: 1.0),
        dark:  UIColor(red: 0.227, green: 0.173, blue: 0.102, alpha: 1.0)
    )

    /// Foreground when on accent surfaces.
    static let accentOn = Color.white

    // MARK: Status colors (rating buttons / state chips) — Apple system hues

    static let again = Color(
        light: UIColor(red: 1.000, green: 0.231, blue: 0.188, alpha: 1.0),
        dark:  UIColor(red: 1.000, green: 0.271, blue: 0.227, alpha: 1.0)
    )

    static let hard = Color(
        light: UIColor(red: 0.961, green: 0.706, blue: 0.000, alpha: 1.0),
        dark:  UIColor(red: 1.000, green: 0.800, blue: 0.200, alpha: 1.0)
    )

    static let good = Color(
        light: UIColor(red: 0.204, green: 0.780, blue: 0.349, alpha: 1.0),
        dark:  UIColor(red: 0.188, green: 0.820, blue: 0.345, alpha: 1.0)
    )

    static let easy = Color(
        light: UIColor(red: 0.180, green: 0.486, blue: 0.965, alpha: 1.0),
        dark:  UIColor(red: 0.290, green: 0.608, blue: 1.000, alpha: 1.0)
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
                    .strokeBorder(FasoltTheme.rule1, lineWidth: FasoltTheme.hairline)
            )
    }

    /// Quiet section label — sentence case, calm, muted. (Replaces the old
    /// uppercase wide-tracked label, the main "AI tell" we're removing.)
    func sectionLabel() -> some View {
        self
            .font(.system(size: 13, weight: .semibold))
            .foregroundStyle(FasoltTheme.ink2)
    }
}

// MARK: - Small components

/// Small round dot used as a deck tag.
struct DeckTag: View {
    let color: Color
    var size: CGFloat = 8
    var body: some View {
        Circle()
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

/// Marigold underline used on hero cards.
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

/// Small quiet caption used throughout the design — sentence case, no gloss.
struct CapsLabel: View {
    let text: String
    var color: Color = FasoltTheme.ink2
    var size: CGFloat = 12
    var body: some View {
        Text(text)
            .font(.system(size: size, weight: .semibold))
            .foregroundStyle(color)
    }
}

/// Primary accent button used as a hero CTA — flat marigold, no gloss.
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
            .opacity(configuration.isPressed ? 0.85 : 1.0)
            .animation(.easeOut(duration: 0.12), value: configuration.isPressed)
    }
}

/// Soft accent button (tinted background, accent text).
struct SoftAccentButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 14, weight: .semibold))
            .foregroundStyle(FasoltTheme.accentText)
            .padding(.horizontal, 16)
            .padding(.vertical, 8)
            .background(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .fill(FasoltTheme.accentSoft)
            )
            .opacity(configuration.isPressed ? 0.85 : 1.0)
    }
}
