# iOS App — Part 2: Dashboard + Study Session

## Overview

The core study loop: a motivational dashboard with stats and a prominent "Study Now" CTA, a full study session with card flip animation and FSRS rating, offline review queuing, and a session summary screen.

**Scope:** DashboardViewModel, DashboardView, StudyViewModel, StudyView, CardView, StudySummaryView, CardRepository.
**Out of scope:** Deck browser, settings, SyncService flush logic, push notifications, streaks — these come in Parts 3–4.

## Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Landing screen | Dashboard with hero study CTA | Motivational stats + one-tap study access |
| Study interaction | Tap to flip + rating buttons | Decided in Part 1 — accessible, no accidental gestures |
| Rating intervals | Labels only (Again/Hard/Good/Easy) | Backend doesn't expose interval previews |
| Session completion | Summary card with stats | Satisfying endpoint, reinforces habit loop |
| Offline rating | Queue silently, no local prediction | Simple, correct — server calculates real schedule on sync |
| Dashboard stats refresh | On appear + pull-to-refresh | Live view, no caching |

## New/Modified Files

```
fasolt.ios/Fasolt/
├── ViewModels/
│   ├── DashboardViewModel.swift    — fetches stats, holds dashboard state
│   └── StudyViewModel.swift        — manages study session lifecycle
├── Views/
│   ├── Dashboard/
│   │   └── DashboardView.swift     — replace stub with real dashboard
│   └── Study/
│       ├── StudyView.swift         — replace stub with session screen
│       ├── CardView.swift          — single card (front/back, flip animation)
│       └── StudySummaryView.swift  — session completion summary
├── Repositories/
│   └── CardRepository.swift        — network + offline queue coordination
```

7 files: 4 new, 3 replacing stubs.

## Data Flow

### Dashboard

```
DashboardView → DashboardViewModel → APIClient
  GET /api/review/stats → { dueCount, totalCards, studiedToday }
  GET /api/overview     → { cardsByState, totalDecks, totalSources }
```

Dashboard fetches stats on appear and on pull-to-refresh. No caching — always live data.

### Study Session

```
StudyView → StudyViewModel → CardRepository → APIClient + SwiftData
  GET /api/review/due?limit=50  → fetch due cards
  POST /api/review/rate          → submit rating (online)
  SwiftData PendingReview        → queue rating (offline)
```

## DashboardViewModel

```swift
@Observable DashboardViewModel {
  var dueCount: Int = 0
  var totalCards: Int = 0
  var studiedToday: Int = 0
  var cardsByState: [String: Int] = [:]
  var isLoading = false
  var errorMessage: String?

  func loadStats() async
}
```

`loadStats()` calls both `/api/review/stats` and `/api/overview` concurrently via `async let`, merges results into the published properties.

## DashboardView Layout

Matches the "Hero Card + Stats Grid" mockup:

1. **Hero card** — blue gradient background, centered layout:
   - "Cards due" label (small, secondary)
   - Due count (large, bold, ~42pt)
   - "Study Now" button (white on translucent background)
   - Tapping navigates to `StudyView` via `NavigationStack`
   - When `dueCount == 0`: shows "All caught up!" and button is disabled

2. **Stats row** — three equal-width pills:
   - Total cards
   - Studied today
   - (Third pill: total decks or sources — placeholder for future streak)

3. **Card state bar** — horizontal stacked bar:
   - Four segments: new (green), review (blue), learning (amber), relearning (red)
   - Labels below with counts
   - Only shown when `totalCards > 0`

4. **Pull-to-refresh** — triggers `loadStats()`

## Study Session State Machine

```
idle → loading → studying ↔ flipped → summary
```

- **idle** — initial state, transitions to loading when view appears
- **loading** — fetching due cards from API. Show progress spinner. On failure, show error with retry.
- **studying** — showing current card front. User taps card or "Show Answer" button to flip.
- **flipped** — showing card back + four rating buttons. User taps a rating to submit and advance.
- **summary** — all cards reviewed. Show session stats.

### StudyViewModel

```swift
@Observable StudyViewModel {
  // State
  enum SessionState { case idle, loading, studying, flipped, summary }
  var state: SessionState = .idle
  var errorMessage: String?

  // Session data
  var cards: [DueCardDTO] = []
  var currentIndex: Int = 0
  var isFlipped: Bool = false

  // Session stats
  var ratingsCount: [String: Int] = ["again": 0, "hard": 0, "good": 0, "easy": 0]
  var cardsStudied: Int = 0

  // Computed
  var currentCard: DueCardDTO?
  var progress: Double            // currentIndex / cards.count
  var totalCards: Int

  // Actions
  func startSession(deckId: String? = nil) async
  func flipCard()
  func rateCard(_ rating: String) async
  func exitSession()              // early exit with confirmation
}
```

### Study flow:

1. `startSession()` → set state to `.loading`, call `cardRepository.fetchDueCards()`, set state to `.studying`
2. `flipCard()` → toggle `isFlipped`, set state to `.flipped`
3. `rateCard(rating)` → call `cardRepository.rateCard()`, increment stats, advance `currentIndex`, reset `isFlipped`, set state to `.studying` (or `.summary` if last card)
4. `exitSession()` → if cards remain, show confirmation alert. Return to dashboard.

## StudyView Layout

### Front state (`.studying`)

- **Progress bar** at top: thin horizontal bar + "5 / 23" counter + X (exit) button
- **Card area** (CardView): rounded rect, centered content
  - "QUESTION" label (small, uppercase, secondary)
  - Front text (large, primary)
  - Source file label at bottom (small, tertiary) — e.g. "biology-101.md"
- **"Show Answer" button** at bottom: full-width, gray background

### Back state (`.flipped`)

- Progress bar (same)
- **Card area** (CardView): same shape, flipped via 3D rotation animation
  - "ANSWER" label
  - Back text
  - Source file + heading label — e.g. "biology-101.md · Cell Structure"
- **Rating buttons** at bottom: four equal-width buttons in a row
  - Again (red-tinted), Hard (amber-tinted), Good (green-tinted), Easy (blue-tinted)
  - Label only, no interval predictions

### CardView

A reusable component that displays one side of a card:

```swift
CardView {
  let label: String         // "QUESTION" or "ANSWER"
  let text: String          // front or back content
  let sourceFile: String?
  let sourceHeading: String?
}
```

### Card Flip Animation

- 3D rotation around Y-axis: `.rotation3DEffect(.degrees(isFlipped ? 180 : 0), axis: (x: 0, y: 1, z: 0))`
- Duration: 0.4s with spring animation
- At 90° (midpoint), swap content from front to back
- Optional: light haptic feedback on flip (`.impact(style: .light)`)

## StudySummaryView

Shown when all cards are reviewed:

- **Completion message** — "Session Complete" or similar
- **Stats card:**
  - Cards studied (total count)
  - Rating breakdown: Again: N, Hard: N, Good: N, Easy: N
- **"Done" button** — returns to dashboard

Simple and clean. No charts or complex visualizations.

## CardRepository

```swift
CardRepository {
  let apiClient: APIClient
  let networkMonitor: NetworkMonitor
  let modelContext: ModelContext

  func fetchDueCards(deckId: String?, limit: Int) async throws -> [DueCardDTO]
  func rateCard(cardId: String, rating: String) async throws -> RateCardResponse?
  func flushPendingReviews() async throws -> Int   // stub for Part 4
}
```

### `fetchDueCards`:
1. Call `GET /api/review/due?limit=N&deckId=...`
2. Return the decoded `[DueCardDTO]`
3. No local caching — due cards are a session snapshot
4. If offline, throw error — study requires at least one online fetch

### `rateCard`:
1. If `networkMonitor.isConnected`:
   - `POST /api/review/rate` with `{ cardId, rating }`
   - Return `RateCardResponse`
2. If offline (or network request fails with network error):
   - Create `PendingReview(cardPublicId: cardId, rating: rating)` in SwiftData
   - Return nil
3. Non-network errors (401, 404, etc.) are thrown — not queued

### `flushPendingReviews`:
- Stub implementation: query `PendingReview` where `synced == false`, return count
- Full implementation in Part 4 (SyncService)

## Navigation

Dashboard navigates to study via `NavigationStack`:

```swift
// In DashboardView
NavigationLink("Study Now", destination: StudyView())
```

StudySummaryView's "Done" button pops back to dashboard:

```swift
@Environment(\.dismiss) var dismiss
// "Done" button calls dismiss()
```

## Error Handling

- **Dashboard load failure:** show inline error message with "Retry" button
- **Due cards fetch failure:** show error state in StudyView with "Retry" or "Back" options
- **Rating submission failure (non-network):** show brief error toast, keep card in queue for re-rating
- **Rating submission failure (network):** silently queue offline, continue session
