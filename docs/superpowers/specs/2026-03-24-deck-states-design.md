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

1. **`ReviewEndpoints.GetDueCards`** — Add study-active filter to the due cards query. Cards only in inactive decks are excluded. When filtering by a specific deckId, also check that deck is active (return empty if inactive).

2. **`ReviewEndpoints.GetStats`** — Due count and total count exclude study-inactive cards.

3. **`OverviewService.GetOverview`** — Due count, total cards, and cards-by-state counts exclude study-inactive cards. Deck count still includes all decks.

4. **`DeckService.ListDecks`** — Return `IsActive` in DTO. Still return all decks (active and inactive). Due count for inactive decks should still be calculated (so UI can show what would be due if reactivated).

5. **`DeckService.GetDeck`** — Return `IsActive` in detail DTO.

### Queries That Don't Change

- **Card list/search** — Browsing tools, show all cards regardless of deck activity
- **Deck detail card list** — Shows all cards in the deck regardless of activity
- **Card creation** — No change

### New Endpoint

`POST /api/decks/{id}/toggle-active` — Flips `IsActive`, returns updated `DeckDto`.

### DTOs

- `DeckDto` — Add `bool IsActive`
- `DeckDetailDto` — Add `bool IsActive`

## MCP

- `ListDecks` response includes `isActive` per deck
- New tool: `SetDeckActive(deckId, isActive)` — explicitly set active state (more intuitive for AI agents than a toggle)

## Frontend (Web)

### Decks List (`DecksView.vue`)

- Inactive decks shown with reduced opacity and an "Inactive" badge
- Inactive decks sorted to the bottom of the list

### Deck Detail (`DeckDetailView.vue`)

- Toggle button in the header: "Deactivate" when active, "Activate" when inactive
- When inactive, visual indicator (e.g., banner or dimmed header)

### Study Dashboard (`StudyView.vue`)

- "Study by deck" section excludes inactive decks
- Due count and total cards (from review stats) already exclude study-inactive cards via backend

### Review Flow

- No frontend changes — backend handles filtering

## iOS

- `DeckDTO` and `CachedDeck` add `isActive: Bool` field
- Deck list: inactive decks dimmed with badge, sorted to bottom
- Deck detail: toggle button to activate/deactivate
- Dashboard: exclude inactive decks from study section

## Database

One migration: add `IsActive` boolean column to `Decks` table with default `true`.

## Out of Scope

- Archive state (hiding decks from list entirely)
- Deck scheduling (study on specific days)
- Forcing all cards into decks
- Per-card active/inactive flag
