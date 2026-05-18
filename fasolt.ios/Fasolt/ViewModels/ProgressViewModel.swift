import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "Progress")

@MainActor
@Observable
final class ProgressViewModel {
    var progress: ProgressDTO?
    var isLoading = false
    var errorMessage: String?

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func load(days: Int = 364) async {
        isLoading = true
        errorMessage = nil
        defer { isLoading = false }

        let endpoint = Endpoint(
            path: "/api/review/progress",
            method: .get,
            queryItems: [URLQueryItem(name: "days", value: "\(days)")]
        )
        do {
            progress = try await apiClient.request(endpoint)
        } catch {
            logger.error("Failed to load progress: \(String(describing: error))")
            errorMessage = "Could not load progress."
        }
    }
}
