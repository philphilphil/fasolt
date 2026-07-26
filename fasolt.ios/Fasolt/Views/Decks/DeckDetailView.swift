import SwiftUI
import UIKit

struct DeckDetailView: View {
    @Environment(\.startStudy) private var startStudy
    @Environment(\.dismiss) private var dismiss
    @State private var viewModel: DeckDetailViewModel
    @State private var sortOrder: CardSortOrder = .dueDate
    @State private var showEditSheet = false
    @State private var cardToDelete: DeckCardDTO?
    @State private var showDeleteCardAlert = false
    @State private var showDeleteDeckAlert = false
    @State private var availableDecks: [DeckDTO] = []
    @State private var showCreateCardSheet = false
    @State private var showConvertAlert = false
    @State private var showUnlinkAlert = false
    @State private var errorMessage: String?
    private let deckRepository: DeckRepository

    init(viewModel: DeckDetailViewModel, deckRepository: DeckRepository) {
        _viewModel = State(initialValue: viewModel)
        self.deckRepository = deckRepository
    }

    // Sheets and alerts are attached through helper functions rather than one long
    // chain: the combined expression is past what the type checker will solve.
    var body: some View {
        alerts(sheets(
            content
                .navigationTitle(viewModel.deckName)
                .navigationBarTitleDisplayMode(.inline)
                .offlineBanner()
                .toolbar { toolbarContent }
        ))
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

    private var content: some View {
        Group {
            if let detail = viewModel.detail {
                VStack(spacing: 0) {
                    List {
                        Section {
                            HStack(spacing: 16) {
                                VStack(spacing: 2) {
                                    Text("\(detail.cardCount)")
                                        .font(.title3.weight(.semibold))
                                        .monospacedDigit()
                                        .foregroundStyle(FasoltTheme.ink0)
                                    Text("cards")
                                        .font(.caption2)
                                        .foregroundStyle(FasoltTheme.ink2)
                                }
                                VStack(spacing: 2) {
                                    Text("\(detail.dueCount)")
                                        .font(.title3.weight(.semibold))
                                        .monospacedDigit()
                                        .foregroundStyle(detail.dueCount > 0 ? FasoltTheme.accentText : FasoltTheme.ink0)
                                    Text("due")
                                        .font(.caption2)
                                        .foregroundStyle(FasoltTheme.ink2)
                                }
                            }
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 4)

                            if detail.isLinked {
                                HStack(spacing: 8) {
                                    LinkedDeckChip(authorHandle: detail.authorHandle)
                                    Spacer(minLength: 0)
                                }
                                .padding(.top, 2)

                                Text("This deck follows the author's updates. Its cards are read-only — your study progress is your own.")
                                    .font(.caption)
                                    .foregroundStyle(FasoltTheme.ink2)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                    .padding(.vertical, 4)
                            } else if let copiedFrom = detail.copiedFromHandle {
                                Text("Copied from @\(copiedFrom).")
                                    .font(.caption)
                                    .foregroundStyle(FasoltTheme.ink2)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                    .padding(.vertical, 4)
                            }

                            if detail.isSuspended {
                                Text(detail.isLinked
                                     ? "You've paused this linked deck. Its cards are excluded from study."
                                     : "This deck is suspended. Cards are excluded from study.")
                                    .font(.caption)
                                    .foregroundStyle(FasoltTheme.ink2)
                                    .frame(maxWidth: .infinity, alignment: .leading)
                                    .padding(.vertical, 4)
                            }
                        }

                        if detail.cards.isEmpty {
                            Section {
                                ContentUnavailableView(
                                    "No cards in this deck",
                                    systemImage: "rectangle.on.rectangle.slash",
                                    description: Text(detail.isLinked
                                                      ? "The author hasn't added any cards yet."
                                                      : "Tap + to add a card to this deck")
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
                                        // Linked cards are the author's — their ids
                                        // address nothing the subscriber can change.
                                        if !detail.isLinked {
                                            Button {
                                                UIPasteboard.general.string = card.id
                                            } label: {
                                                Label("Copy ID", systemImage: "doc.on.doc")
                                            }
                                            .tint(.blue)
                                        }
                                    }
                                    .swipeActions(edge: .trailing, allowsFullSwipe: false) {
                                        if !detail.isLinked {
                                            Button(role: .destructive) {
                                                cardToDelete = card
                                                showDeleteCardAlert = true
                                            } label: {
                                                Label("Delete", systemImage: "trash")
                                            }
                                        }

                                        // Suspending is per-user review state, so it
                                        // stays available on linked cards.
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
                                        .tint(.indigo)
                                    }
                                }
                            }
                        }
                    }

                    if detail.dueCount > 0 && !detail.isSuspended {
                        Button {
                            startStudy(deckId: viewModel.deckId)
                        } label: {
                            Text("Study this deck")
                        }
                        .buttonStyle(AccentButtonStyle())
                        .padding(.horizontal, FasoltTheme.pagePadding)
                        .padding(.vertical, 12)
                        .background(FasoltTheme.paper0)
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
    }

    @ToolbarContentBuilder
    private var toolbarContent: some ToolbarContent {
        if !viewModel.isLinked {
            ToolbarItem(placement: .topBarTrailing) {
                Button {
                    showCreateCardSheet = true
                } label: {
                    Label("New Card", systemImage: "plus")
                }
            }
        }
        ToolbarItem(placement: .topBarTrailing) {
            Menu {
                if !viewModel.isLinked {
                    Button {
                        showEditSheet = true
                    } label: {
                        Label("Edit", systemImage: "pencil")
                    }
                }

                Menu {
                    Picker("Sort cards by", selection: $sortOrder) {
                        ForEach(CardSortOrder.allCases, id: \.self) { order in
                            Text(order.rawValue).tag(order)
                        }
                    }
                } label: {
                    Label("Sort cards by", systemImage: "arrow.up.arrow.down")
                }

                Divider()

                if let detail = viewModel.detail, !detail.isSuspended, detail.cardCount > 0 {
                    Button {
                        startStudy(deckId: viewModel.deckId, mode: .cram)
                    } label: {
                        Label("Custom study", systemImage: "rectangle.stack.badge.play")
                    }
                }

                Button {
                    Task { await viewModel.toggleSuspended() }
                } label: {
                    Label(
                        suspendActionTitle,
                        systemImage: viewModel.detail?.isSuspended == true ? "play.circle" : "pause.circle"
                    )
                }

                Divider()

                if viewModel.isLinked {
                    Button {
                        showConvertAlert = true
                    } label: {
                        Label("Convert to copy", systemImage: "doc.on.doc")
                    }

                    Button(role: .destructive) {
                        showUnlinkAlert = true
                    } label: {
                        Label("Unlink", systemImage: "minus.circle")
                    }
                } else {
                    Button(role: .destructive) {
                        showDeleteDeckAlert = true
                    } label: {
                        Label("Delete", systemImage: "trash")
                    }
                }
            } label: {
                Label("More", systemImage: "ellipsis.circle")
            }
        }
    }

    private func sheets<V: View>(_ view: V) -> some View {
        view
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
            CardFormSheet(mode: .create(initialDeckIds: [viewModel.deckId]), decks: availableDecks) { request, _ in
                // Create stays anchored to this deck; multi-deck create can be done later via Edit.
                try await viewModel.createCard(request)
            }
        }
    }

    private func alerts<V: View>(_ view: V) -> some View {
        view
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
        .alert("Delete Deck", isPresented: $showDeleteDeckAlert) {
            Button("Delete Deck Only", role: .destructive) {
                Task {
                    do {
                        try await viewModel.deleteDeck(deleteCards: false)
                        dismiss()
                    } catch {
                        errorMessage = "Failed to delete deck."
                    }
                }
            }
            Button("Delete Deck and Cards", role: .destructive) {
                Task {
                    do {
                        try await viewModel.deleteDeck(deleteCards: true)
                        dismiss()
                    } catch {
                        errorMessage = "Failed to delete deck."
                    }
                }
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            if let detail = viewModel.detail {
                Text("This deck has \(detail.cardCount) cards. What would you like to do?")
            } else {
                Text("This cannot be undone.")
            }
        }
        .alert("Convert to a copy?", isPresented: $showConvertAlert) {
            Button("Convert") {
                Task {
                    do {
                        try await viewModel.convertToCopy()
                    } catch {
                        errorMessage = "Could not convert this deck to a copy."
                    }
                }
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text(convertAlertMessage)
        }
        .alert("Unlink this deck?", isPresented: $showUnlinkAlert) {
            Button("Unlink", role: .destructive) {
                Task {
                    do {
                        try await viewModel.unlink()
                        dismiss()
                    } catch {
                        errorMessage = "Could not unlink this deck."
                    }
                }
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text(unlinkAlertMessage)
        }
        .alert("Error", isPresented: .init(get: { errorMessage != nil }, set: { if !$0 { errorMessage = nil } })) {
            Button("OK") { errorMessage = nil }
        } message: {
            Text(errorMessage ?? "")
        }
    }

    /// On a linked deck the pause is the subscriber's own, not the author's suspend.
    private var suspendActionTitle: String {
        let isSuspended = viewModel.detail?.isSuspended == true
        if viewModel.isLinked { return isSuspended ? "Resume" : "Pause" }
        return isSuspended ? "Unsuspend" : "Suspend"
    }

    /// Same promise the web app makes, so the two read alike.
    private var unlinkAlertMessage: String {
        let name = viewModel.detail?.name ?? viewModel.deckName
        return "\"\(name)\" is removed from your decks along with your review progress for its "
            + "cards. The author's deck is not affected, and you can link it again from Explore."
    }

    private var convertAlertMessage: String {
        let name = viewModel.detail?.name ?? viewModel.deckName
        let count = viewModel.detail?.cardCount ?? 0
        let author = viewModel.detail?.authorHandle.map { "@\($0)" } ?? "the author"
        return "\"\(name)\" becomes your own deck: \(count) \(count == 1 ? "card is" : "cards are") "
            + "copied into your account and your review progress comes with them. The copy stops "
            + "following \(author), so their future changes won't reach it — but you can edit the cards yourself."
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
            },
            onLoadDeckIds: { id in
                try await viewModel.fetchCardDeckIds(cardId: id)
            },
            onDelete: { id in
                try await viewModel.deleteCard(id: id)
            },
            isReadOnly: detail.isLinked
        )
    }
}
