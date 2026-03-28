import SwiftUI

enum LibrarySegment: String, CaseIterable {
    case decks = "Decks"
    case cards = "Cards"
}

struct LibraryView: View {
    @State private var selectedSegment: LibrarySegment = .decks

    let deckListViewModel: DeckListViewModel
    let cardListViewModel: CardListViewModel
    let deckRepository: DeckRepository
    let cardRepository: CardRepository

    var body: some View {
        NavigationStack {
            VStack(spacing: 0) {
                Picker("View", selection: $selectedSegment) {
                    ForEach(LibrarySegment.allCases, id: \.self) { segment in
                        Text(segment.rawValue).tag(segment)
                    }
                }
                .pickerStyle(.segmented)
                .padding(.horizontal)
                .padding(.vertical, 8)

                switch selectedSegment {
                case .decks:
                    DeckListContent(
                        viewModel: deckListViewModel,
                        deckRepository: deckRepository,
                        cardRepository: cardRepository
                    )
                case .cards:
                    CardListContent(
                        viewModel: cardListViewModel
                    )
                }
            }
        }
    }
}
