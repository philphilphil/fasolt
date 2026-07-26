import SwiftUI

enum LibrarySegment: String, CaseIterable {
    case decks = "Decks"
    case cards = "Cards"
}

struct LibraryView: View {
    @State private var selectedSegment: LibrarySegment = .decks
    @State private var showSnapshots = false

    let deckListViewModel: DeckListViewModel
    let cardListViewModel: CardListViewModel
    let deckRepository: DeckRepository
    let cardRepository: CardRepository
    let snapshotViewModel: SnapshotViewModel
    let publicLibraryRepository: PublicLibraryRepository

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                Picker("View", selection: $selectedSegment) {
                    ForEach(LibrarySegment.allCases, id: \.self) { segment in
                        Text(segment.rawValue).tag(segment)
                    }
                }
                .pickerStyle(.segmented)
                .padding(.horizontal, FasoltTheme.pagePadding)
                .padding(.top, 6)
                .padding(.bottom, 8)
                .background(FasoltTheme.paper0)

                switch selectedSegment {
                case .decks:
                    DeckListContent(
                        viewModel: deckListViewModel,
                        deckRepository: deckRepository,
                        cardRepository: cardRepository,
                        leadingToolbar: { leadingButtons }
                    )
                case .cards:
                    CardListContent(
                        viewModel: cardListViewModel,
                        leadingToolbar: { leadingButtons }
                    )
                }
            }
            .background(FasoltTheme.paper0.ignoresSafeArea())
            .tint(FasoltTheme.accent)
            .sheet(isPresented: $showSnapshots) {
                SnapshotsView(viewModel: snapshotViewModel)
            }
        }
    }

    private var leadingButtons: some View {
        HStack(spacing: 16) {
            snapshotsButton
            exploreButton
        }
    }

    private var snapshotsButton: some View {
        Button {
            showSnapshots = true
        } label: {
            Image(systemName: "clock.arrow.circlepath")
        }
        .accessibilityLabel("Snapshots")
    }

    private var exploreButton: some View {
        NavigationLink {
            ExploreView(repository: publicLibraryRepository, deckRepository: deckRepository)
        } label: {
            Image(systemName: "globe")
        }
        .accessibilityLabel("Explore public decks")
    }
}
