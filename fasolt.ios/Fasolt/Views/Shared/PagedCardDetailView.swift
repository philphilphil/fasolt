import SwiftUI

struct PagedCardDetailView: View {
    let cards: [DeckCardDTO]
    let currentDeckId: String?
    let availableDecks: [DeckDTO]
    let onSaveEdit: (String, UpdateCardRequest) async throws -> Void
    let onToggleSuspended: (String, Bool) async throws -> Void

    @State private var selectedId: String

    init(
        cards: [DeckCardDTO],
        initialCardId: String,
        currentDeckId: String?,
        availableDecks: [DeckDTO],
        onSaveEdit: @escaping (String, UpdateCardRequest) async throws -> Void,
        onToggleSuspended: @escaping (String, Bool) async throws -> Void
    ) {
        self.cards = cards
        self.currentDeckId = currentDeckId
        self.availableDecks = availableDecks
        self.onSaveEdit = onSaveEdit
        self.onToggleSuspended = onToggleSuspended
        _selectedId = State(initialValue: initialCardId)
    }

    private var currentIndex: Int {
        cards.firstIndex(where: { $0.id == selectedId }) ?? 0
    }

    var body: some View {
        TabView(selection: $selectedId) {
            ForEach(cards, id: \.id) { card in
                CardDetailView(
                    card: card,
                    currentDeckId: currentDeckId,
                    availableDecks: availableDecks,
                    onSaveEdit: { request in
                        try await onSaveEdit(card.id, request)
                    },
                    onToggleSuspended: { isSuspended in
                        try await onToggleSuspended(card.id, isSuspended)
                    }
                )
                .tag(card.id)
            }
        }
        .tabViewStyle(.page(indexDisplayMode: .never))
        .toolbar {
            ToolbarItem(placement: .principal) {
                VStack(spacing: 0) {
                    Text("Card")
                        .font(.headline)
                    Text("\(currentIndex + 1) / \(cards.count)")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                        .monospacedDigit()
                }
            }
        }
    }
}
