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

struct CardDTO: Decodable, Sendable {
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
}

struct CardDeckInfoDTO: Decodable, Sendable {
    let id: String
    let name: String
}

// MARK: - Decks

struct DeckDTO: Decodable, Sendable {
    let id: String
    let name: String
    let description: String?
    let cardCount: Int
    let dueCount: Int
    let createdAt: String
}

// MARK: - Review

struct DueCardDTO: Decodable, Sendable {
    let id: String
    let front: String
    let back: String
    let sourceFile: String?
    let sourceHeading: String?
    let state: String
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
    let displayName: String?
}
