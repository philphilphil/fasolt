import SwiftUI

struct CardListView: View {
    @State private var viewModel: CardListViewModel
    @State private var sortOrder: CardSortOrder = .dueDate
    @State private var showCreateSheet = false
    @State private var cardToDelete: CardDTO?
    @State private var showDeleteAlert = false

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
                        description: Text("Tap + to create a card")
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
                } else if viewModel.isSearching && sortedCards(viewModel.filteredCards).isEmpty {
                    ContentUnavailableView.search(text: viewModel.searchText)
                } else {
                    cardsList
                }
            }
            .searchable(text: $viewModel.searchText, prompt: "Search cards")
            .refreshable {
                await viewModel.loadCards()
            }
            .navigationTitle("Cards")
            .toolbar {
                ToolbarItem(placement: .topBarTrailing) {
                    Button {
                        showCreateSheet = true
                    } label: {
                        Label("New Card", systemImage: "plus")
                    }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Menu {
                        Picker("Sort", selection: $sortOrder) {
                            ForEach(CardSortOrder.allCases, id: \.self) { order in
                                Text(order.rawValue).tag(order)
                            }
                        }

                        Toggle("Active Only", isOn: $viewModel.hideInactive)
                    } label: {
                        Label("Sort", systemImage: "arrow.up.arrow.down")
                    }
                }
            }
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
                await viewModel.loadDecks()
            }
            .onReceive(NotificationCenter.default.publisher(for: .appDidBecomeActive)) { _ in
                Task { await viewModel.loadCards() }
            }
            .sheet(isPresented: $showCreateSheet) {
                CardFormSheet(mode: .create, decks: viewModel.availableDecks) { request, deckId in
                    try await viewModel.createCard(request, deckId: deckId)
                }
            }
            .alert("Delete Card", isPresented: $showDeleteAlert, presenting: cardToDelete) { card in
                Button("Delete", role: .destructive) {
                    Task { try? await viewModel.deleteCard(id: card.id) }
                }
                Button("Cancel", role: .cancel) {}
            } message: { _ in
                Text("This cannot be undone.")
            }
        }
    }

    private var cardsList: some View {
        List {
            ForEach(sortedCards(viewModel.filteredCards)) { card in
                NavigationLink {
                    CardDetailView(
                        card: card,
                        deckNames: card.decks.isEmpty ? nil : card.decks.map(\.name)
                    )
                } label: {
                    DeckCardRow(
                        card: card,
                        deckNames: card.decks.isEmpty ? nil : card.decks.map(\.name)
                    )
                }
                .swipeActions(edge: .trailing, allowsFullSwipe: false) {
                    Button(role: .destructive) {
                        cardToDelete = card
                        showDeleteAlert = true
                    } label: {
                        Label("Delete", systemImage: "trash")
                    }

                    Button {
                        Task {
                            try? await viewModel.setSuspended(
                                cardId: card.id,
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

    private func sortedCards(_ cards: [CardDTO]) -> [CardDTO] {
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
