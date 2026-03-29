import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "Snapshots")

@MainActor
@Observable
final class SnapshotViewModel {
    var snapshots: [DeckSnapshotDTO] = []
    var isLoading = false
    var isCreating = false
    var errorMessage: String?
    var createSuccessCount: Int?

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func loadSnapshots() async {
        isLoading = true
        errorMessage = nil

        do {
            let endpoint = Endpoint(path: "/api/snapshots/recent", method: .get)
            snapshots = try await apiClient.request(endpoint)
            logger.info("Loaded \(self.snapshots.count) snapshots")
        } catch {
            logger.error("Failed to load snapshots: \(error)")
            errorMessage = "Could not load snapshots."
        }

        isLoading = false
    }

    func createSnapshot() async {
        isCreating = true
        errorMessage = nil
        createSuccessCount = nil

        do {
            let endpoint = Endpoint(path: "/api/snapshots", method: .post)
            let result: SnapshotCreateResultDTO = try await apiClient.request(endpoint)
            createSuccessCount = result.count
            logger.info("Created snapshots for \(result.count) decks")
            await loadSnapshots()
        } catch {
            logger.error("Failed to create snapshot: \(error)")
            errorMessage = "Could not create snapshot."
        }

        isCreating = false
    }
}
