import Foundation
import SwiftData

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
        return try await apiClient.request(endpoint)
    }

    func rateCard(cardId: String, rating: String) async throws -> RateCardResponse? {
        if networkMonitor.isConnected {
            do {
                let body = RateCardRequest(cardId: cardId, rating: rating)
                let endpoint = Endpoint(path: "/api/review/rate", method: .post, body: body)
                let response: RateCardResponse = try await apiClient.request(endpoint)
                return response
            } catch let error as APIError {
                switch error {
                case .networkError:
                    break
                default:
                    throw error
                }
            }
        }

        let pending = PendingReview(cardPublicId: cardId, rating: rating)
        modelContext.insert(pending)
        try modelContext.save()
        return nil
    }

    func flushPendingReviews() async throws -> Int {
        let descriptor = FetchDescriptor<PendingReview>(
            predicate: #Predicate { !$0.synced }
        )
        let pending = try modelContext.fetch(descriptor)
        return pending.count
    }
}
