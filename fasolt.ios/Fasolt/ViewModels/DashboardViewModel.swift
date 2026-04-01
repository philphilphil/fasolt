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
    var decks: [DeckDTO] = []
    var isLoading = false
    var errorMessage: String?
    var isCreatingDemo = false

    private let apiClient: APIClient
    private let deckRepository: DeckRepository

    init(apiClient: APIClient, deckRepository: DeckRepository) {
        self.apiClient = apiClient
        self.deckRepository = deckRepository
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
        async let decksResult: Result<[DeckDTO], Error> = {
            do { return .success(try await deckRepository.fetchDecks()) }
            catch { return .failure(error) }
        }()

        let (stats, overview, decksFetched) = await (statsResult, overviewResult, decksResult)

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

        if case .success(let d) = decksFetched {
            decks = d
        } else { failed = true }

        if failed {
            logger.error("Partial loadStats failure")
            errorMessage = "Some stats could not be loaded. Pull to refresh."
        }

        isLoading = false
    }

    func createDemoDeck() async {
        isCreatingDemo = true
        defer { isCreatingDemo = false }

        let endpoint = Endpoint(path: "/api/demo-deck", method: .post)
        do {
            let _: DeckDTO = try await apiClient.request(endpoint)
            await loadStats()
        } catch {
            errorMessage = "Could not create demo deck."
        }
    }
}
