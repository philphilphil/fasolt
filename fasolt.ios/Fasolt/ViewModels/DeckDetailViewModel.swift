import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "DeckDetail")

@MainActor
@Observable
final class DeckDetailViewModel {
    var detail: DeckDetailDTO?
    var isLoading = false
    var errorMessage: String?

    private let deckRepository: DeckRepository
    let deckId: String
    let deckName: String

    init(deckRepository: DeckRepository, deckId: String, deckName: String) {
        self.deckRepository = deckRepository
        self.deckId = deckId
        self.deckName = deckName
    }

    func loadDetail() async {
        isLoading = true
        errorMessage = nil

        do {
            detail = try await deckRepository.fetchDeckDetail(id: deckId)
            logger.info("Loaded deck detail: \(self.detail?.cards.count ?? 0) cards")
        } catch {
            logger.error("Failed to load deck detail: \(error)")
            errorMessage = "Could not load deck. Pull to refresh."
        }

        isLoading = false
    }

    func toggleActive() async {
        guard let current = detail else { return }
        let newState = !current.isActive

        do {
            _ = try await deckRepository.setActive(id: deckId, isActive: newState)
            await loadDetail() // Reload to get fresh data
        } catch {
            logger.error("Failed to toggle deck active state: \(error)")
            errorMessage = "Could not update deck status."
        }
    }
}
