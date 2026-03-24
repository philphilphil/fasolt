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
            logger.info("loadStats: fetching stats...")
            let statsEndpoint = Endpoint(path: "/api/review/stats", method: .get)
            let stats: ReviewStatsDTO = try await apiClient.request(statsEndpoint)
            logger.info("loadStats: stats OK — due=\(stats.dueCount)")

            logger.info("loadStats: fetching overview...")
            let overviewEndpoint = Endpoint(path: "/api/review/overview", method: .get)
            let overview: OverviewDTO = try await apiClient.request(overviewEndpoint)
            logger.info("loadStats: overview OK")

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
