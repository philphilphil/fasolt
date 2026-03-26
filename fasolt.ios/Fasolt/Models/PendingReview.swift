import Foundation
import SwiftData

@Model
final class PendingReview {
    @Attribute(.unique) var cardPublicId: String
    var rating: String
    var reviewedAt: Date
    var synced: Bool

    init(cardPublicId: String, rating: String, reviewedAt: Date = .now, synced: Bool = false) {
        self.cardPublicId = cardPublicId
        self.rating = rating
        self.reviewedAt = reviewedAt
        self.synced = synced
    }
}
