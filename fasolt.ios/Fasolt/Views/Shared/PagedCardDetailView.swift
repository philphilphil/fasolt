import SwiftUI

struct PagedCardDetailView: View {
    let cards: [DeckCardDTO]
    let currentDeckId: String?
    let availableDecks: [DeckDTO]
    let onSaveEdit: (String, UpdateCardRequest) async throws -> Void
    let onToggleSuspended: (String, Bool) async throws -> Void
    let onLoadDeckIds: (String) async throws -> [String]
    let onDelete: (String) async throws -> Void

    @Environment(\.dismiss) private var dismiss
    @State private var selectedId: String
    @State private var editingDeckIds: [String]?
    @State private var isLoadingEdit = false
    @State private var showDeleteAlert = false
    @State private var errorMessage: String?

    init(
        cards: [DeckCardDTO],
        initialCardId: String,
        currentDeckId: String?,
        availableDecks: [DeckDTO],
        onSaveEdit: @escaping (String, UpdateCardRequest) async throws -> Void,
        onToggleSuspended: @escaping (String, Bool) async throws -> Void,
        onLoadDeckIds: @escaping (String) async throws -> [String],
        onDelete: @escaping (String) async throws -> Void
    ) {
        self.cards = cards
        self.currentDeckId = currentDeckId
        self.availableDecks = availableDecks
        self.onSaveEdit = onSaveEdit
        self.onToggleSuspended = onToggleSuspended
        self.onLoadDeckIds = onLoadDeckIds
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
                    currentDeckIds: currentDeckId.map { [$0] } ?? [],
                    availableDecks: availableDecks,
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
                    Task { await openEditSheet() }
                } label: {
                    if isLoadingEdit {
                        ProgressView()
                    } else {
                        Label("Edit", systemImage: "pencil")
                    }
                }
                .disabled(currentCard == nil || isLoadingEdit)
            }
            ToolbarItem(placement: .topBarTrailing) {
                Menu {
                    Button {
                        guard let card = currentCard else { return }
                        Task { await toggleSuspended(card: card) }
                    } label: {
                        Label(
                            currentCard?.isSuspended == true ? "Unsuspend" : "Suspend",
                            systemImage: currentCard?.isSuspended == true ? "play.circle" : "pause.circle"
                        )
                    }

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
        .sheet(isPresented: Binding(
            get: { editingDeckIds != nil },
            set: { if !$0 { editingDeckIds = nil } }
        )) {
            if let card = currentCard, let deckIds = editingDeckIds {
                CardFormSheet(
                    mode: .edit(
                        front: card.front,
                        back: card.back,
                        sourceFile: card.sourceFile,
                        deckIds: deckIds,
                        isSuspended: card.isSuspended
                    ),
                    decks: availableDecks,
                    onSave: { request, deckIds in
                        let updateRequest = UpdateCardRequest(
                            front: request.front,
                            back: request.back,
                            sourceFile: request.sourceFile,
                            deckIds: deckIds
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

    private func openEditSheet() async {
        guard let id = currentCard?.id else { return }
        isLoadingEdit = true
        defer { isLoadingEdit = false }
        do {
            editingDeckIds = try await onLoadDeckIds(id)
        } catch {
            errorMessage = "Could not load card."
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
