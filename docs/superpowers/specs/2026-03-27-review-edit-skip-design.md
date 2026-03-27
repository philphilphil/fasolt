# Review Session: Edit & Skip Buttons

**GitHub Issues:** #39 (edit button), #40 (skip button)
**Date:** 2026-03-27

## Summary

Add two actions to the review session: an edit button that opens the card detail page in a new tab (web only), and a skip button that skips the current card without affecting its SRS state.

## Edit Button (#39) — Web Only

### Behavior
- Small pencil icon in the **top-right corner** of the ReviewCard component
- Visible on both front and back of the card
- Clicking opens `/cards/{cardId}?edit=true` in a new browser tab (`target="_blank"`)
- The review session continues uninterrupted in the original tab
- No backend changes needed

### Design
- Ghost/muted style: `text-muted-foreground/50` with hover state
- Small size (14-16px icon) so it doesn't compete with card content
- Uses `click.stop` to prevent the click from also flipping the card
- Not implemented on iOS (cards can't open a web editor from the native app)

## Skip Button (#40) — Web + iOS

### Behavior
- Skips the current card for this session only
- **No review is recorded** — the card's SRS state is untouched
- The skipped card does **not** reappear later in the current session
- Available on both front and back of the card (before and after flipping)
- Progress bar advances when a card is skipped (you're moving through the queue)

### Web Design
- When card is **not flipped**: small, muted text link ("Skip") below the "click to reveal" hint — visually subdued, not in the way
- When card is **flipped**: small muted text link below the rating buttons — clearly secondary to Again/Hard/Good/Easy
- Keyboard shortcut: `S`
- Shortcut hint shown in the context bar alongside existing `space` and `1-4` hints

### iOS Design
- Secondary button below the rating buttons when flipped
- When not flipped: small secondary action below "Show Answer" button
- Subdued tint (`.secondary` style) to distinguish from primary actions
- Haptic feedback on skip (light impact, same as rating)

### Session Summary
- Skipped count shown **separately** from rated cards
- Not included in the "cards reviewed" total number
- Displayed with a neutral/muted color to distinguish from rating breakdowns
- Web: additional column or row in the summary card
- iOS: additional row in the summary list, muted style

### State Changes

**Web (Pinia store):**
- Add `skipped: number` to `sessionStats`
- Add `skip()` method: increments `skipped`, advances `currentIndex`, resets `isFlipped` — no API call
- `isComplete` computed needs adjustment: session is complete when no more cards AND at least one card was reviewed OR skipped (`reviewed > 0 || skipped > 0`)
- `noDueCards` computed needs adjustment: no due cards only when queue is empty AND nothing was reviewed AND nothing was skipped
- `progress` computed already works (based on `currentIndex / queue.length`)

**iOS (StudyViewModel):**
- Add `skippedCount: Int` property
- Add `skipCard()` method: increments `skippedCount`, advances `currentIndex`, resets flip state — no API call
- Summary view receives `skippedCount` as new parameter

## Files to Modify

### Web
- `fasolt.client/src/stores/review.ts` — add skip state and method
- `fasolt.client/src/components/ReviewCard.vue` — add edit icon (top-right corner)
- `fasolt.client/src/components/RatingButtons.vue` — no changes (skip is separate from ratings)
- `fasolt.client/src/components/SessionComplete.vue` — add skipped count display
- `fasolt.client/src/views/ReviewView.vue` — add skip button/link in both states, register `S` keyboard shortcut, add skip hint to context bar

### iOS
- `fasolt.ios/Fasolt/ViewModels/StudyViewModel.swift` — add skip state and method
- `fasolt.ios/Fasolt/Views/Study/StudyView.swift` — add skip button in both states
- `fasolt.ios/Fasolt/Views/Study/StudySummaryView.swift` — add skipped count row

### No Backend Changes
Both features are entirely client-side. Skip doesn't record a review, and edit just opens an existing page in a new tab.
