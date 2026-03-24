import SwiftUI

struct CardListView: View {
    @State private var viewModel: CardListViewModel
    @State private var selectedCard: CardDTO?

    init(viewModel: CardListViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        NavigationStack {
            Group {
                if viewModel.cards.isEmpty && !viewModel.isLoading && viewModel.errorMessage == nil {
                    ContentUnavailableView(
                        "No cards yet",
                        systemImage: "rectangle.on.rectangle",
                        description: Text("Create cards via the API or MCP tools")
                    )
                } else if let error = viewModel.errorMessage, viewModel.cards.isEmpty {
                    ContentUnavailableView {
                        Label("Could not load", systemImage: "wifi.slash")
                    } description: {
                        Text(error)
                    } actions: {
                        Button("Retry") {
                            Task { await viewModel.loadCards() }
                        }
                    }
                } else {
                    List {
                        ForEach(viewModel.filteredCards) { card in
                            Button {
                                selectedCard = card
                            } label: {
                                cardRow(card)
                            }
                            .tint(.primary)
                        }

                        if viewModel.hasMore && viewModel.searchText.isEmpty {
                            ProgressView()
                                .frame(maxWidth: .infinity)
                                .task {
                                    await viewModel.loadMore()
                                }
                        }
                    }
                }
            }
            .searchable(text: $viewModel.searchText, prompt: "Search cards")
            .refreshable {
                await viewModel.loadCards()
            }
            .navigationTitle("Cards")
            .overlay {
                if viewModel.isLoading && viewModel.cards.isEmpty {
                    ProgressView()
                }
            }
            .offlineBanner()
            .task {
                if viewModel.cards.isEmpty {
                    await viewModel.loadCards()
                }
            }
            .sheet(item: $selectedCard) { card in
                cardDetailSheet(card)
            }
        }
    }

    private func cardRow(_ card: CardDTO) -> some View {
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

                if !card.decks.isEmpty {
                    Label(card.decks.map(\.name).joined(separator: ", "), systemImage: "rectangle.stack")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }

                Spacer()

                if let dueText = formattedDueDate(card.dueAt) {
                    Text(dueText)
                        .font(.caption2)
                        .foregroundStyle(isDueOrOverdue(card.dueAt) ? .orange : .secondary)
                }

                Text(card.state)
                    .font(.caption2.weight(.medium))
                    .padding(.horizontal, 8)
                    .padding(.vertical, 2)
                    .background(stateColor(card.state).opacity(0.15), in: Capsule())
                    .foregroundStyle(stateColor(card.state))
            }
        }
        .padding(.vertical, 4)
    }

    private func cardDetailSheet(_ card: CardDTO) -> some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 24) {
                    VStack(spacing: 8) {
                        Text("Front")
                            .font(.caption2)
                            .textCase(.uppercase)
                            .tracking(1)
                            .foregroundStyle(.secondary)
                        Text(card.front)
                            .font(.title3)
                            .multilineTextAlignment(.center)
                    }

                    Divider()

                    VStack(spacing: 8) {
                        Text("Back")
                            .font(.caption2)
                            .textCase(.uppercase)
                            .tracking(1)
                            .foregroundStyle(.secondary)
                        Text(card.back)
                            .font(.title3)
                            .multilineTextAlignment(.center)
                    }

                    Divider()

                    VStack(spacing: 8) {
                        Text("Scheduling")
                            .font(.caption2)
                            .textCase(.uppercase)
                            .tracking(1)
                            .foregroundStyle(.secondary)

                        LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
                            fsrsItem("State", value: card.state.capitalized)
                            fsrsItem("Due", value: formatFullDate(card.dueAt) ?? "Not scheduled")
                            fsrsItem("Stability", value: card.stability.map { String(format: "%.1f", $0) } ?? "\u{2014}")
                            fsrsItem("Difficulty", value: card.difficulty.map { String(format: "%.1f", $0) } ?? "\u{2014}")
                            fsrsItem("Step", value: card.step.map { "\($0)" } ?? "\u{2014}")
                            fsrsItem("Last Review", value: formatFullDate(card.lastReviewedAt) ?? "Never")
                        }
                    }

                    if !card.decks.isEmpty {
                        Divider()
                        HStack(spacing: 4) {
                            Image(systemName: "rectangle.stack")
                            Text(card.decks.map(\.name).joined(separator: ", "))
                        }
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    }

                    if let sourceFile = card.sourceFile {
                        Divider()
                        HStack(spacing: 4) {
                            Image(systemName: "doc.text")
                            Text(sourceFile)
                            if let heading = card.sourceHeading {
                                Text("\u{00B7}")
                                Text(heading)
                            }
                        }
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    }
                }
                .padding(24)
            }
            .navigationTitle("Card Detail")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Done") { selectedCard = nil }
                }
            }
        }
        .presentationDetents([.medium, .large])
    }

    // MARK: - Helpers

    private func stateColor(_ state: String) -> Color {
        switch state {
        case "new": return .green
        case "review": return .blue
        case "learning": return .orange
        case "relearning": return .red
        default: return .secondary
        }
    }

    private func fsrsItem(_ label: String, value: String) -> some View {
        VStack(spacing: 2) {
            Text(label)
                .font(.caption2)
                .foregroundStyle(.secondary)
            Text(value)
                .font(.subheadline.weight(.medium))
        }
        .frame(maxWidth: .infinity)
    }

    private func parseDueDate(_ isoString: String?) -> Date? {
        guard let str = isoString else { return nil }
        return ISO8601DateFormatter().date(from: str)
    }

    private func isDueOrOverdue(_ isoString: String?) -> Bool {
        guard let date = parseDueDate(isoString) else { return false }
        return date <= Date.now
    }

    private func formattedDueDate(_ isoString: String?) -> String? {
        guard let date = parseDueDate(isoString) else { return nil }
        let calendar = Calendar.current
        if calendar.isDateInToday(date) { return "Due today" }
        if calendar.isDateInTomorrow(date) { return "Due tomorrow" }
        if date < Date.now { return "Overdue" }
        let formatter = DateFormatter()
        formatter.dateFormat = "MMM d"
        return "Due \(formatter.string(from: date))"
    }

    private func formatFullDate(_ isoString: String?) -> String? {
        guard let str = isoString, let date = ISO8601DateFormatter().date(from: str) else { return nil }
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.timeStyle = .short
        return formatter.string(from: date)
    }
}
