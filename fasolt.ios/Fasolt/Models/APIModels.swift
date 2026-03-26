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
    let frontSvg: String?
    let backSvg: String?
}

struct CardDeckInfoDTO: Decodable, Sendable {
    let id: String
    let name: String
    let isActive: Bool
}

// MARK: - Decks

struct DeckDTO: Decodable, Sendable {
    let id: String
    let name: String
    let description: String?
    let cardCount: Int
    let dueCount: Int
    let createdAt: String
    let isActive: Bool
}

struct DeckDetailDTO: Decodable, Sendable {
    let id: String
    let name: String
    let description: String?
    let cardCount: Int
    let dueCount: Int
    let isActive: Bool
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
