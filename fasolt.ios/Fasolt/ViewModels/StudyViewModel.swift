import Foundation

@MainActor
@Observable
final class StudyViewModel {
    enum SessionState {
        case idle, loading, studying, flipped, summary
    }

    var state: SessionState = .idle
    var errorMessage: String?
    var cards: [DueCardDTO] = []
    var currentIndex: Int = 0
    var isFlipped: Bool = false
    var ratingsCount: [String: Int] = ["again": 0, "hard": 0, "good": 0, "easy": 0]
    var cardsStudied: Int = 0

    private let cardRepository: CardRepository

    init(cardRepository: CardRepository) {
        self.cardRepository = cardRepository
    }

    var currentCard: DueCardDTO? {
        guard currentIndex < cards.count else { return nil }
        return cards[currentIndex]
    }

    var progress: Double {
        guard !cards.isEmpty else { return 0 }
        return Double(currentIndex) / Double(cards.count)
    }

    var totalCards: Int { cards.count }

    func startSession(deckId: String? = nil) async {
        state = .loading
        errorMessage = nil
        do {
            cards = try await cardRepository.fetchDueCards(deckId: deckId)
            if cards.isEmpty {
                state = .summary
            } else {
                currentIndex = 0
                isFlipped = false
                cardsStudied = 0
                ratingsCount = ["again": 0, "hard": 0, "good": 0, "easy": 0]
                state = .studying
            }
        } catch {
            errorMessage = "Could not load cards. Check your connection."
            state = .idle
        }
    }

    func flipCard() {
        isFlipped = true
        state = .flipped
    }

    func rateCard(_ rating: String) async {
        guard let card = currentCard else { return }
        do {
            _ = try await cardRepository.rateCard(cardId: card.id, rating: rating)
        } catch {
            // Non-network error — continue session anyway
        }
        ratingsCount[rating, default: 0] += 1
        cardsStudied += 1
        currentIndex += 1
        isFlipped = false
        if currentIndex >= cards.count {
            state = .summary
        } else {
            state = .studying
        }
    }

    func exitSession() {
        state = .idle
    }
}
