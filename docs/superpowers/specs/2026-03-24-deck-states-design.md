# Deck States Design

## Overview

Add an active/inactive toggle to decks. Inactive decks and their cards are excluded from study (due cards, dashboard, overview stats). Cards in multiple decks remain active if at least one deck is active. Cards without any deck are always active.

## Requirements

From `docs/requirements/16_deck_states.md`:
- Make decks inactive so they don't show up in dashboard
- All cards of an inactive deck should be inactive too, unless the card is also in another active deck
- WebApp, iOS App, and MCP should support this

## Data Model

### Deck Entity

Add `bool IsActive` property with default `true`:

```csharp
public bool IsActive { get; set; } = true;
```

One EF Core migration to add the column with default `true` (all existing decks remain active).

### Card Activity Rule (Derived)

A card's "study-active" status is derived at query time — no field on Card:

- **Active**: Card has no deck memberships, OR at least one deck with `IsActive = true`
- **Inactive**: Card has one or more decks, ALL with `IsActive = false`

As an EF Core filter expression:
```csharp
c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive)
```

## Backend

### Queries That Change

1. **`ReviewEndpoints.GetDueCards`** — Add study-active filter to the due cards query. Cards only in inactive decks are excluded. When filtering by a specific deckId, if that deck is inactive, return empty (200 OK with empty array — not an error). The frontend hides the "Study this deck" button for inactive decks, so this is a defensive fallback only.

2. **`ReviewEndpoints.GetStats`** — Due count, total count, and `studiedToday` all exclude study-inactive cards. This keeps dashboard numbers consistent — if a deck is inactive, its cards don't contribute to any study metrics.

3. **`OverviewService.GetOverview`** — Due count, total cards, and cards-by-state counts exclude study-inactive cards. `totalDecks` still includes all decks. No separate `activeDecks` count (YAGNI).

4. **`DeckService.ListDecks`** — Return `IsActive` in DTO. Still return all decks (active and inactive). Due count for inactive decks should still be calculated (so UI can show what would be due if reactivated).

5. **`DeckService.GetDeck`** — Return `IsActive` in detail DTO.

### Queries That Don't Change

- **Card list/search** — Browsing tools, show all cards regardless of deck activity
- **Deck detail card list** — Shows all cards in the deck regardless of activity
- **Card creation** — No change
- **`RateCard`** — No activity check. If a user started a review session before a deck was deactivated, they can finish rating cards in progress. This is intentional.

### New Endpoint

`PUT /api/decks/{id}/active` with body `{ "isActive": true/false }` — Sets `IsActive` explicitly, returns updated `DeckDto`. Uses PUT with explicit value (not a toggle) for idempotency and consistency with the existing `PUT /api/decks/{id}` pattern.

### DTOs

- `DeckDto` — Add `bool IsActive` (positional record — update all construction sites: `CreateDeck`, `UpdateDeck`, `ListDecks` in DeckService)
- `DeckDetailDto` — Add `bool IsActive` (update construction in `GetDeck`)

## MCP

- `ListDecks` response includes `isActive` per deck
- New tool: `SetDeckActive(deckId, isActive)` — explicitly set active state (more intuitive for AI agents than a toggle)

## Frontend (Web)

### Types (`types/index.ts`)

- `Deck` interface — Add `isActive: boolean`
- `DeckDetail` interface — Add `isActive: boolean`

### Decks Store (`stores/decks.ts`)

- Add `setActive(id, isActive)` action — calls `PUT /api/decks/{id}/active`

### Decks List (`DecksView.vue`)

- Inactive decks shown with reduced opacity and an "Inactive" badge
- Inactive decks sorted to the bottom of the list

### Deck Detail (`DeckDetailView.vue`)

- Toggle button in the header: "Deactivate" when active, "Activate" when inactive
- When inactive, visual indicator (e.g., banner or dimmed header)
- Hide "Study this deck" button when deck is inactive

### Study Dashboard (`StudyView.vue`)

- "Study by deck" section excludes inactive decks
- Due count and total cards (from review stats) already exclude study-inactive cards via backend

### Review Flow

- No frontend changes — backend handles filtering

## iOS

- `DeckDTO` add `isActive: Bool` field
- `DeckDetailDTO` add `isActive: Bool` field
- `CachedDeck` add `isActive: Bool` stored property (update initializer and construction sites)
- Deck list: inactive decks dimmed with badge, sorted to bottom
- Deck detail: toggle button to activate/deactivate, hide "Study" button when inactive
- Dashboard: exclude inactive decks from study section

## Edge Cases

- **Mid-session deactivation**: If a deck is deactivated while a user is mid-review, the session continues — `RateCard` does not check deck activity. The next session fetch will exclude the inactive deck's cards.
- **Removing a card from its last active deck**: The card becomes study-inactive. This is correct behavior per the activity rule but may surprise users — no special handling needed.

## Database

One migration: add `IsActive` boolean column to `Decks` table with default `true`.

## Out of Scope

- Archive state (hiding decks from list entirely)
- Deck scheduling (study on specific days)
- Forcing all cards into decks
- Per-card active/inactive flag
- `activeDecks` count in overview
- `?active=true` filter parameter on ListDecks (frontend filters client-side)
