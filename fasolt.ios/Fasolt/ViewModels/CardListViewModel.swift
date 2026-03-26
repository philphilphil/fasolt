import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "CardList")

@MainActor
@Observable
final class CardListViewModel {
    var cards: [CardDTO] = []
    var searchResults: [CardSearchResult]?
    var isLoading = false
    var errorMessage: String?
    var hasMore = false
    var searchText = "" {
        didSet { searchTextDidChange() }
    }

    private var nextCursor: String?
    private let apiClient: APIClient
    private var searchTask: Task<Void, Never>?

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

    var isSearching: Bool {
        !searchText.trimmingCharacters(in: .whitespaces).isEmpty
    }

    private func searchTextDidChange() {
        searchTask?.cancel()
        let query = searchText.trimmingCharacters(in: .whitespaces)
        if query.count < 2 {
            searchResults = nil
            return
        }
        searchTask = Task {
            try? await Task.sleep(for: .milliseconds(300))
            guard !Task.isCancelled else { return }
            await performSearch(query)
        }
    }

    private func performSearch(_ query: String) async {
        do {
            let endpoint = Endpoint(path: "/api/search", method: .get, queryItems: [
                URLQueryItem(name: "q", value: query)
            ])
            let response: SearchResponse = try await apiClient.request(endpoint)
            guard !Task.isCancelled else { return }
            searchResults = response.cards
        } catch {
            guard !Task.isCancelled else { return }
            logger.error("Search failed: \(error)")
            searchResults = nil
        }
    }
}
