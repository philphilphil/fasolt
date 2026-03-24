# iOS Parts 3+4: Decks, Settings, Sync & Polish — Design Spec

## Overview

Combined implementation of iOS Parts 3 (Deck Browser + Settings) and 4 (Sync + Polish). These are tightly coupled: the offline deck cache (IOS-3.4) feeds into the SyncService (IOS-4.1), the connectivity indicator (IOS-4.2) applies across all screens, and error/empty states (IOS-4.3) cover the Part 3 views.

## Architecture: Repository Pattern with DeckRepository

Follows the existing `CardRepository` pattern. New `DeckRepository` owns network-first-with-cache-fallback logic for decks. `SyncService` is a standalone service for flushing the `PendingReview` queue.

### New Files

```
Repositories/DeckRepository.swift     — network-first deck fetching + SwiftData cache
Services/SyncService.swift            — flush PendingReview on connectivity restore
ViewModels/DeckListViewModel.swift    — deck list state
ViewModels/DeckDetailViewModel.swift  — deck detail + cards state
ViewModels/SettingsViewModel.swift    — user info fetching
Views/Decks/DeckListView.swift        — replace stub
Views/Decks/DeckDetailView.swift      — new: cards in deck + "Study This Deck"
Views/Decks/DeckCardRow.swift         — reusable card row for deck detail
Views/Settings/SettingsView.swift     — replace stub
Views/Shared/OfflineBanner.swift      — reusable offline indicator
```

### Modified Files

```
Models/APIModels.swift                — add DeckDetailDTO, DeckCardDTO
Views/MainTabView.swift               — wire DeckRepository, SyncService, offline banner
Views/Study/StudyView.swift           — fix mirrored back text bug, add rating haptics
Repositories/CardRepository.swift     — remove dead flushPendingReviews() (moved to SyncService)
FasoltApp.swift                       — no changes (SyncService created in MainTabView)
```

## Data Layer

### New API DTOs (add to APIModels.swift)

```swift
struct DeckDetailDTO: Decodable, Sendable {
    let id: String
    let name: String
    let description: String?
    let cardCount: Int
    let dueCount: Int
    let cards: [DeckCardDTO]
}

struct DeckCardDTO: Decodable, Sendable {
    let id: String
    let front: String
    let back: String
    let sourceFile: String?
    let sourceHeading: String?
    let state: String
    let dueAt: String?
}
```

`DeckDTO`, `UserInfoResponse` already exist in APIModels.swift — no changes needed.

### SwiftData Models

`CachedDeck` and `Card` models already exist with correct fields and relationships. `PendingReview` exists with `cardPublicId`, `rating`, `reviewedAt`, `synced`. No schema changes needed.

## DeckRepository

```swift
@MainActor @Observable
final class DeckRepository {
    private let apiClient: APIClient
    private let networkMonitor: NetworkMonitor
    private let modelContext: ModelContext

    // Network-first, cache fallback
    func fetchDecks() async throws -> [DeckDTO]
    func fetchDeckDetail(id: String) async throws -> DeckDetailDTO
}
```

**`fetchDecks()`:**
1. Try `GET /api/decks`
2. On success: upsert into `CachedDeck` SwiftData (match on `publicId`), delete stale decks not in response, return DTOs
3. On network error: fetch `CachedDeck` from SwiftData, map to `DeckDTO`, return

**`fetchDeckDetail(id:)`:**
1. Try `GET /api/decks/{id}`
2. On success: upsert cards into `Card` SwiftData, associate with `CachedDeck`, return DTO
3. On network error: fetch `CachedDeck` by `publicId` with its `cards` relationship, map to `DeckDetailDTO`, return

## SyncService

```swift
@MainActor @Observable
final class SyncService {
    private let apiClient: APIClient
    private let networkMonitor: NetworkMonitor
    private let modelContext: ModelContext
    var pendingCount: Int = 0

    func startMonitoring() — observe networkMonitor.isConnected, flush on restore
    func flushPendingReviews() async — POST each unsynced PendingReview, delete on success
}
```

**Sync logic:**
- Uses `withObservationTracking` or a simple polling approach to watch `networkMonitor.isConnected`
- When connectivity transitions from `false` to `true`, calls `flushPendingReviews()`
- For each unsynced `PendingReview`: POST to `/api/review/rate`, on success delete from SwiftData
- On 404 (card deleted on server): delete the pending review silently
- On other errors: leave in queue for next connectivity change
- Updates `pendingCount` for UI display

**Cleanup:** Remove `CardRepository.flushPendingReviews()` (currently dead code — only returns count, does not actually sync). All sync responsibility moves to `SyncService`.

**Initialization:** Created in `MainTabView` (same as `CardRepository`), since `ModelContext` is only available within the view hierarchy via `@Environment(\.modelContext)`. `SyncService` starts monitoring in a `.task {}` modifier on `MainTabView`.

## Views

### DeckListView (IOS-3.1)

- `NavigationStack` with title "Decks"
- `List` of decks showing: deck name, card count pill, due count pill
- Pull-to-refresh triggers `viewModel.refresh()`
- Tap navigates to `DeckDetailView`
- Empty state: "No decks yet" message with subtitle
- Loading state: `ProgressView`

### DeckDetailView (IOS-3.2)

- Shows deck name as navigation title
- Card count + due count in header
- `List` of cards showing: front text, source file, state badge
- Tap card to show full detail (front/back) in a sheet
- "Study This Deck" button (prominent, bottom of screen)
- Button disabled if `dueCount == 0`
- Starts study session with `deckId` filter, reusing existing `StudyView` + `StudyViewModel`

### SettingsView (IOS-3.3)

- **Account section:** user email (from `GET /api/account/me`), server URL (from Keychain)
- **Actions section:** Sign Out button (destructive, with confirmation alert)
- **About section:** app version from `Bundle.main`
- On sign out: `authService.signOut()` clears Keychain, `@Observable` triggers navigation back to onboarding

### OfflineBanner (IOS-4.2)

- Thin bar below navigation bar: "Offline" text with `wifi.slash` icon
- Subtle background color (`.secondary` fill with low opacity)
- Implemented as a `ViewModifier` that can be applied to any `NavigationStack`
- Applied in `MainTabView` (covers Dashboard, Decks, Settings tabs)
- Also applied inside `StudyView`'s `NavigationStack` so the banner is visible during study sessions (study allows offline rating via queue)
- Reads `NetworkMonitor.isConnected` from environment
- Auto-shows when `false`, auto-hides when `true`, with animation
- Dashboard: when offline, show last-loaded stats with the offline banner (stats are held in `DashboardViewModel` in-memory state from last successful fetch — no additional SwiftData caching needed since dashboard is always the first tab loaded)

### Error States (IOS-4.3)

- **Network errors:** show error message + "Retry" button (already exists in `StudyView.loadingView`, replicate pattern)
- **Auth expired + refresh failed:** `APIError.unauthorized` after refresh → `authService.signOut()` with message
- **Empty states:** "No decks yet" on deck list, "No cards in this deck" on deck detail, "No cards due" on dashboard (dashboard already handles this)
- **Loading states:** `ProgressView` spinner on all data-loading views

## Bugfix: Mirrored Card Back Text

**Problem:** `StudyView.swift:95-99` applies `rotation3DEffect(.degrees(180))` to the entire `CardView` when flipped, which mirrors the text content horizontally.

**Fix:** Add `.scaleEffect(x: viewModel.isFlipped ? -1 : 1)` to the `CardView` to counter the horizontal mirror caused by the Y-axis rotation. This keeps the 3D flip animation intact while making the back text readable. Applied immediately after the `rotation3DEffect` modifier.

## Haptics Polish (IOS-4.4 — minimal)

- **Rating buttons:** light haptic (`UIImpactFeedbackGenerator(.light)`) on each rating tap
- **Session complete:** success notification haptic (`UINotificationFeedbackGenerator().notificationOccurred(.success)`) when transitioning to summary

Skip: animated dashboard stats, tab bar transitions (P2, defer to later).

## Out of Scope

- **IOS-4.5 (App Icon + Launch Screen):** Deferred. Requires design assets (Fasolt branding) that are not yet available. Will be addressed in a separate pass.
- **IOS-4.4 animated dashboard stats and tab bar transitions:** P2, deferred to a later polish pass.

## Dependency Injection

`MainTabView` creates:
- `DeckRepository(apiClient:networkMonitor:modelContext:)` — same pattern as `CardRepository`
- `SyncService(apiClient:networkMonitor:modelContext:)` — starts monitoring in `.task {}`
- `DeckListView(viewModel:studyViewModelFactory:)` — receives `DeckListViewModel` and a closure to create `StudyViewModel` (for "Study This Deck")

**"Study This Deck" dependency chain:**
`MainTabView` passes a `studyViewModelFactory: (String?) -> StudyViewModel` closure to `DeckListView`, which forwards it to `DeckDetailView`. When the user taps "Study This Deck", `DeckDetailView` calls `studyViewModelFactory(deckId)` to create a `StudyViewModel` with the deck filter. This mirrors the existing `DashboardView.studyViewModelFactory` pattern.

`DeckDetailViewModel` takes only `deckRepository` — it does not need `cardRepository` directly.

## Decisions Made

| Decision | Choice | Rationale |
|---|---|---|
| Cache strategy | Network-first with fallback | Deck data changes rarely; simpler than cache-then-network; no stale flash |
| Deck study | Reuse StudyViewModel with deckId | Same UX, no reason to differ |
| Settings scope | Minimal (email, server URL, logout, version) | Per user request |
| Offline indicator | Top banner below nav bar | iOS convention (Slack, Mail) |
| Polish scope | Rating haptics + session-complete haptic only | Minimal polish per user request |
| Combined build | Parts 3+4 together | Offline cache (3.4) and sync (4.1) are tightly coupled |

## API Endpoints Used

| Endpoint | Used For |
|---|---|
| `GET /api/decks` | Deck list (IOS-3.1) |
| `GET /api/decks/{id}` | Deck detail with cards (IOS-3.2) |
| `GET /api/account/me` | Settings user email (IOS-3.3) |
| `POST /api/review/rate` | SyncService flush (IOS-4.1) |
| `GET /api/review/due?deckId=X` | Study This Deck (IOS-3.2) |
