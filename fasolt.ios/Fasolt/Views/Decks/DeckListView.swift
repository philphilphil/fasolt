import SwiftUI

struct DeckListView: View {
    @State private var viewModel: DeckListViewModel
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
                    List(viewModel.decks, id: \.id) { deck in
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
            .refreshable {
                await viewModel.loadDecks()
            }
            .navigationTitle("Decks")
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

    private func deckRow(_ deck: DeckDTO) -> some View {
        HStack {
            VStack(alignment: .leading, spacing: 4) {
                Text(deck.name)
                    .font(.body.weight(.medium))
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
    }
}
