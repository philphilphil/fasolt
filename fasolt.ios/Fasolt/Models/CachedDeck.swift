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

    var cards: [Card] = []

    init(
        publicId: String, name: String, deckDescription: String? = nil,
        cardCount: Int = 0, dueCount: Int = 0, createdAt: Date = .now,
        isSuspended: Bool = false
    ) {
        self.publicId = publicId
        self.name = name
        self.deckDescription = deckDescription
        self.cardCount = cardCount
        self.dueCount = dueCount
        self.createdAt = createdAt
        self.isSuspended = isSuspended
    }
}
