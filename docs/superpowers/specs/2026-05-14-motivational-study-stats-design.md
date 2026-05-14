# Motivational study stats — Design

GitHub issue: #150

## Goal

Show four lightweight motivational stats on the dashboard (web + iOS):

1. **Current streak** — consecutive study days, allowing no-due rest days
2. **Best streak** — longest streak ever
3. **Total answered cards** — cumulative count of all ratings the user has submitted
4. **Cards answered today** — count of ratings submitted today (user's local day)

Out of scope: badges, leaderboards, freezes, daily-goal config, calendar views.

## What we have today

- `Card.LastReviewedAt` records the *last* review per card — not enough for streak history or "total answered".
- `ReviewService.RateCard` mutates the card and saves. There is no review log.
- `OverviewService` exposes basic counters. `ReviewService.GetStats` exposes `dueCount / totalCards / studiedToday` (today = unique cards with `LastReviewedAt >= UTC midnight`, ignoring TZ).
- User day boundary is configurable via `AppUser.TimeZone` and `AppUser.DayStartHour` (see `DueTimeRounder`).

## Approach

### Append-only review log

Add a new entity `ReviewLog` capturing every rate event:

```csharp
public class ReviewLog
{
    public long Id { get; set; }                // bigserial
    public string UserId { get; set; }
    public Guid CardId { get; set; }
    public string Rating { get; set; }          // again|hard|good|easy
    public DateTimeOffset ReviewedAt { get; set; }
    public DateTimeOffset? ScheduledDueAfter { get; set; }  // new due assigned by FSRS after this rating
    public string StateAfter { get; set; }      // "learning"|"review"|"relearning"
}
```

Indexes: `(UserId, ReviewedAt)`, `(UserId, CardId, ReviewedAt)`.

`ReviewService.RateCard` writes one row per call before `SaveChangesAsync`. Skipped cards are not rated → not logged (matches "skipped cards do not count").

We do **not** backfill historical reviews — the log starts when this feature ships. The "total answered" and streak start from zero/empty for existing users. That's a deliberate simplification, documented in the PR description.

### Streak semantics

A **study day** is a user-tz day with at least one `ReviewLog` row.
A **due day** is a user-tz day where the user had ≥1 card scheduled to be due at any point during that day.

Approximating "due day" without historical snapshots: a day X is a due day iff there exists a card such that, at the end of day X (user tz), the card existed and its latest scheduled-due was ≤ end-of-day X. We reconstruct "latest scheduled due as of T" as:

- The most recent `ReviewLog.ScheduledDueAfter` with `ReviewedAt <= T`, or
- If no log entries exist for the card before T: the card's initial due (treated as `CreatedAt` — i.e., new cards are due immediately).

In SQL: for each candidate day, `EXISTS` query against cards joined with their latest-before-T log row.

### Current streak

Walk back day-by-day from today (user tz), bounded by the user's earliest card-creation day or 365 days, whichever is smaller:

```
streak = 0
cursor = today
if today is a study day:
    streak = 1
cursor -= 1 day
while cursor >= earliest:
    if cursor is a study day:        streak += 1
    elif cursor is a due day:        break        # missed
    else:                            pass         # rest day, preserved
    cursor -= 1 day
return streak
```

Today is special-cased: if the user hasn't reviewed yet but the day isn't over, we don't break the streak — we just don't count today.

### Best streak

Pre-compute on first request (and update on every rate event in-memory). Cached on `AppUser.BestStreak` (new column, default 0). On each rate event:

1. Recompute current streak after the new log row.
2. If `current > user.BestStreak`, set `BestStreak = current`.

For users who already have history when this ships, `BestStreak` starts at 0 and grows with their current streak going forward.

### Totals

- `totalAnswered` = `COUNT(*)` on `ReviewLog` filtered by user.
- `answeredToday` = `COUNT(*)` on `ReviewLog` where `ReviewedAt` falls within the current user-tz day.

## API

New endpoint:

```
GET /api/review/study-stats
```

Response:

```json
{
  "currentStreak": 7,
  "bestStreak": 14,
  "totalAnswered": 482,
  "answeredToday": 12
}
```

Owned by a new service `StudyStatsService` (constructor: `AppDbContext`, `TimeProvider`). Endpoint lives in `ReviewEndpoints.cs` (consistent with the other study-related endpoints).

## Frontend — Web

`StudyView.vue` already has a stats row (`{{ totalCards }} total • {{ studiedToday }} today`). Replace with a four-stat row:

```
🔥 7 day streak     482 total answered     12 today     Best: 14
```

Implementation:

- Add `StudyStats` type and `apiFetch('/review/study-stats')` call.
- Fetch alongside existing stats in `onMounted`.
- Render under the existing "due" hero. Streak shown with a flame emoji or `🔥` (kept text-only to match existing visual restraint).

`fasolt.client/src/api/client.ts` is the path. `fasolt.client/src/types` gets a new `StudyStats` type.

## Frontend — iOS

`DashboardView.swift` already has a `statsRow` with Total / Today / Decks. Replace with a streak-centric layout:

```
[ 🔥 7 day streak ]   <- prominent badge above stats row
[Total 482]  [Today 12]  [Best 14]   <- modified statPills
```

Implementation:

- Add `StudyStatsDTO` to `APIModels.swift`.
- Add a `studyStats` field to `DashboardViewModel`, load it in `loadStats()` alongside the other parallel fetches.
- Add a streak banner above the existing stats row in `DashboardView`.
- Replace `totalCards/studiedToday/totalDecks` pills with `totalAnswered/answeredToday/bestStreak`. We keep `totalCards` for the empty-state check elsewhere in the view.

We do **not** drop `Total cards` from the iOS dashboard immediately — the existing stateBar still needs `totalCards` for proportions. We instead keep the existing `OverviewDTO` data and just add the new streak banner + replace the three stat pills.

## Testing

- Unit tests for `StudyStatsService` covering: empty history, streak with no rest days, streak with rest days, broken streak, today not yet reviewed, day-boundary TZ math.
- Integration test: hit `/api/review/study-stats`, rate a card, hit again, verify counts move.
- Manual: log in as `dev@fasolt.local`, review demo deck cards, refresh dashboard on web and iOS simulator, confirm numbers.

## Rollout

- Single PR per CLAUDE.md guidance.
- One EF migration (`AddReviewLogAndBestStreak`).
- No data backfill. Existing users start from zero for streak/total — acceptable.
