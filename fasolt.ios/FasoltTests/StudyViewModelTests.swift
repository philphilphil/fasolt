import Testing
import Foundation
@testable import Fasolt

@MainActor
final class FakeStudyCardSource: StudyCardSource {
    var dueCalls: [(deckId: String?, limit: Int)] = []
    var customCalls: [String] = []
    var rateCalls: [(cardId: String, rating: String)] = []
    var suspendCalls: [(cardId: String, isSuspended: Bool)] = []

    var dueResult: [DueCardDTO] = []
    var customResult: [DueCardDTO] = []

    func fetchDueCards(deckId: String?, limit: Int) async throws -> [DueCardDTO] {
        dueCalls.append((deckId, limit))
        return dueResult
    }

    func fetchCustomCards(deckId: String) async throws -> [DueCardDTO] {
        customCalls.append(deckId)
        return customResult
    }

    func setSuspended(cardId: String, isSuspended: Bool) async throws {
        suspendCalls.append((cardId, isSuspended))
    }

    func rateCard(cardId: String, rating: String) async throws -> RateCardResponse? {
        rateCalls.append((cardId, rating))
        return nil
    }
}

private func makeCard(_ id: String) -> DueCardDTO {
    DueCardDTO(
        id: id,
        front: "Q\(id)",
        back: "A\(id)",
        sourceFile: nil,
        sourceHeading: nil,
        state: "new",
        frontSvg: nil,
        backSvg: nil
    )
}

@Suite("StudyViewModel — cram mode")
@MainActor
struct StudyViewModelCramTests {

    @Test("startSession with .cram calls fetchCustomCards, not fetchDueCards")
    func cramFetchesCustom() async {
        let fake = FakeStudyCardSource()
        fake.customResult = [makeCard("1"), makeCard("2")]
        let vm = StudyViewModel(cardRepository: fake)

        await vm.startSession(deckId: "deck-1", mode: .cram)

        #expect(fake.customCalls == ["deck-1"])
        #expect(fake.dueCalls.isEmpty)
        #expect(vm.mode == .cram)
        #expect(vm.cards.count == 2)
        #expect(vm.state == .studying)
    }

    @Test("startSession with .normal calls fetchDueCards, not fetchCustomCards")
    func normalFetchesDue() async {
        let fake = FakeStudyCardSource()
        fake.dueResult = [makeCard("1")]
        let vm = StudyViewModel(cardRepository: fake)

        await vm.startSession(deckId: "deck-1", mode: .normal)

        #expect(fake.dueCalls.count == 1)
        #expect(fake.customCalls.isEmpty)
        #expect(vm.mode == .normal)
    }

    @Test("advance() advances index and does NOT call rateCard")
    func advanceDoesNotRate() async {
        let fake = FakeStudyCardSource()
        fake.customResult = [makeCard("1"), makeCard("2")]
        let vm = StudyViewModel(cardRepository: fake)
        await vm.startSession(deckId: "deck-1", mode: .cram)

        vm.flipCard()
        #expect(vm.isFlipped == true)

        vm.advance()

        #expect(fake.rateCalls.isEmpty)
        #expect(vm.currentIndex == 1)
        #expect(vm.cardsStudied == 1)
        #expect(vm.isFlipped == false)
        #expect(vm.state == .studying)
    }

    @Test("advance() at end of queue transitions to .summary")
    func advanceFinishesSession() async {
        let fake = FakeStudyCardSource()
        fake.customResult = [makeCard("1")]
        let vm = StudyViewModel(cardRepository: fake)
        await vm.startSession(deckId: "deck-1", mode: .cram)

        vm.advance()

        #expect(vm.state == .summary)
        #expect(vm.cardsStudied == 1)
        #expect(fake.rateCalls.isEmpty)
    }

    @Test("cram session with empty deck goes straight to .summary")
    func cramEmptyDeck() async {
        let fake = FakeStudyCardSource()
        fake.customResult = []
        let vm = StudyViewModel(cardRepository: fake)

        await vm.startSession(deckId: "deck-1", mode: .cram)

        #expect(vm.state == .summary)
        #expect(vm.cards.isEmpty)
    }

    @Test("cram mode without deckId fails fast with errorMessage")
    func cramRequiresDeck() async {
        let fake = FakeStudyCardSource()
        let vm = StudyViewModel(cardRepository: fake)

        await vm.startSession(deckId: nil, mode: .cram)

        #expect(vm.state == .idle)
        #expect(vm.errorMessage != nil)
        #expect(fake.customCalls.isEmpty)
        #expect(fake.dueCalls.isEmpty)
    }
}
