# Mobile & Sizing Rework Design

## Problem

The app's text and UI elements are too small across the board, and the study mode card fills all vertical space on mobile, pushing rating buttons off-screen and requiring scrolling. The app targets standard phones (~390px wide).

## Scope

Holistic sizing and spacing pass across the entire frontend. No layout or navigation structure changes — just sizing, spacing, and the study mode card layout fix.

## Design

### 1. Global Text Sizing Remap

Establish a 12px minimum for all text in the app.

| Current | New | Affected Components |
|---------|-----|---------------------|
| `text-[10px]` | `text-xs` (12px) | StatCard (labels, delta), BottomNav (labels), SessionComplete (rating labels), CardsView (Badge text, table headers, sort arrows, action buttons), SourcesView (due badge) |
| `text-[11px]` | `text-xs` (12px) | ReviewView (context bar), ReviewCard (question/answer label, source heading), RatingButtons (button text — further bumped, see §2), DecksView (description, card count), SourcesView (card count) |
| `text-xs` → `text-sm` (body content) | `text-sm` (14px) | CardsView table rows (`text-xs` on `<TableRow>`), TopBar search input placeholder |
| `prose-sm` | `prose` (16px base) | ReviewCard markdown content |

**Unchanged at `text-xs`:** Tab nav labels (AppLayout), dropdown menu items, toolbar button labels, column selector items, pagination text. These are UI chrome that reads fine at 12px.

### 2. Study Mode Layout (ReviewView + Children)

**ReviewCard.vue:**
- Remove `flex-1` from the card container — card sizes to its content instead of filling all available vertical space
- Add `min-h-[180px]` so the card still has visual presence for short questions
- Padding: keep `p-5 sm:p-8` (adequate after text bump)
- Text: `prose-sm` → `prose` for card content, `text-[11px]` → `text-xs` for labels

**ReviewView.vue:**
- Outer container: replace `min-h-[calc(100vh-8rem)]` with `min-h-0 flex-1` approach
- Wrap the card area in a flex container that centers the card vertically in remaining space without forcing the card itself to stretch
- The card area (between progress meter and rating buttons) gets `flex-1 flex flex-col items-center justify-center` — the *wrapper* fills space, not the card
- Add `max-h-[60vh] overflow-y-auto` on the card for very long answers — card scrolls internally, buttons stay visible

**RatingButtons.vue:**
- Mobile: buttons become full-width row with `py-3` (≈48px touch targets)
- Text: `text-[11px]` → `text-sm` (14px)
- Button size: `size="sm"` → `size="default"`
- Layout: `gap-1.5` → `gap-2`

**SessionComplete.vue:**
- Rating labels: `text-[10px]` → `text-xs`

### 3. Bottom Nav (BottomNav.vue)

- Icon size: `text-lg` → `text-xl`
- Label text: `text-[10px]` → `text-[11px]`
- Vertical padding: `py-2` → `py-2.5`

### 4. Desktop Behavior

All text sizing bumps apply globally (not behind mobile breakpoints). The study mode card change works on desktop by moving flex-1 from the card to a wrapper div that centers the card — visually similar but the card has natural height.

### 5. Files to Modify

1. `fasolt.client/src/components/ReviewCard.vue` — remove flex-1, add min-h, bump prose
2. `fasolt.client/src/views/ReviewView.vue` — wrapper layout, card max-h
3. `fasolt.client/src/components/RatingButtons.vue` — bigger buttons, bigger text
4. `fasolt.client/src/components/SessionComplete.vue` — bump label sizes
5. `fasolt.client/src/components/StatCard.vue` — bump label/delta sizes
6. `fasolt.client/src/components/BottomNav.vue` — bump icon/label sizes, padding
7. `fasolt.client/src/views/CardsView.vue` — bump table text sizes
8. `fasolt.client/src/views/DecksView.vue` — bump description/count sizes
9. `fasolt.client/src/views/SourcesView.vue` — bump count/badge sizes
10. `fasolt.client/src/components/TopBar.vue` — bump beta badge, search input text

### 6. What This Does NOT Include

- No layout/navigation changes
- No new components
- No PWA features (US-10.2 is P3, separate work)
- No Tailwind config changes or custom breakpoints
- No changes to auth pages or landing page
