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

    private let apiClient: APIClient
    private static let maxCards = 5000

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    var filteredCards: [CardDTO] {
        let query = searchText.trimmingCharacters(in: .whitespaces)
        guard !query.isEmpty else { return cards }
        return cards.filter { card in
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
}
