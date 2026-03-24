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
    private let deckRepository: DeckRepository
    private let studyViewModelFactory: () -> StudyViewModel

    init(
        viewModel: DeckListViewModel,
        deckRepository: DeckRepository,
        studyViewModelFactory: @escaping () -> StudyViewModel
    ) {
        _viewModel = State(initialValue: viewModel)
        self.deckRepository = deckRepository
        self.studyViewModelFactory = studyViewModelFactory
    }

    var body: some View {
        NavigationStack {
            Group {
                if viewModel.decks.isEmpty && !viewModel.isLoading && viewModel.errorMessage == nil {
                    ContentUnavailableView(
                        "No decks yet",
                        systemImage: "rectangle.stack",
                        description: Text("Create decks via the API or MCP tools")
                    )
                } else if let error = viewModel.errorMessage, viewModel.decks.isEmpty {
                    ContentUnavailableView {
                        Label("Could not load", systemImage: "wifi.slash")
                    } description: {
                        Text(error)
                    } actions: {
                        Button("Retry") {
                            Task { await viewModel.loadDecks() }
                        }
                    }
                } else {
                    List(sortedDecks(filteredDecks), id: \.id) { deck in
                        NavigationLink {
                            DeckDetailView(
                                viewModel: DeckDetailViewModel(
                                    deckRepository: deckRepository,
                                    deckId: deck.id,
                                    deckName: deck.name
                                ),
                                studyViewModelFactory: studyViewModelFactory
                            )
                        } label: {
                            deckRow(deck)
                        }
                    }
                }
            }
            .searchable(text: $searchText, prompt: "Search decks")
            .refreshable {
                await viewModel.loadDecks()
            }
            .navigationTitle("Decks")
            .toolbar {
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
            }
            .overlay {
                if viewModel.isLoading && viewModel.decks.isEmpty {
                    ProgressView()
                }
            }
            .offlineBanner()
            .task {
                if viewModel.decks.isEmpty {
                    await viewModel.loadDecks()
                }
            }
            .onAppear {
                if !viewModel.decks.isEmpty {
                    Task { await viewModel.loadDecks() }
                }
            }
        }
    }

    private func sortedDecks(_ decks: [DeckDTO]) -> [DeckDTO] {
        decks.sorted { a, b in
            // Inactive decks always go to the bottom
            if a.isActive != b.isActive { return a.isActive ? true : false }
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
                    if !deck.isActive {
                        Text("Inactive")
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
        .opacity(deck.isActive ? 1 : 0.5)
    }
}
