# iOS App — Part 1: Project Scaffold + Auth

## Overview

Native iOS (SwiftUI) study client for Fasolt. Part 1 covers Xcode project setup, data models, networking layer, OAuth authentication, and the onboarding screen. This is the foundation all subsequent parts build on.

**Scope:** Project scaffold, SwiftData models, APIClient, AuthService, KeychainHelper, NetworkMonitor, OnboardingView.
**Out of scope:** Study session, dashboard, deck browser, settings, sync, offline queue flush — these come in Parts 2–4.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Deployment target | iOS 17+ | `@Observable`, SwiftData, modern SwiftUI navigation |
| Architecture | Repository pattern + service layer | Clean separation, offline logic contained in repositories |
| Local storage | SwiftData | Native, declarative, integrates with `@Observable`; data is simple enough |
| HTTP layer | URLSession + async/await | No third-party dependencies needed |
| Auth | OAuth 2.1 authorization code + PKCE via `ASWebAuthenticationSession` | Backend already supports this; dynamic client registration |
| Token storage | Keychain | Secrets don't belong in SwiftData |
| OAuth redirect | `fasolt://oauth/callback` custom URL scheme | Simple, no server-side universal link setup needed |
| Server URL | Pre-filled production default, hidden self-hosting option | Clean UX for most users, accessible for power users |
| Study interaction | Tap to flip + rating buttons | Accessible, no accidental gestures; swipe can be added later |
| Offline depth | Read + review offline | Cache due cards + deck/card browser data; queue ratings; creation stays online |

## Project Structure

```
fasolt.ios/
├── Fasolt/
│   ├── FasoltApp.swift              — app entry, SwiftData container, auth gate
│   ├── Models/
│   │   ├── Card.swift               — SwiftData @Model + Codable
│   │   ├── CachedDeck.swift         — SwiftData @Model + Codable
│   │   ├── PendingReview.swift      — offline review queue
│   │   └── TokenInfo.swift          — token struct (Keychain, not SwiftData)
│   ├── Services/
│   │   ├── AuthService.swift        — OAuth flow, token refresh, Keychain storage
│   │   ├── APIClient.swift          — HTTP layer, auth interceptor, error handling
│   │   ├── StudyService.swift       — (stub) fetch due cards, submit ratings
│   │   ├── DeckService.swift        — (stub) deck CRUD
│   │   └── SyncService.swift        — (stub) offline queue flush
│   ├── Repositories/
│   │   ├── CardRepository.swift     — (stub) network + cache coordination
│   │   └── DeckRepository.swift     — (stub) network + cache coordination
│   ├── Views/
│   │   ├── Onboarding/
│   │   │   └── OnboardingView.swift — server URL + login flow
│   │   ├── Dashboard/
│   │   │   └── DashboardView.swift  — (stub placeholder)
│   │   ├── Study/
│   │   │   └── StudyView.swift      — (stub placeholder)
│   │   ├── Decks/
│   │   │   └── DeckListView.swift   — (stub placeholder)
│   │   └── Settings/
│   │       └── SettingsView.swift   — (stub placeholder)
│   ├── ViewModels/                  — (empty, populated in Parts 2–3)
│   └── Utilities/
│       ├── KeychainHelper.swift     — Keychain CRUD wrapper
│       └── NetworkMonitor.swift     — NWPathMonitor connectivity wrapper
├── Fasolt.xcodeproj
└── README.md
```

Stubs for Services, Repositories, and Views outside Part 1 scope are empty files with a `// TODO: Part N` comment so the project compiles and the tab navigation works with placeholder screens.

## Auth Flow

### Sequence

```
1. App launch
   → Check Keychain for tokens
   → Valid access token → Dashboard
   → Expired + refresh token → silent refresh → Dashboard
   → No tokens → OnboardingView

2. Onboarding
   → "Sign In" button, production URL pre-filled
   → "Self-hosting? Change server" subtle text link
   → Tapping reveals URL text field

3. Dynamic Client Registration (first time per server)
   → POST /oauth/register
     { client_name: "Fasolt iOS", redirect_uris: ["fasolt://oauth/callback"] }
   → Store client_id in Keychain
   → Skip if client_id already exists for this server URL

4. PKCE Authorization
   → Generate code_verifier (64 random bytes, base64url-encoded)
   → code_challenge = base64url(SHA256(code_verifier))
   → ASWebAuthenticationSession opens:
     GET /oauth/authorize
       ?response_type=code
       &client_id=<client_id>
       &redirect_uri=fasolt://oauth/callback
       &code_challenge=<challenge>
       &code_challenge_method=S256
       &scope=offline_access
   → User authenticates on web login form
   → Redirect: fasolt://oauth/callback?code=<auth_code>

5. Token Exchange
   → POST /oauth/token
     { grant_type: authorization_code, code, redirect_uri, client_id, code_verifier }
   → Response: { access_token, refresh_token, expires_in }
   → Store tokens + computed expiry in Keychain

6. Automatic Token Refresh
   → APIClient checks token expiry before each request
   → If expired: POST /oauth/token
     { grant_type: refresh_token, refresh_token, client_id }
   → Update Keychain with new tokens
   → If refresh fails (401): clear all tokens → OnboardingView
```

### Keychain Keys

| Key | Value | Notes |
|-----|-------|-------|
| `fasolt.serverURL` | Base URL string | Set during onboarding |
| `fasolt.clientId` | OAuth client_id | Per-server, from dynamic registration |
| `fasolt.accessToken` | Bearer token | 1-hour lifetime |
| `fasolt.refreshToken` | Refresh token | 14-day lifetime |
| `fasolt.tokenExpiry` | ISO 8601 date string | Computed from `expires_in` |

## APIClient

```swift
// Core interface
APIClient {
  func request<T: Decodable>(_ endpoint: Endpoint) async throws -> T
  func request(_ endpoint: Endpoint) async throws
}

Endpoint {
  path: String             // e.g. "/api/review/due"
  method: HTTPMethod       // .get, .post, .put, .delete
  queryItems: [URLQueryItem]?
  body: (any Encodable)?
}
```

**Behaviors:**
- Injects `Authorization: Bearer <token>` on every request
- Refreshes token silently if expired before sending
- Maps HTTP errors to typed `APIError` enum: `.unauthorized`, `.notFound`, `.serverError(Int)`, `.networkError(Error)`, `.decodingError(Error)`
- Uses `JSONDecoder`/`JSONEncoder` with default key strategy (backend sends camelCase)
- No retry logic — errors bubble to ViewModel for display
- Base URL read from Keychain

## SwiftData Models

### Card

```swift
@Model Card {
  @Attribute(.unique) var publicId: String
  var front: String
  var back: String
  var sourceFile: String?
  var sourceHeading: String?
  var state: String              // "new", "learning", "review", "relearning"
  var dueAt: Date?
  var stability: Double?
  var difficulty: Double?
  var step: Int?
  var lastReviewedAt: Date?
  var createdAt: Date
  var decks: [CachedDeck]
}
```

### CachedDeck

```swift
@Model CachedDeck {
  @Attribute(.unique) var publicId: String
  var name: String
  var deckDescription: String?
  var cardCount: Int
  var dueCount: Int
  var createdAt: Date
  var cards: [Card]
}
```

### PendingReview

```swift
@Model PendingReview {
  var cardPublicId: String
  var rating: String             // "again", "hard", "good", "easy"
  var reviewedAt: Date
  var synced: Bool = false
}
```

Models are `Codable` for direct API response decoding. Upsert by `publicId` when syncing from API.

## Onboarding UI

Single screen with two visual states:

**Default:** App logo/name, prominent "Sign In" button, subtle "Self-hosting? Change server" text below.

**Expanded:** URL text field slides in with pre-filled production URL, editable. "Sign In" button below.

**States during auth:**
- Loading spinner on button while registering client + opening browser
- Error text inline if auth fails ("Login failed. Please try again.")
- On success: view dismissed, app shows TabView

## Navigation Architecture

```swift
// FasoltApp.swift
@main struct FasoltApp: App {
  @State private var authService = AuthService()

  var body: some Scene {
    WindowGroup {
      if authService.isAuthenticated {
        MainTabView()
      } else {
        OnboardingView()
      }
    }
    .environment(authService)
    .modelContainer(for: [Card.self, CachedDeck.self, PendingReview.self])
  }
}

// MainTabView — three tabs
TabView {
  DashboardView()    // tab 1
  DeckListView()     // tab 2
  SettingsView()     // tab 3
}
```

`AuthService` is `@Observable` and injected via `.environment()`. When `isAuthenticated` changes, the root view swaps between onboarding and main app with an implicit transition.

## Xcode Project Setup Notes

The project will be created as a standard SwiftUI app in `fasolt.ios/`. Key configuration:
- **Bundle ID:** `com.fasolt.app` (or similar)
- **URL Scheme:** `fasolt` registered in Info.plist for OAuth callback
- **Minimum deployment:** iOS 17.0
- **No third-party dependencies** — pure Apple frameworks (SwiftUI, SwiftData, AuthenticationServices, Network, Security)

## Future Parts

- **Part 2:** Dashboard + Study session (hero screen, flip animation, rating buttons, offline review queue)
- **Part 3:** Deck browser + Settings (deck list, card browsing, cached data, logout, server config)
- **Part 4:** Sync + polish (SyncService, connectivity-driven queue flush, haptics, animations, error states)
