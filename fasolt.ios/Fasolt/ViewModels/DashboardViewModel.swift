import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "Dashboard")

@MainActor
@Observable
final class DashboardViewModel {
    var dueCount: Int = 0
    var totalCards: Int = 0
    var studiedToday: Int = 0
    var cardsByState: [String: Int] = [:]
    var totalDecks: Int = 0
    var isLoading = false
    var errorMessage: String?

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func loadStats() async {
        isLoading = true
        errorMessage = nil

        do {
            let statsEndpoint = Endpoint(path: "/api/review/stats", method: .get)
            let overviewEndpoint = Endpoint(path: "/api/review/overview", method: .get)

            async let statsTask: ReviewStatsDTO = apiClient.request(statsEndpoint)
            async let overviewTask: OverviewDTO = apiClient.request(overviewEndpoint)

            let (stats, overview) = try await (statsTask, overviewTask)

            dueCount = stats.dueCount
            totalCards = stats.totalCards
            studiedToday = stats.studiedToday
            cardsByState = overview.cardsByState
            totalDecks = overview.totalDecks
        } catch {
            logger.error("loadStats error: \(error)")
            errorMessage = "Could not load stats. Pull to refresh."
        }

        isLoading = false
    }
}
