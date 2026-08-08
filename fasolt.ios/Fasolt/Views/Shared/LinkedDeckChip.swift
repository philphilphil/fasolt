import SwiftUI

/// Marks a deck the caller links rather than owns. Shows the author's handle when
/// the server knows one.
struct LinkedDeckChip: View {
    let authorHandle: String?

    var body: some View {
        HStack(spacing: 3) {
            Image(systemName: "link")
                .font(.system(size: 9, weight: .semibold))
            Text(authorHandle.map { "Linked · @\($0)" } ?? "Linked")
                .font(.system(size: 10, weight: .medium))
        }
        .padding(.horizontal, 6)
        .padding(.vertical, 2)
        .background(FasoltTheme.accentSoft, in: Capsule())
        .foregroundStyle(FasoltTheme.accentText)
        .lineLimit(1)
    }
}
