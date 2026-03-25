# Misc Bugs & UI Fixes — Design Spec

**GitHub Issue:** #20
**Date:** 2026-03-25

## Overview

A collection of UI bugs and improvements across the web frontend and iOS app. Covers card table unification, card detail layout, search improvements, dark mode toggle relocation, MCP route change, display name removal, and iOS foreground refresh.

---

## 1. Unified Card Table

### Problem

The card tables in `/cards` (CardsView) and `/decks/:id` (DeckDetailView) have different columns and layouts. The Cards view shows cards from inactive decks. No deck filter exists.

### Design

Extract a shared `CardTable` component used by both views. Configuration via props controls which columns and actions appear.

**Columns (in order):**

| Column | `/cards` | `/decks/:id` | Notes |
|--------|----------|--------------|-------|
| Front | Yes | Yes | Truncated to 80 chars, source file subtitle in muted text below |
| State | Yes | Yes | Badge (new/learning/review/relearning) |
| Decks | Yes | No | Deck badges with `+` button |
| Due | Yes | Yes | Formatted `dd.mm.yyyy`, column wide enough to avoid line breaks |
| Actions | Yes | Yes | Context-dependent (see below) |

**Actions:**
- `/cards`: Edit, Delete
- `/decks/:id`: Edit, Remove from Deck, Delete

**Active filter (Cards view only):**
- Default-on checkbox labeled "Active" at the top of the filter bar
- When on: hides cards that belong *only* to inactive decks
- Cards with no deck assignment are always considered active (always shown)
- Cards in at least one active deck are shown

**Deck filter (Cards view only):**
- Dropdown filter for deck, including a "None" option to show only deckless cards

**Delete dialog:**
- If the card belongs to decks: "This card will be permanently deleted and removed from: **Deck A**, **Deck B**."
- If the card has no decks: "This card will be permanently deleted."

### Files to change
- New: `fasolt.client/src/components/CardTable.vue` — shared table component
- Edit: `fasolt.client/src/views/CardsView.vue` — use CardTable, add Active checkbox and Deck filter
- Edit: `fasolt.client/src/views/DeckDetailView.vue` — use CardTable with deck-specific config
- Edit: Card delete dialog markup (inline in CardsView or extracted component)

---

## 2. Card Detail View

### Problem

Deck names aren't clickable. Source, section, and decks are on the same line. Edit mode doesn't support editing source, section, or deck assignment.

### Design

- **Deck links:** Each deck name becomes a `<RouterLink>` to `/decks/:id`
- **Metadata layout:** Move source file, section, and decks to a dedicated line below the current header area. Group as: `Source: filename.md | Section: heading` on one line, `Decks: Deck A, Deck B` on the next
- **Edit mode:** Add source file input, section (heading) input, and deck multi-select to the edit form. Reuse the same controls/patterns as the card table inline editing.

### Files to change
- Edit: `fasolt.client/src/views/CardDetailView.vue`

---

## 3. Dark Mode Toggle

### Problem

Theme toggle is buried in the Settings page. Users expect it in the top navigation.

### Design

- Add a sun/moon icon button to the top-right of the nav bar (TopBar), next to the user menu
- **Behavior:** Click toggles between light and dark mode
- **Default:** System theme (no override stored). Once the user clicks, their choice is persisted to localStorage
- Sun icon shown in dark mode (click → light), moon icon shown in light mode (click → dark)
- Remove the Appearance section from the Settings page
- Keep the `useDarkMode` composable but simplify: only two explicit states (light/dark) plus system as the initial default

### Files to change
- Edit: `fasolt.client/src/components/TopBar.vue` — add toggle button
- Edit: `fasolt.client/src/composables/useDarkMode.ts` — simplify to toggle behavior
- Edit: `fasolt.client/src/views/SettingsView.vue` — remove Appearance card

---

## 4. Search Improvements

### Problem

Search result dropdown is too small (400px max height). Decks appear after cards but should come first.

### Design

- Increase max height from 400px to 1200px (3x)
- Reorder sections: decks first, then cards
- No other changes to search behavior or display

### Files to change
- Edit: `fasolt.client/src/components/SearchResults.vue`

---

## 5. MCP Route Change

### Problem

The MCP help page is at `/mcp`, which conflicts with the backend MCP endpoint at `/mcp`.

### Design

- Change frontend route from `/mcp` to `/mcp-setup`
- Navigation label stays "MCP"
- Update router config and any internal links

### Files to change
- Edit: `fasolt.client/src/router/index.ts`
- Edit: `fasolt.client/src/layouts/AppLayout.vue` (nav link)
- Edit: `fasolt.client/src/components/BottomNav.vue` (mobile nav link)

---

## 6. Display Name Removal

### Problem

Display name adds complexity with little value. Email is sufficient for user identification.

### Design

**Frontend:**
- Remove display name field from Settings page
- Update TopBar to always use email initial for avatar
- Update user menu dropdown to show email only
- Remove `updateProfile` call from auth store (or the display name part of it)

**Backend:**
- Remove `DisplayName` property from the user model/entity
- Remove the profile update endpoint (or the display name part)
- Create EF Core migration to drop the column
- Update seed data if it sets display name

### Files to change
- Edit: `fasolt.client/src/views/SettingsView.vue` — remove display name field
- Edit: `fasolt.client/src/components/TopBar.vue` — use email for initial/label
- Edit: `fasolt.client/src/stores/auth.ts` — remove updateProfile or display name logic
- Edit: Backend user model, DbContext, and profile endpoint
- New: EF Core migration to drop DisplayName column

---

## 7. iOS — Foreground Refresh

### Problem

When the iOS app returns from background, data is stale until manual refresh.

### Design

- Observe `scenePhase` changes (SwiftUI `@Environment(\.scenePhase)`)
- When transitioning from `background` or `inactive` to `active`, trigger a data refresh
- Refresh the current view's data (dashboard stats, deck list, review queue — whatever is currently displayed)
- Add a small debounce (e.g., skip refresh if last fetch was <30 seconds ago) to avoid excessive API calls during rapid app switching

### Files to change
- Edit: Main app entry or root view in the iOS project — add scenePhase observer
- Edit: Relevant view models to expose a refresh method

---

## Out of Scope

- Card table pagination changes (keep existing 20-per-page)
- Any backend API changes beyond display name removal
- Search algorithm or ranking changes
- iOS features beyond foreground refresh
