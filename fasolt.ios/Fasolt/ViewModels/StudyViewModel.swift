import Foundation

@MainActor
protocol StudyCardSource: AnyObject {
    func fetchDueCards(deckId: String?, limit: Int) async throws -> [DueCardDTO]
    func fetchCustomCards(deckId: String) async throws -> [DueCardDTO]
    func setSuspended(cardId: String, isSuspended: Bool) async throws
    func rateCard(cardId: String, rating: String) async throws -> RateCardResponse?
}

extension CardRepository: StudyCardSource {}

@MainActor
@Observable
final class StudyViewModel {
    enum SessionState {
        case idle, loading, studying, flipped, summary
    }

    var state: SessionState = .idle
    var errorMessage: String?
    var ratingError: String?
    var cards: [DueCardDTO] = []
    var currentIndex: Int = 0
    var isFlipped: Bool = false
    var ratingsCount: [String: Int] = ["again": 0, "hard": 0, "good": 0, "easy": 0]
    var cardsStudied: Int = 0
    var failedRatings: Int = 0
    var skippedCount: Int = 0
    var suspendedCount: Int = 0
    var mode: StudyMode = .normal
    private(set) var isRating = false

    var notificationService: NotificationService?

    private var hasRequestedPermission: Bool {
        get { UserDefaults.standard.bool(forKey: "hasRequestedNotificationPermission") }
        set { UserDefaults.standard.set(newValue, forKey: "hasRequestedNotificationPermission") }
    }

    private let cardRepository: any StudyCardSource

    init(cardRepository: any StudyCardSource) {
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

    func startSession(deckId: String? = nil, mode: StudyMode = .normal) async {
        state = .loading
        errorMessage = nil
        self.mode = mode
        do {
            switch mode {
            case .normal:
                cards = try await cardRepository.fetchDueCards(deckId: deckId, limit: 50)
            case .cram:
                guard let deckId else {
                    errorMessage = "A deck is required for custom study."
                    state = .idle
                    return
                }
                cards = try await cardRepository.fetchCustomCards(deckId: deckId)
            }
            if cards.isEmpty {
                state = .summary
            } else {
                currentIndex = 0
                isFlipped = false
                cardsStudied = 0
                failedRatings = 0
                skippedCount = 0
                suspendedCount = 0
                ratingsCount = ["again": 0, "hard": 0, "good": 0, "easy": 0]
                state = .studying
            }
        } catch {
            errorMessage = "Could not load cards. Check your connection."
            state = .idle
        }
    }

    func advance() {
        cardsStudied += 1
        currentIndex += 1
        isFlipped = false
        if currentIndex >= cards.count {
            state = .summary
        } else {
            state = .studying
        }
    }

    func flipCard() {
        isFlipped = true
        state = .flipped
    }

    func skipCard() {
        skippedCount += 1
        currentIndex += 1
        isFlipped = false
        if currentIndex >= cards.count {
            state = .summary
            if cardsStudied > 0 {
                requestNotificationPermissionIfNeeded()
            }
        } else {
            state = .studying
        }
    }

    func suspendCard() async {
        guard let card = currentCard else { return }
        do {
            try await cardRepository.setSuspended(cardId: card.id, isSuspended: true)
        } catch {
            // best-effort — still skip the card
        }
        suspendedCount += 1
        currentIndex += 1
        isFlipped = false
        if currentIndex >= cards.count {
            state = .summary
            if cardsStudied > 0 {
                requestNotificationPermissionIfNeeded()
            }
        } else {
            state = .studying
        }
    }

    func rateCard(_ rating: String) async {
        guard let card = currentCard, !isRating else { return }
        isRating = true
        defer { isRating = false }
        ratingError = nil

        // Advance state immediately (optimistic update) so UI feels responsive
        ratingsCount[rating, default: 0] += 1
        cardsStudied += 1
        currentIndex += 1
        isFlipped = false
        if currentIndex >= cards.count {
            state = .summary
            requestNotificationPermissionIfNeeded()
        } else {
            state = .studying
        }

        // Submit rating to server in background — offline queue handles failures
        do {
            _ = try await cardRepository.rateCard(cardId: card.id, rating: rating)
        } catch {
            failedRatings += 1
            ratingError = "Rating may not have been saved."
        }
    }

    private func requestNotificationPermissionIfNeeded() {
        guard !hasRequestedPermission else { return }
        hasRequestedPermission = true
        Task {
            await notificationService?.requestPermissionAndRegister()
        }
    }

    func exitSession() {
        state = .idle
    }
}
