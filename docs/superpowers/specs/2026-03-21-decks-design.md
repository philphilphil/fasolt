# Epic 5: Decks & Organization — Design Spec

## Scope

US-5.1 (Create decks), US-5.2 (Assign cards), US-5.3 (View deck contents), US-5.4 (Edit/delete decks), US-5.5 (Study by deck), US-5.6 (Dashboard deck table). P1 only. US-5.7 (Tags) deferred.

## Decisions

- **Deck** replaces **Group** as the organizational concept
- **Many-to-many**: Cards can belong to multiple decks via a `DeckCards` join table
- **Simple join table**: No metadata on the relationship (no AddedAt, no ordering)
- **SM-2 is per-card, not per-deck**: Studying a card in any deck updates the same card's schedule
- **Study by deck**: Filter due cards by deck membership

## Database Schema

### Decks

| Column | Type | Notes |
|--------|------|-------|
| Id | Guid | PK |
| UserId | string | FK → AspNetUsers, required |
| Name | string(100) | Required |
| Description | string? | Optional |
| CreatedAt | DateTimeOffset | |

Index on `UserId`.

### DeckCards (join table)

| Column | Type | Notes |
|--------|------|-------|
| DeckId | Guid | FK → Decks, cascade delete |
| CardId | Guid | FK → Cards, cascade delete |

Composite PK on `(DeckId, CardId)`. Index on `CardId`.

## Backend API

All under `/api/decks`, require authorization.

### POST /api/decks

Create a deck.
- Input: `{ name, description? }`
- Response: 201 with deck data

### GET /api/decks

List user's decks with counts.
- Response: `[{ id, name, description, cardCount, dueCount, nextReview, createdAt }]`
- `dueCount`: cards in deck where DueAt is null or <= now
- `nextReview`: earliest DueAt among due cards, formatted as relative time string ("now", "2h", "tomorrow") — or computed client-side

### GET /api/decks/{id}

Deck detail with card list.
- Response: `{ id, name, description, cards: [{ id, front, cardType, state, dueAt }], cardCount, dueCount }`
- Cards sorted by dueAt ASC
- 404 if not found or not owned

### PUT /api/decks/{id}

Update deck name/description.
- Input: `{ name, description? }`

### DELETE /api/decks/{id}

Delete deck. Cards are NOT deleted (just unlinked via cascade on join table).
- 204 No Content

### POST /api/decks/{id}/cards

Add cards to deck.
- Input: `{ cardIds: [guid] }`
- Validates all cards belong to user
- Skips cards already in deck (idempotent)

### DELETE /api/decks/{id}/cards/{cardId}

Remove a card from a deck.
- 204 No Content

### POST /api/decks/{id}/add-file

Add all cards from a file to this deck.
- Input: `{ fileId: guid }`
- Validates file belongs to user
- Adds all non-deleted cards linked to that file

### Update: GET /api/review/due

Add optional `?deckId=` query parameter. When provided, only return due cards that are in the specified deck.

## Frontend

### Types

Replace placeholder `Deck` interface:
```typescript
export interface Deck {
  id: string
  name: string
  description: string | null
  cardCount: number
  dueCount: number
  createdAt: string
}

export interface DeckDetail extends Deck {
  cards: DeckCard[]
}

export interface DeckCard {
  id: string
  front: string
  cardType: string
  state: string
  dueAt: string | null
}
```

### Decks Store (replaces groups store)

- `fetchDecks()` — GET /api/decks
- `createDeck(name, description?)` — POST /api/decks
- `updateDeck(id, name, description?)` — PUT /api/decks/{id}
- `deleteDeck(id)` — DELETE /api/decks/{id}
- `getDeckDetail(id)` — GET /api/decks/{id}
- `addCards(deckId, cardIds)` — POST /api/decks/{id}/cards
- `removeCard(deckId, cardId)` — DELETE /api/decks/{id}/cards/{cardId}
- `addFileCards(deckId, fileId)` — POST /api/decks/{id}/add-file

### DecksView.vue (replaces GroupsView)

Route: `/decks` (update from `/groups`)
- List of decks with name, card count, due count
- Create deck button → dialog with name + description
- Click deck → navigate to `/decks/:id`

### DeckDetailView.vue (new, route `/decks/:id`)

- Deck name, description, card count, due count
- "Study this deck" button → navigates to `/review?deckId=X`
- Card list with front preview, type badge, state, due date
- "Add cards from file" action (dropdown of files)
- Remove card button per row
- Edit/delete deck buttons

### Dashboard Deck Table (real data)

- Replace mock `DeckTable` data with real decks from API
- Click deck row → navigate to `/decks/:id`
- Show due count, card count per deck

### Review by Deck

- Update review store `startSession(deckId?)` to pass deckId to API
- ReviewView reads `deckId` from route query param
- Session scoped to deck's due cards

### Navigation

- Rename "Groups" tab to "Decks" in AppLayout and BottomNav
- Update route from `/groups` to `/decks`
