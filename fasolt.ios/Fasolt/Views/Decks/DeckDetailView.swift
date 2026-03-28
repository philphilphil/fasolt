import SwiftUI
import UIKit

enum CardSortOrder: String, CaseIterable {
    case dueDate = "Due Date"
    case state = "State"
    case front = "Front"
    case sourceFile = "Source"
}

struct DeckDetailView: View {
    @Environment(\.startStudy) private var startStudy
    @State private var viewModel: DeckDetailViewModel
    @State private var sortOrder: CardSortOrder = .dueDate
    @State private var showEditSheet = false
    @State private var cardToDelete: DeckCardDTO?
    @State private var showDeleteCardAlert = false

    init(viewModel: DeckDetailViewModel) {
        _viewModel = State(initialValue: viewModel)
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

                            if detail.isSuspended {
                                Text("This deck is suspended. Cards are excluded from study.")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                    .padding(.vertical, 4)
                            }
                        }

                        if detail.cards.isEmpty {
                            Section {
                                ContentUnavailableView(
                                    "No cards in this deck",
                                    systemImage: "rectangle.on.rectangle.slash",
                                    description: Text("Add cards via the API, MCP tools, or the Cards tab")
                                )
                            }
                        } else {
                            Section("Cards") {
                                ForEach(sortedCards(detail.cards), id: \.id) { card in
                                    NavigationLink {
                                        CardDetailView(card: card)
                                    } label: {
                                        DeckCardRow(card: card, showSourceFile: true)
                                    }
                                    .swipeActions(edge: .trailing, allowsFullSwipe: false) {
                                        Button(role: .destructive) {
                                            cardToDelete = card
                                            showDeleteCardAlert = true
                                        } label: {
                                            Label("Delete", systemImage: "trash")
                                        }

                                        Button {
                                            Task {
                                                try? await viewModel.setCardSuspended(
                                                    id: card.id,
                                                    isSuspended: !card.isSuspended
                                                )
                                            }
                                        } label: {
                                            Label(
                                                card.isSuspended ? "Unsuspend" : "Suspend",
                                                systemImage: card.isSuspended ? "play.circle" : "pause.circle"
                                            )
                                        }
                                        .tint(.orange)
                                    }
                                }
                            }
                        }
                    }

                    if detail.dueCount > 0 && !detail.isSuspended {
                        Button {
                            startStudy(deckId: viewModel.deckId)
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
        .offlineBanner()
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    showEditSheet = true
                } label: {
                    Label("Edit", systemImage: "pencil")
                }
            }
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
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    Task { await viewModel.toggleSuspended() }
                } label: {
                    Label(
                        viewModel.detail?.isSuspended == true ? "Unsuspend" : "Suspend",
                        systemImage: viewModel.detail?.isSuspended == true ? "play.circle" : "pause.circle"
                    )
                }
            }
        }
        .sheet(isPresented: $showEditSheet) {
            if let detail = viewModel.detail {
                DeckFormSheet(
                    mode: .edit(DeckDTO(
                        id: detail.id,
                        name: detail.name,
                        description: detail.description,
                        cardCount: detail.cardCount,
                        dueCount: detail.dueCount,
                        createdAt: "",
                        isSuspended: detail.isSuspended
                    ))
                ) { request in
                    try await viewModel.updateDeck(UpdateDeckRequest(
                        name: request.name,
                        description: request.description
                    ))
                }
            }
        }
        .alert("Delete Card", isPresented: $showDeleteCardAlert, presenting: cardToDelete) { card in
            Button("Delete", role: .destructive) {
                Task { try? await viewModel.deleteCard(id: card.id) }
            }
            Button("Cancel", role: .cancel) {}
        } message: { _ in
            Text("This cannot be undone.")
        }
        .refreshable {
            await viewModel.loadDetail()
        }
        .task {
            if viewModel.detail == nil {
                await viewModel.loadDetail()
            }
        }
        .onAppear {
            if viewModel.detail != nil {
                Task { await viewModel.loadDetail() }
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
}
