# Custom study (cram mode) — v1 design

Issue: [#156](https://github.com/philphilphil/fasolt/issues/156)

## Summary

Add a way to start a **custom study** session for a single deck. The session pulls all (non-suspended) cards from the deck, shuffles them, and presents them with a flip-only flow — no rating, no FSRS state changes, no review-log writes.

This is the cram half of issue #156. The filter builder, Filtered Review mode, and custom card-list builder are deliberately out of scope for v1.

## Scope

### In scope (v1)
- Server endpoint that returns all non-suspended cards in a deck, shuffled, in the existing `DueCardDto` shape.
- Web: entry button on deck detail; reuse `ReviewView.vue` in cram mode with single "Next" button and a small "Custom study — FSRS not adjusted" label.
- iOS: entry button on deck detail bottom CTA + leading swipe action on deck list row; reuse `StudyView` in cram mode with the same single "Next" button and small label.
- iOS deck detail toolbar consolidation (Edit / Sort / Suspend → `...` overflow menu) so the navigation title isn't crammed.

### Out of scope (v1)
- Filter builder (sources, states, tags, lapses, age, sort, limit)
- Filtered Review mode (the "due cards only with FSRS updates" variant from the ticket)
- Custom card-list builder (multi-deck or arbitrary set)
- Server-side session token / persisted custom-deck state
- Offline iOS cram (relies on a local card cache that doesn't exist yet)
- Including suspended cards in cram (user decision; can revisit)
- Banner color tint on the study screen (ticket suggested orange; user prefers a plain muted label)

## Behavior

### What "cram" means
A cram session is a stateless, read-only study queue:

- All non-suspended cards in the deck (regardless of due date, regardless of state — new/learning/review/relearning all included).
- Shuffled order.
- No grade API call between cards — the client simply advances through the queue.
- No FSRS state mutation. No review-log row. No `StudyStats` increment. No streak update.
- Skip and Suspend still work (suspend hits the existing endpoint; the session continues with the remaining cards).
- "Again" is *not* an option — there's no rating, so there's no requeue.

### Session end
When the user reaches the end of the queue (or ends the session manually), they see a minimal completion screen: just the count of cards viewed. No rating breakdown, no streak prompt.

If the user closes the app or navigates away mid-session, the session is forgotten. No resume.

## Server

### New endpoint

```
GET /api/review/custom?deckId={publicId}
```

- Auth: `RequireAuthorization("EmailVerified")` + `RequireRateLimiting("api")` (same as other `/api/review/*` endpoints).
- 404 if the deck doesn't exist or doesn't belong to the user.
- 200 with a `DueCardDto[]` body — same shape as `GET /api/review/due`, so the existing card renderer and types work unchanged.

### Service method

Add `ReviewService.GetCustomStudyCards(string userId, string deckPublicId)`:

- Look up the deck by `(userId, publicId)`. Return null if not found.
- Query all cards where `userId == userId`, deck contains this card, and `card.IsSuspended == false`.
- Project to `DueCardDto`.
- Shuffle in-memory (`Random.Shared`) — deck sizes are bounded and the user benefits from variety per session.
- Return the list.

No limit parameter for v1. If the deck has 10,000 cards we'll return 10,000 — acceptable for an initial release; we can add a server-side cap or pagination if it becomes a problem.

### Tests
- `ReviewServiceTests`: returns all non-suspended cards in shuffled order; excludes suspended; returns null for other users' decks; returns empty list when deck has no cards.
- Integration test (`fasolt.Tests`): calling the endpoint does **not** create review-log entries and does **not** change `card.DueAt` / `card.Stability` / `card.Difficulty`. This is the core invariant of cram mode.

## Web (Vue)

### Routing
Reuse the existing `/review` route. Add a `mode` query parameter:

- `/review?deckId=X` — normal review (default, `mode=normal`).
- `/review?deckId=X&mode=cram` — cram session.

### Store (`stores/review.ts`)
Extend the existing `useReviewStore`:

- Add `mode: 'normal' | 'cram'` ref, defaulting to `'normal'`.
- `startSession(deckId?, mode = 'normal')`:
  - When `mode === 'cram'`, fetch `GET /review/custom?deckId=X` instead of `/review/due`.
  - Set `mode` ref.
- Replace the `rate(...)` call with a new `advance()` function used in cram mode:
  - Increments `sessionStats.reviewed`.
  - Advances `currentIndex`.
  - Resets `isFlipped`.
  - **No API call.**
- `rate(...)` keeps its current behavior in normal mode and is not callable in cram mode (UI doesn't render the rating buttons).
- `endSession()` clears `mode` back to `'normal'`.

### View (`views/ReviewView.vue`)
Read `mode` from `route.query.mode`. Pass it to `review.startSession(deckId, mode)`.

When `review.mode === 'cram'`:
- Context bar at top: replace the `Review` chip with a `Custom study` chip (same styling as the existing `Review` chip).
- Directly under the progress meter, add one small muted line: **"Custom study — FSRS not adjusted"**. Use `text-xs text-muted-foreground` styling, centered, no color block.
- When `isFlipped`: render a single full-width **"Next"** button instead of `RatingButtons`. Reuse the existing button component.
- Keyboard shortcuts in cram mode:
  - `space` to flip (unchanged), then `space` or `n` to advance.
  - `1`–`4` keys do nothing.
  - `s` skip, `x` suspend, `Esc` end — unchanged.
- `SessionComplete` component: in cram mode, hide the rating breakdown — show just "N cards reviewed" and a "Done" button.

### Entry point on web
On `DeckDetailView.vue`, add a "Custom study" secondary button next to the existing primary "Study" button. Both visible when the deck has cards and isn't suspended; "Study" only when `dueCount > 0`. Clicking "Custom study" navigates to `/review?deckId=X&mode=cram`.

### Tests
- Playwright: start a cram session from deck detail, flip a card, click Next, verify queue advances. End the session. Verify no `/review/rate` calls fired. Verify the small label is visible.

## iOS (Swift)

### Repository
`CardRepository` gains:

```swift
func fetchCustomCards(deckId: String) async throws -> [DueCardDTO]
```

Hits `GET /api/review/custom?deckId=...`. Returns the same DTO type as `fetchDueCards`.

### Env action
Extend `StartStudyAction` to carry a mode:

```swift
enum StudyMode { case normal, cram }

struct StartStudyAction: Sendable {
    let action: @Sendable (String?, StudyMode) -> Void
    func callAsFunction(deckId: String? = nil, mode: StudyMode = .normal) {
        action(deckId, mode)
    }
}
```

Default `.normal` keeps existing callers working without changes.

### `StudyViewModel`
- Add `mode: StudyMode` (default `.normal`).
- `startSession(deckId:, mode:)`:
  - When `.cram`, call `cardRepository.fetchCustomCards(deckId:)`.
  - When `.normal`, call existing `fetchDueCards`.
- New `advance()` method for cram: increments local stats, advances index, resets flip. No API call.
- Existing `rate(...)` stays normal-mode-only.

### `StudyView`
- When `viewModel.mode == .cram`:
  - Top of screen: small `Text("Custom study — FSRS not adjusted")` in `.caption`/`.footnote` with `.secondary` foreground.
  - When card is flipped: render single full-width **"Next"** button (`.borderedProminent`). Tapping calls `advance()`.
  - Hide the rating row.
- Session complete sheet: same simplification — show just count, no breakdown.

### Entry points on iOS

**Deck detail (`DeckDetailView.swift`)**:
- Toolbar consolidation (independent improvement, but required so the title fits when we add a CTA):
  - Keep `+` (New Card) as a standalone trailing button.
  - Collapse **Edit**, **Sort**, **Suspend** into a single `Menu { ... } label: { Image(systemName: "ellipsis.circle") }` trailing button.
- Bottom CTA area, when `!isSuspended && cardCount > 0`:
  - **"Study This Deck"** — primary `.borderedProminent`, only when `dueCount > 0` (existing behavior preserved).
  - **"Custom study"** — secondary `.bordered`, always shown when the deck has cards.
  - Stack vertically with 8pt spacing.

**Deck list (`DeckListView.swift`)** — leading swipe action alongside Copy ID:

```swift
.swipeActions(edge: .leading) {
    Button {
        UIPasteboard.general.string = deck.id
    } label: {
        Label("Copy ID", systemImage: "doc.on.doc")
    }
    .tint(.blue)
    Button {
        startStudy(deckId: deck.id, mode: .cram)
    } label: {
        Label("Cram", systemImage: "flame")
    }
    .tint(.orange)
}
```

Disabled (or simply not shown) when `deck.isSuspended` or `deck.cardCount == 0`.

### Tests
- Unit test `StudyViewModel`: starting a cram session calls `fetchCustomCards`, and `advance()` does not call `rate`.
- UI test (if/where iOS UI tests exist for the study flow): start a cram session from deck detail, flip, advance, verify completion screen.

## Data model

No schema changes. Cram doesn't persist anything.

## Failure modes & edge cases

- **Deck has zero non-suspended cards**: endpoint returns `[]`. Client shows the existing "No cards" empty state with a "Back" action.
- **Deck doesn't exist / not owned by user**: 404 from the endpoint. Client shows an error toast and routes back to the deck list / study tab.
- **Network failure mid-fetch**: existing error path (toast / retry button on the study view).
- **User suspends a card mid-session**: same as normal review — the suspend API call fires, the queue advances past the card, the local queue does not retroactively remove other instances of the card (there shouldn't be any in cram since each card appears once).
- **Concurrent web + iOS cram on the same deck**: no conflict — there's no shared state to corrupt.

## Migration / rollout

- No DB migration. No feature flag. Ship behind a single PR.
- Web and iOS changes ship in the same release.
- iOS requires a TestFlight build; if the app store cycle slows things down, the web side can ship first and the iOS swipe action / deck-detail button arrive in the next iOS build — they're independent.

## Open questions deferred to v2

- Filtered Review mode (FSRS-on cram of due cards across filters).
- Filter builder UI (multi-source, multi-state, tags, lapses).
- Custom card-list builder.
- Offline iOS cram against a local SwiftData cache.
- Optional toggle to include suspended cards in cram.
- Server-side caps / pagination for very large decks.
