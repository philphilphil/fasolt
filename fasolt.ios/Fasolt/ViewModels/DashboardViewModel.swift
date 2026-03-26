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

        let statsEndpoint = Endpoint(path: "/api/review/stats", method: .get)
        let overviewEndpoint = Endpoint(path: "/api/review/overview", method: .get)

        // Load independently so one failure doesn't block the other
        async let statsResult: Result<ReviewStatsDTO, Error> = {
            do { return .success(try await apiClient.request(statsEndpoint)) }
            catch { return .failure(error) }
        }()
        async let overviewResult: Result<OverviewDTO, Error> = {
            do { return .success(try await apiClient.request(overviewEndpoint)) }
            catch { return .failure(error) }
        }()

        let (stats, overview) = await (statsResult, overviewResult)

        var failed = false
        if case .success(let s) = stats {
            dueCount = s.dueCount
            totalCards = s.totalCards
            studiedToday = s.studiedToday
        } else { failed = true }

        if case .success(let o) = overview {
            cardsByState = o.cardsByState
            totalDecks = o.totalDecks
        } else { failed = true }

        if failed {
            logger.error("Partial loadStats failure")
            errorMessage = "Some stats could not be loaded. Pull to refresh."
        }

        isLoading = false
    }
}
