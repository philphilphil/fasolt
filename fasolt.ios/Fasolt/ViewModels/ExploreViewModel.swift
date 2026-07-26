import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "Explore")

/// Browsing the public deck library: search, sort, and paged loading.
@MainActor
@Observable
final class ExploreViewModel {
    var decks: [LibraryDeckDTO] = []
    var searchText = ""
    var sort: LibrarySort = .popular
    var isLoading = false
    var isLoadingMore = false
    var errorMessage: String?
    var totalCount = 0

    private var page = 1
    private var hasMore = false
    /// Guards against a stale response from an earlier query overwriting a newer one.
    private var requestToken = 0

    private let repository: PublicLibraryRepository

    init(repository: PublicLibraryRepository) {
        self.repository = repository
    }

    var canLoadMore: Bool { hasMore && !isLoading && !isLoadingMore }

    func load() async {
        requestToken += 1
        let token = requestToken
        page = 1
        isLoading = true
        errorMessage = nil

        do {
            let response = try await repository.fetchDecks(query: searchText, sort: sort, page: 1)
            guard token == requestToken else { return }
            decks = response.items
            totalCount = response.totalCount
            hasMore = response.page * response.pageSize < response.totalCount
            logger.info("Loaded \(response.items.count) of \(response.totalCount) public decks")
        } catch {
            guard token == requestToken else { return }
            logger.error("Failed to load public decks: \(error)")
            errorMessage = "Could not load the library. Pull to refresh."
        }

        if token == requestToken { isLoading = false }
    }

    func loadMore() async {
        guard canLoadMore else { return }
        let token = requestToken
        isLoadingMore = true

        do {
            let response = try await repository.fetchDecks(query: searchText, sort: sort, page: page + 1)
            guard token == requestToken else { return }
            page = response.page
            decks.append(contentsOf: response.items)
            totalCount = response.totalCount
            hasMore = response.page * response.pageSize < response.totalCount
        } catch {
            guard token == requestToken else { return }
            logger.error("Failed to load more public decks: \(error)")
            // Keep what's on screen; the next scroll retries.
            hasMore = false
        }

        if token == requestToken { isLoadingMore = false }
    }
}
