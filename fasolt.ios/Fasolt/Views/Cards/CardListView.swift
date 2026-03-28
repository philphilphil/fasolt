import SwiftUI

struct CardListView: View {
    @State private var viewModel: CardListViewModel

    init(viewModel: CardListViewModel) {
        _viewModel = State(initialValue: viewModel)
    }

    var body: some View {
        NavigationStack {
            CardListContent(viewModel: viewModel)
        }
    }
}

struct CardListContent: View {
    @Bindable var viewModel: CardListViewModel
    @State private var sortOrder: CardSortOrder = .dueDate
    @State private var showCreateSheet = false
    @State private var cardToDelete: CardDTO?
    @State private var showDeleteAlert = false
    @State private var errorMessage: String?

    var body: some View {
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
            } else if viewModel.isSearching && sortedCards(viewModel.filteredCards, by: sortOrder).isEmpty {
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

                    Toggle("Show Suspended", isOn: $viewModel.showSuspended)
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
                Task {
                    do {
                        try await viewModel.deleteCard(id: card.id)
                    } catch {
                        errorMessage = "Failed to delete card."
                    }
                }
            }
            Button("Cancel", role: .cancel) {}
        } message: { _ in
            Text("This cannot be undone.")
        }
        .alert("Error", isPresented: .init(get: { errorMessage != nil }, set: { if !$0 { errorMessage = nil } })) {
            Button("OK") { errorMessage = nil }
        } message: {
            Text(errorMessage ?? "")
        }
    }

    private var cardsList: some View {
        List {
            ForEach(sortedCards(viewModel.filteredCards, by: sortOrder)) { card in
                NavigationLink {
                    CardDetailView(
                        card: card,
                        deckNames: card.decks.isEmpty ? nil : card.decks.map(\.name),
                        currentDeckId: card.decks.first?.id,
                        availableDecks: viewModel.availableDecks,
                        onSaveEdit: { request in
                            try await viewModel.updateCard(id: card.id, request)
                        }
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
                            do {
                                try await viewModel.setSuspended(
                                    cardId: card.id,
                                    isSuspended: !card.isSuspended
                                )
                            } catch {
                                errorMessage = "Failed to update card."
                            }
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
