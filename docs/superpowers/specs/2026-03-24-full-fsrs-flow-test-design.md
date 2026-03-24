# Full FSRS Flow Integration Test

## Goal

Verify that the complete FSRS spaced repetition flow works correctly end-to-end: card creation → review/rating → rescheduling → due card queries, with simulated time spanning 6 months.

## Problem

Existing tests (`FsrsSchedulingTests.cs`) test the FSRS.Core library in isolation. There is no test that exercises the full integration: DB persistence, state mapping, due card queries, and multi-session scheduling over realistic timelines. The review endpoints hardcode `DateTime.UtcNow`, making time simulation impossible.

## Production Code Change

### Inject `TimeProvider` into `ReviewEndpoints.cs`

Replace three hardcoded time calls with .NET 8's built-in `TimeProvider`:

| Location | Current | New |
|---|---|---|
| `GetDueCards` (line 56) | `DateTimeOffset.UtcNow` | `timeProvider.GetUtcNow()` |
| `RateCard` (line 107) | `DateTime.UtcNow` | `timeProvider.GetUtcNow().UtcDateTime` |
| `RateCard` (line 116) | `DateTimeOffset.UtcNow` | `timeProvider.GetUtcNow()` |

ASP.NET Core registers `TimeProvider.System` by default — no new DI registration needed. The endpoint method signatures gain a `TimeProvider timeProvider` parameter, which minimal API resolves automatically.

**Scope:** Only `ReviewEndpoints.cs` changes. No other production files.

## Test Infrastructure

### Dependencies

- Add `Microsoft.Extensions.TimeProvider.Testing` NuGet to `fasolt.Tests` (provides `FakeTimeProvider`)

### Test Setup

- `FakeTimeProvider` initialized to a fixed start date: `2025-01-01T00:00:00Z`
- `TestDb` helper for isolated Postgres test database + seeded user
- FSRS `IScheduler` with `EnableFuzzing = false` for deterministic intervals
- Helper method `RateCardInDb(AppDbContext db, Card card, string rating, IScheduler scheduler, FakeTimeProvider time)`:
  1. Builds `FsrsCard` from DB entity (mirrors `ReviewEndpoints.RateCard` logic)
  2. Calls `scheduler.ReviewCard()` with the fake time
  3. Maps updated FSRS state back to entity
  4. Saves to DB
- Helper method `GetDueCards(AppDbContext db, string userId, FakeTimeProvider time)`:
  1. Queries cards where `DueAt == null || DueAt <= time.GetUtcNow()`
  2. Returns list of due cards

## Test Class: `FsrsFullFlowTests`

### Test 1: `SixMonthSimulation_CardsScheduledCorrectly`

Simulates 180 days of realistic study with ~5 cards.

**Flow:**
1. Create 5 cards in DB (state: "new", DueAt: null)
2. Day loop (180 days):
   - Advance `FakeTimeProvider` to the current simulated day
   - Query due cards from DB
   - Rate each due card with a realistic pattern:
     - Most reviews: "good"
     - Occasional "easy" for well-known cards
     - Occasional "again" to trigger lapses (e.g., every ~20th review)
   - Save updated state to DB
3. Track per-card history: state transitions, intervals, due dates

**Assertions:**
- All cards progress through new → learning → review
- At least one card experiences a lapse (review → relearning → review)
- Intervals grow monotonically for cards with consecutive "good" ratings in review state
- After 180 days, cards in review state have intervals of days/weeks (not minutes)
- No card has a nonsensical state (e.g., negative stability, due date in the past after rating)

### Test 2: `Lapse_CardEntersRelearningAndRecovers`

Focused lapse lifecycle test.

**Flow:**
1. Create a card, rate "easy" to jump to review state
2. Advance past due date, rate "good" a few times to build stability
3. Advance past due date, rate "again" — triggers lapse
4. Verify relearning state, reduced stability
5. Rate "good" through relearning steps
6. Verify card returns to review state with lower stability than before the lapse

### Test 3: `DueCardQuery_RespectsSimulatedTime`

Verifies the due card filter works correctly at specific points in time.

**Flow:**
1. Create 3 cards, review each to different schedules (one due in 1 day, one in 7 days, one in 30 days)
2. Advance time to day 2 — assert only the 1-day card is due
3. Advance time to day 8 — assert the 1-day and 7-day cards are due
4. Advance time to day 31 — assert all 3 are due

## What Is NOT In Scope

- Overview/stats verification (covered by `OverviewServiceTests`)
- HTTP-level endpoint testing (we test at the service/DB layer)
- UI/Playwright testing
- ReviewLog persistence
