import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "CardList")

@MainActor
@Observable
final class CardListViewModel {
    var cards: [CardDTO] = []
    var isLoading = false
    var errorMessage: String?
    var hasMore = false
    var searchText = ""

    private var nextCursor: String?
    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func loadCards() async {
        isLoading = true
        errorMessage = nil
        nextCursor = nil

        do {
            let endpoint = Endpoint(path: "/api/cards", method: .get, queryItems: [
                URLQueryItem(name: "limit", value: "50")
            ])
            let response: PaginatedResponse<CardDTO> = try await apiClient.request(endpoint)
            cards = response.items
            hasMore = response.hasMore
            nextCursor = response.nextCursor
            logger.info("Loaded \(self.cards.count) cards")
        } catch {
            logger.error("Failed to load cards: \(error)")
            errorMessage = "Could not load cards. Pull to refresh."
        }

        isLoading = false
    }

    func loadMore() async {
        guard hasMore, let cursor = nextCursor, !isLoading else { return }

        do {
            let endpoint = Endpoint(path: "/api/cards", method: .get, queryItems: [
                URLQueryItem(name: "limit", value: "50"),
                URLQueryItem(name: "after", value: cursor)
            ])
            let response: PaginatedResponse<CardDTO> = try await apiClient.request(endpoint)
            cards.append(contentsOf: response.items)
            hasMore = response.hasMore
            nextCursor = response.nextCursor
        } catch {
            logger.error("Failed to load more cards: \(error)")
            hasMore = false
        }
    }

    var filteredCards: [CardDTO] {
        if searchText.isEmpty { return cards }
        let query = searchText.lowercased()
        return cards.filter {
            $0.front.lowercased().contains(query) ||
            $0.back.lowercased().contains(query) ||
            ($0.sourceFile?.lowercased().contains(query) ?? false)
        }
    }
}
