# Spaced Repetition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement SM-2 spaced repetition scheduling, study sessions with due cards, ratings, and session summaries (US-4.1, US-4.2, US-4.3, US-4.7).

**Architecture:** Add SM-2 fields to the Card entity via migration. Create an `Sm2Algorithm` service for scheduling calculations. Add `/api/review` endpoints for fetching due cards and submitting ratings. Rewrite the frontend review store to be API-backed, wire ReviewView/ReviewCard to real data with markdown rendering.

**Tech Stack:** .NET 10, EF Core + Npgsql, Vue 3, TypeScript, Pinia, markdown-it, shadcn-vue

---

## File Map

### Backend — New Files
- `fasolt.Server/Application/Services/Sm2Algorithm.cs` — Pure SM-2 calculation logic
- `fasolt.Server/Application/Dtos/ReviewDtos.cs` — DTOs for review endpoints
- `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs` — /api/review endpoints (due, rate, stats)

### Backend — Modified Files
- `fasolt.Server/Domain/Entities/Card.cs` — Add SM-2 fields
- `fasolt.Server/Infrastructure/Data/AppDbContext.cs` — Configure new columns, add index
- `fasolt.Server/Program.cs` — Register `MapReviewEndpoints()`

### Frontend — Modified Files
- `fasolt.client/src/types/index.ts` — Add SM-2 fields to Card, add ReviewStats type
- `fasolt.client/src/stores/review.ts` — Rewrite with API-backed session logic
- `fasolt.client/src/views/ReviewView.vue` — Wire to real data, remove mocks
- `fasolt.client/src/components/ReviewCard.vue` — Add markdown rendering
- `fasolt.client/src/views/DashboardView.vue` — Add "Study now" button with due count

---

## Task 1: Add SM-2 Fields to Card Entity + Migration

**Files:**
- Modify: `fasolt.Server/Domain/Entities/Card.cs`
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs`

- [ ] **Step 1: Add fields to Card entity**

Add after `DeletedAt` in `Card.cs`:

```csharp
public double EaseFactor { get; set; } = 2.5;
public int Interval { get; set; }
public int Repetitions { get; set; }
public DateTimeOffset? DueAt { get; set; }
public string State { get; set; } = "new";
```

- [ ] **Step 2: Configure in DbContext**

In `AppDbContext.cs`, inside the `builder.Entity<Card>()` block, add:

```csharp
entity.Property(e => e.EaseFactor).HasDefaultValue(2.5);
entity.Property(e => e.State).HasMaxLength(20).HasDefaultValue("new").IsRequired();
entity.HasIndex(e => new { e.UserId, e.DueAt });
```

- [ ] **Step 3: Create and apply migration**

```bash
dotnet ef migrations add AddSm2Fields --project fasolt.Server
docker compose up -d
dotnet ef database update --project fasolt.Server
```

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Domain/Entities/Card.cs fasolt.Server/Infrastructure/Data/AppDbContext.cs fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat: add SM-2 fields to Card entity (EaseFactor, Interval, Repetitions, DueAt, State)"
```

---

## Task 2: SM-2 Algorithm Service

**Files:**
- Create: `fasolt.Server/Application/Services/Sm2Algorithm.cs`

- [ ] **Step 1: Create Sm2Algorithm**

```csharp
// fasolt.Server/Application/Services/Sm2Algorithm.cs
namespace Fasolt.Server.Application.Services;

public record Sm2Result(double EaseFactor, int Interval, int Repetitions, string State);

public static class Sm2Algorithm
{
    private const double MinEaseFactor = 1.3;

    public static Sm2Result Calculate(double easeFactor, int interval, int repetitions, int quality)
    {
        // Adjust ease factor: EF' = EF + (0.1 - (5 - q) * (0.08 + (5 - q) * 0.02))
        var newEf = easeFactor + (0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02));
        if (newEf < MinEaseFactor) newEf = MinEaseFactor;

        int newInterval;
        int newReps;

        if (quality < 3) // Again (0) or Hard (2) — treated as failure
        {
            newReps = 0;
            newInterval = quality == 0 ? 0 : 1; // Again = stay in session (0), Hard = 1 day
        }
        else // Good (4) or Easy (5)
        {
            newReps = repetitions + 1;
            newInterval = newReps switch
            {
                1 => 1,
                2 => 6,
                _ => (int)Math.Round(interval * newEf),
            };

            // Easy bonus: 1.3x interval
            if (quality == 5)
                newInterval = (int)Math.Round(newInterval * 1.3);
        }

        // Determine state
        var state = (newReps >= 3 && newEf >= 2.0) ? "mature" : "learning";

        return new Sm2Result(newEf, newInterval, newReps, state);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.Server/Application/Services/Sm2Algorithm.cs
git commit -m "feat: add SM-2 algorithm service"
```

---

## Task 3: Review DTOs

**Files:**
- Create: `fasolt.Server/Application/Dtos/ReviewDtos.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// fasolt.Server/Application/Dtos/ReviewDtos.cs
namespace Fasolt.Server.Application.Dtos;

public record RateCardRequest(Guid CardId, int Quality);

public record RateCardResponse(
    Guid CardId,
    double EaseFactor,
    int Interval,
    int Repetitions,
    DateTimeOffset? DueAt,
    string State);

public record DueCardDto(
    Guid Id,
    string Front,
    string Back,
    string CardType,
    string? SourceHeading,
    Guid? FileId,
    string State,
    DateTimeOffset? DueAt);

public record ReviewStatsDto(int DueCount, int TotalCards, int StudiedToday);
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.Server/Application/Dtos/ReviewDtos.cs
git commit -m "feat: add review DTOs"
```

---

## Task 4: Review API Endpoints

**Files:**
- Create: `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Create ReviewEndpoints**

```csharp
// fasolt.Server/Api/Endpoints/ReviewEndpoints.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Api.Endpoints;

public static class ReviewEndpoints
{
    private static readonly int[] ValidQualities = [0, 2, 4, 5];

    public static void MapReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/review").RequireAuthorization();

        group.MapGet("/due", GetDueCards);
        group.MapPost("/rate", RateCard);
        group.MapGet("/stats", GetStats);
    }

    private static async Task<IResult> GetDueCards(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db,
        int limit = 50)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var now = DateTimeOffset.UtcNow;

        var cards = await db.Cards
            .Where(c => c.UserId == user.Id && (c.DueAt == null || c.DueAt <= now))
            .OrderBy(c => c.DueAt ?? DateTimeOffset.MaxValue) // New cards (null DueAt) last
            .ThenBy(c => c.CreatedAt)
            .Take(limit)
            .Select(c => new DueCardDto(
                c.Id, c.Front, c.Back, c.CardType, c.SourceHeading, c.FileId, c.State, c.DueAt))
            .ToListAsync();

        return Results.Ok(cards);
    }

    private static async Task<IResult> RateCard(
        RateCardRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (!ValidQualities.Contains(request.Quality))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["quality"] = ["Quality must be 0 (Again), 2 (Hard), 4 (Good), or 5 (Easy)."]
            });

        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId && c.UserId == user.Id);

        if (card is null) return Results.NotFound();

        var result = Sm2Algorithm.Calculate(card.EaseFactor, card.Interval, card.Repetitions, request.Quality);

        card.EaseFactor = result.EaseFactor;
        card.Interval = result.Interval;
        card.Repetitions = result.Repetitions;
        card.State = result.State;

        if (result.Interval == 0)
        {
            // Again — card stays available (DueAt = now)
            card.DueAt = DateTimeOffset.UtcNow;
        }
        else
        {
            card.DueAt = DateTimeOffset.UtcNow.AddDays(result.Interval);
        }

        await db.SaveChangesAsync();

        return Results.Ok(new RateCardResponse(
            card.Id, card.EaseFactor, card.Interval, card.Repetitions, card.DueAt, card.State));
    }

    private static async Task<IResult> GetStats(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var now = DateTimeOffset.UtcNow;
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var tomorrowStart = todayStart.AddDays(1);

        var dueCount = await db.Cards
            .CountAsync(c => c.UserId == user.Id && (c.DueAt == null || c.DueAt <= now));

        var totalCards = await db.Cards
            .CountAsync(c => c.UserId == user.Id);

        // Studied today: cards that have been reviewed (Repetitions > 0) and DueAt is in the future from today
        // This is an approximation — proper tracking needs a ReviewHistory table
        var studiedToday = await db.Cards
            .CountAsync(c => c.UserId == user.Id && c.Repetitions > 0 && c.DueAt > todayStart && c.DueAt <= tomorrowStart.AddDays(365));

        return Results.Ok(new ReviewStatsDto(dueCount, totalCards, studiedToday));
    }
}
```

- [ ] **Step 2: Register in Program.cs**

Add after `app.MapCardEndpoints();`:

```csharp
app.MapReviewEndpoints();
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build fasolt.Server
```

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/ReviewEndpoints.cs fasolt.Server/Program.cs
git commit -m "feat: add /api/review endpoints (due, rate, stats)"
```

---

## Task 5: Frontend Types Update

**Files:**
- Modify: `fasolt.client/src/types/index.ts`

- [ ] **Step 1: Add SM-2 fields to Card interface**

Update the `Card` interface to add:

```typescript
export interface Card {
  id: string
  fileId: string | null
  sourceHeading: string | null
  front: string
  back: string
  cardType: 'file' | 'section' | 'custom'
  createdAt: string
  easeFactor: number
  interval: number
  repetitions: number
  dueAt: string | null
  state: 'new' | 'learning' | 'mature'
}
```

Add new types:

```typescript
export interface DueCard {
  id: string
  front: string
  back: string
  cardType: string
  sourceHeading: string | null
  fileId: string | null
  state: string
  dueAt: string | null
}

export interface ReviewStats {
  dueCount: number
  totalCards: number
  studiedToday: number
}
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/types/index.ts
git commit -m "feat: add SM-2 fields to Card type, add DueCard and ReviewStats types"
```

---

## Task 6: Rewrite Review Store

**Files:**
- Modify: `fasolt.client/src/stores/review.ts`

- [ ] **Step 1: Rewrite store**

```typescript
// fasolt.client/src/stores/review.ts
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { DueCard, ReviewStats } from '@/types'
import { apiFetch } from '@/api/client'

export const useReviewStore = defineStore('review', () => {
  const queue = ref<DueCard[]>([])
  const currentIndex = ref(0)
  const isFlipped = ref(false)
  const isActive = ref(false)
  const loading = ref(false)

  const sessionStats = ref({
    reviewed: 0,
    again: 0,
    hard: 0,
    good: 0,
    easy: 0,
    startTime: 0,
  })

  const currentCard = computed(() =>
    currentIndex.value < queue.value.length ? queue.value[currentIndex.value] : null
  )

  const isComplete = computed(() => isActive.value && currentCard.value === null)

  const progress = computed(() => {
    if (queue.value.length === 0) return 0
    return Math.round((currentIndex.value / queue.value.length) * 100)
  })

  const sessionTime = computed(() => {
    if (!sessionStats.value.startTime) return 0
    return Math.round((Date.now() - sessionStats.value.startTime) / 1000)
  })

  async function startSession() {
    loading.value = true
    try {
      const cards = await apiFetch<DueCard[]>('/review/due')
      queue.value = cards
      currentIndex.value = 0
      isFlipped.value = false
      isActive.value = true
      sessionStats.value = { reviewed: 0, again: 0, hard: 0, good: 0, easy: 0, startTime: Date.now() }
    } finally {
      loading.value = false
    }
  }

  function flipCard() {
    isFlipped.value = true
  }

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
      // Re-queue Again card at the back
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

  function endSession() {
    isActive.value = false
    queue.value = []
    currentIndex.value = 0
    isFlipped.value = false
  }

  async function fetchStats(): Promise<ReviewStats> {
    return apiFetch<ReviewStats>('/review/stats')
  }

  return {
    queue, currentCard, isFlipped, isActive, isComplete, loading,
    progress, sessionStats, sessionTime,
    startSession, flipCard, rate, endSession, fetchStats,
  }
})
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/stores/review.ts
git commit -m "feat: rewrite review store with API-backed SM-2 session"
```

---

## Task 7: Wire ReviewView and ReviewCard

**Files:**
- Modify: `fasolt.client/src/views/ReviewView.vue`
- Modify: `fasolt.client/src/components/ReviewCard.vue`

- [ ] **Step 1: Update ReviewCard with markdown rendering**

Read `ReviewCard.vue`. Replace it with:

```vue
<script setup lang="ts">
import type { DueCard } from '@/types'
import { useMarkdown } from '@/composables/useMarkdown'

const props = defineProps<{ card: DueCard; isFlipped: boolean }>()
defineEmits<{ flip: [] }>()

const { render } = useMarkdown()
</script>

<template>
  <div
    class="flex flex-1 cursor-pointer flex-col items-center justify-center rounded-lg border border-border bg-card p-5 sm:p-8"
    @click="$emit('flip')"
  >
    <div class="text-[11px] uppercase tracking-widest text-muted-foreground">
      {{ isFlipped ? 'Answer' : 'Question' }}
    </div>
    <div
      class="mt-3 w-full max-w-lg text-center"
      :class="isFlipped ? 'text-muted-foreground' : 'text-foreground'"
    >
      <div class="prose prose-sm dark:prose-invert max-w-none" v-html="render(card.front)" />
    </div>
    <div v-if="isFlipped" class="mt-4 w-full max-w-lg text-center">
      <div class="prose prose-sm dark:prose-invert max-w-none" v-html="render(card.back)" />
    </div>
    <div v-if="card.sourceHeading" class="mt-3 font-mono text-[11px] text-muted-foreground">
      {{ card.sourceHeading }}
    </div>
  </div>
</template>
```

- [ ] **Step 2: Rewrite ReviewView**

Read `ReviewView.vue`. Replace the script section — remove mock data, wire to real store. The key changes:

- Remove `mockCards` and `import type { Card, ReviewRating }`
- `onMounted`: call `review.startSession()` instead of passing mock cards
- Rating handler: call `review.rate(quality)` with numeric quality (0/2/4/5) instead of string
- The existing template structure (ProgressMeter, ReviewCard, RatingButtons, SessionComplete) stays — just wire props/events to the rewritten store

Read the full file first, then update the script to remove mocks and wire to the API-backed store. Keep keyboard shortcuts working (Space to flip, 1=Again/0, 2=Hard/2, 3=Good/4, 4=Easy/5).

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/ReviewView.vue fasolt.client/src/components/ReviewCard.vue
git commit -m "feat: wire ReviewView and ReviewCard to real SM-2 data with markdown rendering"
```

---

## Task 8: Dashboard Study Button

**Files:**
- Modify: `fasolt.client/src/views/DashboardView.vue`

- [ ] **Step 1: Add study button with due count**

Read the file. Add a "Study now" button that links to `/review` and shows the due card count. Fetch due count from the review store's `fetchStats()` on mount. Replace the hardcoded mock stats with real `dueCount` and `totalCards` from the API. Keep other mock stats (retention, streak) as placeholders for now.

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/views/DashboardView.vue
git commit -m "feat: add Study now button and real due/total stats on dashboard"
```

---

## Task 9: Playwright Smoke Test

- [ ] **Step 1: Start full stack**
- [ ] **Step 2: Upload a test .md file, create cards from sections**
- [ ] **Step 3: Navigate to /review, verify cards appear**
- [ ] **Step 4: Flip a card, rate it Good**
- [ ] **Step 5: Verify the card advances, session progresses**
- [ ] **Step 6: Rate remaining cards, verify session complete summary**
- [ ] **Step 7: Go to dashboard, verify due count updated**
- [ ] **Step 8: Commit any fixes**

---

## Task 10: Move Requirements to Done

```bash
mv docs/requirements/04-spaced-repetition.md docs/requirements/done/
git add docs/requirements/04-spaced-repetition.md docs/requirements/done/04-spaced-repetition.md
git commit -m "docs: move 04-spaced-repetition.md to done/"
```
