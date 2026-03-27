# Service Layer Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract business logic from 3 endpoint files into service classes and fix a circular dependency in SearchService.

**Architecture:** Move logic from endpoint layer to Application/Services, keeping endpoints as thin HTTP delegates. Fix SearchService's import of Api.Endpoints by moving DTOs to Application/Dtos.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, FSRS.Core, xUnit + FluentAssertions

---

## Stream A: Search DTOs (ARCH-H004)

### Task A1: Move search DTOs to Application layer

**Files:**
- Create: `fasolt.Server/Application/Dtos/SearchDtos.cs`
- Modify: `fasolt.Server/Api/Endpoints/SearchEndpoints.cs` — remove DTO records, add using
- Modify: `fasolt.Server/Application/Services/SearchService.cs` — replace `Api.Endpoints` import with `Application.Dtos`

- [ ] **Step 1: Create `Application/Dtos/SearchDtos.cs`**

```csharp
namespace Fasolt.Server.Application.Dtos;

public record SearchResponse(
    List<CardSearchResult> Cards,
    List<DeckSearchResult> Decks);

public record CardSearchResult(string Id, string Headline, string State);
public record DeckSearchResult(string Id, string Headline, int CardCount);
```

- [ ] **Step 2: Update SearchService.cs — fix circular dependency**

Replace:
```csharp
using Fasolt.Server.Api.Endpoints;
```
With:
```csharp
using Fasolt.Server.Application.Dtos;
```

- [ ] **Step 3: Update SearchEndpoints.cs — remove DTOs, add import**

Remove the 3 record declarations at the bottom of the file. Add:
```csharp
using Fasolt.Server.Application.Dtos;
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build fasolt.Server`
Expected: Success, no errors

- [ ] **Step 5: Run existing search tests**

Run: `dotnet test fasolt.Tests --filter SearchService`
Expected: All pass

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Application/Dtos/SearchDtos.cs fasolt.Server/Api/Endpoints/SearchEndpoints.cs fasolt.Server/Application/Services/SearchService.cs
git commit -m "refactor: move search DTOs to Application layer (ARCH-H004)"
```

---

## Stream B: ReviewService (ARCH-H001)

### Task B1: Create ReviewService

**Files:**
- Create: `fasolt.Server/Application/Services/ReviewService.cs`
- Modify: `fasolt.Server/Program.cs:202` — register service
- Modify: `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs` — thin out to delegate

- [ ] **Step 1: Create `Application/Services/ReviewService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using FSRS.Core.Enums;
using FSRS.Core.Interfaces;
using FsrsCard = FSRS.Core.Models.Card;

namespace Fasolt.Server.Application.Services;

public class ReviewService(AppDbContext db, IScheduler scheduler, TimeProvider timeProvider)
{
    private static readonly Dictionary<string, Rating> ValidRatings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["again"] = Rating.Again,
        ["hard"] = Rating.Hard,
        ["good"] = Rating.Good,
        ["easy"] = Rating.Easy,
    };

    internal static string MapState(State state) => state switch
    {
        State.Learning => "learning",
        State.Review => "review",
        State.Relearning => "relearning",
        _ => "new",
    };

    internal static State ParseState(string state) => state switch
    {
        "learning" => State.Learning,
        "review" => State.Review,
        "relearning" => State.Relearning,
        _ => default,
    };

    public async Task<List<DueCardDto>> GetDueCards(string userId, int limit = 50, string? deckId = null)
    {
        var take = Math.Clamp(limit, 1, 200);
        var now = timeProvider.GetUtcNow();
        var query = db.Cards
            .Where(c => c.UserId == userId && (c.DueAt == null || c.DueAt <= now));

        query = query.Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive));

        if (deckId is not null)
        {
            var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == userId);
            if (deck is null) return null!; // endpoint returns NotFound
            if (!deck.IsActive) return [];
            query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deck.Id));
        }

        return await query
            .OrderBy(c => c.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(c => c.CreatedAt)
            .Take(take)
            .Select(c => new DueCardDto(c.PublicId, c.Front, c.Back, c.SourceFile, c.SourceHeading, c.State, c.FrontSvg, c.BackSvg))
            .ToListAsync();
    }

    public async Task<RateCardResponse?> RateCard(string userId, RateCardRequest request)
    {
        if (!ValidRatings.TryGetValue(request.Rating, out var fsrsRating))
            return null; // endpoint returns ValidationProblem

        var card = await db.Cards.FirstOrDefaultAsync(c => c.PublicId == request.CardId && c.UserId == userId);
        if (card is null) return null;

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

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var (updated, _) = scheduler.ReviewCard(fsrsCard, fsrsRating, now, null);

        card.Stability = updated.Stability;
        card.Difficulty = updated.Difficulty;
        card.Step = updated.Step;
        card.State = MapState(updated.State);
        card.DueAt = new DateTimeOffset(updated.Due, TimeSpan.Zero);
        card.LastReviewedAt = timeProvider.GetUtcNow();

        await db.SaveChangesAsync();
        return new RateCardResponse(card.PublicId, card.Stability, card.Difficulty, card.DueAt, card.State);
    }

    public async Task<ReviewStatsDto> GetStats(string userId)
    {
        var now = timeProvider.GetUtcNow();
        var activeCards = db.Cards
            .Where(c => c.UserId == userId)
            .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive));

        var dueCount = await activeCards.CountAsync(c => c.DueAt == null || c.DueAt <= now);
        var totalCards = await activeCards.CountAsync();
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var studiedToday = await activeCards.CountAsync(c =>
            c.LastReviewedAt != null && c.LastReviewedAt >= todayStart);

        return new ReviewStatsDto(dueCount, totalCards, studiedToday);
    }
}
```

Note: `GetDueCards` returns `null!` when deck not found — the endpoint checks for this to return 404. This preserves the existing HTTP semantics without coupling the service to `IResult`. A cleaner approach would be a result type, but YAGNI for this refactor.

- [ ] **Step 2: Register in Program.cs**

After the existing `builder.Services.AddScoped<OverviewService>();` line (~207), add:
```csharp
builder.Services.AddScoped<ReviewService>();
```

- [ ] **Step 3: Thin out ReviewEndpoints.cs**

Replace the entire file with:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/review").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/due", GetDueCards);
        group.MapPost("/rate", RateCard);
        group.MapGet("/stats", GetStats);
        group.MapGet("/overview", GetOverview);
    }

    private static async Task<IResult> GetDueCards(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, ReviewService reviewService,
        int limit = 50, string? deckId = null)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var cards = await reviewService.GetDueCards(user.Id, limit, deckId);
        if (cards is null) return Results.NotFound();
        return Results.Ok(cards);
    }

    private static async Task<IResult> RateCard(
        RateCardRequest request, ClaimsPrincipal principal, UserManager<AppUser> userManager,
        ReviewService reviewService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        // Check if rating is valid before calling service
        var validRatings = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "again", "hard", "good", "easy" };
        if (!validRatings.Contains(request.Rating))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rating"] = ["Rating must be 'again', 'hard', 'good', or 'easy'."]
            });

        var result = await reviewService.RateCard(user.Id, request);
        if (result is null) return Results.NotFound();
        return Results.Ok(result);
    }

    private static async Task<IResult> GetStats(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, ReviewService reviewService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var stats = await reviewService.GetStats(user.Id);
        return Results.Ok(stats);
    }

    private static async Task<IResult> GetOverview(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, OverviewService overviewService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var overview = await overviewService.GetOverview(user.Id);
        return Results.Ok(overview);
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build fasolt.Server`
Expected: Success

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Application/Services/ReviewService.cs fasolt.Server/Api/Endpoints/ReviewEndpoints.cs fasolt.Server/Program.cs
git commit -m "refactor: extract ReviewService from ReviewEndpoints (ARCH-H001)"
```

### Task B2: Rewrite review tests

**Files:**
- Delete: `fasolt.Tests/FsrsSchedulingTests.cs`
- Rewrite: `fasolt.Tests/FsrsFullFlowTests.cs` → rename to `fasolt.Tests/ReviewServiceTests.cs`
- Modify: `fasolt.Tests/ReviewTests.cs` — update to use ReviewService

- [ ] **Step 1: Delete FsrsSchedulingTests.cs**

```bash
rm fasolt.Tests/FsrsSchedulingTests.cs
```

This file only tests the third-party FSRS.Core library directly — not our code.

- [ ] **Step 2: Rewrite FsrsFullFlowTests.cs as ReviewServiceTests.cs**

Delete `fasolt.Tests/FsrsFullFlowTests.cs` and create `fasolt.Tests/ReviewServiceTests.cs`:

```csharp
using FluentAssertions;
using FSRS.Core.Configurations;
using FSRS.Core.Interfaces;
using FSRS.Core.Services;
using Microsoft.Extensions.Time.Testing;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class ReviewServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly IScheduler _scheduler;

    private string UserId => _db.UserId;

    public ReviewServiceTests()
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

    private ReviewService CreateService(Server.Infrastructure.Data.AppDbContext db)
        => new(db, _scheduler, _time);

    private async Task<string> CreateCard(Server.Infrastructure.Data.AppDbContext db, string front, string back)
    {
        var cardSvc = new CardService(db);
        var card = await cardSvc.CreateCard(UserId, front, back, null, null);
        return card.Id;
    }

    // --- RateCard tests ---

    [Fact]
    public async Task RateCard_InvalidRating_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Q?", "A.");

        var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "invalid"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task RateCard_CardNotFound_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.RateCard(UserId, new RateCardRequest("nonexistent", "good"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task RateCard_Good_MovesToLearning()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Q?", "A.");

        var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "good"));

        result.Should().NotBeNull();
        result!.State.Should().Be("learning");
        result.Stability.Should().BeGreaterThan(0);
        result.DueAt.Should().BeAfter(_time.GetUtcNow());
    }

    [Fact]
    public async Task RateCard_Easy_MovesToReview()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Q?", "A.");

        var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "easy"));

        result.Should().NotBeNull();
        result!.State.Should().Be("review");
    }

    // --- Lapse and recovery ---

    [Fact]
    public async Task Lapse_CardEntersRelearningAndRecovers()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Lapse Q?", "Lapse A.");

        // Rate easy to get into review
        var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "easy"));
        result!.State.Should().Be("review");
        var stabilityAfterEasy = result.Stability!.Value;

        // Build stability with good reviews
        for (var i = 0; i < 3; i++)
        {
            _time.SetUtcNow(result!.DueAt!.Value.AddMinutes(1));
            result = await svc.RateCard(UserId, new RateCardRequest(cardId, "good"));
            result!.State.Should().Be("review");
        }
        var stabilityBeforeLapse = result!.Stability!.Value;
        stabilityBeforeLapse.Should().BeGreaterThan(stabilityAfterEasy);

        // Lapse: rate again
        _time.SetUtcNow(result.DueAt!.Value.AddMinutes(1));
        result = await svc.RateCard(UserId, new RateCardRequest(cardId, "again"));
        result!.State.Should().Be("relearning");
        result.Stability!.Value.Should().BeLessThan(stabilityBeforeLapse);

        // Recover
        for (var i = 0; i < 5 && result!.State != "review"; i++)
        {
            _time.SetUtcNow(result.DueAt!.Value.AddMinutes(1));
            result = await svc.RateCard(UserId, new RateCardRequest(cardId, "good"));
        }
        result!.State.Should().Be("review");
    }

    // --- GetDueCards tests ---

    [Fact]
    public async Task GetDueCards_ReturnsNewCards()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Due Q?", "Due A.");

        var due = await svc.GetDueCards(UserId);

        due.Should().ContainSingle(c => c.Id == cardId);
    }

    [Fact]
    public async Task GetDueCards_ExcludesFutureCards()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Q?", "A.");

        // Rate easy to push due date into the future
        var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "easy"));

        var due = await svc.GetDueCards(UserId);
        due.Should().NotContain(c => c.Id == cardId);
    }

    [Fact]
    public async Task GetDueCards_RespectsTimeProgression()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var start = _time.GetUtcNow();

        var card1Id = await CreateCard(db, "Due 1d Q?", "Due 1d A.");
        var card2Id = await CreateCard(db, "Due 7d Q?", "Due 7d A.");

        // Rate both easy to get into review
        await svc.RateCard(UserId, new RateCardRequest(card1Id, "easy"));
        await svc.RateCard(UserId, new RateCardRequest(card2Id, "easy"));

        // Manually set due dates for precise control
        var card1 = await db.Cards.FindAsync(db.Cards.First(c => c.PublicId == card1Id).Id);
        var card2 = await db.Cards.FindAsync(db.Cards.First(c => c.PublicId == card2Id).Id);
        card1!.DueAt = start.AddDays(1);
        card2!.DueAt = start.AddDays(7);
        await db.SaveChangesAsync();

        // Day 2: only card1 due
        _time.SetUtcNow(start.AddDays(2));
        var due = await svc.GetDueCards(UserId);
        due.Should().ContainSingle(c => c.Id == card1Id);

        // Day 8: both due
        _time.SetUtcNow(start.AddDays(8));
        due = await svc.GetDueCards(UserId);
        due.Select(c => c.Id).Should().Contain(card1Id).And.Contain(card2Id);
    }

    [Fact]
    public async Task GetDueCards_NonexistentDeck_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.GetDueCards(UserId, deckId: "nonexistent");

        result.Should().BeNull();
    }

    // --- GetStats tests ---

    [Fact]
    public async Task GetStats_CountsDueAndTotal()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        await CreateCard(db, "Q1?", "A1.");
        await CreateCard(db, "Q2?", "A2.");

        var stats = await svc.GetStats(UserId);

        stats.TotalCards.Should().Be(2);
        stats.DueCount.Should().Be(2); // new cards are immediately due
        stats.StudiedToday.Should().Be(0);
    }

    [Fact]
    public async Task GetStats_TracksStudiedToday()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Q?", "A.");

        await svc.RateCard(UserId, new RateCardRequest(cardId, "good"));

        var stats = await svc.GetStats(UserId);
        stats.StudiedToday.Should().Be(1);
    }

    // --- 6-month simulation (ported from FsrsFullFlowTests) ---

    [Fact]
    public async Task SixMonthSimulation_CardsScheduledCorrectly()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var start = _time.GetUtcNow();

        // Create 5 cards
        var cardIds = new List<string>();
        for (var i = 1; i <= 5; i++)
            cardIds.Add(await CreateCard(db, $"Q{i}?", $"A{i}."));

        var statesReached = cardIds.ToDictionary(id => id, _ => new HashSet<string>());
        var lapsedCards = new HashSet<string>();
        var reviewCounts = cardIds.ToDictionary(id => id, _ => 0);
        var consecutiveGoodIntervals = cardIds.ToDictionary(id => id, _ => new List<TimeSpan>());

        for (var day = 0; day < 180; day++)
        {
            _time.SetUtcNow(start.AddDays(day));
            var dueCards = await svc.GetDueCards(UserId, limit: 200);

            foreach (var dueCard in dueCards)
            {
                var totalReviews = reviewCounts[dueCard.Id]++;
                var previousState = dueCard.State;

                string rating;
                if (previousState == "review" && totalReviews == 3)
                    rating = "again";
                else if (previousState == "review" && totalReviews % 7 == 0)
                    rating = "easy";
                else
                    rating = "good";

                var result = await svc.RateCard(UserId, new RateCardRequest(dueCard.Id, rating));
                result.Should().NotBeNull();

                statesReached[dueCard.Id].Add(result!.State);

                if (previousState == "review" && result.State == "relearning")
                {
                    lapsedCards.Add(dueCard.Id);
                    consecutiveGoodIntervals[dueCard.Id].Clear();
                }

                if (rating == "good" && result.State == "review")
                {
                    var interval = result.DueAt!.Value - _time.GetUtcNow();
                    consecutiveGoodIntervals[dueCard.Id].Add(interval);
                }

                result.Stability.Should().BeGreaterThan(0);
                result.DueAt.Should().BeAfter(_time.GetUtcNow());
                result.State.Should().BeOneOf("learning", "review", "relearning");
            }
        }

        foreach (var (id, states) in statesReached)
            states.Should().Contain("review", $"card {id} should reach review state");

        lapsedCards.Should().NotBeEmpty();

        foreach (var (id, intervals) in consecutiveGoodIntervals)
        {
            if (intervals.Count < 3) continue;
            var lastThree = intervals.Skip(intervals.Count - 3).ToList();
            for (var i = 1; i < lastThree.Count; i++)
                lastThree[i].Should().BeGreaterThanOrEqualTo(lastThree[i - 1]);
        }
    }
}
```

- [ ] **Step 3: Update ReviewTests.cs to use ReviewService**

Replace `fasolt.Tests/ReviewTests.cs`:

```csharp
using FluentAssertions;
using FSRS.Core.Configurations;
using FSRS.Core.Services;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class ReviewTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task NewCard_AppearsDue()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var scheduler = new SchedulerFactory(new SchedulerOptions
        {
            DesiredRetention = 0.9, MaximumInterval = 36500, EnableFuzzing = false,
        }).CreateScheduler();
        var reviewSvc = new ReviewService(db, scheduler, TimeProvider.System);

        var card = await cardSvc.CreateCard(UserId, "Review Q?", "Review A.", "review-source.md", "## Section");

        var dueCards = await reviewSvc.GetDueCards(UserId);

        dueCards.Should().Contain(c => c.Id == card.Id);
        var target = dueCards.Single(c => c.Id == card.Id);
        target.SourceFile.Should().Be("review-source.md");
        target.SourceHeading.Should().Be("## Section");
    }
}
```

- [ ] **Step 4: Run all tests**

Run: `dotnet test fasolt.Tests`
Expected: All pass

- [ ] **Step 5: Commit**

```bash
git add -A fasolt.Tests/
git commit -m "test: rewrite review tests to use ReviewService directly"
```

---

## Stream C: DeviceTokenService (ARCH-H002)

### Task C1: Create DeviceTokenService and NotificationDtos

**Files:**
- Create: `fasolt.Server/Application/Services/DeviceTokenService.cs`
- Create: `fasolt.Server/Application/Dtos/NotificationDtos.cs`
- Modify: `fasolt.Server/Program.cs` — register service
- Modify: `fasolt.Server/Api/Endpoints/NotificationEndpoints.cs` — thin out, remove DTOs

- [ ] **Step 1: Create `Application/Dtos/NotificationDtos.cs`**

```csharp
namespace Fasolt.Server.Application.Dtos;

public record UpsertDeviceTokenRequest(string Token);
public record UpdateNotificationSettingsRequest(int IntervalHours);
public record NotificationSettingsResponse(int IntervalHours, bool HasDeviceToken);
```

- [ ] **Step 2: Create `Application/Services/DeviceTokenService.cs`**

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class DeviceTokenService(AppDbContext db, UserManager<AppUser> userManager)
{
    private static readonly int[] AllowedIntervals = [4, 6, 8, 10, 12, 24];

    public async Task UpsertDeviceToken(string userId, string token)
    {
        var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == userId);
        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.Token = token;
            existing.UpdatedAt = now;
        }
        else
        {
            db.DeviceTokens.Add(new DeviceToken
            {
                UserId = userId,
                Token = token,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteDeviceToken(string userId)
    {
        var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == userId);
        if (existing is not null)
        {
            db.DeviceTokens.Remove(existing);
            await db.SaveChangesAsync();
        }
    }

    public async Task<NotificationSettingsResponse> GetSettings(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        var hasToken = await db.DeviceTokens.AnyAsync(d => d.UserId == userId);
        return new NotificationSettingsResponse(user!.NotificationIntervalHours, hasToken);
    }

    public async Task<bool> UpdateSettings(string userId, int intervalHours)
    {
        if (!AllowedIntervals.Contains(intervalHours))
            return false;

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return false;

        user.NotificationIntervalHours = intervalHours;
        await userManager.UpdateAsync(user);
        return true;
    }
}
```

- [ ] **Step 3: Register in Program.cs**

Add after `ReviewService` registration:
```csharp
builder.Services.AddScoped<DeviceTokenService>();
```

- [ ] **Step 4: Thin out NotificationEndpoints.cs**

Replace the entire file:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization().RequireRateLimiting("api");

        group.MapPut("/device-token", UpsertDeviceToken);
        group.MapDelete("/device-token", DeleteDeviceToken);
        group.MapGet("/settings", GetSettings);
        group.MapPut("/settings", UpdateSettings);
    }

    private static async Task<IResult> UpsertDeviceToken(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeviceTokenService service,
        UpsertDeviceTokenRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest("Token is required.");

        await service.UpsertDeviceToken(user.Id, request.Token);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteDeviceToken(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeviceTokenService service)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        await service.DeleteDeviceToken(user.Id);
        return Results.NoContent();
    }

    private static async Task<IResult> GetSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeviceTokenService service)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var settings = await service.GetSettings(user.Id);
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeviceTokenService service,
        UpdateNotificationSettingsRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var success = await service.UpdateSettings(user.Id, request.IntervalHours);
        if (!success)
            return Results.BadRequest($"intervalHours must be one of: 4, 6, 8, 10, 12, 24");

        return Results.NoContent();
    }
}
```

- [ ] **Step 5: Build and run tests**

Run: `dotnet build fasolt.Server && dotnet test fasolt.Tests`
Expected: Success

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Application/Services/DeviceTokenService.cs fasolt.Server/Application/Dtos/NotificationDtos.cs fasolt.Server/Api/Endpoints/NotificationEndpoints.cs fasolt.Server/Program.cs
git commit -m "refactor: extract DeviceTokenService from NotificationEndpoints (ARCH-H002)"
```

### Task C2: Add DeviceTokenService tests

**Files:**
- Create: `fasolt.Tests/DeviceTokenServiceTests.cs`

- [ ] **Step 1: Create `fasolt.Tests/DeviceTokenServiceTests.cs`**

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class DeviceTokenServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private DeviceTokenService CreateService(AppDbContext db)
    {
        var store = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<AppUser>(db);
        var userManager = new UserManager<AppUser>(
            store, Options.Create(new IdentityOptions()), null!, null!, null!, null!, null!, null!,
            NullLogger<UserManager<AppUser>>.Instance);
        return new DeviceTokenService(db, userManager);
    }

    [Fact]
    public async Task UpsertDeviceToken_CreatesNew()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        await svc.UpsertDeviceToken(UserId, "token-abc");

        var stored = await db.DeviceTokens.SingleAsync(t => t.UserId == UserId);
        stored.Token.Should().Be("token-abc");
    }

    [Fact]
    public async Task UpsertDeviceToken_UpdatesExisting()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        await svc.UpsertDeviceToken(UserId, "old-token");
        await svc.UpsertDeviceToken(UserId, "new-token");

        var stored = await db.DeviceTokens.SingleAsync(t => t.UserId == UserId);
        stored.Token.Should().Be("new-token");
    }

    [Fact]
    public async Task DeleteDeviceToken_RemovesToken()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        await svc.UpsertDeviceToken(UserId, "to-delete");
        await svc.DeleteDeviceToken(UserId);

        var count = await db.DeviceTokens.CountAsync(t => t.UserId == UserId);
        count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteDeviceToken_WhenNoneExists_IsIdempotent()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        // Should not throw
        await svc.DeleteDeviceToken(UserId);

        var count = await db.DeviceTokens.CountAsync(t => t.UserId == UserId);
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetSettings_ReturnsDefaultInterval()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var settings = await svc.GetSettings(UserId);

        settings.IntervalHours.Should().Be(8);
        settings.HasDeviceToken.Should().BeFalse();
    }

    [Fact]
    public async Task GetSettings_ReflectsDeviceToken()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        await svc.UpsertDeviceToken(UserId, "test-token");
        var settings = await svc.GetSettings(UserId);

        settings.HasDeviceToken.Should().BeTrue();
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(6, true)]
    [InlineData(8, true)]
    [InlineData(10, true)]
    [InlineData(12, true)]
    [InlineData(24, true)]
    [InlineData(5, false)]
    [InlineData(0, false)]
    [InlineData(48, false)]
    public async Task UpdateSettings_ValidatesInterval(int interval, bool expected)
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.UpdateSettings(UserId, interval);

        result.Should().Be(expected);
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test fasolt.Tests --filter DeviceTokenService`
Expected: All pass

- [ ] **Step 3: Commit**

```bash
git add fasolt.Tests/DeviceTokenServiceTests.cs
git commit -m "test: add DeviceTokenService tests"
```

---

## Stream D: AdminService Expansion (ARCH-H003)

### Task D1: Move GetLogs and TriggerPushForUser into AdminService

**Files:**
- Create: `fasolt.Server/Application/Dtos/LogDtos.cs`
- Modify: `fasolt.Server/Application/Services/AdminService.cs` — add methods + dependencies
- Modify: `fasolt.Server/Api/Endpoints/AdminEndpoints.cs` — thin out

- [ ] **Step 1: Create `Application/Dtos/LogDtos.cs`**

```csharp
namespace Fasolt.Server.Application.Dtos;

public record LogEntryDto(int Id, string Type, string Message, string? Detail, bool Success, DateTimeOffset CreatedAt);

public record LogListResponse(List<LogEntryDto> Logs, int TotalCount, int Page, int PageSize);

public record PushResult(string Message, bool TokenValid);
```

- [ ] **Step 2: Expand AdminService.cs**

Replace the entire file:

```csharp
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Server.Infrastructure.Services;

namespace Fasolt.Server.Application.Services;

public class AdminService(AppDbContext db, ApnsService? apnsService = null)
{
    public async Task<AdminUserListResponse> ListUsers(int page, int pageSize)
    {
        var totalCount = await db.Users.CountAsync();

        var users = await db.Users
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email!,
                db.Cards.Count(c => c.UserId == u.Id),
                db.Decks.Count(d => d.UserId == u.Id),
                u.LockoutEnabled && u.LockoutEnd > DateTimeOffset.UtcNow,
                db.DeviceTokens.Any(d => d.UserId == u.Id)))
            .ToListAsync();

        return new AdminUserListResponse(users, totalCount, page, pageSize);
    }

    public async Task<LogListResponse> GetLogs(int page, int pageSize, string? type)
    {
        var query = db.Logs.AsQueryable();
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<LogType>(type, true, out var logType))
            query = query.Where(l => l.Type == logType);

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LogEntryDto(l.Id, l.Type.ToString(), l.Message, l.Detail, l.Success, l.CreatedAt))
            .ToListAsync();

        return new LogListResponse(logs, total, page, pageSize);
    }

    public async Task<PushResult?> TriggerPushForUser(string userId)
    {
        if (apnsService is null) return null;

        var user = await db.Users.FindAsync(userId);
        if (user is null) return null;

        var deviceToken = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == userId);
        if (deviceToken is null) return null;

        var now = DateTimeOffset.UtcNow;

        var dueCardsByDeck = await db.Cards
            .Where(c => c.UserId == userId && (c.DueAt == null || c.DueAt <= now))
            .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive))
            .SelectMany(c => c.DeckCards.DefaultIfEmpty(),
                (card, deckCard) => new { DeckName = deckCard != null ? deckCard.Deck.Name : null })
            .GroupBy(x => x.DeckName ?? "Unsorted")
            .Select(g => new { DeckName = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalDue = dueCardsByDeck.Sum(g => g.Count);
        string body;
        if (totalDue == 0)
            body = "Test notification — no cards currently due";
        else
        {
            var breakdown = string.Join(", ",
                dueCardsByDeck.OrderByDescending(g => g.Count).Select(g => $"{g.Count} in {g.DeckName}"));
            body = $"You have {totalDue} card{(totalDue == 1 ? "" : "s")} due: {breakdown}";
        }

        var tokenValid = await apnsService.SendNotification(deviceToken.Token, "Cards due", body, totalDue);

        db.Logs.Add(new AppLog
        {
            Type = LogType.Notification,
            Message = tokenValid
                ? $"Admin push to {user.Email}: {body}"
                : $"Invalid token for {user.Email}, removed",
            Detail = tokenValid ? null : "Token returned 410 Gone",
            Success = tokenValid,
            CreatedAt = now,
        });

        if (!tokenValid)
            db.DeviceTokens.Remove(deviceToken);

        await db.SaveChangesAsync();

        return new PushResult(
            tokenValid ? $"Push sent: {body}" : "Token was invalid and has been removed.",
            tokenValid);
    }
}
```

Note: `ApnsService?` is nullable because it's only registered when APNs credentials are configured. The `TriggerPushForUser` method returns null if ApnsService is unavailable.

- [ ] **Step 3: Thin out AdminEndpoints.cs**

Replace the entire file:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization("AdminCookieOnly");

        group.MapGet("/users", ListUsers);
        group.MapPost("/users/{id}/lock", LockUser);
        group.MapPost("/users/{id}/unlock", UnlockUser);
        group.MapGet("/logs", GetLogs);
        group.MapPost("/users/{id}/push", TriggerPushForUser);
    }

    private static async Task<IResult> ListUsers(
        int? page,
        int? pageSize,
        AdminService adminService)
    {
        var p = page ?? 1;
        var ps = Math.Clamp(pageSize ?? 50, 1, 100);
        var result = await adminService.ListUsers(p, ps);
        return Results.Ok(result);
    }

    private static async Task<IResult> LockUser(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var currentUser = await userManager.GetUserAsync(principal);
        if (currentUser is null) return Results.Unauthorized();

        if (currentUser.Id == id)
            return Results.BadRequest(new { error = "Cannot lock your own account." });

        var user = await userManager.FindByIdAsync(id);
        if (user is null) return Results.NotFound();

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        return Results.Ok();
    }

    private static async Task<IResult> UnlockUser(
        string id,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return Results.NotFound();

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        return Results.Ok();
    }

    private static async Task<IResult> GetLogs(
        AdminService adminService,
        int? page,
        int? pageSize,
        string? type)
    {
        var p = page ?? 1;
        var ps = Math.Clamp(pageSize ?? 50, 1, 100);
        var result = await adminService.GetLogs(p, ps, type);
        return Results.Ok(result);
    }

    private static async Task<IResult> TriggerPushForUser(
        string id,
        AdminService adminService)
    {
        var result = await adminService.TriggerPushForUser(id);
        if (result is null)
            return Results.NotFound();

        return Results.Ok(new { message = result.Message });
    }
}
```

Note: `LockUser` and `UnlockUser` stay in the endpoint — they're thin Identity calls, not business logic.

- [ ] **Step 4: Build and run tests**

Run: `dotnet build fasolt.Server && dotnet test fasolt.Tests`
Expected: Success

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Application/Services/AdminService.cs fasolt.Server/Application/Dtos/LogDtos.cs fasolt.Server/Api/Endpoints/AdminEndpoints.cs
git commit -m "refactor: move GetLogs and TriggerPushForUser into AdminService (ARCH-H003)"
```

---

## Post-merge: Final verification

After all streams merge:

- [ ] **Run full build**: `dotnet build fasolt.Server`
- [ ] **Run full test suite**: `dotnet test fasolt.Tests`
- [ ] **Verify no circular dependency**: `grep -r "Fasolt.Server.Api" fasolt.Server/Application/` should return nothing
