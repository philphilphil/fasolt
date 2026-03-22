# MCP Agent Enhancements Design

## Summary

Three new MCP tools to improve the agent experience: UpdateCard, DeleteCardsBySource, and GetOverview.

## 1. UpdateCard

Add to existing `CardTools` class.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `cardId` | Guid? | No | Identify card by ID |
| `sourceFile` | string? | No | Identify card by natural key (with `front`) |
| `front` | string? | No | Current front text for natural key lookup |
| `newFront` | string? | No | Updated front text |
| `newBack` | string? | No | Updated back text |
| `newSourceFile` | string? | No | Updated source file |
| `newSourceHeading` | string? | No | Updated source heading |

### Lookup Logic

- If `cardId` is provided, look up by ID
- Otherwise, require both `sourceFile` and `front` to find the card by natural key `(SourceFile, Front)`
- Natural key lookup is **case-insensitive**, matching existing duplicate detection behavior in `BulkCreateCards`
- Error if neither lookup path is satisfied
- At least one `new*` field must be provided

### Duplicate Detection on Update

If `newFront` or `newSourceFile` would result in a `(SourceFile, Front)` pair that already exists on a different card, return `{ error: "A card with this front text already exists for this source" }`. This prevents silently creating colliding natural keys.

### Returns

- Success: updated `CardDto`
- Not found: `{ error: "Card not found" }`
- Invalid params: `{ error: "Provide cardId or both sourceFile and front" }`
- Duplicate: `{ error: "A card with this front text already exists for this source" }`

### Service Changes

Add new method `CardService.UpdateCardFields` that:
- Takes a card (looked up by ID or natural key) and optional new field values
- Checks for natural key collisions when front or sourceFile changes
- Preserves all SRS state (easeFactor, interval, repetitions, dueAt, state, lastReviewedAt)
- Separate from existing `UpdateCard(userId, cardId, front, back)` to avoid parameter name confusion

## 2. DeleteCardsBySource

Add to existing `CardTools` class.

### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `sourceFile` | string | Yes | Exact source file name to match |

### Behavior

- Deletes all cards for the authenticated user where `SourceFile` matches exactly
- Case-sensitive match (consistent with how cards are stored)
- Uses `ExecuteDeleteAsync` for efficiency (safe because database FK cascade on `DeckCard.CardId` handles join table cleanup)

### Returns

`{ deleted: true, deletedCount: int }` or `{ deleted: false, deletedCount: 0 }` if no cards matched.

### Service Changes

New method: `CardService.DeleteCardsBySource(string userId, string sourceFile)` returning the count of deleted rows.

## 3. GetOverview

New `OverviewTools` class in `Api/McpTools/`.

### Parameters

None.

### Returns

```json
{
  "totalCards": 42,
  "dueCards": 7,
  "cardsByState": { "new": 10, "learning": 5, "review": 27 },
  "totalDecks": 3,
  "totalSources": 8
}
```

### Service

New `OverviewService` in `Application/Services/` with a single method `GetOverview(string userId)`.

### Queries

- `totalCards`: count of all user's cards
- `dueCards`: count where `DueAt <= now` or `DueAt == null` (matches existing due logic)
- `cardsByState`: group by `State`, count each. Valid states: `"new"`, `"learning"`, `"review"`. Always include all three keys, defaulting to 0
- `totalDecks`: count of user's decks
- `totalSources`: count distinct non-null `SourceFile` values

### DTO

New `OverviewDto` record in `Application/Dtos/`.

## Files to Create/Modify

### New Files
- `fasolt.Server/Api/McpTools/OverviewTools.cs`
- `fasolt.Server/Application/Services/OverviewService.cs`
- `fasolt.Server/Application/Dtos/OverviewDto.cs`

### Modified Files
- `fasolt.Server/Api/McpTools/CardTools.cs` — add UpdateCard, DeleteCardsBySource tools
- `fasolt.Server/Application/Services/CardService.cs` — extend UpdateCard, add DeleteCardsBySource
- `fasolt.Server/Program.cs` — register OverviewService in DI
- `CLAUDE.md` — update MCP tools list with new tools
