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
        // Token-guarded, unlike loadMore()'s reset: a superseded load() is always
        // followed by the newer load(), which owns the flag from that point on —
        // clearing it here would hide the spinner while the newer request runs.
        defer { if token == requestToken { isLoading = false } }

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
    }

    func loadMore() async {
        guard canLoadMore else { return }
        let token = requestToken
        isLoadingMore = true
        // Unconditional reset: if a subsequent load() supersedes this request (bumps
        // requestToken), this call's data must not apply, but its flag still must
        // clear — nothing else will ever reset isLoadingMore for it otherwise.
        defer { isLoadingMore = false }

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
            // Keep what's on screen and keep hasMore set: `page` did not advance, so
            // the next scroll asks for the same page again instead of ending the list
            // on one flaky request.
        }
    }
}
