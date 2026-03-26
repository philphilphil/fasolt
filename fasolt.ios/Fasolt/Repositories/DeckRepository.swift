import Foundation
import SwiftData
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "DeckRepository")

@MainActor
@Observable
final class DeckRepository {
    private let apiClient: APIClient
    private let networkMonitor: NetworkMonitor
    private let modelContext: ModelContext

    init(apiClient: APIClient, networkMonitor: NetworkMonitor, modelContext: ModelContext) {
        self.apiClient = apiClient
        self.networkMonitor = networkMonitor
        self.modelContext = modelContext
    }

    func fetchDecks() async throws -> [DeckDTO] {
        let endpoint = Endpoint(path: "/api/decks", method: .get)
        let decks: [DeckDTO] = try await apiClient.request(endpoint)
        logger.info("Fetched \(decks.count) decks from API")
        cacheDeckList(decks)
        return decks
    }

    func fetchDeckDetail(id: String) async throws -> DeckDetailDTO {
        let endpoint = Endpoint(path: "/api/decks/\(id)", method: .get)
        let detail: DeckDetailDTO = try await apiClient.request(endpoint)
        logger.info("Fetched deck detail '\(detail.name)' with \(detail.cards.count) cards")
        cacheDeckDetail(detail)
        return detail
    }

    // MARK: - Cache Write

    private func cacheDeckList(_ decks: [DeckDTO]) {
        let incomingIds = Set(decks.map(\.id))

        // Remove stale decks
        let allCached = try? modelContext.fetch(FetchDescriptor<CachedDeck>())
        for cached in allCached ?? [] {
            if !incomingIds.contains(cached.publicId) {
                modelContext.delete(cached)
            }
        }

        // Upsert
        for dto in decks {
            let dtoId = dto.id
            let predicate = #Predicate<CachedDeck> { $0.publicId == dtoId }
            let existing = try? modelContext.fetch(FetchDescriptor(predicate: predicate)).first

            if let deck = existing {
                deck.name = dto.name
                deck.deckDescription = dto.description
                deck.cardCount = dto.cardCount
                deck.dueCount = dto.dueCount
                deck.isActive = dto.isActive
            } else {
                let deck = CachedDeck(
                    publicId: dto.id,
                    name: dto.name,
                    deckDescription: dto.description,
                    cardCount: dto.cardCount,
                    dueCount: dto.dueCount,
                    createdAt: DateFormatters.parseISO8601(dto.createdAt) ?? {
                        logger.warning("Failed to parse createdAt '\(dto.createdAt)' for deck \(dto.id)")
                        return Date.now
                    }(),
                    isActive: dto.isActive
                )
                modelContext.insert(deck)
            }
        }

        do {
            try modelContext.save()
        } catch {
            logger.error("Failed to save deck list cache: \(error.localizedDescription)")
        }
    }

    private func cacheDeckDetail(_ detail: DeckDetailDTO) {
        let detailId = detail.id
        let predicate = #Predicate<CachedDeck> { $0.publicId == detailId }
        guard let deck = try? modelContext.fetch(FetchDescriptor(predicate: predicate)).first else { return }

        let incomingCardIds = Set(detail.cards.map(\.id))

        // Remove cards no longer in this deck
        for card in deck.cards where !incomingCardIds.contains(card.publicId) {
            card.decks.removeAll { $0.publicId == deck.publicId }
        }

        for cardDto in detail.cards {
            let cardDtoId = cardDto.id
            let cardPredicate = #Predicate<Card> { $0.publicId == cardDtoId }
            let existing = try? modelContext.fetch(FetchDescriptor(predicate: cardPredicate)).first

            if let card = existing {
                card.front = cardDto.front
                card.back = cardDto.back
                card.sourceFile = cardDto.sourceFile
                card.sourceHeading = cardDto.sourceHeading
                card.state = cardDto.state
                card.dueAt = cardDto.dueAt.flatMap { DateFormatters.parseISO8601($0) }
                card.stability = cardDto.stability
                card.difficulty = cardDto.difficulty
                card.step = cardDto.step
                card.lastReviewedAt = cardDto.lastReviewedAt.flatMap { DateFormatters.parseISO8601($0) }
                card.frontSvg = cardDto.frontSvg
                card.backSvg = cardDto.backSvg
                if !card.decks.contains(where: { $0.publicId == deck.publicId }) {
                    card.decks.append(deck)
                }
            } else {
                let card = Card(
                    publicId: cardDto.id,
                    front: cardDto.front,
                    back: cardDto.back,
                    sourceFile: cardDto.sourceFile,
                    sourceHeading: cardDto.sourceHeading,
                    state: cardDto.state,
                    dueAt: cardDto.dueAt.flatMap { DateFormatters.parseISO8601( $0) },
                    stability: cardDto.stability,
                    difficulty: cardDto.difficulty,
                    step: cardDto.step,
                    lastReviewedAt: cardDto.lastReviewedAt.flatMap { DateFormatters.parseISO8601( $0) },
                    frontSvg: cardDto.frontSvg,
                    backSvg: cardDto.backSvg
                )
                card.decks.append(deck)
                modelContext.insert(card)
            }
        }

        do {
            try modelContext.save()
        } catch {
            logger.error("Failed to save deck detail cache: \(error.localizedDescription)")
        }
    }

    // MARK: - Cache Read

    private func loadCachedDecks() -> [DeckDTO] {
        let descriptor = FetchDescriptor<CachedDeck>(sortBy: [SortDescriptor(\.name)])
        guard let cached = try? modelContext.fetch(descriptor) else { return [] }
        return cached.map { deck in
            DeckDTO(
                id: deck.publicId,
                name: deck.name,
                description: deck.deckDescription,
                cardCount: deck.cardCount,
                dueCount: deck.dueCount,
                createdAt: DateFormatters.formatISO8601( deck.createdAt),
                isActive: deck.isActive
            )
        }
    }

    private func loadCachedDeckDetail(id: String) -> DeckDetailDTO? {
        let predicate = #Predicate<CachedDeck> { $0.publicId == id }
        guard let deck = try? modelContext.fetch(FetchDescriptor(predicate: predicate)).first else { return nil }
        let cards = deck.cards.map { card in
            DeckCardDTO(
                id: card.publicId,
                front: card.front,
                back: card.back,
                sourceFile: card.sourceFile,
                sourceHeading: card.sourceHeading,
                state: card.state,
                dueAt: card.dueAt.map { DateFormatters.formatISO8601( $0) },
                stability: card.stability,
                difficulty: card.difficulty,
                step: card.step,
                lastReviewedAt: card.lastReviewedAt.map { DateFormatters.formatISO8601( $0) },
                frontSvg: card.frontSvg,
                backSvg: card.backSvg
            )
        }
        return DeckDetailDTO(
            id: deck.publicId,
            name: deck.name,
            description: deck.deckDescription,
            cardCount: deck.cardCount,
            dueCount: deck.dueCount,
            isActive: deck.isActive,
            cards: cards
        )
    }

    func setActive(id: String, isActive: Bool) async throws -> DeckDTO {
        let endpoint = Endpoint(
            path: "/api/decks/\(id)/active",
            method: .put,
            body: ["isActive": isActive]
        )
        let result: DeckDTO = try await apiClient.request(endpoint)

        // Update cache
        let predicate = #Predicate<CachedDeck> { $0.publicId == id }
        if let cached = try? modelContext.fetch(FetchDescriptor(predicate: predicate)).first {
            cached.isActive = result.isActive
            do {
                try modelContext.save()
            } catch {
                logger.error("Failed to save isActive state: \(error.localizedDescription)")
            }
        }

        return result
    }
}
