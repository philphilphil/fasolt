import SwiftUI

/// The public deck library. Read-only: decks are browsed and imported here, never
/// published — publishing stays on the web app, where handles are claimed.
struct ExploreView: View {
    @State private var viewModel: ExploreViewModel
    private let repository: PublicLibraryRepository
    private let deckRepository: DeckRepository

    init(viewModel: ExploreViewModel, repository: PublicLibraryRepository, deckRepository: DeckRepository) {
        _viewModel = State(initialValue: viewModel)
        self.repository = repository
        self.deckRepository = deckRepository
    }

    var body: some View {
        NavigationStack {
            contentView
                .scrollContentBackground(.hidden)
                .background(FasoltTheme.paper0.ignoresSafeArea())
                .navigationTitle("Explore")
                .navigationBarTitleDisplayMode(.inline)
                .tint(FasoltTheme.accent)
                .searchable(text: $viewModel.searchText, prompt: "Search public decks")
                .refreshable { await viewModel.load() }
                .toolbar { toolbarContent }
                .overlay { if viewModel.isLoading && viewModel.decks.isEmpty { ProgressView() } }
                .offlineBanner()
                .task { if viewModel.decks.isEmpty { await viewModel.load() } }
                .onChange(of: viewModel.sort) { _, _ in Task { await viewModel.load() } }
                .onSubmit(of: .search) { Task { await viewModel.load() } }
                .onChange(of: viewModel.searchText) { _, newValue in
                    // Clearing the box should restore the full listing right away;
                    // typing waits for submit so every keystroke isn't a request.
                    if newValue.isEmpty { Task { await viewModel.load() } }
                }
        }
    }

    @ViewBuilder
    private var contentView: some View {
        if let error = viewModel.errorMessage, viewModel.decks.isEmpty {
            ContentUnavailableView {
                Label("Could not load", systemImage: "wifi.slash")
            } description: {
                Text(error)
            } actions: {
                Button("Retry") { Task { await viewModel.load() } }
            }
        } else if viewModel.decks.isEmpty && !viewModel.isLoading {
            if viewModel.searchText.isEmpty {
                ContentUnavailableView(
                    "No public decks yet",
                    systemImage: "books.vertical",
                    description: Text("Shared decks from the community will show up here.")
                )
            } else {
                ContentUnavailableView.search(text: viewModel.searchText)
            }
        } else {
            deckList
        }
    }

    private var deckList: some View {
        List {
            ForEach(viewModel.decks) { deck in
                NavigationLink {
                    ExploreDeckDetailView(
                        viewModel: ExploreDeckViewModel(
                            publicId: deck.id,
                            repository: repository,
                            deckRepository: deckRepository
                        )
                    )
                } label: {
                    deckRow(deck)
                }
                .listRowBackground(FasoltTheme.paper1)
                .listRowSeparatorTint(FasoltTheme.rule2)
                .onAppear {
                    if deck.id == viewModel.decks.last?.id {
                        Task { await viewModel.loadMore() }
                    }
                }
            }

            if viewModel.isLoadingMore {
                HStack {
                    Spacer()
                    ProgressView()
                    Spacer()
                }
                .listRowBackground(FasoltTheme.paper0)
            }
        }
    }

    private func deckRow(_ deck: LibraryDeckDTO) -> some View {
        HStack(spacing: 12) {
            DeckInitialsBadge(
                name: deck.name,
                color: FasoltTheme.deckColor(for: deck.id)
            )

            VStack(alignment: .leading, spacing: 3) {
                Text(deck.name)
                    .font(.system(size: 16, weight: .medium))
                    .foregroundStyle(FasoltTheme.ink0)
                    .lineLimit(1)
                if let description = deck.description, !description.isEmpty {
                    Text(description)
                        .font(.system(size: 13))
                        .foregroundStyle(FasoltTheme.ink2)
                        .lineLimit(1)
                }
                if let handle = deck.authorHandle {
                    Text("@\(handle)")
                        .font(.system(size: 12))
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
                    text: deck.copyCount > 0 ? "\(deck.copyCount) imports" : "cards",
                    color: FasoltTheme.ink2,
                    size: 9.5
                )
            }
        }
        .padding(.vertical, 4)
    }

    @ToolbarContentBuilder
    private var toolbarContent: some ToolbarContent {
        ToolbarItem(placement: .topBarTrailing) {
            Menu {
                Picker("Sort decks by", selection: $viewModel.sort) {
                    ForEach(LibrarySort.allCases, id: \.self) { sort in
                        Text(sort.label).tag(sort)
                    }
                }
            } label: {
                Label("Sort decks by", systemImage: "arrow.up.arrow.down")
            }
        }
    }
}
