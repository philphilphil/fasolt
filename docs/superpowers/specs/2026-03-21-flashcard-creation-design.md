# Epic 3: Flashcard Creation — Design Spec

## Scope

Implements US-3.1 (Create from file), US-3.2 (Create from section), US-3.3 (Edit cards), US-3.4 (Delete cards), US-3.5 (Custom cards), US-3.8 (Card preview). Priority levels P0–P1.

US-3.6 (Cloze deletion), US-3.7 (Reversed cards), US-3.9 (Quick card from selection) are deferred to a separate P2 requirements file.

## Decisions

- **Content storage**: Store both a markdown snapshot (in `Back`) and a pointer to the source (fileId + heading). Snapshot ensures study stability; pointer enables future "refresh from source" functionality.
- **Card front**: Auto-generated from heading text or filename, but editable in a creation dialog before saving.
- **Long content warning**: Non-blocking warning when file content exceeds 2000 characters. User can still create the card.
- **Soft delete**: Cards use `DeletedAt` for soft delete to preserve review history for stats.
- **Card preview**: Live markdown preview built into the create/edit dialogs. No separate preview view.

## Database Schema

### Cards

| Column | Type | Notes |
|--------|------|-------|
| Id | Guid | PK |
| UserId | string | FK → AspNetUsers, required |
| FileId | Guid? | Nullable FK → MarkdownFiles, SetNull on delete |
| SourceHeading | string? | Heading text used to extract content (pointer for refresh) |
| Front | text | Question side (markdown) |
| Back | text | Answer side (markdown snapshot) |
| CardType | string | `file`, `section`, `custom` |
| CreatedAt | DateTimeOffset | |
| DeletedAt | DateTimeOffset? | Null = active, set = soft-deleted |

Constraints:
- Index on `UserId` for list queries
- Index on `FileId` for card count lookups
- FK to MarkdownFiles with `SetNull` on delete (card survives file deletion, loses link)

## Backend API

All endpoints under `/api/cards`, all require authorization. Scoped to the authenticated user. All queries filter `WHERE DeletedAt IS NULL` unless stated otherwise.

### POST /api/cards

Create a card.

- Input: `{ fileId?, sourceHeading?, front, back, cardType }`
- Validation: `front` and `back` required, `cardType` must be `file`, `section`, or `custom`
- If `cardType` is `file` or `section`, `fileId` is required and must belong to the user
- Response: 201 Created with card data

### GET /api/cards

List user's active (non-deleted) cards.

- Query params: `?fileId=` to filter by source file
- Response: array of `{ id, fileId, sourceHeading, front, back, cardType, createdAt }`

### GET /api/cards/{id}

Get card detail.

- 404 if not found, not owned, or soft-deleted

### PUT /api/cards/{id}

Edit card front and back.

- Input: `{ front, back }`
- Does not change cardType, fileId, sourceHeading, or createdAt
- Does not reset review schedule (no review fields exist yet — Epic 4)
- 404 if not found, not owned, or soft-deleted

### DELETE /api/cards/{id}

Soft delete a card.

- Sets `DeletedAt` to `DateTimeOffset.UtcNow`
- 204 No Content on success
- 404 if not found, not owned, or already deleted

### GET /api/cards/extract

Extract content from a file for card creation preview.

- Query params: `fileId` (required), `heading` (optional)
- If no heading: returns full file content with frontmatter stripped
- If heading provided: returns markdown slice from that heading to the next same-or-higher-level heading
- Response: `{ front, back }` — front is the heading text (or filename), back is the extracted markdown
- 404 if file not found or not owned

### Update to GET /api/files

Update the existing file list endpoint to return real `cardCount` instead of hardcoded 0. Use a `COUNT` subquery on non-deleted cards per fileId.

## Content Extraction Service

`ContentExtractor` in `Application/Services/`:

### StripFrontmatter(string markdown) → string

If content starts with `---\n`, find closing `---\n`, return everything after. Same logic as the frontend `useMarkdown.stripFrontmatter`.

### ExtractSection(string markdown, string heading) → string?

1. Scan lines (tracking code fences like `HeadingExtractor`)
2. Find the line matching the target heading
3. Determine its level (number of `#` characters)
4. Collect all lines from there until hitting a heading of same or higher level, or EOF
5. Return the collected lines joined, or null if heading not found

## Frontend

### New Components

**CardCreateDialog.vue** — Modal dialog for creating cards:
- Props: `fileId?`, `sourceHeading?`, `initialFront`, `initialBack`, `cardType`
- Editable `front` and `back` fields (textareas)
- Live markdown preview of both fields using `useMarkdown`
- Warning banner when `back` content exceeds 2000 characters: "This card is quite long. Consider creating cards from specific sections instead."
- Save and Cancel buttons
- Emits `created` event with the new card

**CardEditDialog.vue** — Modal dialog for editing cards:
- Same layout as create dialog, pre-filled with existing card content
- Props: card data
- Save and Cancel buttons
- Emits `updated` event

### New View

**CardsView.vue** (route: `/cards`):
- Table listing all user's cards: front preview (truncated), source file name, card type badge, created date
- Edit button → opens `CardEditDialog`
- Delete button → confirmation dialog, then soft delete
- "New card" button → opens `CardCreateDialog` with `cardType: 'custom'`, empty fields
- Filter by source file (dropdown)

### Modified Views

**FileDetailView.vue**:
- Wire up "Create cards" buttons in heading sidebar — each opens `CardCreateDialog` with extracted section content (calls `GET /api/cards/extract?fileId=&heading=`)
- Add "Create card from file" button in the header

**FilesView.vue**:
- Add "Create card" button per file row (in expanded heading area or as a row action)
- Opens `CardCreateDialog` with `cardType: 'file'`

### New Store

**cards.ts** (Pinia):
- `cards` ref, `loading` ref
- `fetchCards(fileId?)` — GET /api/cards with optional filter
- `createCard(data)` — POST /api/cards
- `updateCard(id, data)` — PUT /api/cards/{id}
- `deleteCard(id)` — DELETE /api/cards/{id}
- `extractContent(fileId, heading?)` — GET /api/cards/extract

### Router

- Add route: `/cards` → `CardsView.vue` (protected)
- Add "Cards" tab to navigation between "Files" and "Groups"

### Types

Add to `types/index.ts`:

```typescript
export interface Card {
  id: string
  fileId: string | null
  sourceHeading: string | null
  front: string
  back: string
  cardType: 'file' | 'section' | 'custom'
  createdAt: string
}

export interface ExtractedContent {
  front: string
  back: string
}
```

Replace the existing `Card` interface (which has SM-2 review fields) — those fields will be added in Epic 4.
