import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "DeckDetail")

@MainActor
@Observable
final class DeckDetailViewModel {
    var detail: DeckDetailDTO?
    var isLoading = false
    var errorMessage: String?

    private let deckRepository: DeckRepository
    private let cardRepository: CardRepository
    let deckId: String
    var deckName: String

    init(deckRepository: DeckRepository, cardRepository: CardRepository, deckId: String, deckName: String) {
        self.deckRepository = deckRepository
        self.cardRepository = cardRepository
        self.deckId = deckId
        self.deckName = deckName
    }

    func loadDetail() async {
        isLoading = true
        errorMessage = nil

        do {
            detail = try await deckRepository.fetchDeckDetail(id: deckId)
            logger.info("Loaded deck detail: \(self.detail?.cards.count ?? 0) cards")
        } catch {
            logger.error("Failed to load deck detail: \(error)")
            errorMessage = "Could not load deck. Pull to refresh."
        }

        isLoading = false
    }

    func toggleSuspended() async {
        guard let current = detail else { return }
        let newState = !current.isSuspended

        do {
            _ = try await deckRepository.setSuspended(id: deckId, isSuspended: newState)
            await loadDetail() // Reload to get fresh data
        } catch {
            logger.error("Failed to toggle deck suspended state: \(error)")
            errorMessage = "Could not update deck status."
        }
    }

    func deleteCard(id: String) async throws {
        try await cardRepository.deleteCard(id: id)
        await loadDetail()
    }

    func setCardSuspended(id: String, isSuspended: Bool) async throws {
        try await cardRepository.setSuspended(cardId: id, isSuspended: isSuspended)
        await loadDetail()
    }

    func updateCard(id: String, _ request: UpdateCardRequest) async throws {
        _ = try await cardRepository.updateCard(id: id, request)
        await loadDetail()
    }

    /// Replaces the card's deck assignment with a single deck (matches edit-sheet behavior).
    func assignCardToDeck(cardId: String, deckId: String) async throws {
        let card = try await cardRepository.fetchCard(id: cardId)
        let request = UpdateCardRequest(
            front: card.front,
            back: card.back,
            sourceFile: card.sourceFile,
            sourceHeading: card.sourceHeading,
            deckIds: [deckId]
        )
        _ = try await cardRepository.updateCard(id: cardId, request)
        await loadDetail()
    }

    /// Removes the card from a single deck, preserving any other deck assignments.
    func removeCardFromDeck(cardId: String, deckId: String) async throws {
        let card = try await cardRepository.fetchCard(id: cardId)
        let remaining = card.decks.map(\.id).filter { $0 != deckId }
        let request = UpdateCardRequest(
            front: card.front,
            back: card.back,
            sourceFile: card.sourceFile,
            sourceHeading: card.sourceHeading,
            deckIds: remaining
        )
        _ = try await cardRepository.updateCard(id: cardId, request)
        await loadDetail()
    }

    func createCard(_ request: CreateCardRequest) async throws {
        let finalRequest = CreateCardRequest(
            front: request.front,
            back: request.back,
            sourceFile: request.sourceFile,
            sourceHeading: request.sourceHeading,
            deckId: deckId
        )
        _ = try await cardRepository.createCard(finalRequest)
        await loadDetail()
    }

    func updateDeck(_ request: UpdateDeckRequest) async throws {
        let updated = try await deckRepository.updateDeck(id: deckId, request)
        deckName = updated.name
        await loadDetail()
    }

    func deleteDeck(deleteCards: Bool) async throws {
        try await deckRepository.deleteDeck(id: deckId, deleteCards: deleteCards)
    }
}
