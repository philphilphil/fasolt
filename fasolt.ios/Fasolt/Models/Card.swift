import Foundation
import SwiftData

@Model
final class Card {
    @Attribute(.unique) var publicId: String
    var front: String
    var back: String
    var sourceFile: String?
    var sourceHeading: String?
    var state: String
    var dueAt: Date?
    var stability: Double?
    var difficulty: Double?
    var step: Int?
    var lastReviewedAt: Date?
    var createdAt: Date
    var frontSvg: String?
    var backSvg: String?

    @Relationship(inverse: \CachedDeck.cards)
    var decks: [CachedDeck] = []

    init(
        publicId: String, front: String, back: String,
        sourceFile: String? = nil, sourceHeading: String? = nil,
        state: String = "new", dueAt: Date? = nil,
        stability: Double? = nil, difficulty: Double? = nil,
        step: Int? = nil, lastReviewedAt: Date? = nil,
        createdAt: Date = .now,
        frontSvg: String? = nil, backSvg: String? = nil
    ) {
        self.publicId = publicId
        self.front = front
        self.back = back
        self.sourceFile = sourceFile
        self.sourceHeading = sourceHeading
        self.state = state
        self.dueAt = dueAt
        self.stability = stability
        self.difficulty = difficulty
        self.step = step
        self.lastReviewedAt = lastReviewedAt
        self.createdAt = createdAt
        self.frontSvg = frontSvg
        self.backSvg = backSvg
    }
}
