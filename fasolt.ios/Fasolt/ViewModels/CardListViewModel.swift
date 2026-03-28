import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "CardList")

@MainActor
@Observable
final class CardListViewModel {
    var cards: [CardDTO] = []
    var isLoading = false
    var errorMessage: String?
    var searchText = ""
    var showSuspended = true
    var availableDecks: [DeckDTO] = []

    private let apiClient: APIClient
    private let cardRepository: CardRepository
    private let deckRepository: DeckRepository
    private static let maxCards = 5000

    init(apiClient: APIClient, cardRepository: CardRepository, deckRepository: DeckRepository) {
        self.apiClient = apiClient
        self.cardRepository = cardRepository
        self.deckRepository = deckRepository
    }

    var filteredCards: [CardDTO] {
        var result = cards

        if !showSuspended {
            result = result.filter { !$0.isSuspended }
        }

        let query = searchText.trimmingCharacters(in: .whitespaces)
        guard !query.isEmpty else { return result }
        return result.filter { card in
            card.front.localizedCaseInsensitiveContains(query) ||
            card.back.localizedCaseInsensitiveContains(query) ||
            (card.sourceFile?.localizedCaseInsensitiveContains(query) ?? false) ||
            (card.sourceHeading?.localizedCaseInsensitiveContains(query) ?? false)
        }
    }

    var isSearching: Bool {
        !searchText.trimmingCharacters(in: .whitespaces).isEmpty
    }

    func loadCards() async {
        guard !isLoading else { return }
        isLoading = true
        errorMessage = nil

        do {
            var allCards: [CardDTO] = []
            var cursor: String?

            repeat {
                var queryItems = [URLQueryItem(name: "limit", value: "200")]
                if let cursor {
                    queryItems.append(URLQueryItem(name: "after", value: cursor))
                }
                let endpoint = Endpoint(path: "/api/cards", method: .get, queryItems: queryItems)
                let response: PaginatedResponse<CardDTO> = try await apiClient.request(endpoint)
                allCards.append(contentsOf: response.items)
                cursor = response.hasMore ? response.nextCursor : nil
            } while cursor != nil && allCards.count < Self.maxCards

            cards = allCards
            logger.info("Loaded \(self.cards.count) cards")
        } catch {
            logger.error("Failed to load cards: \(error)")
            errorMessage = "Could not load cards. Pull to refresh."
        }

        isLoading = false
    }

    func loadDecks() async {
        do {
            availableDecks = try await deckRepository.fetchDecks()
        } catch {
            logger.error("Failed to load decks for picker: \(error)")
        }
    }

    func createCard(_ request: CreateCardRequest, deckId: String?) async throws {
        let card = try await cardRepository.createCard(request)
        if let deckId {
            _ = try await cardRepository.updateCard(
                id: card.id,
                UpdateCardRequest(
                    front: card.front,
                    back: card.back,
                    sourceFile: card.sourceFile,
                    sourceHeading: card.sourceHeading,
                    deckIds: [deckId]
                )
            )
        }
        await loadCards()
    }

    func deleteCard(id: String) async throws {
        try await cardRepository.deleteCard(id: id)
        cards.removeAll { $0.id == id }
    }

    func setSuspended(cardId: String, isSuspended: Bool) async throws {
        try await cardRepository.setSuspended(cardId: cardId, isSuspended: isSuspended)
        await loadCards()
    }

    func updateCard(id: String, _ request: UpdateCardRequest) async throws {
        _ = try await cardRepository.updateCard(id: id, request)
        await loadCards()
    }
}
