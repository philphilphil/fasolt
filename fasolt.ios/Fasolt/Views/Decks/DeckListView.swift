import SwiftUI

enum DeckSortOrder: String, CaseIterable {
    case name = "Name"
    case cardCount = "Card Count"
    case dueCount = "Due Count"
}

struct DeckListView: View {
    @State private var viewModel: DeckListViewModel
    private let deckRepository: DeckRepository
    private let cardRepository: CardRepository

    init(viewModel: DeckListViewModel, deckRepository: DeckRepository, cardRepository: CardRepository) {
        _viewModel = State(initialValue: viewModel)
        self.deckRepository = deckRepository
        self.cardRepository = cardRepository
    }

    var body: some View {
        NavigationStack {
            DeckListContent(
                viewModel: viewModel,
                deckRepository: deckRepository,
                cardRepository: cardRepository
            ) { EmptyView() }
        }
    }
}

struct DeckListContent<Leading: View>: View {
    @Environment(\.startStudy) private var startStudy
    var viewModel: DeckListViewModel
    @State private var searchText = ""
    @State private var sortOrder: DeckSortOrder = .name
    @State private var showCreateSheet = false
    @State private var deckToDelete: DeckDTO?
    @State private var showDeleteConfirmation = false
    @State private var errorMessage: String?
    let deckRepository: DeckRepository
    let cardRepository: CardRepository
    @ViewBuilder var leadingToolbar: () -> Leading

    init(
        viewModel: DeckListViewModel,
        deckRepository: DeckRepository,
        cardRepository: CardRepository,
        @ViewBuilder leadingToolbar: @escaping () -> Leading = { EmptyView() }
    ) {
        self.viewModel = viewModel
        self.deckRepository = deckRepository
        self.cardRepository = cardRepository
        self.leadingToolbar = leadingToolbar
    }

    var body: some View {
        contentView
            .scrollContentBackground(.hidden)
            .background(FasoltTheme.paper0.ignoresSafeArea())
            .searchable(text: $searchText, prompt: "Search decks")
            .refreshable { await viewModel.loadDecks() }
            .navigationTitle("Library")
            .navigationBarTitleDisplayMode(.large)
            .toolbar { toolbarContent }
            .overlay { if viewModel.isLoading && viewModel.decks.isEmpty { ProgressView() } }
            .offlineBanner()
            .task { if viewModel.decks.isEmpty { await viewModel.loadDecks() } }
            .onAppear { Task { await viewModel.loadDecks() } }
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
                    Task {
                        do {
                            try await viewModel.deleteDeck(id: deck.id, deleteCards: false)
                        } catch {
                            errorMessage = "Failed to delete deck."
                        }
                    }
                }
                Button("Delete Deck and Cards", role: .destructive) {
                    Task {
                        do {
                            try await viewModel.deleteDeck(id: deck.id, deleteCards: true)
                        } catch {
                            errorMessage = "Failed to delete deck."
                        }
                    }
                }
                Button("Cancel", role: .cancel) {}
            } message: { deck in
                Text("This deck has \(deck.cardCount) cards. What would you like to do?")
            }
            .alert("Error", isPresented: .init(get: { errorMessage != nil }, set: { if !$0 { errorMessage = nil } })) {
                Button("OK") { errorMessage = nil }
            } message: {
                Text(errorMessage ?? "")
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
                        ),
                        deckRepository: deckRepository
                    )
                } label: {
                    deckRow(deck)
                }
                .listRowBackground(FasoltTheme.paper1)
                .listRowSeparatorTint(FasoltTheme.rule2)
                .swipeActions(edge: .leading, allowsFullSwipe: false) {
                    Button {
                        UIPasteboard.general.string = deck.id
                    } label: {
                        Label("Copy ID", systemImage: "doc.on.doc")
                    }
                    .tint(.blue)

                    if !deck.isSuspended && deck.cardCount > 0 {
                        Button {
                            startStudy(deckId: deck.id, mode: .cram)
                        } label: {
                            Label("Custom study", systemImage: "rectangle.stack.badge.play")
                        }
                        .tint(.orange)
                    }
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
                            do {
                                try await viewModel.setSuspended(
                                    id: deck.id,
                                    isSuspended: !deck.isSuspended
                                )
                            } catch {
                                errorMessage = "Failed to update deck."
                            }
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
        ToolbarItem(placement: .topBarLeading) {
            leadingToolbar()
        }
        ToolbarItem(placement: .topBarTrailing) {
            Button {
                showCreateSheet = true
            } label: {
                Label("New Deck", systemImage: "plus")
            }
        }
        ToolbarItem(placement: .topBarTrailing) {
            Menu {
                Picker("Sort decks by", selection: $sortOrder) {
                    ForEach(DeckSortOrder.allCases, id: \.self) { order in
                        Text(order.rawValue).tag(order)
                    }
                }
            } label: {
                Label("Sort decks by", systemImage: "arrow.up.arrow.down")
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
        HStack(spacing: 12) {
            DeckInitialsBadge(
                name: deck.name,
                color: FasoltTheme.deckColor(for: deck.id)
            )

            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 6) {
                    Text(deck.name)
                        .font(.system(size: 16, weight: .medium))
                        .foregroundStyle(FasoltTheme.ink0)
                        .lineLimit(1)
                    if deck.isSuspended {
                        Text("Suspended")
                            .font(.system(size: 10, weight: .medium))
                            .padding(.horizontal, 6)
                            .padding(.vertical, 2)
                            .background(FasoltTheme.paper2, in: Capsule())
                            .foregroundStyle(FasoltTheme.ink2)
                    }
                }
                if let description = deck.description, !description.isEmpty {
                    Text(description)
                        .font(.system(size: 13))
                        .foregroundStyle(FasoltTheme.ink2)
                        .lineLimit(1)
                }
            }

            Spacer(minLength: 8)

            VStack(alignment: .trailing, spacing: 1) {
                Text("\(deck.cardCount)")
                    .font(.system(size: 16, weight: .semibold))
                    .monospacedDigit()
                    .foregroundStyle(FasoltTheme.ink0)
                CapsLabel(
                    text: deck.dueCount > 0 ? "\(deck.dueCount) due" : "cards",
                    color: deck.dueCount > 0 ? FasoltTheme.accent : FasoltTheme.ink2,
                    size: 9.5
                )
            }
        }
        .padding(.vertical, 4)
        .opacity(deck.isSuspended ? 0.55 : 1)
    }
}
