import SwiftUI

struct DeckCardRow: View {
    let card: any CardDisplayable
    var deckNames: [String]? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(card.front)
                .font(.body)
                .lineLimit(2)

            if card.sourceFile != nil || (deckNames != nil && !deckNames!.isEmpty) {
                HStack(spacing: 12) {
                    if let sourceFile = card.sourceFile {
                        Label(sourceFile, systemImage: "doc.text")
                    }

                    if let deckNames, !deckNames.isEmpty {
                        Label(deckNames.joined(separator: ", "), systemImage: "rectangle.stack")
                    }
                }
                .font(.caption)
                .foregroundStyle(.secondary)
            }

            HStack(spacing: 8) {
                if let dueText = formattedDueDate(card.dueAt) {
                    Text(dueText)
                        .foregroundStyle(isDueOrOverdue(card.dueAt) ? .orange : .secondary)
                }

                Text(card.state)
                    .padding(.horizontal, 8)
                    .padding(.vertical, 2)
                    .background(stateColor(card.state).opacity(0.15), in: Capsule())
                    .foregroundStyle(stateColor(card.state))
            }
            .font(.caption)
        }
        .padding(.vertical, 2)
    }
}
