# Epic 4: Spaced Repetition Study

## US-4.1 — Study Due Cards (P0)

As a user, I want to study my due cards so I can review material at optimal intervals.

**Acceptance criteria:**

- Show card front, tap/click to reveal back
- After reveal, rate with 4 buttons: Again, Hard, Good, Easy (mapped to SM-2 qualities 0, 2, 4, 5)
- SM-2 algorithm calculates next review date based on rating
- Cards disappear from "due" after review
- Session continues until no cards remain
- Keyboard shortcuts: Space to flip, 1/2/3/4 for Again/Hard/Good/Easy

## US-4.2 — SM-2 Scheduling (P0)

As a user, I want the app to schedule reviews using spaced repetition so I see cards just before I'd forget them.

**Acceptance criteria:**

- New cards are immediately available for first study (not deferred to next day)
- After first study, interval starts at 1 day (Good) or 10 minutes (Again)
- Quality ratings adjust ease factor and interval per SM-2
- Minimum ease factor: 1.3
- Failed cards (Again) reset to short interval and stay in session
- Next review date stored per card
- Card states: New → Learning → Mature (see [overview](00-overview.md) for definitions)

## US-4.3 — Study Session Summary (P1)

As a user, I want to see a summary after a study session so I know how I did.

**Acceptance criteria:**

- Cards reviewed count
- Breakdown by rating (again/hard/good/easy)
- Time spent
- Cards still due today
- "Study more" or "Done" options

## US-4.4 — Study by Group (P1)

As a user, I want to study cards from a specific group so I can focus on one topic at a time.

**Acceptance criteria:**

- Select group before starting study session
- Only due cards from that group shown
- Same SM-2 scheduling applies
- Session summary scoped to group

_Depends on: US-5.1, US-5.2_

## US-4.5 — Daily Study Limit (P2)

As a user, I want to set a daily limit for new cards and reviews so I don't get overwhelmed.

**Acceptance criteria:**

- Configurable max new cards per day (default: 20)
- Configurable max reviews per day (default: 100)
- Settings page to adjust limits
- Show remaining capacity on dashboard

## US-4.6 — Undo Last Rating (P2)

As a user, I want to undo my last rating if I accidentally tapped the wrong button.

**Acceptance criteria:**

- Undo available for last card only
- Reverts scheduling changes
- Card reappears for re-rating

## US-4.7 — Markdown Rendering During Review (P1)

As a user, I want cards to render markdown properly during review so code blocks, lists, and formatting display correctly.

**Acceptance criteria:**

- Full markdown rendering on both front and back
- Syntax-highlighted code blocks
- Tables rendered properly
- Images displayed as alt text placeholder (consistent with US-2.3 MVP approach)

## US-4.8 — Suspend and Bury Cards (P2)

As a user, I want to temporarily skip cards without deleting them so I can manage my review load.

**Acceptance criteria:**

- **Suspend**: remove card from review indefinitely until manually unsuspended
- **Bury**: skip card for today only, automatically unburied tomorrow
- Both actions available during review and from card list
- Suspended/buried status visible in card list with filter
