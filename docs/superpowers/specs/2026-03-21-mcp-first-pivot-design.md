# MCP-First Pivot Design

## Overview

Pivot spaced-md from a full-stack SRS app (with web-based file upload and card creation) to an **API-first SRS backend + study frontend**. All content input happens via the API, with the MCP server as the flagship client. The web app becomes a dedicated study and review interface.

### Target User

Obsidian users who take markdown notes and use AI tools (Claude Code, Claude Desktop, Cursor) to generate flashcards from their notes. Cards flow in via MCP; users review them in the web app.

### Core Principle

The user's vault is the source of truth for content. The server never stores markdown files — it stores cards and their scheduling state. Source file/heading metadata on cards is informational only (provenance, grouping).

## Data Model Changes

### Entities to Remove

- **MarkdownFile** — `Id`, `UserId`, `FileName`, `Content`, `SizeBytes`, `UploadedAt`, `SearchVector`
- **FileHeading** — `Id`, `FileId`, `Level`, `Text`, `SortOrder`

### Card Entity Changes

Remove:
- `FileId` (Guid? FK to MarkdownFile)
- `File` navigation property
- `CardType` (string — was "file", "section", "custom"; no longer meaningful)

Add:
- `SourceFile` (string?, max 255) — original filename, e.g. `"distributed-systems.md"`
- `SourceHeading` stays but becomes a plain string with no FK relationship

The `SourceHeading` field already exists as a string. The change is that it no longer implies a relationship to a `FileHeading` entity.

### Migration Strategy

1. Add `SourceFile` column to `Cards`
2. Populate `SourceFile` from joined `MarkdownFiles.FileName` where `FileId` is not null
3. Drop `FileId` FK and column from `Cards`
4. Drop `CardType` column from `Cards`
5. Drop `FileHeadings` table
6. Drop `MarkdownFiles` table
7. Update `SearchVector` on Cards to remove any file-related indexing
8. Add index on `(UserId, SourceFile)` for the sources query

## API Changes

### Endpoints to Remove

| Method | Path | Reason |
|--------|------|--------|
| POST | `/api/files` | No file storage |
| GET | `/api/files` | No file storage |
| GET | `/api/files/{id}` | No file storage |
| DELETE | `/api/files/{id}` | No file storage |

### Endpoints to Modify

**POST /api/cards/bulk**

Current request:
```json
{
  "fileId": "guid",
  "deckId": "guid",
  "cards": [{ "front": "...", "back": "...", "sourceHeading": "..." }]
}
```

New request:
```json
{
  "deckId": "guid",
  "sourceFile": "distributed-systems.md",
  "cards": [
    { "front": "...", "back": "...", "sourceFile": "override.md", "sourceHeading": "CAP Theorem" }
  ]
}
```

- `fileId` replaced by `sourceFile` (optional string)
- Top-level `sourceFile` is a default; per-card `sourceFile` overrides it
- `sourceHeading` moves into each card item (already there)
- Duplicate detection keys on `(UserId, Front)` instead of `(UserId, FileId, Front)`

**GET /api/cards**

- Drop `fileId` query param
- Add `sourceFile` query param (optional string filter)
- Keep `deckId` filter and cursor pagination

**POST /api/cards** (single create)

- Drop `fileId` and `cardType` from `CreateCardRequest`
- Add optional `sourceFile` string

**GET /api/search?q=...**

- Remove file results from search response
- Search cards (front + back) and decks (name) only
- Drop file-related full-text search config from DbContext

### Endpoints to Add

**GET /api/sources**

Returns distinct `sourceFile` values for the authenticated user with card counts.

Response:
```json
{
  "items": [
    { "sourceFile": "distributed-systems.md", "cardCount": 12, "dueCount": 3 },
    { "sourceFile": "kubernetes.md", "cardCount": 8, "dueCount": 1 }
  ]
}
```

Uses a simple `GROUP BY` on `Cards.SourceFile WHERE SourceFile IS NOT NULL`.

### Endpoints Unchanged

- `GET /api/health`
- `/api/identity/*` (all auth endpoints)
- `POST /api/tokens`, `GET /api/tokens`, `DELETE /api/tokens/{id}`
- `GET /api/decks`, `POST /api/decks`, `PUT /api/decks/{id}`, `DELETE /api/decks/{id}`
- `GET /api/decks/{id}` (detail)
- `GET /api/cards/{id}`
- `PUT /api/cards/{id}`
- `DELETE /api/cards/{id}`
- `POST /api/reviews`
- `GET /api/reviews/stats`

## MCP Server Changes

### Tools to Remove

- `UploadFile` — no file storage
- `ListFiles` — replaced by `ListSources`
- `GetFile` — nothing to retrieve

### Tools to Modify

**CreateCards**
```
CreateCards(
  cards: [{ front, back, sourceFile?, sourceHeading? }],
  deckId?: string,
  sourceFile?: string  // default for all cards
)
```

**SearchCards** — searches cards and decks only (no file results).

**ListCards** — `fileId` filter becomes `sourceFile` string filter.

### Tools to Add

**ListSources** — calls `GET /api/sources`, returns source files with card counts.

### Final MCP Tool Set (6 tools)

1. `CreateCards` — create cards with optional source metadata
2. `SearchCards` — search cards and decks
3. `ListCards` — list/filter cards
4. `ListSources` — list source files with counts
5. `ListDecks` — list decks
6. `CreateDeck` — create a deck

## Frontend Changes

### Delete Entirely

**Views:**
- `FilesView.vue` — file list with upload
- `FileDetailView.vue` — file detail with markdown rendering

**Components:**
- `FileUpdatePreviewDialog.vue`

**Store:**
- `stores/files.ts` — all file CRUD operations

**Routes:**
- `/files` and `/files/:id`

**Store methods in other stores referencing files:**
- `decks.ts` — `addFileCards` method
- `cards.ts` — `extractContent` method and any `fileId` parameters

### Modify

**`stores/cards.ts`:**
- Remove `fileId` from fetch params
- Add `sourceFile` filter param
- Remove `CardType` references
- Update `CreateCardRequest` type

**`stores/decks.ts`:**
- Remove `addFileCards` method

**`stores/dashboard.ts`:**
- Remove file-related stats

**Card list/detail views:**
- Show `sourceFile` and `sourceHeading` as metadata strings instead of file links

**Search:**
- Remove file results from search display

**Router:**
- Remove `/files` and `/files/:id` routes

### Add

**Sources view (`/sources`):**
- Simple list: source filename, card count, due count
- Click a source to navigate to cards list filtered by `sourceFile`
- No file content display — purely metadata

**Navigation:**
- Replace "Files" nav item with "Sources"

## Backend Code to Delete

### Domain Layer
- `Domain/Entities/MarkdownFile.cs`
- `Domain/Entities/FileHeading.cs`

### Application Layer
- `Application/Services/FileComparer.cs`
- `Application/Services/HeadingExtractor.cs`
- `Application/Services/ContentExtractor.cs`
- File-related DTOs from `Application/Dtos/`

### Infrastructure Layer
- MarkdownFile + FileHeading DbSet and configuration from `AppDbContext.cs`
- File-related indexes and constraints

### API Layer
- `Api/Endpoints/FileEndpoints.cs`
- File-related registration in endpoint mapping

### MCP
- `Tools/FileTools.cs`

## Deployment

Both hosted SaaS and self-hosted remain supported. No changes to deployment model — the pivot simplifies the server (less storage, no file processing) which makes self-hosting easier.

## Out of Scope

- Obsidian plugin (future client, same API)
- File content storage or processing
- AI-powered card generation on the server (stays client-side)
- Changes to SM-2 algorithm or review flow
- Changes to auth or user management
