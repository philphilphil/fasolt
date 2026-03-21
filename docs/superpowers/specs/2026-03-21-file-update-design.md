# File Update (Re-upload) — Design Spec

## Scope

When a user uploads a new version of an existing `.md` file, show a preview of how cards will be affected, then apply the update on confirmation.

## Decisions

- **Detection**: Both auto-detect on upload (duplicate filename triggers preview) and explicit "Update file" button on file detail page.
- **Card updates**: Automatic — confirming the upload auto-updates all affected card backs with new content.
- **Orphaned cards** (heading removed): User chooses per card to delete or keep (unlinked). Shown in preview dialog.
- **New sections**: Informational only in the preview. User creates cards later via the normal flow.
- **Heading matching**: Exact text match. Renamed headings show as one orphan + one new section.

## Backend

### FileComparer Service

`FileComparer` in `Application/Services/`:

**`Compare(string oldContent, string newContent, List<Card> existingCards, string fileName)`** returns:

```
FileComparisonResult {
  UpdatedCards: List<{ CardId, Front, OldBack, NewBack }>
  OrphanedCards: List<{ CardId, Front, SourceHeading }>
  UnchangedCardIds: List<Guid>
  NewSections: List<{ Heading, HasMarkers }>
}
```

Logic:
1. For each existing card linked to this file:
   - If `CardType == "file"`: extract full stripped content from new file, compare to card's `Back`. Also re-derive front from first H1 or filename.
   - If `CardType == "section"`: find section by `SourceHeading` in new content using `ContentExtractor.ExtractSection`. Strip `?::` markers via `ContentExtractor.ParseMarkers`. Compare cleaned content to card's `Back`.
   - If heading not found in new content → orphaned card.
   - If content identical → unchanged.
   - If content differs → updated card (include old and new back).
2. Collect all headings from new content (via `HeadingExtractor.Extract`). Any heading not referenced by an existing card → new section. Check if section has `?::` markers via `ParseMarkers`.

### API Endpoints

#### POST /api/files/preview-update

Preview what would change without saving.

- Input: `multipart/form-data` with single `.md` file
- Finds existing file by `fileName` + `userId`
- Runs `FileComparer.Compare` with old content, new content, and existing non-deleted cards for this file
- Response:
  ```json
  {
    "fileId": "guid",
    "fileName": "string",
    "updatedCards": [{ "cardId": "guid", "front": "string", "oldBack": "string", "newBack": "string" }],
    "orphanedCards": [{ "cardId": "guid", "front": "string", "sourceHeading": "string" }],
    "unchangedCount": 0,
    "newSections": [{ "heading": "string", "hasMarkers": true }]
  }
  ```
- 404 if no existing file with that filename for this user (caller falls through to normal upload)
- Validation: `.md` extension, 1MB max (same as upload)

#### POST /api/files/{id}/update

Confirm and apply the update.

- Input: `multipart/form-data` with the `.md` file + form field `deleteCardIds` (repeated form field, e.g. `deleteCardIds=guid1&deleteCardIds=guid2`)
- Validation:
  - File must exist and belong to user, `.md` extension, 1MB max
  - All `deleteCardIds` must belong to the current user AND be linked to this file. Reject with 400 if any ID fails validation.
- Actions (single `SaveChangesAsync`):
  1. Replace `MarkdownFile.Content` and `SizeBytes`, update `UploadedAt` to now
  2. Delete all `FileHeading` rows for this file, insert new ones from `HeadingExtractor.Extract`
  3. For each card linked to this file:
     - `CardType == "file"`: update `Back` with new stripped content. Only update `Front` if it still matches the old derived value (first H1 or filename) — preserve manually edited fronts.
     - `CardType == "section"`: find section by `SourceHeading`, strip markers, update `Back`. If heading not found, handled by orphan logic below.
  4. For cards in `deleteCardIds`: set `DeletedAt` (soft delete)
  5. For orphaned cards NOT in `deleteCardIds`: set `FileId = null` (unlink, keep snapshot)
- Response: `{ updatedCount, deletedCount, orphanedCount }`

### Bulk Upload

Bulk upload (`POST /api/files/bulk`) does not support the update flow. Duplicate filenames in bulk uploads continue to be rejected with an error per file. The update flow is single-file only, accessed via the preview-update endpoint or the "Update file" button.

## Frontend

### Upload Flow Change

In `FilesView`, change the upload logic:
1. On file select/drop, call `POST /api/files/preview-update` first
2. If 404 → no existing file, proceed with normal `POST /api/files` upload
3. If 200 → show `FileUpdatePreviewDialog` with the preview data
4. On confirm → call `POST /api/files/{id}/update`
5. On cancel → do nothing

### FileUpdatePreviewDialog.vue

New dialog component showing:

- **Header**: "Updating {fileName}"
- **Updated cards** section: list showing card front text with "content changed" indicator. Auto-updates on confirm, no per-card opt-out.
- **Orphaned cards** section: list showing card front + old heading. Each has toggle: "Keep (unlink from file)" / "Delete". Default: keep.
- **Unchanged**: count only ("N cards unchanged")
- **New sections**: list of heading names, informational ("N new sections available for card creation")
- **Confirm / Cancel** buttons

### FileDetailView Update Button

Add "Update file" button in the header (next to "Create card" and "Source" buttons). Opens a file picker. Selected file goes through the same preview-update flow.

### Types

Add to `types/index.ts`:

```typescript
export interface FileUpdatePreview {
  fileId: string
  fileName: string
  updatedCards: { cardId: string; front: string; oldBack: string; newBack: string }[]
  orphanedCards: { cardId: string; front: string; sourceHeading: string }[]
  unchangedCount: number
  newSections: { heading: string; hasMarkers: boolean }[]
}
```

### Store

Add to files store:
- `previewUpdate(file: File): Promise<FileUpdatePreview>` — calls preview-update endpoint
- `confirmUpdate(fileId: string, file: File, deleteCardIds: string[]): Promise<void>` — calls update endpoint
