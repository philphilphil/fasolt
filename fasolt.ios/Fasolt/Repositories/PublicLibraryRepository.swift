import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "PublicLibrary")

/// The public deck library: browse, and import a shared deck either as a copy
/// (an owned clone) or as a link (a live subscription to the author's deck).
///
/// Nothing here is cached — the library is a remote, search-driven surface, and a
/// stale listing is worse than an honest "could not load".
@MainActor
@Observable
final class PublicLibraryRepository {
    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func fetchDecks(query: String, sort: LibrarySort, page: Int) async throws -> LibraryListResponse {
        var items = [URLQueryItem(name: "page", value: String(page))]
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        if !trimmed.isEmpty { items.append(URLQueryItem(name: "q", value: trimmed)) }
        if sort == .recent { items.append(URLQueryItem(name: "sort", value: sort.rawValue)) }

        let endpoint = Endpoint(path: "/api/library", method: .get, queryItems: items)
        let response: LibraryListResponse = try await apiClient.request(endpoint)
        logger.info("Fetched \(response.items.count) public decks (page \(page))")
        return response
    }

    func fetchDeck(publicId: String) async throws -> LibraryDeckDetailDTO {
        let endpoint = Endpoint(path: "/api/library/decks/\(publicId)", method: .get)
        return try await apiClient.request(endpoint)
    }

    /// Clones the deck into the caller's account. The copy is fully owned: its cards
    /// are new, its progress starts fresh, and it stops following the author.
    func copyDeck(publicId: String) async throws -> DeckDTO {
        let endpoint = Endpoint(path: "/api/library/decks/\(publicId)/copy", method: .post)
        let deck: DeckDTO = try await apiClient.request(endpoint)
        logger.info("Copied library deck \(publicId) into deck \(deck.id)")
        return deck
    }

    /// Links the deck into the caller's account. Idempotent server-side.
    func subscribe(publicId: String) async throws -> DeckDTO {
        let endpoint = Endpoint(path: "/api/library/decks/\(publicId)/subscribe", method: .post)
        let deck: DeckDTO = try await apiClient.request(endpoint)
        logger.info("Linked library deck \(publicId)")
        return deck
    }
}

// MARK: - Error copy

enum LibraryImportError {
    /// Maps an import/link failure onto a sentence the user can act on. The backend
    /// reports its structured cases (`deck_too_large`, `own_deck`, …) with a human
    /// `message`, which APIClient surfaces as the `badRequest` detail.
    static func message(for error: any Error, fallback: String) -> String {
        guard let apiError = error as? APIError else { return fallback }
        switch apiError {
        case .badRequest(let detail):
            return detail ?? fallback
        case .forbidden:
            return "Verify your email address before importing decks."
        case .notFound:
            return "This deck is no longer shared."
        case .unauthorized:
            return "Your session expired. Sign in again to import decks."
        case .networkError:
            return "You appear to be offline. Try again when you're connected."
        default:
            return fallback
        }
    }
}
