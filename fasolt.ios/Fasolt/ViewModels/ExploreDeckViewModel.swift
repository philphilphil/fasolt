import Foundation
import os

private let logger = Logger(subsystem: "com.fasolt.app", category: "ExploreDeck")

/// How the caller already relates to a shared deck — it decides which import
/// actions make sense.
enum SharedDeckRelation: Sendable {
    /// Not yet resolved, or the caller has neither copied nor linked this deck.
    case notImported
    /// The caller owns this deck; it is theirs to publish, not to import.
    case owner
    /// The caller already links this deck.
    case linked
}

@MainActor
@Observable
final class ExploreDeckViewModel {
    let publicId: String

    var deck: LibraryDeckDetailDTO?
    var relation: SharedDeckRelation = .notImported
    var isLoading = false
    var errorMessage: String?

    /// Non-nil once a copy succeeded — the copy is a new, separate deck.
    var copiedDeck: DeckDTO?
    /// True when `copiedDeck` came from converting an existing link rather than a fresh copy.
    var copiedFromLink = false
    var importingMode: ImportMode?
    var isUnlinking = false
    var importError: String?

    enum ImportMode: Sendable { case copy, link }

    /// Any import or unlink in flight — the actions stay disabled until it settles.
    var isBusy: Bool { importingMode != nil || isUnlinking }

    private let repository: PublicLibraryRepository
    private let deckRepository: DeckRepository

    init(publicId: String, repository: PublicLibraryRepository, deckRepository: DeckRepository) {
        self.publicId = publicId
        self.repository = repository
        self.deckRepository = deckRepository
    }

    func load() async {
        isLoading = true
        errorMessage = nil

        do {
            deck = try await repository.fetchDeck(publicId: publicId)
        } catch {
            logger.error("Failed to load public deck \(self.publicId): \(error)")
            errorMessage = "Could not load this deck. Pull to refresh."
        }

        isLoading = false
        await refreshRelation()
    }

    /// Owner/linked state comes from the caller's own deck list — a linked deck keeps
    /// the author's public id, so a plain id match is enough to tell them apart.
    func refreshRelation() async {
        do {
            let mine = try await deckRepository.fetchDecks()
            guard let match = mine.first(where: { $0.id == publicId }) else {
                relation = .notImported
                return
            }
            relation = match.isLinked ? .linked : .owner
        } catch {
            // Leave the actions in their default state; the import call reports the
            // real error if there is one.
            logger.error("Could not resolve deck relation: \(error)")
        }
    }

    func copyToMyDecks() async {
        guard !isBusy else { return }
        // Capture before the call — a successful convert removes the link, so
        // `relation` won't read `.linked` anymore once we refresh below.
        let wasLinked = relation == .linked
        importingMode = .copy
        importError = nil

        do {
            copiedDeck = try await repository.copyDeck(publicId: publicId)
            copiedFromLink = wasLinked
            if let deck {
                // Reflect the new import without a full round-trip.
                self.deck = LibraryDeckDetailDTO(
                    id: deck.id,
                    name: deck.name,
                    description: deck.description,
                    authorHandle: deck.authorHandle,
                    cardCount: deck.cardCount,
                    copyCount: deck.copyCount + 1,
                    visibility: deck.visibility,
                    publishedAt: deck.publishedAt,
                    sampleCards: deck.sampleCards
                )
            }
            if wasLinked {
                // The link is gone now — refresh so the view stops offering "Unlink"
                // and drops the stale "Linked" status line.
                await refreshRelation()
            }
        } catch {
            logger.error("Copy failed for \(self.publicId): \(error)")
            importError = LibraryImportError.message(
                for: error,
                fallback: "Could not copy this deck. Please try again."
            )
        }

        importingMode = nil
    }

    /// Undoes a link from the screen that created it. Progress made through the link
    /// goes with it, which the confirmation in the view spells out.
    func unlinkDeck() async {
        guard !isBusy else { return }
        isUnlinking = true
        importError = nil

        do {
            try await deckRepository.unlink(id: publicId)
            relation = .notImported
        } catch {
            logger.error("Unlink failed for \(self.publicId): \(error)")
            importError = LibraryImportError.message(
                for: error,
                fallback: "Could not unlink this deck. Please try again."
            )
        }

        isUnlinking = false
    }

    func linkDeck() async {
        guard !isBusy else { return }
        importingMode = .link
        importError = nil
        copiedDeck = nil
        copiedFromLink = false

        do {
            _ = try await repository.subscribe(publicId: publicId)
            relation = .linked
        } catch {
            logger.error("Link failed for \(self.publicId): \(error)")
            importError = LibraryImportError.message(
                for: error,
                fallback: "Could not link this deck. Please try again."
            )
        }

        importingMode = nil
    }
}
