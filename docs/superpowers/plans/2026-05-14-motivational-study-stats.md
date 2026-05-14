# Motivational study stats — Implementation plan

Spec: `docs/superpowers/specs/2026-05-14-motivational-study-stats-design.md`
Branch: `feat/study-stats-dashboard`

## API contract (locked)

```
GET /api/review/study-stats
```

Response (200):

```json
{
  "currentStreak": 0,
  "bestStreak": 0,
  "totalAnswered": 0,
  "answeredToday": 0
}
```

All fields are non-negative integers. All four fields are always present (no nulls).

## Track 1 — Backend (.NET)

1. **Entity** `fasolt.Server/Domain/Entities/ReviewLog.cs`:
   ```csharp
   public class ReviewLog
   {
       public long Id { get; set; }
       public string UserId { get; set; } = default!;
       public AppUser User { get; set; } = default!;
       public Guid CardId { get; set; }
       public Card Card { get; set; } = default!;
       public string Rating { get; set; } = default!;
       public DateTimeOffset ReviewedAt { get; set; }
       public DateTimeOffset? ScheduledDueAfter { get; set; }
       public string StateAfter { get; set; } = default!;
   }
   ```

2. **AppDbContext**: register `DbSet<ReviewLog>`, configure key + indexes:
   - PK `Id` (long, identity)
   - Index `(UserId, ReviewedAt)`
   - Index `(CardId, ReviewedAt)`
   - FK `UserId` → AppUser, OnDelete Cascade
   - FK `CardId` → Card, OnDelete Cascade
   - `Rating` HasMaxLength(20), `StateAfter` HasMaxLength(20)

3. **AppUser**: add `public int BestStreak { get; set; } = 0;` and configure default in `OnModelCreating`.

4. **Migration** named `AddReviewLogAndBestStreak`. Create with:
   ```
   dotnet ef migrations add AddReviewLogAndBestStreak --project fasolt.Server --output-dir Infrastructure/Data/Migrations
   ```

5. **ReviewService.RateCard**: after FSRS update, append a `ReviewLog` row before `SaveChangesAsync`. Then call `StudyStatsService.UpdateBestStreakIfNeeded(userId)` (so streak best persists). Order: log row first, then bestStreak recompute (uses freshly written log).

6. **New service** `fasolt.Server/Application/Services/StudyStatsService.cs`:

   Constructor: `(AppDbContext db, TimeProvider timeProvider)`.

   Public methods:
   - `Task<StudyStatsDto> GetStats(string userId)`
   - `Task UpdateBestStreakIfNeeded(string userId)`

   Algorithm for `GetStats`:
   - Load user (for `TimeZone`, `DayStartHour`, `BestStreak`).
   - Determine today's user-local day boundary via `DueTimeRounder.ResolveTimeZone` + `DayStartHour`.
   - `totalAnswered = COUNT(*)` on `ReviewLogs` for user.
   - `answeredToday = COUNT(*)` on `ReviewLogs` where ReviewedAt within `[todayStart, tomorrowStart)`.
   - `currentStreak`: walk back day-by-day from today (max 365 iterations):
     - Day is **study day** if any `ReviewLog` row falls within its [start, end).
     - Day is **due day** if `EXISTS` a card created on/before day-end whose latest-known scheduled-due as of day-end ≤ day-end. Latest-known = `MAX(ReviewLog.ScheduledDueAfter)` for that card with `ReviewedAt <= dayEnd`, or `Card.CreatedAt` if none.
     - Loop:
       ```
       if today is study day:
           streak = 1
       cursor = today - 1
       while cursor >= cutoff:
           if cursor is study day: streak += 1
           elif cursor is due day: break
           cursor -= 1
       ```
   - `bestStreak = max(user.BestStreak, currentStreak)`.

   `UpdateBestStreakIfNeeded`: compute current streak; if > user.BestStreak, persist new value.

   **Performance note**: do the per-day "study day" and "due day" checks via batched SQL where possible. Simplest acceptable v1: fetch the set of distinct study days for the user in one query (postgres `date_trunc` or app-side bucketing of ReviewedAt). Then per-day "due day" check is one query per day during the gap-walk. Bound the walk at 365 days.

7. **DTO** `fasolt.Server/Application/Dtos/StudyStatsDto.cs`:
   ```csharp
   public record StudyStatsDto(int CurrentStreak, int BestStreak, int TotalAnswered, int AnsweredToday);
   ```

8. **Endpoint**: in `ReviewEndpoints.cs`, add `group.MapGet("/study-stats", GetStudyStats);` and handler using `StudyStatsService`.

9. **DI**: register `StudyStatsService` in `Program.cs` alongside the others.

10. **Tests** `fasolt.Tests/StudyStatsServiceTests.cs`:
    - Empty: all zeros.
    - One review today: currentStreak=1, totalAnswered=1, answeredToday=1, bestStreak=1.
    - Two consecutive days: streak=2.
    - Gap day with no due cards: streak preserved across gap (use FakeTimeProvider, rate easy to push due far out, jump 2 days, rate again on a new card created the next day — verify streak counts 2 with a no-due gap between).
    - Gap day with due cards but no review: streak breaks.
    - BestStreak persists when current streak resets.

## Track 2 — Web frontend (Vue)

1. **Type** `fasolt.client/src/types/index.ts`: add
   ```ts
   export interface StudyStats {
     currentStreak: number
     bestStreak: number
     totalAnswered: number
     answeredToday: number
   }
   ```

2. **Store** `fasolt.client/src/stores/review.ts`: add
   ```ts
   async function fetchStudyStats(): Promise<StudyStats> {
     return apiFetch<StudyStats>('/review/study-stats')
   }
   ```
   Return it from the store.

3. **View** `fasolt.client/src/views/StudyView.vue`:
   - Add `studyStats` ref `{ currentStreak: 0, bestStreak: 0, totalAnswered: 0, answeredToday: 0 }`.
   - Fetch in `onMounted` parallel with `fetchStats()`.
   - Render under the existing CTA, before the "Study by deck" section.
   - Layout — a small horizontally-arranged row of four stats with the streak emphasized:
     ```
     [🔥 7]            482            12            14
     day streak        answered       today         best
     ```
   - Use existing typography (Tailwind classes already in this file).
   - If `totalAnswered === 0`, hide the row (users with no review history don't need it).

## Track 3 — iOS frontend

1. **DTO** `fasolt.ios/Fasolt/Models/APIModels.swift`: add
   ```swift
   struct StudyStatsDTO: Decodable, Sendable {
       let currentStreak: Int
       let bestStreak: Int
       let totalAnswered: Int
       let answeredToday: Int
   }
   ```

2. **ViewModel** `fasolt.ios/Fasolt/ViewModels/DashboardViewModel.swift`:
   - Add `var studyStats: StudyStatsDTO?`.
   - In `loadStats()`, add a fourth `async let` fetching `/api/review/study-stats`; on success store on `studyStats`; on failure leave existing data intact.

3. **View** `fasolt.ios/Fasolt/Views/Dashboard/DashboardView.swift`:
   - Add a `streakBanner` view rendered above `statsRow` when `viewModel.studyStats?.totalAnswered ?? 0 > 0`:
     ```
     🔥  7 day streak     · best 14
     ```
     Use `.ultraThinMaterial` background + rounded rectangle to match other cards.
   - Modify `statsRow` so the three pills become "Total answered" / "Today" / "Best streak" (using `studyStats` values) when `studyStats` is present; otherwise keep the existing Total/Today/Decks fallback.
   - Existing `stateBar` and `deckSection` stay unchanged.

## Verification

After all three tracks land:

1. `dotnet test fasolt.Tests` — all green
2. `dotnet build` — no warnings introduced
3. `cd fasolt.client && npx tsc --noEmit && npm run build` — clean
4. iOS: `xcodebuild -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 17 Pro' build` (or similar) — succeeds
5. Manual: `make dev`, log in as dev user, rate a card on web, refresh — see numbers move. Same on iOS sim.
