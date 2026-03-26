import Foundation
import SwiftData
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "SyncService")

@MainActor
@Observable
final class SyncService {
    private let apiClient: APIClient
    private let networkMonitor: NetworkMonitor
    private let modelContext: ModelContext
    var pendingCount: Int = 0

    private var wasConnected = true
    private var isFlushing = false

    init(apiClient: APIClient, networkMonitor: NetworkMonitor, modelContext: ModelContext) {
        self.apiClient = apiClient
        self.networkMonitor = networkMonitor
        self.modelContext = modelContext
    }

    func startMonitoring() async {
        updatePendingCount()

        // Flush immediately if we have pending items and are online
        if networkMonitor.isConnected && pendingCount > 0 {
            await flushPendingReviews()
        }

        // Watch for connectivity changes
        while !Task.isCancelled {
            let isConnected = networkMonitor.isConnected

            if isConnected && !wasConnected {
                logger.info("Connectivity restored — flushing pending reviews")
                await flushPendingReviews()
            }

            wasConnected = isConnected
            try? await Task.sleep(for: .seconds(30))
        }
    }

    func flushPendingReviews() async {
        guard !isFlushing else { return }
        isFlushing = true
        defer { isFlushing = false }

        let descriptor = FetchDescriptor<PendingReview>(
            predicate: #Predicate<PendingReview> { !$0.synced }
        )
        guard let pending = try? modelContext.fetch(descriptor), !pending.isEmpty else {
            pendingCount = 0
            return
        }

        logger.info("Flushing \(pending.count) pending reviews")

        flushLoop: for review in pending {
            do {
                let body = RateCardRequest(cardId: review.cardPublicId, rating: review.rating)
                let endpoint = Endpoint(path: "/api/review/rate", method: .post, body: body)
                let _: RateCardResponse = try await apiClient.request(endpoint)
                modelContext.delete(review)
                logger.info("Synced review for card \(review.cardPublicId)")
            } catch let error as APIError {
                switch error {
                case .notFound:
                    // Card deleted on server — discard silently
                    modelContext.delete(review)
                    logger.info("Card \(review.cardPublicId) not found on server — discarded")
                case .networkError:
                    // Lost connectivity mid-flush — stop, retry later
                    logger.info("Lost connectivity during flush — will retry")
                    break flushLoop
                default:
                    logger.error("Failed to sync review for \(review.cardPublicId): \(error)")
                }
            } catch {
                logger.error("Unexpected error syncing review: \(error)")
            }
        }

        try? modelContext.save()
        updatePendingCount()
    }

    private func updatePendingCount() {
        let descriptor = FetchDescriptor<PendingReview>(
            predicate: #Predicate<PendingReview> { !$0.synced }
        )
        pendingCount = (try? modelContext.fetchCount(descriptor)) ?? 0
    }
}
