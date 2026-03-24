# Full FSRS Flow Integration Test — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add integration tests that verify FSRS spaced repetition works correctly end-to-end over a simulated 6-month timeline, including due card query filtering.

**Architecture:** Inject .NET's built-in `TimeProvider` into `ReviewEndpoints.cs` (replacing 4 hardcoded time calls). Tests use `FakeTimeProvider` to simulate time progression, combined with `TestDb` for isolated Postgres databases and FSRS.Core's `IScheduler` for deterministic scheduling.

**Tech Stack:** .NET 10, xUnit, FluentAssertions, FSRS.Core, Microsoft.Extensions.TimeProvider.Testing, EF Core + Postgres

**Spec:** `docs/superpowers/specs/2026-03-24-full-fsrs-flow-test-design.md`

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `fasolt.Tests/fasolt.Tests.csproj` | Add TimeProvider.Testing NuGet |
| Modify | `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs` | Inject `TimeProvider`, replace 4 hardcoded time calls |
| Create | `fasolt.Tests/FsrsFullFlowTests.cs` | All 3 integration tests + helper methods |

---

### Task 1: Add TimeProvider.Testing NuGet dependency

**Files:**
- Modify: `fasolt.Tests/fasolt.Tests.csproj`

- [ ] **Step 1: Add NuGet package**

Run:
```bash
cd fasolt.Tests && dotnet add package Microsoft.Extensions.TimeProvider.Testing
```

- [ ] **Step 2: Verify it restores**

Run:
```bash
dotnet restore fasolt.Tests/fasolt.Tests.csproj
```
Expected: success, no errors

- [ ] **Step 3: Commit**

```bash
git add fasolt.Tests/fasolt.Tests.csproj
git commit -m "chore: add Microsoft.Extensions.TimeProvider.Testing to test project"
```

---

### Task 2: Inject TimeProvider into ReviewEndpoints

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs`

The goal is to replace 4 hardcoded `DateTime.UtcNow`/`DateTimeOffset.UtcNow` calls with `TimeProvider`. ASP.NET Core minimal API auto-resolves `TimeProvider` from DI (already registered as `TimeProvider.System` by default).

- [ ] **Step 1: Update `GetDueCards` signature and time usage**

In `ReviewEndpoints.cs`, change the `GetDueCards` method signature to accept `TimeProvider timeProvider`:

```csharp
private static async Task<IResult> GetDueCards(
    ClaimsPrincipal principal, UserManager<AppUser> userManager, AppDbContext db, TimeProvider timeProvider, int limit = 50, string? deckId = null)
```

Replace line 56:
```csharp
// Before:
var now = DateTimeOffset.UtcNow;
// After:
var now = timeProvider.GetUtcNow();
```

- [ ] **Step 2: Update `RateCard` signature and time usage**

Change the `RateCard` method signature to accept `TimeProvider timeProvider`:

```csharp
private static async Task<IResult> RateCard(
    RateCardRequest request, ClaimsPrincipal principal, UserManager<AppUser> userManager,
    AppDbContext db, IScheduler scheduler, TimeProvider timeProvider)
```

Replace line 111:
```csharp
// Before:
var now = DateTime.UtcNow;
// After:
var now = timeProvider.GetUtcNow().UtcDateTime;
```

Replace line 120:
```csharp
// Before:
card.LastReviewedAt = DateTimeOffset.UtcNow;
// After:
card.LastReviewedAt = timeProvider.GetUtcNow();
```

- [ ] **Step 3: Update `GetStats` signature and time usage**

Change the `GetStats` method signature to accept `TimeProvider timeProvider`:

```csharp
private static async Task<IResult> GetStats(
    ClaimsPrincipal principal, UserManager<AppUser> userManager, AppDbContext db, TimeProvider timeProvider)
```

Replace line 132:
```csharp
// Before:
var now = DateTimeOffset.UtcNow;
// After:
var now = timeProvider.GetUtcNow();
```

- [ ] **Step 4: Verify the project builds**

Run:
```bash
dotnet build fasolt.Server/fasolt.Server.csproj
```
Expected: Build succeeded, 0 errors

- [ ] **Step 5: Run existing tests to confirm no regression**

Run:
```bash
dotnet test fasolt.Tests/fasolt.Tests.csproj
```
Expected: All existing tests pass

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Api/Endpoints/ReviewEndpoints.cs
git commit -m "refactor: inject TimeProvider into ReviewEndpoints for testable time"
```

---

### Task 3: Write test helpers and `Lapse_CardEntersRelearningAndRecovers` test

**Files:**
- Create: `fasolt.Tests/FsrsFullFlowTests.cs`

This task creates the test class with shared infrastructure and the simplest test first.

- [ ] **Step 1: Write the test file with helpers and the lapse test**

Create `fasolt.Tests/FsrsFullFlowTests.cs`:

```csharp
using FluentAssertions;
using FSRS.Core.Configurations;
using FSRS.Core.Enums;
using FSRS.Core.Interfaces;
using FSRS.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;
using FsrsCard = FSRS.Core.Models.Card;

namespace Fasolt.Tests;

public class FsrsFullFlowTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly IScheduler _scheduler;

    private string UserId => _db.UserId;

    public FsrsFullFlowTests()
    {
        var options = new SchedulerOptions
        {
            DesiredRetention = 0.9,
            MaximumInterval = 36500,
            EnableFuzzing = false,
        };
        _scheduler = new SchedulerFactory(options).CreateScheduler();
    }

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    // --- Helpers ---

    private static State ParseState(string state) => state switch
    {
        "learning" => State.Learning,
        "review" => State.Review,
        "relearning" => State.Relearning,
        _ => default,
    };

    private static string MapState(State state) => state switch
    {
        State.Learning => "learning",
        State.Review => "review",
        State.Relearning => "relearning",
        _ => "new",
    };

    /// <summary>
    /// Mirrors ReviewEndpoints.RateCard logic: builds FsrsCard from DB entity,
    /// calls scheduler, maps result back, saves.
    /// </summary>
    private async Task RateCardInDb(AppDbContext db, Card card, Rating rating)
    {
        var fsrsCard = card.State == "new"
            ? new FsrsCard { Due = card.DueAt?.UtcDateTime ?? card.CreatedAt.UtcDateTime }
            : new FsrsCard
            {
                State = ParseState(card.State),
                Stability = card.Stability,
                Difficulty = card.Difficulty,
                Step = card.Step,
                Due = card.DueAt?.UtcDateTime ?? card.CreatedAt.UtcDateTime,
                LastReview = card.LastReviewedAt?.UtcDateTime,
            };

        var now = _time.GetUtcNow().UtcDateTime;
        var (updated, _) = _scheduler.ReviewCard(fsrsCard, rating, now, null);

        card.Stability = updated.Stability;
        card.Difficulty = updated.Difficulty;
        card.Step = updated.Step;
        card.State = MapState(updated.State);
        card.DueAt = new DateTimeOffset(updated.Due, TimeSpan.Zero);
        card.LastReviewedAt = _time.GetUtcNow();

        await db.SaveChangesAsync();
    }

    private async Task<List<Card>> GetDueCards(AppDbContext db)
    {
        var now = _time.GetUtcNow();
        return await db.Cards
            .Where(c => c.UserId == UserId && (c.DueAt == null || c.DueAt <= now))
            .OrderBy(c => c.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync();
    }

    private Card CreateCardEntity(string front, string back)
    {
        return new Card
        {
            Id = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..12],
            UserId = UserId,
            Front = front,
            Back = back,
            State = "new",
            CreatedAt = _time.GetUtcNow(),
        };
    }

    // --- Tests ---

    [Fact]
    public async Task Lapse_CardEntersRelearningAndRecovers()
    {
        await using var db = _db.CreateDbContext();
        var card = CreateCardEntity("Lapse Q?", "Lapse A.");
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        // Rate "easy" to jump straight to review state
        await RateCardInDb(db, card, Rating.Easy);
        card.State.Should().Be("review");
        var stabilityAfterEasy = card.Stability!.Value;

        // Build stability with a few "good" reviews
        for (var i = 0; i < 3; i++)
        {
            _time.SetUtcNow(card.DueAt!.Value.AddMinutes(1));
            await RateCardInDb(db, card, Rating.Good);
            card.State.Should().Be("review");
        }
        var stabilityBeforeLapse = card.Stability!.Value;
        stabilityBeforeLapse.Should().BeGreaterThan(stabilityAfterEasy);

        // Lapse: rate "again"
        _time.SetUtcNow(card.DueAt!.Value.AddMinutes(1));
        await RateCardInDb(db, card, Rating.Again);
        card.State.Should().Be("relearning");
        card.Stability!.Value.Should().BeLessThan(stabilityBeforeLapse,
            "stability should decrease after lapse");

        // Recover: rate "good" until back in review
        var maxSteps = 5;
        for (var i = 0; i < maxSteps && card.State != "review"; i++)
        {
            _time.SetUtcNow(card.DueAt!.Value.AddMinutes(1));
            await RateCardInDb(db, card, Rating.Good);
        }
        card.State.Should().Be("review", "card should recover from relearning");
        card.Stability!.Value.Should().BeLessThan(stabilityBeforeLapse,
            "post-lapse stability should be lower than pre-lapse");
    }
}
```

- [ ] **Step 2: Run test to verify it passes**

Run:
```bash
dotnet test fasolt.Tests/fasolt.Tests.csproj --filter "FsrsFullFlowTests.Lapse_CardEntersRelearningAndRecovers" -v normal
```
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add fasolt.Tests/FsrsFullFlowTests.cs
git commit -m "test: add FSRS lapse lifecycle integration test with FakeTimeProvider"
```

---

### Task 4: Write `DueCardQuery_RespectsSimulatedTime` test

**Files:**
- Modify: `fasolt.Tests/FsrsFullFlowTests.cs`

- [ ] **Step 1: Add the due card query test**

Add this test method to `FsrsFullFlowTests`:

```csharp
[Fact]
public async Task DueCardQuery_RespectsSimulatedTime()
{
    await using var db = _db.CreateDbContext();
    var start = _time.GetUtcNow();

    // Create 3 cards and get them into review state
    var card1Day = CreateCardEntity("Due 1d Q?", "Due 1d A.");
    var card7Day = CreateCardEntity("Due 7d Q?", "Due 7d A.");
    var card30Day = CreateCardEntity("Due 30d Q?", "Due 30d A.");
    db.Cards.AddRange(card1Day, card7Day, card30Day);
    await db.SaveChangesAsync();

    // Rate all "easy" to get into review state
    foreach (var card in new[] { card1Day, card7Day, card30Day })
    {
        await RateCardInDb(db, card, Rating.Easy);
        card.State.Should().Be("review");
    }

    // Manually set DueAt for precise control
    card1Day.DueAt = start.AddDays(1);
    card7Day.DueAt = start.AddDays(7);
    card30Day.DueAt = start.AddDays(30);
    await db.SaveChangesAsync();

    // Day 2: only card1Day should be due
    _time.SetUtcNow(start.AddDays(2));
    var due = await GetDueCards(db);
    due.Should().ContainSingle(c => c.PublicId == card1Day.PublicId);

    // Day 8: card1Day and card7Day should be due
    _time.SetUtcNow(start.AddDays(8));
    due = await GetDueCards(db);
    due.Select(c => c.PublicId).Should().BeEquivalentTo(
        new[] { card1Day.PublicId, card7Day.PublicId });

    // Day 31: all 3 should be due
    _time.SetUtcNow(start.AddDays(31));
    due = await GetDueCards(db);
    due.Select(c => c.PublicId).Should().BeEquivalentTo(
        new[] { card1Day.PublicId, card7Day.PublicId, card30Day.PublicId });
}
```

- [ ] **Step 2: Run test to verify it passes**

Run:
```bash
dotnet test fasolt.Tests/fasolt.Tests.csproj --filter "FsrsFullFlowTests.DueCardQuery_RespectsSimulatedTime" -v normal
```
Expected: PASS

- [ ] **Step 3: Commit**

```bash
git add fasolt.Tests/FsrsFullFlowTests.cs
git commit -m "test: add due card query time-filtering integration test"
```

---

### Task 5: Write `SixMonthSimulation_CardsScheduledCorrectly` test

**Files:**
- Modify: `fasolt.Tests/FsrsFullFlowTests.cs`

This is the big one. It creates 5 cards and simulates 180 days of study.

- [ ] **Step 1: Add the 6-month simulation test**

Add this test method to `FsrsFullFlowTests`:

```csharp
[Fact]
public async Task SixMonthSimulation_CardsScheduledCorrectly()
{
    await using var db = _db.CreateDbContext();
    var start = _time.GetUtcNow();

    // Create 5 cards
    var cards = Enumerable.Range(1, 5)
        .Select(i => CreateCardEntity($"Q{i}?", $"A{i}."))
        .ToList();
    db.Cards.AddRange(cards);
    await db.SaveChangesAsync();

    // Tracking: per-card state history and interval tracking for monotonicity
    var statesReached = cards.ToDictionary(c => c.PublicId, _ => new HashSet<string>());
    var lapsedCards = new HashSet<string>();
    // Track consecutive "good" intervals in review state per card (reset on lapse)
    var consecutiveGoodIntervals = cards.ToDictionary(c => c.PublicId, _ => new List<TimeSpan>());
    var reviewCounts = cards.ToDictionary(c => c.PublicId, _ => 0);

    // Simulate 180 days
    for (var day = 0; day < 180; day++)
    {
        _time.SetUtcNow(start.AddDays(day));
        var dueCards = await GetDueCards(db);

        foreach (var card in dueCards)
        {
            var totalReviews = reviewCounts[card.PublicId]++;
            var previousState = card.State;
            var previousDueAt = card.DueAt;

            // Rating pattern: mostly good, occasional again for lapses
            Rating rating;
            if (previousState == "review" && totalReviews > 0 && totalReviews % 20 == 0)
            {
                rating = Rating.Again; // trigger lapse every ~20th review
            }
            else if (previousState == "review" && totalReviews % 15 == 0)
            {
                rating = Rating.Easy;
            }
            else
            {
                rating = Rating.Good;
            }

            await RateCardInDb(db, card, rating);

            // Track state
            statesReached[card.PublicId].Add(card.State);

            // Track lapses
            if (previousState == "review" && card.State == "relearning")
            {
                lapsedCards.Add(card.PublicId);
                consecutiveGoodIntervals[card.PublicId].Clear(); // reset on lapse
            }

            // Track consecutive good intervals in review state
            if (rating == Rating.Good && card.State == "review" && previousDueAt.HasValue)
            {
                var interval = card.DueAt!.Value - _time.GetUtcNow();
                consecutiveGoodIntervals[card.PublicId].Add(interval);
            }

            // Invariants: every review should produce valid state
            card.Stability.Should().BeGreaterThan(0,
                $"card {card.PublicId} should have positive stability after review on day {day}");
            card.DueAt.Should().BeAfter(_time.GetUtcNow(),
                $"card {card.PublicId} due date should be in the future after review on day {day}");
            card.State.Should().BeOneOf("learning", "review", "relearning",
                $"card {card.PublicId} should be in a valid state after review on day {day}");
        }
    }

    // --- Final assertions ---

    // All cards should have reached review state at some point
    foreach (var (publicId, states) in statesReached)
    {
        states.Should().Contain("review",
            $"card {publicId} should have reached review state within 180 days");
    }

    // At least one card should have lapsed
    lapsedCards.Should().NotBeEmpty(
        "at least one card should have experienced a lapse during 180 days");

    // Intervals should grow for cards with consecutive good ratings in review state
    foreach (var (publicId, intervals) in consecutiveGoodIntervals)
    {
        if (intervals.Count < 3) continue; // need enough data points
        var lastThree = intervals.Skip(intervals.Count - 3).ToList();
        for (var i = 1; i < lastThree.Count; i++)
        {
            lastThree[i].Should().BeGreaterThanOrEqualTo(lastThree[i - 1],
                $"card {publicId}: interval {i} should be >= interval {i - 1} for consecutive good reviews");
        }
    }

    // Cards in review state should have intervals measured in days, not minutes
    foreach (var card in cards.Where(c => c.State == "review"))
    {
        var interval = card.DueAt!.Value - _time.GetUtcNow();
        interval.Should().BeGreaterThan(TimeSpan.FromHours(12),
            $"card {card.PublicId} in review state after 180 days should have interval > 12 hours");
    }
}
```

- [ ] **Step 2: Run test to verify it passes**

Run:
```bash
dotnet test fasolt.Tests/fasolt.Tests.csproj --filter "FsrsFullFlowTests.SixMonthSimulation_CardsScheduledCorrectly" -v normal
```
Expected: PASS

- [ ] **Step 3: Run all tests to confirm no regressions**

Run:
```bash
dotnet test fasolt.Tests/fasolt.Tests.csproj -v normal
```
Expected: All tests pass

- [ ] **Step 4: Commit**

```bash
git add fasolt.Tests/FsrsFullFlowTests.cs
git commit -m "test: add 6-month FSRS simulation integration test"
```

---

### Task 6: Move requirement to done

**Files:**
- Move: `docs/requirements/18_full_fsrs_flow_test.md` → `docs/requirements/done/18_full_fsrs_flow_test.md`

- [ ] **Step 1: Move the requirement file**

```bash
mv docs/requirements/18_full_fsrs_flow_test.md docs/requirements/done/18_full_fsrs_flow_test.md
```

- [ ] **Step 2: Commit**

```bash
git add docs/requirements/18_full_fsrs_flow_test.md docs/requirements/done/18_full_fsrs_flow_test.md
git commit -m "docs: move full FSRS flow test requirement to done"
```
