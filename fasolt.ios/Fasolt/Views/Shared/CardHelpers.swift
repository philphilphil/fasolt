import SwiftUI

// MARK: - CardDisplayable Protocol

protocol CardDisplayable {
    var front: String { get }
    var back: String { get }
    var sourceFile: String? { get }
    var sourceHeading: String? { get }
    var state: String { get }
    var dueAt: String? { get }
    var stability: Double? { get }
    var difficulty: Double? { get }
    var step: Int? { get }
    var lastReviewedAt: String? { get }
    var frontSvg: String? { get }
    var backSvg: String? { get }
}

extension CardDTO: CardDisplayable {}
extension DeckCardDTO: CardDisplayable {}

// MARK: - Shared Date Formatters

enum DateFormatters {
    nonisolated(unsafe) static let iso8601: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter
    }()

    static let mediumDateTime: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .short
        return f
    }()

    static let shortDate: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "MMM d"
        return f
    }()
}

// MARK: - Card Helper Functions

func stateColor(_ state: String) -> Color {
    switch state {
    case "new": return .green
    case "review": return .blue
    case "learning": return .orange
    case "relearning": return .red
    default: return .secondary
    }
}

func parseISODate(_ isoString: String?) -> Date? {
    guard let str = isoString else { return nil }
    return DateFormatters.iso8601.date(from: str)
}

func formatISODate(_ isoString: String?) -> String? {
    guard let date = parseISODate(isoString) else { return nil }
    return DateFormatters.mediumDateTime.string(from: date)
}

func formattedDueDate(_ isoString: String?) -> String? {
    guard let date = parseISODate(isoString) else { return nil }
    let calendar = Calendar.current
    if calendar.isDateInToday(date) { return "Due today" }
    if calendar.isDateInTomorrow(date) { return "Due tomorrow" }
    if calendar.isDateInYesterday(date) { return "Overdue" }
    if date < Date.now { return "Overdue" }
    return "Due \(DateFormatters.shortDate.string(from: date))"
}

func isDueOrOverdue(_ isoString: String?) -> Bool {
    guard let date = parseISODate(isoString) else { return false }
    return date <= Date.now
}

// MARK: - Shared FSRS Grid Item

struct FSRSItem: View {
    let label: String
    let value: String

    var body: some View {
        VStack(spacing: 2) {
            Text(label)
                .font(.caption2)
                .foregroundStyle(.secondary)
            Text(value)
                .font(.subheadline.weight(.medium))
        }
        .frame(maxWidth: .infinity)
    }
}
