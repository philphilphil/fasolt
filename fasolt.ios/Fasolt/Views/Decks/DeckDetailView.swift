import SwiftUI
import UIKit

enum CardSortOrder: String, CaseIterable {
    case dueDate = "Due Date"
    case state = "State"
    case front = "Front"
    case sourceFile = "Source"
}

struct DeckDetailView: View {
    @State private var viewModel: DeckDetailViewModel
    @State private var selectedCard: DeckCardDTO?
    @State private var sortOrder: CardSortOrder = .dueDate
    private let studyViewModelFactory: () -> StudyViewModel

    init(
        viewModel: DeckDetailViewModel,
        studyViewModelFactory: @escaping () -> StudyViewModel
    ) {
        _viewModel = State(initialValue: viewModel)
        self.studyViewModelFactory = studyViewModelFactory
    }

    var body: some View {
        Group {
            if let detail = viewModel.detail {
                VStack(spacing: 0) {
                    List {
                        Section {
                            HStack(spacing: 16) {
                                VStack(spacing: 2) {
                                    Text("\(detail.cardCount)")
                                        .font(.title3.weight(.semibold))
                                    Text("cards")
                                        .font(.caption2)
                                        .foregroundStyle(.secondary)
                                }
                                VStack(spacing: 2) {
                                    Text("\(detail.dueCount)")
                                        .font(.title3.weight(.semibold))
                                        .foregroundStyle(detail.dueCount > 0 ? .orange : .primary)
                                    Text("due")
                                        .font(.caption2)
                                        .foregroundStyle(.secondary)
                                }
                            }
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 4)
                        }

                        if detail.cards.isEmpty {
                            Section {
                                ContentUnavailableView(
                                    "No cards in this deck",
                                    systemImage: "rectangle.on.rectangle.slash",
                                    description: Text("Add cards via the API or MCP tools")
                                )
                            }
                        } else {
                            Section("Cards") {
                                ForEach(sortedCards(detail.cards), id: \.id) { card in
                                    Button {
                                        selectedCard = card
                                    } label: {
                                        DeckCardRow(card: card)
                                    }
                                    .tint(.primary)
                                }
                            }
                        }
                    }

                    if detail.dueCount > 0 {
                        NavigationLink {
                            StudyView(viewModel: studyViewModelFactory(), deckId: viewModel.deckId)
                        } label: {
                            Text("Study This Deck")
                                .font(.headline)
                                .frame(maxWidth: .infinity)
                                .padding(.vertical, 14)
                        }
                        .buttonStyle(.borderedProminent)
                        .padding()
                    }
                }
            } else if let error = viewModel.errorMessage {
                ContentUnavailableView {
                    Label("Could not load", systemImage: "wifi.slash")
                } description: {
                    Text(error)
                } actions: {
                    Button("Retry") {
                        Task { await viewModel.loadDetail() }
                    }
                }
            } else {
                ProgressView()
            }
        }
        .navigationTitle(viewModel.deckName)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Menu {
                    Picker("Sort", selection: $sortOrder) {
                        ForEach(CardSortOrder.allCases, id: \.self) { order in
                            Text(order.rawValue).tag(order)
                        }
                    }
                } label: {
                    Label("Sort", systemImage: "arrow.up.arrow.down")
                }
            }
        }
        .refreshable {
            await viewModel.loadDetail()
        }
        .sheet(item: $selectedCard) { card in
            cardDetailSheet(card)
        }
        .task {
            if viewModel.detail == nil {
                await viewModel.loadDetail()
            }
        }
    }

    private func sortedCards(_ cards: [DeckCardDTO]) -> [DeckCardDTO] {
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

    private func cardDetailSheet(_ card: DeckCardDTO) -> some View {
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

                    // FSRS Info
                    VStack(spacing: 8) {
                        Text("Scheduling")
                            .font(.caption2)
                            .textCase(.uppercase)
                            .tracking(1)
                            .foregroundStyle(.secondary)

                        LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 12) {
                            fsrsItem("State", value: card.state.capitalized)
                            fsrsItem("Due", value: formatDate(card.dueAt) ?? "Not scheduled")
                            fsrsItem("Stability", value: card.stability.map { String(format: "%.1f", $0) } ?? "\u{2014}")
                            fsrsItem("Difficulty", value: card.difficulty.map { String(format: "%.1f", $0) } ?? "\u{2014}")
                            fsrsItem("Step", value: card.step.map { "\($0)" } ?? "\u{2014}")
                            fsrsItem("Last Review", value: formatDate(card.lastReviewedAt) ?? "Never")
                        }
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

    private func formatDate(_ isoString: String?) -> String? {
        guard let str = isoString, let date = ISO8601DateFormatter().date(from: str) else { return nil }
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.timeStyle = .short
        return formatter.string(from: date)
    }
}
