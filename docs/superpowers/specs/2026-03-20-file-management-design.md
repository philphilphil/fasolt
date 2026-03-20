# Epic 2: Markdown File Management — Design Spec

## Scope

Implements US-2.1 (Upload), US-2.2 (File List), US-2.3 (Markdown Preview), US-2.4 (Delete), US-2.5 (Browse by Headings), and US-2.6 (Bulk Upload). Priority levels P0–P2.

US-2.7 (Obsidian Vault Import, P3) is deferred.

## Decisions

- **Frontmatter handling**: Store full original file content in DB. Strip YAML frontmatter at display time only (client-side). Preserves data for future Obsidian tag import (US-2.7).
- **Heading extraction**: Extract on upload, store in dedicated table. Avoids re-parsing on every file list/detail fetch.
- **Markdown rendering**: markdown-it on the frontend. Headings extracted server-side via regex.
- **"Create card from section" button**: Rendered as disabled placeholder until Epic 3 (flashcard creation) is implemented.

## Database Schema

### MarkdownFiles

| Column | Type | Notes |
|--------|------|-------|
| Id | Guid | PK |
| UserId | string | FK → AspNetUsers, required |
| FileName | string(255) | Required |
| Content | text | Full original markdown including frontmatter |
| SizeBytes | long | |
| UploadedAt | DateTimeOffset | |

Constraints:
- Unique index on `(UserId, FileName)` — no duplicate filenames per user
- Index on `UserId` for fast list queries

### FileHeadings

| Column | Type | Notes |
|--------|------|-------|
| Id | Guid | PK |
| FileId | Guid | FK → MarkdownFiles, cascade delete |
| Level | int | 1–6 |
| Text | string | Heading text |
| SortOrder | int | Order of appearance in file |

## Backend API

All endpoints under `/api/files`, all require authorization. Scoped to the authenticated user.

### POST /api/files

Upload a single `.md` file.

- Input: `multipart/form-data` with single `IFormFile`
- Validation: `.md` extension only, max 1MB, reject duplicate filename for same user
- On success: insert `MarkdownFile` + extracted `FileHeading` rows in one transaction
- Response: 201 Created with file metadata (id, fileName, sizeBytes, uploadedAt, headings)

### POST /api/files/bulk

Upload multiple `.md` files.

- Input: `multipart/form-data` with `IFormFileCollection`
- Same per-file validation as single upload
- Response: 200 with array of per-file results (success with metadata, or failure with reason)

### GET /api/files

List all files for the authenticated user.

- Response: array of `{ id, fileName, sizeBytes, uploadedAt, cardCount (0 for now), headings: [{ level, text }] }`

### GET /api/files/{id}

Get file detail with content.

- Response: file metadata + `content` field (full content, frontmatter stripped server-side... actually no — frontmatter stripped client-side to keep API response lossless)
- Returns full content; client strips frontmatter before rendering
- 404 if file doesn't exist or belongs to another user

### DELETE /api/files/{id}

Delete a file.

- Cascade deletes associated headings via FK constraint
- "Keep or delete associated cards" — UI-only for now, no cards exist yet
- 204 No Content on success, 404 if not found/not owned

## Heading Extraction

`HeadingExtractor` in `Application/Services/`:
- Input: markdown string
- Output: `List<FileHeadingDto>` with level, text, sortOrder
- Pattern: `^#{1,6}\s+(.+)$` per line
- Skips headings inside fenced code blocks (tracks ``` state)
- Runs synchronously, called during upload transaction

## Frontend

### Dependencies

- `markdown-it` — npm install for markdown rendering

### Files Store (`files.ts`)

Replace mock data with API-backed methods:
- `fetchFiles()` — GET /api/files, populates reactive file list
- `uploadFile(file: File)` — POST /api/files with FormData
- `uploadFiles(files: File[])` — POST /api/files/bulk with FormData
- `deleteFile(id: string)` — DELETE /api/files/{id}
- `getFileContent(id: string)` — GET /api/files/{id}

### API Upload Helper

Add `apiUpload` function alongside `apiFetch` in `api/client.ts`:
- Uses `FormData`, does not set `Content-Type` (browser sets multipart boundary)
- Same error handling pattern as `apiFetch`

### FilesView.vue Updates

- **Upload zone**: Wire drag-and-drop handler + hidden file input. Accept `.md` only, validate 1MB client-side.
- **Multi-file support**: `multiple` attribute on file input, drop handler accepts multiple files.
- **Upload feedback**: Confirmation toast with filename on success. Error messages for wrong type, too large, duplicate name. Progress indicator for batch (X of Y).
- **Sort controls**: Client-side sorting by name, date, card count.
- **Delete**: Button per file row, confirmation via shadcn AlertDialog. "Keep or delete cards" option in dialog (UI-only for now).
- **Empty state**: Already exists, keep as-is.

### New: FileDetailView.vue (route: /files/:id)

- Fetches file content from GET /api/files/{id}
- Strips YAML frontmatter client-side before rendering (regex: starts with `---\n`, find closing `---\n`, slice)
- Renders markdown with markdown-it
  - Images: override image rule to show alt-text placeholder
- Toggle between rendered preview and source view
- Heading tree panel (sidebar or collapsible):
  - Auto-generated from FileHeadings data
  - Nested indentation by heading level
  - Click heading to scroll to corresponding section in preview
  - Disabled "Create card from section" button on each heading (placeholder for Epic 3)

### Router

Add route: `/files/:id` → `FileDetailView.vue` (protected)

### Composable: useMarkdown

Small composable that initializes and returns a shared markdown-it instance with:
- Image rule overridden to render alt-text placeholder
- Standard features: headings, bold/italic, lists, code blocks, links, blockquotes
