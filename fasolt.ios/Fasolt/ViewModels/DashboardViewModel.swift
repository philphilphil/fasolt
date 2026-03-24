import Foundation

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
            async let statsResult: ReviewStatsDTO = {
                let endpoint = Endpoint(path: "/api/review/stats", method: .get)
                return try await apiClient.request(endpoint)
            }()

            async let overviewResult: OverviewDTO = {
                let endpoint = Endpoint(path: "/api/review/overview", method: .get)
                return try await apiClient.request(endpoint)
            }()

            let stats = try await statsResult
            let overview = try await overviewResult

            dueCount = stats.dueCount
            totalCards = stats.totalCards
            studiedToday = stats.studiedToday
            cardsByState = overview.cardsByState
            totalDecks = overview.totalDecks
        } catch {
            errorMessage = "Could not load stats. Pull to refresh."
        }

        isLoading = false
    }
}
