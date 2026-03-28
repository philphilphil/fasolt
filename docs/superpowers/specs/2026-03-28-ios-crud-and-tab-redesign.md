# iOS: Full CRUD for Cards & Decks + Tab Restructure

**Issue:** #42
**Date:** 2026-03-28

## Overview

Add full card and deck management (create, edit, delete, suspend/unsuspend) to the iOS app, then restructure the tab bar from 4 tabs to 3. CRUD is a secondary feature — quick edits and occasional management — so the design keeps the browse/study experience clean by isolating editing into separate sheets.

## Approach: Incremental (Two Phases)

1. **Phase 1 — CRUD Features:** Add create/edit sheets, swipe actions, and delete confirmations to the existing views. No navigation changes.
2. **Phase 2 — Tab Restructure:** Merge Decks and Cards tabs into a single "Library" tab, rename Dashboard to "Study." 3 tabs total.

This ordering is low-risk: CRUD patterns (sheets, swipe actions) are identical regardless of tab structure, so nothing is thrown away.

---

## Phase 1: CRUD Features

### Card Create

- **Entry point:** "+" button in the toolbar of CardListView.
- **Presentation:** `.sheet` modal slides up.
- **Form fields:**
  - Front (multiline, required)
  - Back (multiline, required)
  - Source File (optional text)
  - Heading (optional text)
  - Deck (optional picker from existing decks)
- **Actions:** Cancel (top-left) dismisses the sheet. Save (top-right) is disabled until Front and Back are non-empty. Save calls the create cards API endpoint, dismisses the sheet, and refreshes the card list.

### Card Edit

- **Entry point:** "Edit" button in the toolbar of CardDetailView.
- **Presentation:** `.sheet` modal, same form layout as Card Create, pre-populated with current values.
- **Form fields:** Same as Card Create (Front, Back, Source File, Heading, Deck).
- **Actions:** Cancel dismisses without saving. Save calls the update cards API endpoint, dismisses the sheet, and refreshes the detail view.

### Card Swipe Actions

- **Applies to:** Card rows in CardListView and DeckDetailView.
- **Swipe left reveals:**
  - **Suspend** (orange) — toggles immediately, no confirmation. Row updates to show suspended visual state (0.5 opacity, existing pattern). Label changes to "Unsuspend" when the card is already suspended.
  - **Delete** (red) — shows a confirmation alert: "Delete this card? This cannot be undone." with "Delete" (destructive) and "Cancel" buttons.
- Delete calls the delete cards API endpoint, removes the row with animation, and refreshes counts.

### Deck Create

- **Entry point:** "+" button in the toolbar of DeckListView.
- **Presentation:** `.sheet` modal.
- **Form fields:**
  - Name (required)
  - Description (optional, multiline)
- **Actions:** Cancel dismisses. Save is disabled until Name is non-empty. Save calls the create deck API endpoint, dismisses the sheet, and refreshes the deck list.

### Deck Edit

- **Entry point:** "Edit" button in the toolbar of DeckDetailView.
- **Presentation:** `.sheet` modal, pre-populated with current Name and Description.
- **Actions:** Cancel dismisses. Save calls the update deck API endpoint, dismisses the sheet, and refreshes the detail view.

### Deck Swipe Actions

- **Applies to:** Deck rows in DeckListView.
- **Swipe left reveals:**
  - **Suspend** (orange) — toggles immediately, no confirmation. Label changes to "Unsuspend" when already suspended.
  - **Delete** (red) — shows a confirmation alert with three buttons:
    - "Delete Deck Only" — removes the deck, contained cards become unassigned.
    - "Delete Deck and Cards" — removes the deck and all its cards.
    - "Cancel"
- Delete calls the appropriate API endpoint, removes the row with animation, and refreshes counts.

---

## Phase 2: Tab Restructure

### Tab Bar Changes

Current: **Dashboard | Decks | Cards | Settings** (4 tabs)
New: **Study | Library | Settings** (3 tabs)

- **Study** — the current DashboardView, renamed. Same layout, same content, same icon (or switch to `book.fill`). No functional changes.
- **Library** — new combined view replacing Decks and Cards tabs.
- **Settings** — unchanged.

### Library View

- **Top:** `Picker` with `.pickerStyle(.segmented)` — two segments: "Decks" and "Cards."
- **Decks segment:** Shows the existing DeckListView content (search, sort, pull-to-refresh, swipe actions, NavigationLink to DeckDetailView).
- **Cards segment:** Shows the existing CardListView content (search, sort, filter, swipe actions, NavigationLink to CardDetailView).
- **Navigation:** The entire Library tab is wrapped in a `NavigationStack`. Drill-down into deck detail and card detail works as before.
- **"+" button:** Present in the toolbar for both segments — creates a deck or card depending on the active segment.

---

## What's NOT in Scope

- Bulk operations (multi-select, bulk delete/move)
- Drag-and-drop reordering
- Card preview/markdown rendering in the create/edit form
- Assign/remove cards from decks via a dedicated UI (use the Deck picker in the card edit sheet instead)
- New visual design language or theme changes — the existing visual style is retained
- Changes to the Study flow, Settings, or Onboarding

---

## API Endpoints Used

All endpoints already exist — no backend changes needed.

| Operation | Endpoint |
|-----------|----------|
| Create card | `POST /api/cards` |
| Update card | `PUT /api/cards/{id}` |
| Delete card | `DELETE /api/cards/{id}` |
| Suspend/unsuspend card | `PUT /api/cards/{id}/suspended` |
| Create deck | `POST /api/decks` |
| Update deck | `PUT /api/decks/{id}` |
| Delete deck | `DELETE /api/decks/{id}` |
| Suspend/unsuspend deck | `PUT /api/decks/{id}/suspended` |
