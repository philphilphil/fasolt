import Foundation

// MARK: - Auth

struct TokenResponse: Decodable, Sendable {
    let accessToken: String
    let refreshToken: String?
    let expiresIn: Int
    let tokenType: String

    enum CodingKeys: String, CodingKey {
        case accessToken = "access_token"
        case refreshToken = "refresh_token"
        case expiresIn = "expires_in"
        case tokenType = "token_type"
    }
}

// MARK: - Cards

struct CardDTO: Decodable, Sendable, Identifiable {
    let id: String
    let front: String
    let back: String
    let sourceFile: String?
    let state: String
    let dueAt: String?
    let stability: Double?
    let difficulty: Double?
    let step: Int?
    let lastReviewedAt: String?
    let createdAt: String
    let decks: [CardDeckInfoDTO]
    let isSuspended: Bool
    let frontSvg: String?
    let backSvg: String?
}

struct CardDeckInfoDTO: Decodable, Sendable {
    let id: String
    let name: String
    let isSuspended: Bool
}

// MARK: - Decks

/// A deck in the caller's own list. When `isLinked` is true the deck belongs to
/// `authorHandle`: the counts and `isSuspended` are still the caller's own, but the
/// content is read-only.
///
/// The sharing fields arrived with the public library; servers older than that omit
/// them, so they are decoded leniently (see `init(from:)` below).
struct DeckDTO: Decodable, Sendable {
    let id: String
    let name: String
    let description: String?
    let cardCount: Int
    let dueCount: Int
    let createdAt: String
    let isSuspended: Bool
    var visibility: String = DeckVisibility.private_
    var copiedFromHandle: String? = nil
    var isLinked: Bool = false
    var authorHandle: String? = nil
}

extension DeckDTO {
    private enum CodingKeys: String, CodingKey {
        case id, name, description, cardCount, dueCount, createdAt, isSuspended
        case visibility, copiedFromHandle, isLinked, authorHandle
    }

    init(from decoder: any Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id = try c.decode(String.self, forKey: .id)
        name = try c.decode(String.self, forKey: .name)
        description = try c.decodeIfPresent(String.self, forKey: .description)
        cardCount = try c.decode(Int.self, forKey: .cardCount)
        dueCount = try c.decode(Int.self, forKey: .dueCount)
        createdAt = try c.decode(String.self, forKey: .createdAt)
        isSuspended = try c.decode(Bool.self, forKey: .isSuspended)
        visibility = try c.decodeIfPresent(String.self, forKey: .visibility) ?? DeckVisibility.private_
        copiedFromHandle = try c.decodeIfPresent(String.self, forKey: .copiedFromHandle)
        isLinked = try c.decodeIfPresent(Bool.self, forKey: .isLinked) ?? false
        authorHandle = try c.decodeIfPresent(String.self, forKey: .authorHandle)
    }
}

struct DeckDetailDTO: Decodable, Sendable {
    let id: String
    let name: String
    let description: String?
    let cardCount: Int
    let dueCount: Int
    let isSuspended: Bool
    let cards: [DeckCardDTO]
    var visibility: String = DeckVisibility.private_
    var copiedFromHandle: String? = nil
    var isLinked: Bool = false
    var authorHandle: String? = nil
}

extension DeckDetailDTO {
    private enum CodingKeys: String, CodingKey {
        case id, name, description, cardCount, dueCount, isSuspended, cards
        case visibility, copiedFromHandle, isLinked, authorHandle
    }

    init(from decoder: any Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id = try c.decode(String.self, forKey: .id)
        name = try c.decode(String.self, forKey: .name)
        description = try c.decodeIfPresent(String.self, forKey: .description)
        cardCount = try c.decode(Int.self, forKey: .cardCount)
        dueCount = try c.decode(Int.self, forKey: .dueCount)
        isSuspended = try c.decode(Bool.self, forKey: .isSuspended)
        cards = try c.decodeIfPresent([DeckCardDTO].self, forKey: .cards) ?? []
        visibility = try c.decodeIfPresent(String.self, forKey: .visibility) ?? DeckVisibility.private_
        copiedFromHandle = try c.decodeIfPresent(String.self, forKey: .copiedFromHandle)
        isLinked = try c.decodeIfPresent(Bool.self, forKey: .isLinked) ?? false
        authorHandle = try c.decodeIfPresent(String.self, forKey: .authorHandle)
    }
}

/// Wire values of the server's deck visibility enum.
enum DeckVisibility {
    // `private` is a keyword; the trailing underscore keeps the wire value readable.
    static let private_ = "private"
    static let unlisted = "unlisted"
}

struct DeckCardDTO: Decodable, Sendable, Identifiable {
    let id: String
    let front: String
    let back: String
    let sourceFile: String?
    let state: String
    let dueAt: String?
    let isSuspended: Bool
    let stability: Double?
    let difficulty: Double?
    let step: Int?
    let lastReviewedAt: String?
    let frontSvg: String?
    let backSvg: String?
}

// MARK: - Public library

/// A public deck as served by `/api/library`. Carries no source-file provenance —
/// nothing here may leak the author's vault.
struct LibraryDeckDTO: Decodable, Sendable, Identifiable {
    let id: String
    let name: String
    let description: String?
    let authorHandle: String?
    let cardCount: Int
    let copyCount: Int
    let publishedAt: String?
}

struct LibraryListResponse: Decodable, Sendable {
    let items: [LibraryDeckDTO]
    let totalCount: Int
    let page: Int
    let pageSize: Int
}

struct LibrarySampleCardDTO: Decodable, Sendable {
    let front: String
    let back: String
    let frontSvg: String?
    let backSvg: String?
}

struct LibraryDeckDetailDTO: Decodable, Sendable {
    let id: String
    let name: String
    let description: String?
    let authorHandle: String?
    let cardCount: Int
    let copyCount: Int
    let visibility: String
    let publishedAt: String?
    let sampleCards: [LibrarySampleCardDTO]
}

enum LibrarySort: String, CaseIterable, Sendable {
    case popular
    case recent

    var label: String {
        switch self {
        case .popular: return "Popular"
        case .recent: return "Recent"
        }
    }
}

// MARK: - Review

struct DueCardDTO: Decodable, Sendable {
    let id: String
    let front: String
    let back: String
    let sourceFile: String?
    let state: String
    let frontSvg: String?
    let backSvg: String?
}

struct RateCardRequest: Encodable, Sendable {
    let cardId: String
    let rating: String
}

struct RateCardResponse: Decodable, Sendable {
    let cardId: String
    let stability: Double?
    let difficulty: Double?
    let dueAt: String?
    let state: String
}

struct ReviewStatsDTO: Decodable, Sendable {
    let dueCount: Int
    let totalCards: Int
    let studiedToday: Int
}

struct StudyStatsDTO: Decodable, Sendable {
    let currentStreak: Int
    let bestStreak: Int
    let totalAnswered: Int
    let answeredToday: Int
    let last7Days: [DailyActivityDTO]
}

// MARK: - Progress

struct DailyActivityDTO: Decodable, Sendable, Identifiable {
    let date: String
    let count: Int
    let hadDue: Bool

    var id: String { date }
}

struct ProgressDTO: Decodable, Sendable {
    let currentStreak: Int
    let bestStreak: Int
    let totalAnswered: Int
    let answeredToday: Int
    let answeredThisWeek: Int
    let answeredThisMonth: Int
    let dailyActivity: [DailyActivityDTO]
    let ratingMix: RatingMixDTO?
}

struct RatingMixDTO: Decodable, Sendable {
    let again: Int
    let hard: Int
    let good: Int
    let easy: Int

    var total: Int { again + hard + good + easy }
}

// MARK: - Overview

struct OverviewDTO: Decodable, Sendable {
    let totalCards: Int
    let dueCards: Int
    let cardsByState: [String: Int]
    let totalDecks: Int
    let totalSources: Int
}

// MARK: - Pagination

struct PaginatedResponse<T: Decodable & Sendable>: Decodable, Sendable {
    let items: [T]
    let hasMore: Bool
    let nextCursor: String?
}

// MARK: - User

struct UserInfoResponse: Decodable, Sendable {
    let email: String
    let isAdmin: Bool
    let externalProvider: String?
    let displayName: String?
}

// MARK: - Notifications

struct DeviceTokenRequest: Encodable, Sendable {
    let token: String
}

struct NotificationSettingsResponse: Decodable, Sendable {
    let intervalHours: Int
    let hasDeviceToken: Bool
}

struct UpdateNotificationSettingsRequest: Encodable, Sendable {
    let intervalHours: Int
}

struct EmptyResponse: Decodable, Sendable {}

// MARK: - Scheduling Settings

struct SchedulingSettingsResponse: Decodable, Sendable {
    let desiredRetention: Double
    let maximumInterval: Int
    let dayStartHour: Int
    let timeZone: String?
}

struct UpdateSchedulingSettingsRequest: Encodable, Sendable {
    let desiredRetention: Double
    let maximumInterval: Int
    let dayStartHour: Int
    let timeZone: String
}

// MARK: - Card Requests

struct CreateCardRequest: Encodable, Sendable {
    let front: String
    let back: String
    let sourceFile: String?
    let deckId: String?
}

struct UpdateCardRequest: Encodable, Sendable {
    let front: String
    let back: String
    let sourceFile: String?
    let deckIds: [String]?
}

struct SetSuspendedRequest: Encodable, Sendable {
    let isSuspended: Bool
}

// MARK: - Deck Requests

struct CreateDeckRequest: Encodable, Sendable {
    let name: String
    let description: String?
}

struct UpdateDeckRequest: Encodable, Sendable {
    let name: String
    let description: String?
}

// MARK: - Snapshots

struct DeckSnapshotDTO: Decodable, Sendable, Identifiable {
    let id: String
    let deckName: String?
    let cardCount: Int
    let createdAt: String
}

struct SnapshotCreateResultDTO: Decodable, Sendable {
    let created: Int
    let skipped: Int
}
