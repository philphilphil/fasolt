# Deck Snapshots Design Spec

## Problem

MCP tools allow AI agents to modify and delete cards and decks. A misunderstood instruction can wipe out study data. Users need a way to create backups and selectively restore from them.

## Overview

A "Snapshot All" action creates individual per-deck snapshots in one operation. Each snapshot captures the full state of a deck (metadata + all cards with content and FSRS state) as a JSON blob. Snapshots can be created from the web, iOS app, and MCP. Restore is web-only and presents a diff dialog where the user selects exactly what to restore.

## Data Model

### DeckSnapshot Entity

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| PublicId | string | 12-char unique identifier |
| DeckId | Guid | FK → Deck (no cascade delete) |
| UserId | string | FK → AppUser (stored directly for orphan survival) |
| Version | int | Schema version for backwards compatibility, starts at 1 |
| CardCount | int | Denormalized for list display |
| Data | jsonb | Full deck state |
| CreatedAt | DateTimeOffset | |

No cascade delete from Deck — snapshots survive deck deletion so they can still be browsed. Cleanup is via retention policy only.

`UserId` is stored on the snapshot directly (not just via Deck FK) so snapshots remain accessible even if the deck is deleted.

### Data JSON Schema (Version 1)

```json
{
  "deckName": "Japanese Vocab",
  "deckDescription": "Core vocabulary deck",
  "cards": [
    {
      "cardId": "guid",
      "publicId": "12-char",
      "front": "What is the te-form of 食べる?",
      "back": "食べて",
      "frontSvg": null,
      "backSvg": null,
      "sourceFile": "japanese/verbs.md",
      "sourceHeading": "Te-form",
      "createdAt": "2026-03-18T10:00:00Z",
      "stability": 12.4,
      "difficulty": 0.3,
      "step": 0,
      "dueAt": "2026-03-25T00:00:00Z",
      "state": "Review",
      "lastReviewedAt": "2026-03-20T00:00:00Z"
    }
  ]
}
```

### Versioning Strategy

Each snapshot stores a `Version` integer. When deserializing during restore, the version determines the expected shape. If the Card model changes over time:
- New fields added: old snapshots won't have them — use sensible defaults during deserialization.
- Fields renamed/removed: version tells which shape to expect, apply transformation.

## Retention Policy

Keep the last 10 snapshots per deck. After creating new snapshots, delete the oldest ones exceeding the limit. This runs inline during the create flow — no background job.

Empty decks (0 cards) are skipped during snapshot creation — no value in snapshotting nothing.

## API Endpoints

### REST

| Method | Route | Description |
|--------|-------|-------------|
| POST | `/api/snapshots` | Create snapshots for all decks. Returns count created. |
| GET | `/api/decks/{id}/snapshots` | List snapshots for a deck (id = PublicId). Returns PublicId, CardCount, CreatedAt. Newest first. |
| GET | `/api/snapshots/{id}` | Get snapshot detail by PublicId (full data). |
| GET | `/api/snapshots/{id}/diff` | Compute diff between snapshot and current deck state (id = PublicId). Returns three buckets. |
| POST | `/api/snapshots/{id}/restore` | Execute selective restore (id = PublicId). |

All `{id}` parameters are PublicIds, consistent with the rest of the API.

### Diff Response Shape

```json
{
  "deleted": [
    {
      "cardId": "guid",
      "front": "...",
      "back": "...",
      "sourceFile": "...",
      "stability": 12.4,
      "dueAt": "2026-03-25T00:00:00Z"
    }
  ],
  "modified": [
    {
      "cardId": "guid",
      "front": "...",
      "currentFront": "...",
      "back": "...",
      "currentBack": "...",
      "snapshotStability": 5.2,
      "currentStability": 14.8,
      "hasContentChanges": true,
      "hasFsrsChanges": true
    }
  ],
  "added": [
    {
      "cardId": "guid",
      "front": "...",
      "back": "..."
    }
  ]
}
```

### Restore Request Shape

```json
{
  "restoreDeletedCardIds": ["guid1", "guid2"],
  "revertModifiedCardIds": ["guid3"]
}
```

These IDs are the `cardId` (Guid) values from the diff response — not PublicIds. The backend validates that all provided IDs exist in the snapshot before executing.

### Restore Backend Logic

**For deleted cards (card in snapshot, not in current deck):**
1. Check if the card still exists in the system (just removed from deck, not truly deleted).
2. If exists: update card to snapshot state, re-add to deck.
3. If truly deleted: create new card with snapshot content and FSRS state (new Id and PublicId), add to deck.

**Note on re-restored cards:** When a truly deleted card is restored, it gets a new Id/PublicId. If a user later restores from an older snapshot that references the original cardId, that card will appear as "deleted" again (since the original Id no longer exists). This is expected — the user can restore it again, creating another new card. This is simpler than trying to track lineage across restores.

**For modified cards (card in both, user chose to revert):**
1. Update existing card's front, back, frontSvg, backSvg, sourceFile, sourceHeading, createdAt, and FSRS fields to snapshotted values.

### MCP Tools

| Tool | Parameters | Description |
|------|-----------|-------------|
| `CreateSnapshot` | none | Snapshot all decks. Returns count created. |
| `ListSnapshots` | `deckId?` (optional) | List snapshots. Without deckId, lists the 50 most recent across all decks. Returns PublicId, DeckName, CardCount, CreatedAt. |

No restore via MCP — web only.

## Frontend (Web)

### Navigation

Add a "Snapshots" button in the DeckDetailView header, alongside Edit/Delete. Navigates to `/decks/{id}/snapshots`.

### Snapshot Creation

"Create Snapshot" button on the dashboard/overview page. This is a global action (snapshots all decks). Shows success toast with count.

### Snapshots List Page (`/decks/{id}/snapshots`)

- Deck name shown as context/breadcrumb
- List of snapshots, newest first
- Each row: date/time, card count, "Restore" button
- Empty state when no snapshots exist

### Restore Dialog

Triggered by clicking "Restore" on a snapshot. Fetches `/api/snapshots/{id}/diff`.

Three sections:
1. **Deleted since snapshot** — cards in snapshot but not in current deck. Red badge. Each card shows front text, source, FSRS summary. Checkbox per card, **checked by default**.
2. **Modified since snapshot** — cards with content or FSRS changes. Amber badge. Shows before/after diff for changed fields. Checkbox per card, **unchecked by default**.
3. **Added since snapshot** — cards in current deck but not in snapshot. Green badge. Info-only, no checkboxes. Note that these are unaffected.

Footer shows selected count and "Cancel" / "Restore Selected" buttons. On success, navigate back to deck detail with success toast.

## iOS App

### Settings Screen

Add a "Snapshots" section:
- **"Create Snapshot" button** — taps to snapshot all decks via `POST /api/snapshots`. Shows success alert with count.
- **Snapshot history list** — recent snapshots grouped by deck. Each entry shows: deck name, date/time, card count.
- **Restore note** — text at bottom of section: "To restore a snapshot, visit the web app."

No changes to iOS deck detail views.

## Testing

### Backend Unit Tests
- Snapshot creation for all decks
- Retention: 11th snapshot triggers deletion of oldest
- Diff computation: deleted, modified, added buckets
- Restore: deleted card (still exists vs truly deleted), modified card revert
- Version deserialization with missing/extra fields

### Playwright E2E
- Create snapshot from dashboard, verify appears in deck's snapshot list
- Restore flow: delete a card → snapshot exists from before → navigate to snapshots → click restore → verify diff dialog shows deleted card → select and restore → verify card is back in deck
- Empty state when no snapshots exist

### MCP
- `CreateSnapshot` tool creates snapshots, returns count
- `ListSnapshots` with and without deckId filter

### iOS
- Manual testing: create snapshot from settings, verify list updates, verify restore note displays
