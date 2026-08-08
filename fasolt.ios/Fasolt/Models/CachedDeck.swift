import Foundation
import SwiftData

@Model
final class CachedDeck {
    @Attribute(.unique) var publicId: String
    var name: String
    var deckDescription: String?
    var cardCount: Int
    var dueCount: Int
    var createdAt: Date
    var isSuspended: Bool = false

    /// Sharing state, cached alongside the rest: a linked deck belongs to
    /// `authorHandle` and is read-only, and the offline UI has to know that — every
    /// edit affordance is keyed off it. Defaults keep the store lightly migratable.
    var isLinked: Bool = false
    var authorHandle: String?
    var copiedFromHandle: String?

    var cards: [Card] = []

    init(
        publicId: String, name: String, deckDescription: String? = nil,
        cardCount: Int = 0, dueCount: Int = 0, createdAt: Date = .now,
        isSuspended: Bool = false,
        isLinked: Bool = false, authorHandle: String? = nil, copiedFromHandle: String? = nil
    ) {
        self.publicId = publicId
        self.name = name
        self.deckDescription = deckDescription
        self.cardCount = cardCount
        self.dueCount = dueCount
        self.createdAt = createdAt
        self.isSuspended = isSuspended
        self.isLinked = isLinked
        self.authorHandle = authorHandle
        self.copiedFromHandle = copiedFromHandle
    }
}
