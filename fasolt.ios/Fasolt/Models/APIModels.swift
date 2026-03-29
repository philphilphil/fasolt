import Foundation

// MARK: - Auth

struct ClientRegistrationRequest: Encodable, Sendable {
    let clientName: String
    let redirectUris: [String]

    enum CodingKeys: String, CodingKey {
        case clientName = "client_name"
        case redirectUris = "redirect_uris"
    }
}

struct ClientRegistrationResponse: Decodable, Sendable {
    let clientId: String

    enum CodingKeys: String, CodingKey {
        case clientId = "client_id"
    }
}

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

struct RegisterRequest: Codable, Sendable {
    let email: String
    let password: String
}

// MARK: - Cards

struct CardDTO: Decodable, Sendable, Identifiable {
    let id: String
    let front: String
    let back: String
    let sourceFile: String?
    let sourceHeading: String?
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

struct DeckDTO: Decodable, Sendable {
    let id: String
    let name: String
    let description: String?
    let cardCount: Int
    let dueCount: Int
    let createdAt: String
    let isSuspended: Bool
}

struct DeckDetailDTO: Decodable, Sendable {
    let id: String
    let name: String
    let description: String?
    let cardCount: Int
    let dueCount: Int
    let isSuspended: Bool
    let cards: [DeckCardDTO]
}

struct DeckCardDTO: Decodable, Sendable, Identifiable {
    let id: String
    let front: String
    let back: String
    let sourceFile: String?
    let sourceHeading: String?
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

// MARK: - Review

struct DueCardDTO: Decodable, Sendable {
    let id: String
    let front: String
    let back: String
    let sourceFile: String?
    let sourceHeading: String?
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
}

struct UpdateSchedulingSettingsRequest: Encodable, Sendable {
    let desiredRetention: Double
    let maximumInterval: Int
}

// MARK: - Card Requests

struct CreateCardRequest: Encodable, Sendable {
    let front: String
    let back: String
    let sourceFile: String?
    let sourceHeading: String?
    let deckId: String?
}

struct UpdateCardRequest: Encodable, Sendable {
    let front: String
    let back: String
    let sourceFile: String?
    let sourceHeading: String?
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
