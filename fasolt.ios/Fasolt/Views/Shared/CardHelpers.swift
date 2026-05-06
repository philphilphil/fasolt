import SwiftUI

// MARK: - CardDisplayable Protocol

protocol CardDisplayable {
    var id: String { get }
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
    var isSuspended: Bool { get }
    var frontSvg: String? { get }
    var backSvg: String? { get }
}

extension CardDTO: CardDisplayable {}
extension DeckCardDTO: CardDisplayable {}

// MARK: - Shared Date Formatters

enum DateFormatters {
    private static let queue = DispatchQueue(label: "com.fasolt.dateFormatting")

    // These formatters are NOT thread-safe, but access is serialized through `queue`.
    // nonisolated(unsafe) is required for Swift 6 strict concurrency; the DispatchQueue
    // provides the actual synchronization guarantee.
    nonisolated(unsafe) private static let _iso8601: ISO8601DateFormatter = {
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        return formatter
    }()

    private static let _mediumDateTime: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .medium
        f.timeStyle = .short
        return f
    }()

    private static let _shortDate: DateFormatter = {
        let f = DateFormatter()
        f.dateFormat = "MMM d"
        return f
    }()

    // Thread-safe ISO8601 access
    static func parseISO8601(_ string: String) -> Date? {
        queue.sync { _iso8601.date(from: string) }
    }

    static func formatISO8601(_ date: Date) -> String {
        queue.sync { _iso8601.string(from: date) }
    }

    static func formatMediumDateTime(_ date: Date) -> String {
        queue.sync { _mediumDateTime.string(from: date) }
    }

    static func formatShortDate(_ date: Date) -> String {
        queue.sync { _shortDate.string(from: date) }
    }
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
    return DateFormatters.parseISO8601(str)
}

func formatISODate(_ isoString: String?) -> String? {
    guard let date = parseISODate(isoString) else { return nil }
    return DateFormatters.formatMediumDateTime(date)
}

func formattedDueDate(_ isoString: String?) -> String? {
    guard let date = parseISODate(isoString) else { return nil }
    let calendar = Calendar.current
    if calendar.isDateInToday(date) { return "Due today" }
    if calendar.isDateInTomorrow(date) { return "Due tomorrow" }
    if calendar.isDateInYesterday(date) { return "Overdue" }
    if date < Date.now { return "Overdue" }
    return "Due \(DateFormatters.formatShortDate(date))"
}

func isDueOrOverdue(_ isoString: String?) -> Bool {
    guard let date = parseISODate(isoString) else { return false }
    return date <= Date.now
}

// MARK: - Card Sort Order

enum CardSortOrder: String, CaseIterable {
    case dueDate = "Due Date"
    case state = "State"
    case front = "Front"
    case sourceFile = "Source"
}

// MARK: - Shared Sort Function

func sortedCards<T: CardDisplayable & Identifiable>(_ cards: [T], by sortOrder: CardSortOrder) -> [T] {
    cards.sorted { a, b in
        switch sortOrder {
        case .dueDate:
            let aDate = a.dueAt ?? ""
            let bDate = b.dueAt ?? ""
            if aDate.isEmpty && bDate.isEmpty { return a.front < b.front }
            if aDate.isEmpty { return false }
            if bDate.isEmpty { return true }
            return aDate < bDate
        case .state:
            let order = ["new": 0, "learning": 1, "relearning": 2, "review": 3]
            return (order[a.state] ?? 99) < (order[b.state] ?? 99)
        case .front:
            return a.front.localizedCaseInsensitiveCompare(b.front) == .orderedAscending
        case .sourceFile:
            return (a.sourceFile ?? "").localizedCaseInsensitiveCompare(b.sourceFile ?? "") == .orderedAscending
        }
    }
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
