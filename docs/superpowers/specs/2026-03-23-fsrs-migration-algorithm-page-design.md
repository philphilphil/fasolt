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

Keep: `State` (values change to match FSRS: `"New"`, `"Learning"`, `"Review"`, `"Relearning"`), `DueAt`, `LastReviewedAt`.

**DB migration:** Add new columns, drop old SM-2 columns. Existing cards reset to `New` state — the product is pre-launch with only dev data, no real user history to preserve.

**Review service:** Replace `Sm2Algorithm.Calculate()` call with FSRS.Core's `IScheduler.ReviewCard(card, rating)`. The library returns the updated card state with new due date, stability, difficulty, etc.

**Rating mapping:** The current API accepts `quality` as an integer (0, 2, 4, 5). Change to accept a string rating matching the UI buttons:

| UI Button | Current (SM-2) | New (FSRS)       |
|-----------|----------------|------------------|
| Again     | `quality: 0`   | `rating: "again"` |
| Hard      | `quality: 2`   | `rating: "hard"`  |
| Good      | `quality: 4`   | `rating: "good"`  |
| Easy      | `quality: 5`   | `rating: "easy"`  |

**DTOs:** Update `DueCardDto` and `RateCardResponse` to expose FSRS fields (`stability`, `difficulty`, `reps`, `lapses`, `state`) instead of SM-2 fields.

**FSRS configuration:** Register via DI with sensible defaults:
- `DesiredRetention`: 0.9 (90% target)
- `MaximumInterval`: 36500 (100 years, effectively unlimited)
- `EnableFuzzing`: true (adds slight randomness to prevent review clustering)

### Frontend Changes

**Types:** Update `Card`, `DueCard` interfaces — replace `easeFactor`/`interval`/`repetitions` with `stability`/`difficulty`/`reps`/`lapses`.

**Review store:** Change `ratingToQuality` mapping from integer quality values to string rating values. The store already uses `again`/`hard`/`good`/`easy` internally.

**Card detail views:** Update any display of SRS fields to show FSRS fields instead.

**State labels:** Map FSRS states to display: `New` → "New", `Learning` → "Learning", `Review` → "Review", `Relearning` → "Relearning".

### MCP Tools

The MCP `CreateCards` tool creates cards with default SRS state — no changes needed since new cards start with default FSRS values. The `GetOverview` tool reports cards by state — update state grouping to match FSRS states.

### Tests

- **Unit tests for FSRS integration:** Verify that reviewing a new card with each rating produces expected state transitions and scheduling.
- **Update existing tests:** `CardServiceTests.UpdateCardFields_ById_PreservesSrsState` — update to preserve FSRS fields instead of SM-2 fields.
- **Playwright E2E:** Run through a full review session to confirm the UI still works end-to-end.

### Delete

- `Sm2Algorithm.cs` — no longer needed, FSRS.Core replaces it entirely.

## Part 2: Algorithm Explainer Page

### Route

- Path: `/algorithm` (public, no auth required)
- Added to the Vue Router alongside other public routes

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

- Add "Algorithm" link to footer or landing page
- Accessible without login
