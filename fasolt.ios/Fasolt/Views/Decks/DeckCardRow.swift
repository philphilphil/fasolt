import SwiftUI

struct DeckCardRow: View {
    let card: DeckCardDTO
    var deckNames: [String]? = nil

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

                if let deckNames, !deckNames.isEmpty {
                    Label(deckNames.joined(separator: ", "), systemImage: "rectangle.stack")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }

                Spacer()

                if let dueText = formattedDueDate {
                    Text(dueText)
                        .font(.caption2)
                        .foregroundStyle(isDueOrOverdue ? .orange : .secondary)
                }

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

    private var parsedDueDate: Date? {
        guard let dueAt = card.dueAt else { return nil }
        return ISO8601DateFormatter().date(from: dueAt)
    }

    private var isDueOrOverdue: Bool {
        guard let date = parsedDueDate else { return false }
        return date <= Date.now
    }

    private var formattedDueDate: String? {
        guard let date = parsedDueDate else { return nil }
        let calendar = Calendar.current
        if calendar.isDateInToday(date) { return "Due today" }
        if calendar.isDateInTomorrow(date) { return "Due tomorrow" }
        if calendar.isDateInYesterday(date) { return "Overdue" }
        if date < Date.now { return "Overdue" }
        let formatter = DateFormatter()
        formatter.dateFormat = "MMM d"
        return "Due \(formatter.string(from: date))"
    }
}
