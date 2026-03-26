import SwiftUI

struct DeckCardRow: View {
    let card: any CardDisplayable
    var deckNames: [String]? = nil
    var showSourceFile = false

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(card.front)
                .font(.body)
                .lineLimit(2)

            HStack(spacing: 6) {
                if showSourceFile, let sourceFile = card.sourceFile {
                    HStack(spacing: 2) {
                        Image(systemName: "doc.text")
                        Text(sourceFile)
                    }
                }

                if let deckNames, !deckNames.isEmpty {
                    HStack(spacing: 2) {
                        Image(systemName: "rectangle.stack")
                        Text(deckNames.joined(separator: ", "))
                    }
                }

                Spacer()

                if let dueText = formattedDueDate(card.dueAt) {
                    Text(dueText)
                        .foregroundStyle(isDueOrOverdue(card.dueAt) ? .orange : .secondary)
                }

                Text(card.state)
                    .font(.caption2.weight(.medium))
                    .padding(.horizontal, 8)
                    .padding(.vertical, 2)
                    .background(stateColor(card.state).opacity(0.15), in: Capsule())
                    .foregroundStyle(stateColor(card.state))
            }
            .font(.caption2)
            .foregroundStyle(.secondary)
            .lineLimit(1)
        }
        .padding(.vertical, 2)
    }
}
