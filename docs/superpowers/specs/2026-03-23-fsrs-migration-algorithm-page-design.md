# FSRS Migration + Algorithm Explainer Page

## Overview

Migrate the spaced repetition engine from SM-2 to FSRS (Free Spaced Repetition Scheduler) and add a public page explaining how the algorithm works.

**Why FSRS:** SM-2 (1987) uses a fixed formula with no personalization. FSRS is the modern standard — trained on 700M+ reviews, 20-30% fewer reviews for the same retention, and now the default in Anki. Benchmarks show 99.6% superiority over SM-2 on prediction accuracy.

**Library:** [FSRS.Core](https://github.com/TranPhucTien/FSRS.Core) v1.0.7 — the only .NET FSRS implementation. MIT licensed, targets .NET 8+, DI-friendly API. Community port of the official py-fsrs.

## Part 1: FSRS Migration

### Backend Changes

**New dependency:** `FSRS.Core` NuGet package.

**Card entity** — replace SM-2 fields with FSRS fields:

| Remove (SM-2)     | Add (FSRS)        | Type             | Default |
|--------------------|--------------------|------------------|---------|
| `EaseFactor`       | `Stability`        | `double`         | 0       |
| `Interval`         | `Difficulty`       | `double`         | 0       |
| `Repetitions`      | `ElapsedDays`      | `int`            | 0       |
|                    | `ScheduledDays`    | `int`            | 0       |
|                    | `Reps`             | `int`            | 0       |
|                    | `Lapses`           | `int`            | 0       |

Keep: `State` (values change to match FSRS: `"new"`, `"learning"`, `"review"`, `"relearning"` — lowercase to match existing convention), `DueAt`, `LastReviewedAt`.

**DB migration:** Add new columns, drop old SM-2 columns. Reset all existing cards to `"new"` state with default FSRS values — the product is pre-launch with only dev data, no real user history to preserve. The migration must explicitly `UPDATE Cards SET State = 'new'` to handle any `"mature"` values.

**Review service:** Replace `Sm2Algorithm.Calculate()` call with FSRS.Core's `IScheduler.ReviewCard(card, rating)`. The library returns a tuple of `(updatedCard, reviewLog)`. Use `updatedCard.Due` directly for `DueAt` — do not manually compute from `ScheduledDays`.

**Rating mapping:** The current API accepts `quality` as an integer (0, 2, 4, 5). Change to accept a string rating matching the UI buttons:

| UI Button | Current (SM-2) | New (FSRS)       |
|-----------|----------------|------------------|
| Again     | `quality: 0`   | `rating: "again"` |
| Hard      | `quality: 2`   | `rating: "hard"`  |
| Good      | `quality: 4`   | `rating: "good"`  |
| Easy      | `quality: 5`   | `rating: "easy"`  |

**DTOs:** Update `RateCardRequest` to accept `string Rating` instead of `int Quality`. Update `DueCardDto` and `RateCardResponse` to expose FSRS fields (`stability`, `difficulty`, `reps`, `lapses`, `state`) instead of SM-2 fields.

**FSRS configuration:** Register via DI with sensible defaults:
- `DesiredRetention`: 0.9 (90% target)
- `MaximumInterval`: 36500 (100 years, effectively unlimited)
- `EnableFuzzing`: true (adds slight randomness to prevent review clustering)

### Frontend Changes

**Types (`types/index.ts`):** Update `Card`, `DueCard` interfaces — replace `easeFactor`/`interval`/`repetitions` with `stability`/`difficulty`/`reps`/`lapses`. Update `Card.state` type union from `'new' | 'learning' | 'mature'` to `'new' | 'learning' | 'review' | 'relearning'`.

**Review store (`stores/review.ts`):** Change `rate()` to send `{ cardId, rating: "again" }` string instead of `{ cardId, quality: 0 }` integer.

**Review view (`views/ReviewView.vue`):** Update `ratingToQuality` map and keyboard shortcuts to use string ratings instead of integer quality values.

**Cards view (`views/CardsView.vue`):** Update state filter dropdown — replace `"mature"` with `"review"` and add `"relearning"`.

**Card detail views:** Update any display of SRS fields to show FSRS fields instead.

### Backend Services

**`CardService.cs`:** Update hardcoded SM-2 defaults in `BulkCreateCards` (`EaseFactor = 2.5`, etc.) to FSRS defaults.

**`OverviewService.cs`:** Update `AllStates` array to `["new", "learning", "review", "relearning"]`.

**`AppDbContext.cs`:** Remove `HasDefaultValue(2.5)` for `EaseFactor`, add default value configs for FSRS columns.

**`ReviewEndpoints.cs`:** Remove `ValidQualities` array, add string rating validation.

### MCP Tools

The MCP `CreateCards` tool creates cards with default SRS state — no changes needed since new cards start with default FSRS values. The `GetOverview` tool reports cards by state — update state grouping to match FSRS states.

### Tests

**Backend:**
- Unit tests for FSRS integration: verify reviewing a new card with each rating produces expected state transitions and scheduling.
- Update `CardServiceTests.UpdateCardFields_ById_PreservesSrsState` to preserve FSRS fields.
- Update `ReviewTests` for new rating format and FSRS card defaults.
- Update `OverviewServiceTests` to include `"relearning"` state.

**Frontend:**
- Update `__tests__/stores/review.test.ts` — change integer quality values to string ratings.
- Update `__tests__/composables/useSearch.test.ts` — change `"mature"` state to `"review"`.
- Update `__tests__/types.test.ts` if it references SM-2 fields.

**E2E:** Playwright test through a full review session.

### Delete

- `Sm2Algorithm.cs` — no longer needed, FSRS.Core replaces it entirely.

### Also Update

- `CLAUDE.md` — change SM-2 references to FSRS.

### Files Affected (complete list)

| File | Change |
|------|--------|
| `fasolt.Server/Application/Services/Sm2Algorithm.cs` | Delete |
| `fasolt.Server/Domain/Entities/Card.cs` | Replace SM-2 fields with FSRS fields |
| `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs` | FSRS rating logic, remove `ValidQualities` |
| `fasolt.Server/Application/Dtos/ReviewDtos.cs` | Update all DTOs |
| `fasolt.Server/Application/Services/CardService.cs` | FSRS defaults in `BulkCreateCards` |
| `fasolt.Server/Application/Services/OverviewService.cs` | `AllStates` array |
| `fasolt.Server/Infrastructure/Data/AppDbContext.cs` | Column configs |
| `fasolt.Server/Infrastructure/Data/Migrations/` | New migration |
| `fasolt.Server/Program.cs` | Register FSRS.Core DI |
| `fasolt.client/src/types/index.ts` | `Card`, `DueCard` interfaces, state union |
| `fasolt.client/src/stores/review.ts` | String ratings instead of int quality |
| `fasolt.client/src/views/ReviewView.vue` | Rating map, keyboard shortcuts |
| `fasolt.client/src/views/CardsView.vue` | State filter dropdown |
| `fasolt.client/src/views/CardDetailView.vue` | SRS field display |
| `fasolt.client/src/views/LandingView.vue` | Algorithm page link |
| `fasolt.client/src/router/index.ts` | New `/algorithm` route |
| `fasolt.Tests/CardServiceTests.cs` | FSRS field assertions |
| `fasolt.Tests/ReviewTests.cs` | Rating format, card defaults |
| `fasolt.Tests/OverviewServiceTests.cs` | `"relearning"` state |
| `fasolt.client/src/__tests__/stores/review.test.ts` | String ratings |
| `fasolt.client/src/__tests__/composables/useSearch.test.ts` | `"mature"` → `"review"` |
| `CLAUDE.md` | SM-2 → FSRS references |

## Part 2: Algorithm Explainer Page

### Route

- Path: `/algorithm` (public, no auth required)
- Added to Vue Router alongside other public routes (`/login`, `/register`, `/`)

### Content Sections

1. **What is Spaced Repetition?** — Brief intro: the concept of reviewing at increasing intervals to optimize long-term memory.

2. **The FSRS Algorithm** — Explain the Three Component Model of Memory:
   - **Difficulty** (D) — how inherently hard a card is (updated with each review)
   - **Stability** (S) — how long the memory lasts before recall probability drops (the core scheduling driver)
   - **Retrievability** (R) — probability you can recall the card right now (decays over time)

3. **How Reviews Work** — The four rating buttons and what they do:
   - Again → resets stability, card re-enters learning
   - Hard → small stability increase
   - Good → normal stability increase
   - Easy → large stability increase

4. **How Intervals Grow** — A simple example walkthrough showing how a card's intervals increase over successive "Good" reviews (e.g., 1d → 3d → 8d → 21d → ...).

5. **Why FSRS?** — Brief comparison to SM-2: personalized scheduling, machine-learning-optimized defaults, 20-30% fewer reviews for the same retention.

### Design

Follow existing page patterns (see `McpView.vue` for a similar informational page):
- `Card` / `CardHeader` / `CardContent` components for each section
- Clean typography, no interactive elements (static explainer)
- Responsive layout matching existing pages

### Navigation

- Add "Algorithm" link to the landing page (`LandingView.vue`)
- Accessible without login
