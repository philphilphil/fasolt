# FSRS Migration + Algorithm Explainer Page

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace SM-2 with FSRS for spaced repetition scheduling and add a public algorithm explainer page.

**Architecture:** Install FSRS.Core NuGet package, replace SM-2 fields on the Card entity with FSRS fields (Stability, Difficulty, Step), wire FSRS.Core's `IScheduler` into the review endpoint, update all DTOs/types/tests, and add a new Vue page at `/algorithm`.

**Tech Stack:** FSRS.Core 1.0.7, .NET 10, EF Core, Vue 3, shadcn-vue, Tailwind CSS 3

**Spec:** `docs/superpowers/specs/2026-03-23-fsrs-migration-algorithm-page-design.md`

---

## File Structure

### Backend — Create
- (none — FSRS.Core handles the algorithm)

### Backend — Modify
| File | Change |
|------|--------|
| `fasolt.Server/fasolt.Server.csproj` | FSRS.Core package already added |
| `fasolt.Server/Domain/Entities/Card.cs` | Replace SM-2 fields with FSRS fields |
| `fasolt.Server/Infrastructure/Data/AppDbContext.cs` | Update column defaults |
| `fasolt.Server/Infrastructure/Data/Migrations/` | New migration |
| `fasolt.Server/Program.cs` | Register FSRS.Core DI |
| `fasolt.Server/Application/Dtos/ReviewDtos.cs` | FSRS fields in DTOs |
| `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs` | Use IScheduler, string ratings |
| `fasolt.Server/Application/Services/CardService.cs` | FSRS defaults in BulkCreateCards |
| `fasolt.Server/Application/Services/OverviewService.cs` | Add "relearning" state |
| `CLAUDE.md` | SM-2 → FSRS references |

### Backend — Delete
| File | Reason |
|------|--------|
| `fasolt.Server/Application/Services/Sm2Algorithm.cs` | Replaced by FSRS.Core |

### Frontend — Create
| File | Purpose |
|------|---------|
| `fasolt.client/src/views/AlgorithmView.vue` | Public algorithm explainer page |

### Frontend — Modify
| File | Change |
|------|--------|
| `fasolt.client/src/types/index.ts` | FSRS fields, state union |
| `fasolt.client/src/stores/review.ts` | String ratings |
| `fasolt.client/src/views/ReviewView.vue` | String ratings, keyboard shortcuts |
| `fasolt.client/src/views/CardsView.vue` | State filter dropdown |
| `fasolt.client/src/views/CardDetailView.vue` | (no SM-2 fields displayed — no change needed) |
| `fasolt.client/src/views/LandingView.vue` | Algorithm link in footer |
| `fasolt.client/src/router/index.ts` | `/algorithm` route |

### Tests — Modify
| File | Change |
|------|--------|
| `fasolt.Tests/CardServiceTests.cs` | FSRS field names in PreservesSrsState test |
| `fasolt.Tests/OverviewServiceTests.cs` | Add "relearning" state |
| `fasolt.client/src/__tests__/stores/review.test.ts` | FSRS fields in mock data, string ratings |
| `fasolt.client/src/__tests__/types.test.ts` | Replace SM-2 fields in Card/DueCard mocks |
| `fasolt.client/src/__tests__/stores/cards.test.ts` | Replace SM-2 fields in Card mocks |
| `fasolt.client/src/__tests__/composables/useSearch.test.ts` | "mature" → "review" |

---

## FSRS.Core API Reference

Key types from the library (verified via reflection):

```csharp
// FSRS.Core.Models.Card
Card {
    Guid CardId { get; set; }
    State State { get; set; }          // enum: Learning=1, Review=2, Relearning=3
    int? Step { get; set; }            // current learning step index
    double? Stability { get; set; }
    double? Difficulty { get; set; }
    DateTime Due { get; set; }
    DateTime? LastReview { get; set; }
}

// FSRS.Core.Enums.Rating
enum Rating { Again=1, Hard=2, Good=3, Easy=4 }

// FSRS.Core.Enums.State
enum State { Learning=1, Review=2, Relearning=3 }

// IScheduler.ReviewCard returns (Card updatedCard, ReviewLog log)
(Card, ReviewLog) ReviewCard(Card card, Rating rating, DateTime? reviewDateTime, TimeSpan? reviewDuration)

// DI registration
builder.Services.AddFsrs(options => {
    options.DesiredRetention = 0.9;
    options.MaximumInterval = 36500;
    options.EnableFuzzing = true;
});
```

**Important:** FSRS.Core's `State` enum has no `New` value (0). A card that has never been reviewed has `State = 0` (default) which is not a named enum value. Our DB `State` column is a string, so we map: `0 → "new"`, `Learning → "learning"`, `Review → "review"`, `Relearning → "relearning"`.

---

## Task 1: Update Card Entity and Database

**Files:**
- Modify: `fasolt.Server/Domain/Entities/Card.cs`
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs`
- Create: new migration in `fasolt.Server/Infrastructure/Data/Migrations/`

- [ ] **Step 1: Update Card.cs — replace SM-2 fields with FSRS fields**

Replace in `fasolt.Server/Domain/Entities/Card.cs`:

```csharp
// REMOVE these 3 lines:
public double EaseFactor { get; set; } = 2.5;
public int Interval { get; set; }
public int Repetitions { get; set; }

// ADD these 3 lines in their place:
public double? Stability { get; set; }
public double? Difficulty { get; set; }
public int? Step { get; set; }
```

Keep `DueAt`, `State`, `LastReviewedAt` unchanged.

- [ ] **Step 2: Update AppDbContext.cs — fix column config**

In `fasolt.Server/Infrastructure/Data/AppDbContext.cs`, delete this line (line 33):

```csharp
entity.Property(e => e.EaseFactor).HasDefaultValue(2.5);
```

The `State` default on the next line is already correct and should remain.

- [ ] **Step 3: Create EF Core migration**

Run:
```bash
dotnet ef migrations add ReplaceSm2WithFsrs --project fasolt.Server
```

- [ ] **Step 4: Edit the generated migration to reset card states**

Open the newly created migration file and add this SQL at the end of the `Up` method, before the closing brace:

```csharp
migrationBuilder.Sql("UPDATE \"Cards\" SET \"State\" = 'new' WHERE \"State\" NOT IN ('new', 'learning', 'review', 'relearning')");
```

This converts any `"mature"` values to valid FSRS states.

- [ ] **Step 5: Apply migration to dev database**

Run:
```bash
dotnet ef database update --project fasolt.Server
```

Expected: Migration applies, old SM-2 columns dropped, new FSRS columns added.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Domain/Entities/Card.cs fasolt.Server/Infrastructure/Data/AppDbContext.cs "fasolt.Server/Infrastructure/Data/Migrations/"
git commit -m "feat: replace SM-2 fields with FSRS fields on Card entity"
```

---

## Task 2: Register FSRS.Core DI, Delete SM-2, Update DTOs and Review Endpoint

**Files:**
- Modify: `fasolt.Server/Program.cs`
- Delete: `fasolt.Server/Application/Services/Sm2Algorithm.cs`
- Modify: `fasolt.Server/Application/Dtos/ReviewDtos.cs`
- Modify: `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs`

- [ ] **Step 1: Register FSRS.Core in Program.cs**

Add after the existing `builder.Services.AddScoped<OverviewService>();` line (~line 158):

```csharp
builder.Services.AddFsrs(options =>
{
    options.DesiredRetention = 0.9;
    options.MaximumInterval = 36500;
    options.EnableFuzzing = true;
});
```

Add `using FSRS.Core.Extensions;` at the top of Program.cs.

- [ ] **Step 2: Delete Sm2Algorithm.cs**

```bash
rm fasolt.Server/Application/Services/Sm2Algorithm.cs
```

- [ ] **Step 3: Update ReviewDtos.cs**

Replace the entire file content of `fasolt.Server/Application/Dtos/ReviewDtos.cs`:

```csharp
namespace Fasolt.Server.Application.Dtos;

public record RateCardRequest(string CardId, string Rating);

public record RateCardResponse(string CardId, double? Stability, double? Difficulty, DateTimeOffset? DueAt, string State);

public record DueCardDto(
    string Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State);

public record ReviewStatsDto(int DueCount, int TotalCards, int StudiedToday);
```

- [ ] **Step 4: Rewrite ReviewEndpoints.cs to use FSRS.Core**

Replace the entire file content of `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using FSRS.Core.Enums;
using FSRS.Core.Interfaces;
using FsrsCard = FSRS.Core.Models.Card;

namespace Fasolt.Server.Api.Endpoints;

public static class ReviewEndpoints
{
    private static readonly Dictionary<string, Rating> ValidRatings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["again"] = Rating.Again,
        ["hard"] = Rating.Hard,
        ["good"] = Rating.Good,
        ["easy"] = Rating.Easy,
    };

    public static void MapReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/review").RequireAuthorization();
        group.MapGet("/due", GetDueCards);
        group.MapPost("/rate", RateCard);
        group.MapGet("/stats", GetStats);
    }

    private static string MapState(State state) => state switch
    {
        State.Learning => "learning",
        State.Review => "review",
        State.Relearning => "relearning",
        _ => "new",
    };

    private static State ParseState(string state) => state switch
    {
        "learning" => State.Learning,
        "review" => State.Review,
        "relearning" => State.Relearning,
        _ => default,
    };

    private static async Task<IResult> GetDueCards(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, AppDbContext db, int limit = 50, string? deckId = null)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var take = Math.Clamp(limit, 1, 200);
        var now = DateTimeOffset.UtcNow;
        var query = db.Cards
            .Where(c => c.UserId == user.Id && (c.DueAt == null || c.DueAt <= now));

        if (deckId is not null)
        {
            var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == user.Id);
            if (deck is null) return Results.NotFound();
            query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deck.Id));
        }

        var cards = await query
            .OrderBy(c => c.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(c => c.CreatedAt)
            .Take(take)
            .Select(c => new DueCardDto(c.PublicId, c.Front, c.Back, c.SourceFile, c.SourceHeading, c.State))
            .ToListAsync();

        return Results.Ok(cards);
    }

    private static async Task<IResult> RateCard(
        RateCardRequest request, ClaimsPrincipal principal, UserManager<AppUser> userManager,
        AppDbContext db, IScheduler scheduler)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (!ValidRatings.TryGetValue(request.Rating, out var fsrsRating))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rating"] = ["Rating must be 'again', 'hard', 'good', or 'easy'."]
            });

        var card = await db.Cards.FirstOrDefaultAsync(c => c.PublicId == request.CardId && c.UserId == user.Id);
        if (card is null) return Results.NotFound();

        // Build FSRS.Core Card from our entity
        var fsrsCard = new FsrsCard
        {
            State = ParseState(card.State),
            Stability = card.Stability,
            Difficulty = card.Difficulty,
            Step = card.Step,
            Due = card.DueAt?.UtcDateTime ?? card.CreatedAt.UtcDateTime,
            LastReview = card.LastReviewedAt?.UtcDateTime,
        };

        var now = DateTime.UtcNow;
        var (updated, _) = scheduler.ReviewCard(fsrsCard, fsrsRating, now, null);

        // Map back to our entity
        card.Stability = updated.Stability;
        card.Difficulty = updated.Difficulty;
        card.Step = updated.Step;
        card.State = MapState(updated.State);
        card.DueAt = new DateTimeOffset(updated.Due, TimeSpan.Zero);
        card.LastReviewedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(new RateCardResponse(card.PublicId, card.Stability, card.Difficulty, card.DueAt, card.State));
    }

    private static async Task<IResult> GetStats(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var now = DateTimeOffset.UtcNow;
        var dueCount = await db.Cards.CountAsync(c => c.UserId == user.Id && (c.DueAt == null || c.DueAt <= now));
        var totalCards = await db.Cards.CountAsync(c => c.UserId == user.Id);
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var studiedToday = await db.Cards.CountAsync(c =>
            c.UserId == user.Id && c.LastReviewedAt != null && c.LastReviewedAt >= todayStart);

        return Results.Ok(new ReviewStatsDto(dueCount, totalCards, studiedToday));
    }
}
```

- [ ] **Step 5: Verify build succeeds**

```bash
dotnet build fasolt.Server 2>&1 | tail -5
```

Expected: Build succeeds with no errors.

- [ ] **Step 6: Commit**

```bash
git rm fasolt.Server/Application/Services/Sm2Algorithm.cs
git add fasolt.Server/Program.cs fasolt.Server/Application/Dtos/ReviewDtos.cs fasolt.Server/Api/Endpoints/ReviewEndpoints.cs
git commit -m "feat: replace SM-2 with FSRS.Core scheduler"
```

---

## Task 3: Update CardService and OverviewService

**Files:**
- Modify: `fasolt.Server/Application/Services/CardService.cs:101-115`
- Modify: `fasolt.Server/Application/Services/OverviewService.cs:9`

- [ ] **Step 1: Update CardService.cs BulkCreateCards defaults**

In `fasolt.Server/Application/Services/CardService.cs`, in the `BulkCreateCards` method (~line 101-115), replace:

```csharp
EaseFactor = 2.5,
Interval = 0,
Repetitions = 0,
State = "new",
```

with:

```csharp
State = "new",
```

(The FSRS fields `Stability`, `Difficulty`, `Step` default to `null` which is correct for new cards.)

- [ ] **Step 2: Update OverviewService.cs AllStates**

In `fasolt.Server/Application/Services/OverviewService.cs`, line 9, replace:

```csharp
private static readonly string[] AllStates = ["new", "learning", "review"];
```

with:

```csharp
private static readonly string[] AllStates = ["new", "learning", "review", "relearning"];
```

- [ ] **Step 3: Verify build succeeds**

```bash
dotnet build fasolt.Server 2>&1 | tail -5
```

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Application/Services/CardService.cs fasolt.Server/Application/Services/OverviewService.cs
git commit -m "feat: update CardService and OverviewService for FSRS fields"
```

---

## Task 4: Update Backend Tests

**Files:**
- Modify: `fasolt.Tests/CardServiceTests.cs:169-198`
- Modify: `fasolt.Tests/OverviewServiceTests.cs` (if it exists — check first)

- [ ] **Step 1: Update PreservesSrsState test in CardServiceTests.cs**

In `fasolt.Tests/CardServiceTests.cs`, replace the `UpdateCardFields_ById_PreservesSrsState` test (~lines 169-198):

```csharp
[Fact]
public async Task UpdateCardFields_ById_PreservesSrsState()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    var card = await svc.CreateCard(UserId, "Old front", "Old back", "notes.md", "Heading");

    // Simulate some SRS state by updating directly
    var entity = await db.Cards.FindAsync(card.Id);
    entity!.Stability = 5.0;
    entity.Difficulty = 4.2;
    entity.Step = 2;
    entity.State = "review";
    await db.SaveChangesAsync();

    var result = await svc.UpdateCardFields(UserId, card.Id,
        new UpdateCardFieldsRequest(NewFront: "New front", NewBack: "New back"));

    result.Status.Should().Be(UpdateCardStatus.Success);
    result.Card!.Front.Should().Be("New front");
    result.Card.Back.Should().Be("New back");

    // Verify SRS state preserved
    await using var db2 = _db.CreateDbContext();
    var reloaded = await db2.Cards.FindAsync(card.Id);
    reloaded!.Stability.Should().Be(5.0);
    reloaded.Difficulty.Should().Be(4.2);
    reloaded.Step.Should().Be(2);
    reloaded.State.Should().Be("review");
}
```

- [ ] **Step 2: Check if OverviewServiceTests.cs needs updating**

```bash
grep -n "AllStates\|relearning\|mature" fasolt.Tests/OverviewServiceTests.cs 2>/dev/null || echo "file not found or no matches"
```

If it references state arrays, add "relearning" to the expected states.

- [ ] **Step 3: Run backend tests**

```bash
dotnet test fasolt.Tests --verbosity normal 2>&1 | tail -20
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add fasolt.Tests/
git commit -m "test: update backend tests for FSRS fields"
```

---

## Task 5: Update Frontend Types and Review Store

**Files:**
- Modify: `fasolt.client/src/types/index.ts`
- Modify: `fasolt.client/src/stores/review.ts`

- [ ] **Step 1: Update types/index.ts**

In `fasolt.client/src/types/index.ts`, replace the `Card` interface:

```typescript
export interface Card {
  id: string
  sourceFile: string | null
  sourceHeading: string | null
  front: string
  back: string
  createdAt: string
  stability: number | null
  difficulty: number | null
  dueAt: string | null
  state: 'new' | 'learning' | 'review' | 'relearning'
  decks: { id: string; name: string }[]
}
```

Replace the `DueCard` interface:

```typescript
export interface DueCard {
  id: string
  front: string
  back: string
  sourceFile: string | null
  sourceHeading: string | null
  state: string
}
```

- [ ] **Step 2: Update stores/review.ts — change rate() to send string rating**

In `fasolt.client/src/stores/review.ts`, change the `rate` function signature and body. Replace:

```typescript
async function rate(quality: number) {
    const card = currentCard.value
    if (!card) return

    await apiFetch('/review/rate', {
      method: 'POST',
      body: JSON.stringify({ cardId: card.id, quality }),
    })

    sessionStats.value.reviewed++
    if (quality === 0) {
      sessionStats.value.again++
      queue.value.push({ ...card })
    } else if (quality === 2) {
      sessionStats.value.hard++
    } else if (quality === 4) {
      sessionStats.value.good++
    } else if (quality === 5) {
      sessionStats.value.easy++
    }

    currentIndex.value++
    isFlipped.value = false
  }
```

with:

```typescript
async function rate(rating: 'again' | 'hard' | 'good' | 'easy') {
    const card = currentCard.value
    if (!card) return

    await apiFetch('/review/rate', {
      method: 'POST',
      body: JSON.stringify({ cardId: card.id, rating }),
    })

    sessionStats.value.reviewed++
    sessionStats.value[rating]++
    if (rating === 'again') {
      queue.value.push({ ...card })
    }

    currentIndex.value++
    isFlipped.value = false
  }
```

- [ ] **Step 3: Commit**

```bash
cd fasolt.client && git add src/types/index.ts src/stores/review.ts
git commit -m "feat: update frontend types and review store for FSRS"
```

---

## Task 6: Update ReviewView and CardsView

**Files:**
- Modify: `fasolt.client/src/views/ReviewView.vue`
- Modify: `fasolt.client/src/views/CardsView.vue:209-213`

- [ ] **Step 1: Update ReviewView.vue — remove ratingToQuality, use string ratings**

In `fasolt.client/src/views/ReviewView.vue`, remove the `ratingToQuality` map and update keyboard shortcuts. Replace the `<script setup>` section:

Remove lines 17-22 (the `ratingToQuality` map).

Replace the keyboard shortcuts (lines 33-36):

```typescript
'1': () => { if (review.isFlipped) review.rate('again') },
'2': () => { if (review.isFlipped) review.rate('hard') },
'3': () => { if (review.isFlipped) review.rate('good') },
'4': () => { if (review.isFlipped) review.rate('easy') },
```

Replace the `onRate` function (lines 45-47):

```typescript
async function onRate(rating: ReviewRating) {
  await review.rate(rating)
}
```

- [ ] **Step 2: Update CardsView.vue state filter dropdown**

In `fasolt.client/src/views/CardsView.vue`, replace the state filter options (~lines 209-213):

```html
<option value="">All states</option>
<option value="new">new</option>
<option value="learning">learning</option>
<option value="review">review</option>
<option value="relearning">relearning</option>
```

- [ ] **Step 3: Commit**

```bash
cd fasolt.client && git add src/views/ReviewView.vue src/views/CardsView.vue
git commit -m "feat: update ReviewView and CardsView for FSRS ratings and states"
```

---

## Task 7: Update Frontend Tests

**Files:**
- Modify: `fasolt.client/src/__tests__/stores/review.test.ts`
- Modify: `fasolt.client/src/__tests__/types.test.ts`
- Modify: `fasolt.client/src/__tests__/stores/cards.test.ts`
- Modify: `fasolt.client/src/__tests__/composables/useSearch.test.ts`

- [ ] **Step 1: Update review.test.ts — remove SM-2 fields from mocks, use string ratings**

In `fasolt.client/src/__tests__/stores/review.test.ts`:

Replace all mock card objects to remove `easeFactor`, `interval`, `repetitions`. For example, replace:

```typescript
{ id: 'c1', front: 'What is CAP?', back: 'Consistency, Availability, Partition tolerance', sourceFile: 'cap.md', sourceHeading: '## Overview', state: 'learning', easeFactor: 2.5, interval: 1, repetitions: 0 },
```

with:

```typescript
{ id: 'c1', front: 'What is CAP?', back: 'Consistency, Availability, Partition tolerance', sourceFile: 'cap.md', sourceHeading: '## Overview', state: 'learning' },
```

Apply the same pattern to all mock card objects in the file (4 occurrences).

Replace all mock rate responses. For example, replace:

```typescript
mockApiFetch.mockResolvedValueOnce({ cardId: 'c1', easeFactor: 2.5, interval: 1, repetitions: 1, dueAt: null, state: 'learning' })
```

with:

```typescript
mockApiFetch.mockResolvedValueOnce({ cardId: 'c1', stability: 1.0, difficulty: 5.0, dueAt: null, state: 'learning' })
```

Replace all `store.rate(4)` calls with `store.rate('good')`.

- [ ] **Step 2: Update types.test.ts — replace SM-2 fields with FSRS fields**

In `fasolt.client/src/__tests__/types.test.ts`, update all `Card` mock objects (3 occurrences). Replace:

```typescript
easeFactor: 2.5,
interval: 1,
repetitions: 0,
```

with:

```typescript
stability: null,
difficulty: null,
```

Update all `DueCard` mock objects (3 occurrences). Remove `easeFactor`, `interval`, `repetitions` fields entirely (they no longer exist on `DueCard`).

- [ ] **Step 3: Update cards.test.ts — replace SM-2 fields with FSRS fields**

In `fasolt.client/src/__tests__/stores/cards.test.ts`, update all mock Card objects (2 occurrences). Replace:

```typescript
easeFactor: 2.5, interval: 1, repetitions: 0,
```

with:

```typescript
stability: null, difficulty: null,
```

- [ ] **Step 4: Update useSearch.test.ts — change "mature" to "review"**

In `fasolt.client/src/__tests__/composables/useSearch.test.ts`, line 50, replace:

```typescript
{ id: 'c2', headline: 'Q2', state: 'mature' },
```

with:

```typescript
{ id: 'c2', headline: 'Q2', state: 'review' },
```

- [ ] **Step 3: Run frontend tests**

```bash
cd fasolt.client && npm test -- --run 2>&1 | tail -20
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
cd fasolt.client && git add src/__tests__/
git commit -m "test: update frontend tests for FSRS fields and string ratings"
```

---

## Task 8: Algorithm Explainer Page

**Files:**
- Create: `fasolt.client/src/views/AlgorithmView.vue`
- Modify: `fasolt.client/src/router/index.ts`
- Modify: `fasolt.client/src/views/LandingView.vue`

- [ ] **Step 1: Create AlgorithmView.vue**

Create `fasolt.client/src/views/AlgorithmView.vue`:

```vue
<script setup lang="ts">
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
</script>

<template>
  <div class="mx-auto max-w-3xl px-6 py-12 space-y-8">
    <div>
      <h1 class="text-lg font-semibold tracking-tight">How the Algorithm Works</h1>
      <p class="text-xs text-muted-foreground mt-1">
        fasolt uses FSRS (Free Spaced Repetition Scheduler) to schedule your reviews.
      </p>
    </div>

    <!-- What is Spaced Repetition -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">What is Spaced Repetition?</CardTitle>
      </CardHeader>
      <CardContent class="text-xs text-muted-foreground leading-relaxed space-y-3">
        <p>
          Spaced repetition is a study technique where you review material at increasing intervals.
          Instead of cramming, you see a card right before you're likely to forget it.
          Each successful recall makes the memory stronger, so the next review can wait longer.
        </p>
        <p>
          A new card might be reviewed after 1 day, then 3 days, then 8 days, then 3 weeks —
          growing exponentially as the memory stabilizes.
        </p>
      </CardContent>
    </Card>

    <!-- The FSRS Algorithm -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">The FSRS Algorithm</CardTitle>
      </CardHeader>
      <CardContent class="text-xs text-muted-foreground leading-relaxed space-y-3">
        <p>
          FSRS (Free Spaced Repetition Scheduler) is a modern, open-source algorithm based on the
          <strong class="text-foreground">Three Component Model of Memory</strong>. It tracks three variables for each card:
        </p>
        <dl class="space-y-3 pl-1">
          <div>
            <dt class="font-medium text-foreground">Stability (S)</dt>
            <dd>How long the memory lasts. Higher stability means longer intervals between reviews. This is the core scheduling driver.</dd>
          </div>
          <div>
            <dt class="font-medium text-foreground">Difficulty (D)</dt>
            <dd>How inherently hard the card is for you. Updated with each review based on your rating.</dd>
          </div>
          <div>
            <dt class="font-medium text-foreground">Retrievability (R)</dt>
            <dd>The probability you can recall the card right now. Decays over time — when it drops below the target retention (90%), the card becomes due.</dd>
          </div>
        </dl>
        <p>
          Unlike older algorithms that use the same fixed formula for everyone, FSRS's default parameters were
          optimized on over 700 million reviews from 20,000 users, making it significantly more accurate out of the box.
        </p>
      </CardContent>
    </Card>

    <!-- How Reviews Work -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">How Reviews Work</CardTitle>
      </CardHeader>
      <CardContent class="text-xs text-muted-foreground leading-relaxed space-y-3">
        <p>When you review a card, you rate how well you recalled it. Each rating affects the card differently:</p>
        <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
          <div class="rounded border border-border/60 p-3 space-y-1">
            <div class="font-medium text-foreground">Again</div>
            <div>You forgot. Stability resets and the card re-enters the learning phase for short-interval reviews.</div>
          </div>
          <div class="rounded border border-border/60 p-3 space-y-1">
            <div class="font-medium text-foreground">Hard</div>
            <div>You recalled with significant difficulty. Stability increases slightly, difficulty goes up.</div>
          </div>
          <div class="rounded border border-border/60 p-3 space-y-1">
            <div class="font-medium text-foreground">Good</div>
            <div>Normal recall. Stability increases proportionally, the standard path.</div>
          </div>
          <div class="rounded border border-border/60 p-3 space-y-1">
            <div class="font-medium text-foreground">Easy</div>
            <div>Effortless recall. Large stability increase, difficulty decreases. Longer until next review.</div>
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- How Intervals Grow -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">How Intervals Grow</CardTitle>
      </CardHeader>
      <CardContent class="text-xs text-muted-foreground leading-relaxed space-y-3">
        <p>Here's a typical progression for a card rated "Good" each time:</p>
        <div class="flex flex-wrap items-center gap-2 text-foreground font-mono text-[11px]">
          <span class="rounded bg-muted/50 px-2 py-1">1d</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="rounded bg-muted/50 px-2 py-1">3d</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="rounded bg-muted/50 px-2 py-1">8d</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="rounded bg-muted/50 px-2 py-1">21d</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="rounded bg-muted/50 px-2 py-1">55d</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="rounded bg-muted/50 px-2 py-1">4mo</span>
          <span class="text-muted-foreground">&rarr;</span>
          <span class="text-[10px]">...</span>
        </div>
        <p>
          The intervals grow roughly exponentially. If you rate "Easy", they grow even faster.
          If you rate "Again", the card resets and works its way back up from short intervals.
        </p>
      </CardContent>
    </Card>

    <!-- Why FSRS -->
    <Card class="border-border/60">
      <CardHeader>
        <CardTitle class="text-sm">Why FSRS?</CardTitle>
      </CardHeader>
      <CardContent class="text-xs text-muted-foreground leading-relaxed space-y-3">
        <p>
          FSRS replaced the SM-2 algorithm (created in 1987) as the standard for spaced repetition.
          It's now the default in Anki, the most popular flashcard app.
        </p>
        <p>Key advantages:</p>
        <ul class="list-disc list-inside space-y-1 pl-1">
          <li><strong class="text-foreground">20-30% fewer reviews</strong> for the same retention rate</li>
          <li><strong class="text-foreground">Optimized defaults</strong> trained on 700M+ real reviews</li>
          <li><strong class="text-foreground">Better handling of overdue cards</strong> — SM-2 penalized you for reviewing late, FSRS adapts</li>
          <li><strong class="text-foreground">Open source</strong> — transparent, peer-reviewed, continuously improving</li>
        </ul>
      </CardContent>
    </Card>
  </div>
</template>
```

- [ ] **Step 2: Add route in router/index.ts**

In `fasolt.client/src/router/index.ts`, add after the landing page route (~line 39):

```typescript
{
  path: '/algorithm',
  name: 'algorithm',
  component: () => import('@/views/AlgorithmView.vue'),
  meta: { public: true },
},
```

- [ ] **Step 3: Add link in LandingView.vue footer**

In `fasolt.client/src/views/LandingView.vue`, in the footer section (~line 144), replace the existing GitHub link:

```html
<a
  href="https://github.com"
  class="text-xs text-muted-foreground hover:text-accent"
>
  GitHub
</a>
```

with:

```html
<div class="flex items-center gap-4">
  <RouterLink
    to="/algorithm"
    class="text-xs text-muted-foreground hover:text-accent"
  >
    How the algorithm works
  </RouterLink>
  <a
    href="https://github.com"
    class="text-xs text-muted-foreground hover:text-accent"
  >
    GitHub
  </a>
</div>
```

- [ ] **Step 4: Commit**

```bash
cd fasolt.client && git add src/views/AlgorithmView.vue src/router/index.ts src/views/LandingView.vue
git commit -m "feat: add public algorithm explainer page"
```

---

## Task 9: Update CLAUDE.md and Clean Up

**Files:**
- Modify: `CLAUDE.md`
- Modify: `docs/requirements/15_srs_algo.md` (move to done)

- [ ] **Step 1: Update CLAUDE.md SM-2 references**

In `CLAUDE.md`, replace:

```
Cards are reviewed using spaced repetition (SM-2 algorithm), which schedules reviews at increasing intervals based on how well you recall each card.
```

with:

```
Cards are reviewed using spaced repetition (FSRS algorithm), which schedules reviews at increasing intervals based on how well you recall each card.
```

- [ ] **Step 2: Move requirement to done**

```bash
mv docs/requirements/15_srs_algo.md docs/requirements/done/
```

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md docs/requirements/
git commit -m "docs: update CLAUDE.md for FSRS, move requirement to done"
```

---

## Task 10: E2E Verification

- [ ] **Step 1: Start the full stack**

```bash
./dev.sh
```

Wait for backend + frontend to be ready.

- [ ] **Step 2: Run Playwright E2E test — full review flow**

Using Playwright MCP: navigate to the app, log in with dev credentials (`dev@fasolt.local` / `Dev1234!`), create some cards via the MCP tools, start a review session, rate cards with each button (Again, Hard, Good, Easy), verify the session completes and dashboard stats update.

- [ ] **Step 3: Test the algorithm page**

Navigate to `/algorithm` without logging in. Verify the page renders all 5 sections, is responsive, and matches the app's visual style.

- [ ] **Step 4: Verify landing page footer link**

Navigate to `/`. Verify the "How the algorithm works" link appears in the footer and navigates to `/algorithm`.
