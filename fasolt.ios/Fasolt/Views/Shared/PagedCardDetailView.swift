import SwiftUI

struct PagedCardDetailView: View {
    let cards: [DeckCardDTO]
    let currentDeckId: String?
    let availableDecks: [DeckDTO]
    let onSaveEdit: (String, UpdateCardRequest) async throws -> Void
    let onToggleSuspended: (String, Bool) async throws -> Void

    @State private var selectedId: String
    @State private var showEditSheet = false

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

    private var currentCard: DeckCardDTO? {
        cards.first(where: { $0.id == selectedId })
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
                    },
                    showsToolbarActions: false
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
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    if let id = currentCard?.id {
                        UIPasteboard.general.string = id
                        UIImpactFeedbackGenerator(style: .light).impactOccurred()
                    }
                } label: {
                    Label("Copy ID", systemImage: "doc.on.doc")
                }
                .disabled(currentCard == nil)
            }
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    showEditSheet = true
                } label: {
                    Label("Edit", systemImage: "pencil")
                }
                .disabled(currentCard == nil)
            }
        }
        .sheet(isPresented: $showEditSheet) {
            if let card = currentCard {
                CardFormSheet(
                    mode: .edit(
                        front: card.front,
                        back: card.back,
                        sourceFile: card.sourceFile,
                        sourceHeading: card.sourceHeading,
                        deckId: currentDeckId,
                        isSuspended: card.isSuspended
                    ),
                    decks: availableDecks,
                    onSave: { request, deckId in
                        let updateRequest = UpdateCardRequest(
                            front: request.front,
                            back: request.back,
                            sourceFile: request.sourceFile,
                            sourceHeading: request.sourceHeading,
                            deckIds: deckId.map { [$0] }
                        )
                        try await onSaveEdit(card.id, updateRequest)
                    },
                    onToggleSuspended: { isSuspended in
                        try await onToggleSuspended(card.id, isSuspended)
                    }
                )
            }
        }
    }
}
