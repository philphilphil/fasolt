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
                        leadingToolbar: { snapshotsButton }
                    )
                case .cards:
                    CardListContent(
                        viewModel: cardListViewModel,
                        leadingToolbar: { snapshotsButton }
                    )
                }
            }
            .background(FasoltTheme.paper0.ignoresSafeArea())
            .sheet(isPresented: $showSnapshots) {
                SnapshotsView(viewModel: snapshotViewModel)
            }
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
}
