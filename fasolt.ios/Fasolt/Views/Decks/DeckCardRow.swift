import SwiftUI

struct DeckCardRow: View {
    let card: DeckCardDTO

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(card.front)
                .font(.body)
                .lineLimit(2)

            HStack(spacing: 8) {
                if let sourceFile = card.sourceFile {
                    Label(sourceFile, systemImage: "doc.text")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }

                Spacer()

                Text(card.state)
                    .font(.caption2.weight(.medium))
                    .padding(.horizontal, 8)
                    .padding(.vertical, 2)
                    .background(stateColor.opacity(0.15), in: Capsule())
                    .foregroundStyle(stateColor)
            }
        }
        .padding(.vertical, 4)
    }

    private var stateColor: Color {
        switch card.state {
        case "new": return .green
        case "review": return .blue
        case "learning": return .orange
        case "relearning": return .red
        default: return .secondary
        }
    }
}
