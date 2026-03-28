import Foundation
import SwiftData
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "CardRepository")

@MainActor
@Observable
final class CardRepository {
    private let apiClient: APIClient
    private let networkMonitor: NetworkMonitor
    private let modelContext: ModelContext

    init(apiClient: APIClient, networkMonitor: NetworkMonitor, modelContext: ModelContext) {
        self.apiClient = apiClient
        self.networkMonitor = networkMonitor
        self.modelContext = modelContext
    }

    func fetchDueCards(deckId: String? = nil, limit: Int = 50) async throws -> [DueCardDTO] {
        var queryItems = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let deckId {
            queryItems.append(URLQueryItem(name: "deckId", value: deckId))
        }
        let endpoint = Endpoint(path: "/api/review/due", method: .get, queryItems: queryItems)
        let cards: [DueCardDTO] = try await apiClient.request(endpoint)
        logger.info("Fetched \(cards.count) due cards")
        return cards
    }

    func createCard(_ request: CreateCardRequest) async throws -> CardDTO {
        let endpoint = Endpoint(path: "/api/cards", method: .post, body: request)
        let card: CardDTO = try await apiClient.request(endpoint)
        logger.info("Created card \(card.id)")
        return card
    }

    func updateCard(id: String, _ request: UpdateCardRequest) async throws -> CardDTO {
        let endpoint = Endpoint(path: "/api/cards/\(id)", method: .put, body: request)
        let card: CardDTO = try await apiClient.request(endpoint)
        logger.info("Updated card \(card.id)")
        return card
    }

    func deleteCard(id: String) async throws {
        let endpoint = Endpoint(path: "/api/cards/\(id)", method: .delete)
        let _ = try await apiClient.request(endpoint) as EmptyResponse
        logger.info("Deleted card \(id)")
    }

    func setSuspended(cardId: String, isSuspended: Bool) async throws {
        let endpoint = Endpoint(
            path: "/api/cards/\(cardId)/suspended",
            method: .put,
            body: SetSuspendedRequest(isSuspended: isSuspended)
        )
        struct SuspendResponse: Decodable { let id: String }
        let _ = try await apiClient.request(endpoint) as SuspendResponse
        logger.info("\(isSuspended ? "Suspended" : "Unsuspended") card \(cardId)")
    }

    func rateCard(cardId: String, rating: String) async throws -> RateCardResponse? {
        if networkMonitor.isConnected {
            do {
                let body = RateCardRequest(cardId: cardId, rating: rating)
                let endpoint = Endpoint(path: "/api/review/rate", method: .post, body: body)
                let response: RateCardResponse = try await apiClient.request(endpoint)
                logger.info("Rated card \(cardId) as \(rating)")
                return response
            } catch let error as APIError {
                switch error {
                case .networkError:
                    logger.warning("Network error rating card \(cardId), queueing offline")
                default:
                    // Non-transient errors (400, 403, etc.) — don't queue, rethrow
                    throw error
                }
            }
        }

        logger.info("Queueing offline review for card \(cardId) with rating \(rating)")
        let descriptor = FetchDescriptor<PendingReview>(
            predicate: #Predicate { $0.cardPublicId == cardId && $0.synced == false }
        )
        let existing = (try? modelContext.fetch(descriptor)) ?? []
        for old in existing {
            modelContext.delete(old)
        }
        let pending = PendingReview(cardPublicId: cardId, rating: rating)
        modelContext.insert(pending)
        try modelContext.save()
        return nil
    }
}
