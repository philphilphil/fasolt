import SwiftUI
import UIKit

struct DeckDetailView: View {
    @Environment(\.startStudy) private var startStudy
    @State private var viewModel: DeckDetailViewModel
    @State private var sortOrder: CardSortOrder = .dueDate
    @State private var showEditSheet = false
    @State private var cardToDelete: DeckCardDTO?
    @State private var showDeleteCardAlert = false
    @State private var availableDecks: [DeckDTO] = []
    @State private var showCreateCardSheet = false
    @State private var errorMessage: String?
    private let deckRepository: DeckRepository

    init(viewModel: DeckDetailViewModel, deckRepository: DeckRepository) {
        _viewModel = State(initialValue: viewModel)
        self.deckRepository = deckRepository
    }

    var body: some View {
        Group {
            if let detail = viewModel.detail {
                VStack(spacing: 0) {
                    List {
                        Section {
                            HStack(spacing: 16) {
                                VStack(spacing: 2) {
                                    Text("\(detail.cardCount)")
                                        .font(.title3.weight(.semibold))
                                    Text("cards")
                                        .font(.caption2)
                                        .foregroundStyle(.secondary)
                                }
                                VStack(spacing: 2) {
                                    Text("\(detail.dueCount)")
                                        .font(.title3.weight(.semibold))
                                        .foregroundStyle(detail.dueCount > 0 ? .orange : .primary)
                                    Text("due")
                                        .font(.caption2)
                                        .foregroundStyle(.secondary)
                                }
                            }
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 4)

                            if detail.isSuspended {
                                Text("This deck is suspended. Cards are excluded from study.")
                                    .font(.caption)
                                    .foregroundStyle(.secondary)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                    .padding(.vertical, 4)
                            }
                        }

                        if detail.cards.isEmpty {
                            Section {
                                ContentUnavailableView(
                                    "No cards in this deck",
                                    systemImage: "rectangle.on.rectangle.slash",
                                    description: Text("Tap + to add a card to this deck")
                                )
                            }
                        } else {
                            Section("Cards") {
                                ForEach(sortedCards(detail.cards, by: sortOrder), id: \.id) { card in
                                    NavigationLink {
                                        pagedDestination(for: card, in: detail)
                                    } label: {
                                        DeckCardRow(card: card, showSourceFile: true)
                                    }
                                    .swipeActions(edge: .leading) {
                                        Button {
                                            UIPasteboard.general.string = card.id
                                        } label: {
                                            Label("Copy ID", systemImage: "doc.on.doc")
                                        }
                                        .tint(.blue)
                                    }
                                    .swipeActions(edge: .trailing, allowsFullSwipe: false) {
                                        Button(role: .destructive) {
                                            cardToDelete = card
                                            showDeleteCardAlert = true
                                        } label: {
                                            Label("Delete", systemImage: "trash")
                                        }

                                        Button {
                                            Task {
                                                do {
                                                    try await viewModel.setCardSuspended(
                                                        id: card.id,
                                                        isSuspended: !card.isSuspended
                                                    )
                                                } catch {
                                                    errorMessage = "Failed to update card."
                                                }
                                            }
                                        } label: {
                                            Label(
                                                card.isSuspended ? "Unsuspend" : "Suspend",
                                                systemImage: card.isSuspended ? "play.circle" : "pause.circle"
                                            )
                                        }
                                        .tint(.orange)
                                    }
                                }
                            }
                        }
                    }

                    if detail.dueCount > 0 && !detail.isSuspended {
                        Button {
                            startStudy(deckId: viewModel.deckId)
                        } label: {
                            Text("Study This Deck")
                                .font(.headline)
                                .frame(maxWidth: .infinity)
                                .padding(.vertical, 14)
                        }
                        .buttonStyle(.borderedProminent)
                        .padding()
                    }
                }
            } else if let error = viewModel.errorMessage {
                ContentUnavailableView {
                    Label("Could not load", systemImage: "wifi.slash")
                } description: {
                    Text(error)
                } actions: {
                    Button("Retry") {
                        Task { await viewModel.loadDetail() }
                    }
                }
            } else {
                ProgressView()
            }
        }
        .navigationTitle(viewModel.deckName)
        .offlineBanner()
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    showCreateCardSheet = true
                } label: {
                    Label("New Card", systemImage: "plus")
                }
            }
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    showEditSheet = true
                } label: {
                    Label("Edit", systemImage: "pencil")
                }
            }
            ToolbarItem(placement: .topBarTrailing) {
                Menu {
                    Picker("Sort", selection: $sortOrder) {
                        ForEach(CardSortOrder.allCases, id: \.self) { order in
                            Text(order.rawValue).tag(order)
                        }
                    }
                } label: {
                    Label("Sort", systemImage: "arrow.up.arrow.down")
                }
            }
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    Task { await viewModel.toggleSuspended() }
                } label: {
                    Label(
                        viewModel.detail?.isSuspended == true ? "Unsuspend" : "Suspend",
                        systemImage: viewModel.detail?.isSuspended == true ? "play.circle" : "pause.circle"
                    )
                }
            }
        }
        .sheet(isPresented: $showEditSheet) {
            if let detail = viewModel.detail {
                DeckFormSheet(
                    mode: .edit(DeckDTO(
                        id: detail.id,
                        name: detail.name,
                        description: detail.description,
                        cardCount: detail.cardCount,
                        dueCount: detail.dueCount,
                        createdAt: "",
                        isSuspended: detail.isSuspended
                    ))
                ) { request in
                    try await viewModel.updateDeck(UpdateDeckRequest(
                        name: request.name,
                        description: request.description
                    ))
                }
            }
        }
        .sheet(isPresented: $showCreateCardSheet) {
            CardFormSheet(mode: .create, decks: []) { request, _ in
                try await viewModel.createCard(request)
            }
        }
        .alert("Delete Card", isPresented: $showDeleteCardAlert, presenting: cardToDelete) { card in
            Button("Delete", role: .destructive) {
                Task {
                    do {
                        try await viewModel.deleteCard(id: card.id)
                    } catch {
                        errorMessage = "Failed to delete card."
                    }
                }
            }
            Button("Cancel", role: .cancel) {}
        } message: { _ in
            Text("This cannot be undone.")
        }
        .alert("Error", isPresented: .init(get: { errorMessage != nil }, set: { if !$0 { errorMessage = nil } })) {
            Button("OK") { errorMessage = nil }
        } message: {
            Text(errorMessage ?? "")
        }
        .refreshable {
            await viewModel.loadDetail()
        }
        .task {
            if viewModel.detail == nil {
                await viewModel.loadDetail()
            }
            do { availableDecks = try await deckRepository.fetchDecks() } catch {
                // Non-critical: deck picker in card edit will be empty
            }
        }
        .onAppear {
            if viewModel.detail != nil {
                Task { await viewModel.loadDetail() }
            }
        }
    }

    @ViewBuilder
    private func pagedDestination(for card: DeckCardDTO, in detail: DeckDetailDTO) -> some View {
        PagedCardDetailView(
            cards: sortedCards(detail.cards, by: sortOrder),
            initialCardId: card.id,
            currentDeckId: viewModel.deckId,
            availableDecks: availableDecks,
            onSaveEdit: { id, request in
                try await viewModel.updateCard(id: id, request)
            },
            onToggleSuspended: { id, isSuspended in
                try await viewModel.setCardSuspended(id: id, isSuspended: isSuspended)
            }
        )
    }
}
