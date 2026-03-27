# Implementation Plan: Review Edit & Skip Buttons

**Spec:** `docs/superpowers/specs/2026-03-27-review-edit-skip-design.md`
**Issues:** #39, #40

## Track 1: Web (Vue + Pinia)

### Step 1: Update review store (`fasolt.client/src/stores/review.ts`)
- Add `skipped: 0` to `sessionStats` initial state and `startSession` reset
- Add `skip()` method: increment `sessionStats.skipped`, advance `currentIndex`, reset `isFlipped` — no API call
- Update `isComplete`: `isActive && currentCard === null && (sessionStats.reviewed > 0 || sessionStats.skipped > 0)`
- Update `noDueCards`: `isActive && queue.length === 0 && sessionStats.reviewed === 0 && sessionStats.skipped === 0`

### Step 2: Add edit icon to ReviewCard (`fasolt.client/src/components/ReviewCard.vue`)
- Add a pencil icon (lucide `Pencil` or inline SVG) in the top-right corner of the card div
- Style: `text-muted-foreground/40 hover:text-muted-foreground` transition, small (14px)
- `@click.stop` to prevent card flip
- Opens `window.open(\`/cards/${card.id}?edit=true\`, '_blank')`
- Requires adding `card.id` — ReviewCard currently receives the full `DueCard` object so `card.id` is already available

### Step 3: Add skip button to ReviewView (`fasolt.client/src/views/ReviewView.vue`)
- When NOT flipped: add a small "Skip" text button below the "Click the card or press space" hint, muted style
- When flipped: add a small "Skip" text button below the RatingButtons
- Style: `text-xs text-muted-foreground/50 hover:text-muted-foreground` — clearly secondary
- Register `S` keyboard shortcut: `'s': () => { if (!review.isComplete) review.skip() }`
- Add skip hint to context bar: `<KbdHint keys="s" /> skip`

### Step 4: Update SessionComplete (`fasolt.client/src/components/SessionComplete.vue`)
- Accept new prop `skippedCount: number`
- Show skipped count below the rating grid (or as a 5th column) only if `skippedCount > 0`
- Use muted/neutral color (e.g., `text-muted-foreground`)
- Update ReviewView to pass `review.sessionStats.skipped` as the prop

## Track 2: iOS (SwiftUI)

### Step 5: Update StudyViewModel (`fasolt.ios/Fasolt/ViewModels/StudyViewModel.swift`)
- Add `var skippedCount: Int = 0`
- Reset `skippedCount = 0` in `startSession`
- Add `skipCard()` method: increment `skippedCount`, advance `currentIndex`, reset `isFlipped`, update state (summary if last card, studying otherwise)
- Update summary condition: show summary if `currentIndex >= cards.count` and `(cardsStudied > 0 || skippedCount > 0)`

### Step 6: Add skip button to StudyView (`fasolt.ios/Fasolt/Views/Study/StudyView.swift`)
- When NOT flipped: add small "Skip" button below "Show Answer", `.secondary` foreground, `.caption` font
- When flipped: add "Skip" button below the rating buttons row, same subdued style
- Haptic feedback (light impact) on skip
- Call `viewModel.skipCard()`

### Step 7: Update StudySummaryView (`fasolt.ios/Fasolt/Views/Study/StudySummaryView.swift`)
- Accept new `skippedCount: Int = 0` parameter
- Show "Skipped" row only if `skippedCount > 0`, with `.secondary` color (no colored circle, or gray circle)
- Pass `skippedCount` from StudyView when creating summary

## Parallel Execution

- **Track 1** (Steps 1-4): Web — independent, no backend changes
- **Track 2** (Steps 5-7): iOS — independent, no backend changes
- Both tracks can run in parallel
