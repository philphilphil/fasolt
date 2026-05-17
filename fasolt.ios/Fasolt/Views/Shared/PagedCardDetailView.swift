import SwiftUI

struct PagedCardDetailView: View {
    let cards: [DeckCardDTO]
    let currentDeckId: String?
    let availableDecks: [DeckDTO]
    let onSaveEdit: (String, UpdateCardRequest) async throws -> Void
    let onToggleSuspended: (String, Bool) async throws -> Void
    let onAssignToDeck: (String, String) async throws -> Void
    let onRemoveFromDeck: (String, String) async throws -> Void
    let onDelete: (String) async throws -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var selectedId: String
    @State private var showEditSheet = false
    @State private var showDeleteAlert = false
    @State private var errorMessage: String?

    init(
        cards: [DeckCardDTO],
        initialCardId: String,
        currentDeckId: String?,
        availableDecks: [DeckDTO],
        onSaveEdit: @escaping (String, UpdateCardRequest) async throws -> Void,
        onToggleSuspended: @escaping (String, Bool) async throws -> Void,
        onAssignToDeck: @escaping (String, String) async throws -> Void,
        onRemoveFromDeck: @escaping (String, String) async throws -> Void,
        onDelete: @escaping (String) async throws -> Void
    ) {
        self.cards = cards
        self.currentDeckId = currentDeckId
        self.availableDecks = availableDecks
        self.onSaveEdit = onSaveEdit
        self.onToggleSuspended = onToggleSuspended
        self.onAssignToDeck = onAssignToDeck
        self.onRemoveFromDeck = onRemoveFromDeck
        self.onDelete = onDelete
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
                Menu {
                    Button {
                        showEditSheet = true
                    } label: {
                        Label("Edit", systemImage: "pencil")
                    }

                    Divider()

                    Menu {
                        ForEach(availableDecks, id: \.id) { deck in
                            Button {
                                guard let id = currentCard?.id else { return }
                                Task { await assignToDeck(cardId: id, deckId: deck.id) }
                            } label: {
                                Label(deck.name, systemImage: deck.id == currentDeckId ? "checkmark" : "rectangle.stack")
                            }
                        }
                    } label: {
                        Label("Move to deck", systemImage: "rectangle.stack")
                    }

                    if let deckId = currentDeckId {
                        Button {
                            guard let id = currentCard?.id else { return }
                            Task { await removeFromDeck(cardId: id, deckId: deckId) }
                        } label: {
                            Label("Remove from this deck", systemImage: "rectangle.stack.badge.minus")
                        }
                    }

                    Button {
                        guard let card = currentCard else { return }
                        Task { await toggleSuspended(card: card) }
                    } label: {
                        Label(
                            currentCard?.isSuspended == true ? "Unsuspend" : "Suspend",
                            systemImage: currentCard?.isSuspended == true ? "play.circle" : "pause.circle"
                        )
                    }

                    Divider()

                    Button {
                        if let id = currentCard?.id {
                            UIPasteboard.general.string = id
                            UIImpactFeedbackGenerator(style: .light).impactOccurred()
                        }
                    } label: {
                        Label("Copy ID", systemImage: "doc.on.doc")
                    }

                    Divider()

                    Button(role: .destructive) {
                        showDeleteAlert = true
                    } label: {
                        Label("Delete", systemImage: "trash")
                    }
                } label: {
                    Label("More", systemImage: "ellipsis.circle")
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
        .alert("Delete Card", isPresented: $showDeleteAlert) {
            Button("Delete", role: .destructive) {
                guard let id = currentCard?.id else { return }
                Task { await delete(cardId: id) }
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("This cannot be undone.")
        }
        .alert("Error", isPresented: .init(get: { errorMessage != nil }, set: { if !$0 { errorMessage = nil } })) {
            Button("OK") { errorMessage = nil }
        } message: {
            Text(errorMessage ?? "")
        }
    }

    private func assignToDeck(cardId: String, deckId: String) async {
        do {
            try await onAssignToDeck(cardId, deckId)
            dismiss()
        } catch {
            errorMessage = "Could not move card."
        }
    }

    private func removeFromDeck(cardId: String, deckId: String) async {
        do {
            try await onRemoveFromDeck(cardId, deckId)
            dismiss()
        } catch {
            errorMessage = "Could not remove card from deck."
        }
    }

    private func toggleSuspended(card: DeckCardDTO) async {
        do {
            try await onToggleSuspended(card.id, !card.isSuspended)
        } catch {
            errorMessage = "Could not update card."
        }
    }

    private func delete(cardId: String) async {
        do {
            try await onDelete(cardId)
            dismiss()
        } catch {
            errorMessage = "Could not delete card."
        }
    }
}
