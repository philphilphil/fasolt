import SwiftUI

enum DeckSortOrder: String, CaseIterable {
    case name = "Name"
    case cardCount = "Card Count"
    case dueCount = "Due Count"
}

struct DeckListView: View {
    @State private var viewModel: DeckListViewModel
    @State private var searchText = ""
    @State private var sortOrder: DeckSortOrder = .name
    @State private var showCreateSheet = false
    @State private var deckToDelete: DeckDTO?
    @State private var showDeleteConfirmation = false
    private let deckRepository: DeckRepository
    private let cardRepository: CardRepository

    init(
        viewModel: DeckListViewModel,
        deckRepository: DeckRepository,
        cardRepository: CardRepository
    ) {
        _viewModel = State(initialValue: viewModel)
        self.deckRepository = deckRepository
        self.cardRepository = cardRepository
    }

    var body: some View {
        NavigationStack {
            contentView
                .searchable(text: $searchText, prompt: "Search decks")
                .refreshable { await viewModel.loadDecks() }
                .navigationTitle("Decks")
                .toolbar { toolbarContent }
                .overlay { if viewModel.isLoading && viewModel.decks.isEmpty { ProgressView() } }
                .offlineBanner()
                .task { if viewModel.decks.isEmpty { await viewModel.loadDecks() } }
                .onAppear { if !viewModel.decks.isEmpty { Task { await viewModel.loadDecks() } } }
                .onReceive(NotificationCenter.default.publisher(for: .appDidBecomeActive)) { _ in
                    Task { await viewModel.loadDecks() }
                }
                .onReceive(NotificationCenter.default.publisher(for: .studySessionEnded)) { _ in
                    Task { await viewModel.loadDecks() }
                }
                .sheet(isPresented: $showCreateSheet) {
                    DeckFormSheet(mode: .create) { request in
                        try await viewModel.createDeck(request)
                    }
                }
                .alert("Delete Deck", isPresented: $showDeleteConfirmation, presenting: deckToDelete) { deck in
                    Button("Delete Deck Only", role: .destructive) {
                        Task { try? await viewModel.deleteDeck(id: deck.id, deleteCards: false) }
                    }
                    Button("Delete Deck and Cards", role: .destructive) {
                        Task { try? await viewModel.deleteDeck(id: deck.id, deleteCards: true) }
                    }
                    Button("Cancel", role: .cancel) {}
                } message: { deck in
                    Text("This deck has \(deck.cardCount) cards. What would you like to do?")
                }
        }
    }

    @ViewBuilder
    private var contentView: some View {
        if viewModel.decks.isEmpty && !viewModel.isLoading && viewModel.errorMessage == nil {
            ContentUnavailableView(
                "No decks yet",
                systemImage: "rectangle.stack",
                description: Text("Tap + to create a deck")
            )
        } else if let error = viewModel.errorMessage, viewModel.decks.isEmpty {
            ContentUnavailableView {
                Label("Could not load", systemImage: "wifi.slash")
            } description: {
                Text(error)
            } actions: {
                Button("Retry") { Task { await viewModel.loadDecks() } }
            }
        } else {
            deckList
        }
    }

    private var deckList: some View {
        List {
            ForEach(sortedDecks(filteredDecks), id: \.id) { deck in
                NavigationLink {
                    DeckDetailView(
                        viewModel: DeckDetailViewModel(
                            deckRepository: deckRepository,
                            cardRepository: cardRepository,
                            deckId: deck.id,
                            deckName: deck.name
                        )
                    )
                } label: {
                    deckRow(deck)
                }
                .swipeActions(edge: .trailing, allowsFullSwipe: false) {
                    Button(role: .destructive) {
                        deckToDelete = deck
                        showDeleteConfirmation = true
                    } label: {
                        Label("Delete", systemImage: "trash")
                    }
                    Button {
                        Task {
                            try? await viewModel.setSuspended(
                                id: deck.id,
                                isSuspended: !deck.isSuspended
                            )
                        }
                    } label: {
                        Label(
                            deck.isSuspended ? "Unsuspend" : "Suspend",
                            systemImage: deck.isSuspended ? "play.circle" : "pause.circle"
                        )
                    }
                    .tint(.orange)
                }
            }
        }
    }

    @ToolbarContentBuilder
    private var toolbarContent: some ToolbarContent {
        ToolbarItem(placement: .topBarTrailing) {
            Menu {
                Picker("Sort", selection: $sortOrder) {
                    ForEach(DeckSortOrder.allCases, id: \.self) { order in
                        Text(order.rawValue).tag(order)
                    }
                }
            } label: {
                Label("Sort", systemImage: "arrow.up.arrow.down")
            }
        }
        ToolbarItem(placement: .topBarTrailing) {
            Button {
                showCreateSheet = true
            } label: {
                Label("New Deck", systemImage: "plus")
            }
        }
    }

    private func sortedDecks(_ decks: [DeckDTO]) -> [DeckDTO] {
        decks.sorted { a, b in
            // Inactive decks always go to the bottom
            if a.isSuspended != b.isSuspended { return !a.isSuspended ? true : false }
            switch sortOrder {
            case .name:
                return a.name.localizedCaseInsensitiveCompare(b.name) == .orderedAscending
            case .cardCount:
                return a.cardCount > b.cardCount
            case .dueCount:
                return a.dueCount > b.dueCount
            }
        }
    }

    private var filteredDecks: [DeckDTO] {
        if searchText.isEmpty { return viewModel.decks }
        let query = searchText.lowercased()
        return viewModel.decks.filter {
            $0.name.lowercased().contains(query) ||
            ($0.description?.lowercased().contains(query) ?? false)
        }
    }

    private func deckRow(_ deck: DeckDTO) -> some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                HStack(spacing: 6) {
                    Text(deck.name)
                        .font(.body.weight(.medium))
                    if deck.isSuspended {
                        Text("Suspended")
                            .font(.caption2.weight(.medium))
                            .padding(.horizontal, 6)
                            .padding(.vertical, 2)
                            .background(.secondary.opacity(0.2), in: Capsule())
                            .foregroundStyle(.secondary)
                    }
                }
                if let description = deck.description {
                    Text(description)
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .lineLimit(1)
                }
            }

            Spacer()

            HStack(spacing: 12) {
                VStack(spacing: 2) {
                    Text("\(deck.cardCount)")
                        .font(.subheadline.weight(.semibold))
                    Text("cards")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                }

                if deck.dueCount > 0 {
                    VStack(spacing: 2) {
                        Text("\(deck.dueCount)")
                            .font(.subheadline.weight(.semibold))
                            .foregroundStyle(.orange)
                        Text("due")
                            .font(.caption2)
                            .foregroundStyle(.secondary)
                    }
                }
            }
        }
        .padding(.vertical, 4)
        .opacity(deck.isSuspended ? 0.5 : 1)
    }
}
