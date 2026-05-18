import SwiftUI

struct DeckCardRow: View {
    let card: any CardDisplayable
    var deckNames: [String]? = nil
    var primaryDeckId: String? = nil
    var showSourceFile = false

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(card.front.stripMarkdown())
                .font(.system(size: 15.5))
                .foregroundStyle(FasoltTheme.ink0)
                .lineLimit(2)

            HStack(spacing: 8) {
                if let primaryDeckId, let deckNames, let firstDeck = deckNames.first {
                    DeckTag(color: FasoltTheme.deckColor(for: primaryDeckId), size: 6)
                    Text(firstDeck)
                        .font(.system(size: 12.5))
                        .foregroundStyle(FasoltTheme.ink2)
                        .lineLimit(1)
                } else if showSourceFile, let sourceFile = card.sourceFile {
                    Image(systemName: "doc.text")
                        .font(.system(size: 10))
                        .foregroundStyle(FasoltTheme.ink2)
                    Text(sourceFile)
                        .font(.system(size: 12.5))
                        .foregroundStyle(FasoltTheme.ink2)
                        .lineLimit(1)
                }

                Text("·")
                    .font(.system(size: 12.5))
                    .foregroundStyle(FasoltTheme.ink3)

                if let dueText = formattedDueDate(card.dueAt) {
                    let isDue = isDueOrOverdue(card.dueAt)
                    Text(dueText)
                        .font(.system(size: 12.5, weight: isDue ? .semibold : .regular))
                        .monospacedDigit()
                        .foregroundStyle(isDue ? FasoltTheme.accent : FasoltTheme.ink2)
                } else {
                    Text("No due")
                        .font(.system(size: 12.5))
                        .foregroundStyle(FasoltTheme.ink2)
                }

                Spacer(minLength: 6)

                if card.isSuspended {
                    Text("Suspended")
                        .font(.system(size: 10, weight: .medium))
                        .padding(.horizontal, 8)
                        .padding(.vertical, 2)
                        .background(FasoltTheme.paper2, in: Capsule())
                        .foregroundStyle(FasoltTheme.ink2)
                }

                stateChip(card.state)
            }
            .lineLimit(1)
        }
        .padding(.vertical, 3)
        .opacity(card.isSuspended ? 0.55 : 1)
    }

    private func stateChip(_ state: String) -> some View {
        let color = stateColor(state)
        return Text(state)
            .font(.system(size: 11, weight: .medium))
            .padding(.horizontal, 8)
            .padding(.vertical, 2)
            .overlay(
                Capsule().strokeBorder(color, lineWidth: 1)
            )
            .foregroundStyle(color)
    }
}
