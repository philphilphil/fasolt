import SwiftUI

struct DeckCardRow: View {
    let card: any CardDisplayable
    var deckNames: [String]? = nil

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(card.front)
                .font(.body)
                .lineLimit(2)

            HStack(spacing: 6) {
                if let sourceFile = card.sourceFile {
                    Label(sourceFile, systemImage: "doc.text")
                        .lineLimit(1)
                }

                if let deckNames, !deckNames.isEmpty {
                    Label(deckNames.joined(separator: ", "), systemImage: "rectangle.stack")
                        .lineLimit(1)
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
        }
        .padding(.vertical, 2)
    }
}
