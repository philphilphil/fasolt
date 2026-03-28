import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "DeckList")

@MainActor
@Observable
final class DeckListViewModel {
    var decks: [DeckDTO] = []
    var isLoading = false
    var errorMessage: String?

    private let deckRepository: DeckRepository

    init(deckRepository: DeckRepository) {
        self.deckRepository = deckRepository
    }

    func loadDecks() async {
        isLoading = true
        errorMessage = nil

        do {
            decks = try await deckRepository.fetchDecks()
            logger.info("Loaded \(self.decks.count) decks")
        } catch {
            logger.error("Failed to load decks: \(error)")
            errorMessage = "Could not load decks. Pull to refresh."
        }

        isLoading = false
    }

    func createDeck(_ request: CreateDeckRequest) async throws {
        _ = try await deckRepository.createDeck(request)
        await loadDecks()
    }

    func deleteDeck(id: String, deleteCards: Bool) async throws {
        try await deckRepository.deleteDeck(id: id, deleteCards: deleteCards)
        await loadDecks()
    }

    func setSuspended(id: String, isSuspended: Bool) async throws {
        _ = try await deckRepository.setSuspended(id: id, isSuspended: isSuspended)
        await loadDecks()
    }
}
