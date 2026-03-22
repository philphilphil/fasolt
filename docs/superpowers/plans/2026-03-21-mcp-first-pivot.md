# MCP-First Pivot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pivot fasolt from a full-stack SRS app to an API-first SRS backend + study frontend, removing all file storage and making MCP/API the primary content input path.

**Architecture:** Remove MarkdownFile/FileHeading entities entirely. Card gains a `SourceFile` string field for provenance. File processing services (FileComparer, HeadingExtractor, ContentExtractor) are deleted. A new `/api/sources` endpoint provides card grouping by source file. The MCP server drops file tools, gains ListSources.

**Tech Stack:** .NET 10, EF Core + Postgres, Vue 3 + TypeScript, MCP (.NET)

**Spec:** `docs/superpowers/specs/2026-03-21-mcp-first-pivot-design.md`

---

## File Map

### Files to Delete
- `fasolt.Server/Domain/Entities/MarkdownFile.cs`
- `fasolt.Server/Domain/Entities/FileHeading.cs`
- `fasolt.Server/Application/Services/FileComparer.cs`
- `fasolt.Server/Application/Services/HeadingExtractor.cs`
- `fasolt.Server/Application/Services/ContentExtractor.cs`
- `fasolt.Server/Application/Dtos/FileDtos.cs`
- `fasolt.Server/Api/Endpoints/FileEndpoints.cs`
- `fasolt.Mcp/Tools/FileTools.cs`
- `fasolt.client/src/views/FilesView.vue`
- `fasolt.client/src/views/FileDetailView.vue`
- `fasolt.client/src/components/FileUpdatePreviewDialog.vue`
- `fasolt.client/src/stores/files.ts`

### Files to Modify
- `fasolt.Server/Domain/Entities/Card.cs`
- `fasolt.Server/Infrastructure/Data/AppDbContext.cs`
- `fasolt.Server/Application/Dtos/CardDtos.cs`
- `fasolt.Server/Application/Dtos/BulkCardDtos.cs`
- `fasolt.Server/Application/Dtos/ReviewDtos.cs`
- `fasolt.Server/Application/Dtos/DeckDtos.cs`
- `fasolt.Server/Api/Endpoints/CardEndpoints.cs`
- `fasolt.Server/Api/Endpoints/DeckEndpoints.cs`
- `fasolt.Server/Api/Endpoints/SearchEndpoints.cs`
- `fasolt.Server/Program.cs`
- `fasolt.Mcp/Tools/CardTools.cs`
- `fasolt.Mcp/ApiClient.cs`
- `fasolt.client/src/types/index.ts`
- `fasolt.client/src/router/index.ts`
- `fasolt.client/src/stores/cards.ts`
- `fasolt.client/src/stores/decks.ts`
- `fasolt.client/src/api/client.ts`
- `fasolt.client/src/composables/useSearch.ts`
- `fasolt.client/src/views/CardsView.vue`
- `fasolt.client/src/views/DeckDetailView.vue`
- `fasolt.client/src/components/AppLayout.vue` (or equivalent layout with "Files" nav link)
- `fasolt.client/src/components/BottomNav.vue` (mobile nav with "Files" link)

### Files to Create
- `fasolt.Server/Api/Endpoints/SourceEndpoints.cs`
- `fasolt.Server/Application/Dtos/SourceDtos.cs`
- `fasolt.Mcp/Tools/SourceTools.cs`
- `fasolt.client/src/views/SourcesView.vue`
- `fasolt.client/src/stores/sources.ts`
- EF Migration (auto-generated)

---

### Task 1: Data Model — Update Card Entity and DTOs

Remove `FileId`, `File` nav, and `CardType` from Card. Add `SourceFile`. Update all DTOs to match.

**Files:**
- Modify: `fasolt.Server/Domain/Entities/Card.cs`
- Modify: `fasolt.Server/Application/Dtos/CardDtos.cs`
- Modify: `fasolt.Server/Application/Dtos/BulkCardDtos.cs`
- Modify: `fasolt.Server/Application/Dtos/ReviewDtos.cs`
- Modify: `fasolt.Server/Application/Dtos/DeckDtos.cs`

- [ ] **Step 1: Update Card entity**

In `Card.cs`, remove:
```csharp
public Guid? FileId { get; set; }
public MarkdownFile? File { get; set; }
public string CardType { get; set; } = "custom";
```

Add:
```csharp
public string? SourceFile { get; set; }
```

Remove the `using` for MarkdownFile if present.

- [ ] **Step 2: Update CardDtos.cs**

Replace the file with:
```csharp
namespace Fasolt.Server.Application.Dtos;

public record CreateCardRequest(string? SourceFile, string? SourceHeading, string Front, string Back);

public record UpdateCardRequest(string Front, string Back);

public record CardDto(
    Guid Id, string? SourceFile, string? SourceHeading,
    string Front, string Back, string State,
    DateTimeOffset CreatedAt, List<CardDeckInfoDto> Decks);

public record CardDeckInfoDto(Guid Id, string Name);
```

- [ ] **Step 3: Update BulkCardDtos.cs**

Replace with:
```csharp
namespace Fasolt.Server.Application.Dtos;

public record BulkCreateCardsRequest(string? SourceFile, Guid? DeckId, List<BulkCardItem> Cards);

public record BulkCardItem(string Front, string Back, string? SourceFile = null, string? SourceHeading = null);

public record BulkCreateCardsResponse(List<CardDto> Created, List<SkippedCardDto> Skipped);

public record SkippedCardDto(string Front, string Reason);
```

- [ ] **Step 4: Update ReviewDtos.cs**

In `DueCardDto`, remove `CardType` and `FileId`, add `SourceFile`:
```csharp
public record DueCardDto(
    Guid Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State, double EaseFactor, int Interval, int Repetitions);
```

- [ ] **Step 5: Update DeckDtos.cs**

Remove `CardType` from `DeckCardDto`, add `SourceFile`, `SourceHeading`, `Back`:
```csharp
public record DeckCardDto(
    Guid Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State, DateTimeOffset? DueAt);
```

Delete `AddFileToDeckRequest` record entirely.

- [ ] **Step 6: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Build errors in endpoint files (Card, Deck, Search, File) — these reference removed fields. That's expected; we fix them in subsequent tasks.

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Domain/Entities/Card.cs \
  fasolt.Server/Application/Dtos/CardDtos.cs \
  fasolt.Server/Application/Dtos/BulkCardDtos.cs \
  fasolt.Server/Application/Dtos/ReviewDtos.cs \
  fasolt.Server/Application/Dtos/DeckDtos.cs
git commit -m "refactor: update Card entity and DTOs for MCP-first pivot

Remove FileId, CardType from Card. Add SourceFile string.
Update all DTOs to match new schema."
```

---

### Task 2: Delete File Entities, Services, DTOs, and Endpoints

Remove all file-related backend code.

**Files:**
- Delete: `fasolt.Server/Domain/Entities/MarkdownFile.cs`
- Delete: `fasolt.Server/Domain/Entities/FileHeading.cs`
- Delete: `fasolt.Server/Application/Services/FileComparer.cs`
- Delete: `fasolt.Server/Application/Services/HeadingExtractor.cs`
- Delete: `fasolt.Server/Application/Services/ContentExtractor.cs`
- Delete: `fasolt.Server/Application/Dtos/FileDtos.cs`
- Delete: `fasolt.Server/Api/Endpoints/FileEndpoints.cs`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Delete entity files**

```bash
rm fasolt.Server/Domain/Entities/MarkdownFile.cs
rm fasolt.Server/Domain/Entities/FileHeading.cs
```

- [ ] **Step 2: Delete service files**

```bash
rm fasolt.Server/Application/Services/FileComparer.cs
rm fasolt.Server/Application/Services/HeadingExtractor.cs
rm fasolt.Server/Application/Services/ContentExtractor.cs
```

- [ ] **Step 3: Delete file DTOs and endpoints**

```bash
rm fasolt.Server/Application/Dtos/FileDtos.cs
rm fasolt.Server/Api/Endpoints/FileEndpoints.cs
```

- [ ] **Step 4: Remove MapFileEndpoints from Program.cs**

In `Program.cs`, delete the line:
```csharp
app.MapFileEndpoints();
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: delete all file-related backend code

Remove MarkdownFile, FileHeading entities, FileComparer, HeadingExtractor,
ContentExtractor services, FileDtos, FileEndpoints."
```

---

### Task 3: Update AppDbContext

Remove file/heading DbSets and configuration. Update Card configuration to drop FileId FK/index, add SourceFile column config.

**Files:**
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs`

- [ ] **Step 1: Remove DbSets**

Delete these lines:
```csharp
public DbSet<MarkdownFile> MarkdownFiles => Set<MarkdownFile>();
public DbSet<FileHeading> FileHeadings => Set<FileHeading>();
```

Remove corresponding `using` statements if present.

- [ ] **Step 2: Remove MarkdownFile entity configuration**

Delete the entire `modelBuilder.Entity<MarkdownFile>(entity => { ... });` block (lines ~24–38).

- [ ] **Step 3: Remove FileHeading entity configuration**

Delete the entire `modelBuilder.Entity<FileHeading>(entity => { ... });` block (lines ~40–45).

- [ ] **Step 4: Update Card entity configuration**

In the `modelBuilder.Entity<Card>()` block:

Remove the `FileId` index:
```csharp
entity.HasIndex(e => e.FileId);
```

Remove the File FK relationship:
```csharp
entity.HasOne(e => e.File)
    .WithMany()
    .HasForeignKey(e => e.FileId)
    .OnDelete(DeleteBehavior.SetNull);
```

Remove `CardType` max length config:
```csharp
entity.Property(e => e.CardType).HasMaxLength(20).IsRequired();
```

Add `SourceFile` config:
```csharp
entity.Property(e => e.SourceFile).HasMaxLength(255);
entity.HasIndex(e => new { e.UserId, e.SourceFile });
```

- [ ] **Step 5: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Build errors only in CardEndpoints.cs, DeckEndpoints.cs, SearchEndpoints.cs (fixed in next tasks).

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Infrastructure/Data/AppDbContext.cs
git commit -m "refactor: update DbContext for MCP-first pivot

Remove MarkdownFile/FileHeading DbSets and config.
Update Card config: drop FileId FK, add SourceFile column."
```

---

### Task 4: Update CardEndpoints

Rewrite card endpoints to use `SourceFile` instead of `FileId`/`CardType`. Remove the Extract endpoint.

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/CardEndpoints.cs`

- [ ] **Step 1: Remove Extract endpoint registration**

Delete the route mapping line:
```csharp
group.MapGet("/extract", Extract);
```

- [ ] **Step 2: Delete the entire Extract method**

Remove the full `Extract` static method (lines ~194–235).

- [ ] **Step 3: Update Create endpoint**

In the `Create` method:
- Remove `FileId` validation (the `if (request.FileId.HasValue)` block that checks MarkdownFiles)
- Remove `CardType` validation (the `ValidCardTypes` array and the check against it)
- Remove `CardType` assignment logic
- Set `SourceFile` from request:

```csharp
var card = new Card
{
    Id = Guid.NewGuid(),
    UserId = userId,
    Front = request.Front.Trim(),
    Back = request.Back.Trim(),
    SourceFile = request.SourceFile?.Trim(),
    SourceHeading = request.SourceHeading?.Trim(),
    CreatedAt = DateTimeOffset.UtcNow,
    State = "new"
};
```

- [ ] **Step 4: Update List endpoint**

Replace `fileId` query parameter with `sourceFile`:
```csharp
string? sourceFile = null
```

Replace the filter:
```csharp
if (sourceFile is not null)
    query = query.Where(c => c.SourceFile == sourceFile);
```

Update the projection to use `SourceFile` instead of `FileId` and remove `CardType`:
```csharp
new CardDto(c.Id, c.SourceFile, c.SourceHeading, c.Front, c.Back, c.State, c.CreatedAt,
    c.DeckCards.Select(dc => new CardDeckInfoDto(dc.DeckId, dc.Deck.Name)).ToList())
```

- [ ] **Step 5: Update BulkCreate endpoint**

Remove `FileId` validation block (the `if (request.FileId.HasValue)` MarkdownFiles check).

Update duplicate detection to key on `(UserId, SourceFile, Front)`. Since per-card `sourceFile` can override the request-level default, resolve each card's effective sourceFile first, then check duplicates per source group:
```csharp
// Resolve effective sourceFile per card
var cardsBySource = request.Cards
    .GroupBy(c => c.SourceFile?.Trim() ?? request.SourceFile?.Trim());

foreach (var group in cardsBySource)
{
    var sourceFile = group.Key;
    var existingFronts = await db.Cards
        .Where(c => c.UserId == userId && c.SourceFile == sourceFile)
        .Select(c => c.Front.ToLower())
        .ToListAsync();
    // Check duplicates within this source group...
}
```

For cards with null SourceFile (both card-level and request-level), duplicate detection falls back to `(UserId, Front)` where `SourceFile IS NULL`.

Remove `CardType` derivation logic. Update card construction:
```csharp
var card = new Card
{
    Id = Guid.NewGuid(),
    UserId = userId,
    Front = item.Front.Trim(),
    Back = item.Back.Trim(),
    SourceFile = item.SourceFile?.Trim() ?? request.SourceFile?.Trim(),
    SourceHeading = item.SourceHeading?.Trim(),
    CreatedAt = DateTimeOffset.UtcNow,
    State = "new"
};
```

Update DTO construction to use `CardDto` with `SourceFile` instead of `FileId`/`CardType`.

- [ ] **Step 6: Update GetById and ToDto helper**

Update the `ToDto` helper or inline projections to map `SourceFile` instead of `FileId`/`CardType`.

- [ ] **Step 7: Remove unused usings**

Remove any `using` referencing MarkdownFile, FileComparer, ContentExtractor, HeadingExtractor, FileDtos.

- [ ] **Step 8: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Errors only in DeckEndpoints and SearchEndpoints.

- [ ] **Step 9: Commit**

```bash
git add fasolt.Server/Api/Endpoints/CardEndpoints.cs
git commit -m "refactor: update CardEndpoints for MCP-first pivot

Remove Extract endpoint, FileId/CardType logic.
Use SourceFile string for card provenance and duplicate detection."
```

---

### Task 5: Update DeckEndpoints

Remove AddFileCards endpoint. Update projections that reference CardType.

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/DeckEndpoints.cs`

- [ ] **Step 1: Remove AddFileCards route registration**

Delete:
```csharp
group.MapPost("/{id:guid}/add-file", AddFileCards);
```

- [ ] **Step 2: Delete the entire AddFileCards method**

Remove the full `AddFileCards` static method (lines ~225–263).

- [ ] **Step 3: Update GetById projection**

In the deck detail endpoint, update the `DeckCardDto` projection to remove `CardType` and add `SourceFile`, `SourceHeading`, `Back`:
```csharp
new DeckCardDto(dc.Card.Id, dc.Card.Front, dc.Card.Back,
    dc.Card.SourceFile, dc.Card.SourceHeading,
    dc.Card.State, dc.Card.DueAt)
```

- [ ] **Step 4: Remove unused usings**

Remove references to MarkdownFile if present.

- [ ] **Step 5: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Errors only in SearchEndpoints.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Api/Endpoints/DeckEndpoints.cs
git commit -m "refactor: update DeckEndpoints for MCP-first pivot

Remove AddFileCards endpoint. Update DeckCardDto projection."
```

---

### Task 6: Update SearchEndpoints

Remove file search results and CardType from search.

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/SearchEndpoints.cs`

- [ ] **Step 1: Remove file search query**

Delete the entire block that queries `db.MarkdownFiles` (lines ~62–74).

- [ ] **Step 2: Update SearchResponse record**

Remove `Files` list and `FileSearchResult` record:
```csharp
record SearchResponse(List<CardSearchResult> Cards, List<DeckSearchResult> Decks);
record CardSearchResult(Guid Id, string Headline, string State);
record DeckSearchResult(Guid Id, string Headline, int CardCount);
```

Remove `CardType` from `CardSearchResult`.

- [ ] **Step 3: Update card search raw SQL**

The card search uses raw SQL that selects `c."CardType"` — this column will be dropped. Update the SQL to remove it:
```sql
SELECT c."Id",
       ts_headline('english', c."Front", plainto_tsquery('english', {0}),
           'StartSel=<mark>,StopSel=</mark>,MaxFragments=1') AS "Headline",
       c."State"
FROM "Cards" c
WHERE c."UserId" = {1}
  AND c."DeletedAt" IS NULL
  AND c."SearchVector" @@ plainto_tsquery('english', {0})
ORDER BY ts_rank(c."SearchVector", plainto_tsquery('english', {0})) DESC
LIMIT 10
```

- [ ] **Step 4: Update response construction**

Change `new SearchResponse(cards, decks, files)` to `new SearchResponse(cards, decks)`.
Also update the early return: `new SearchResponse([], [], [])` → `new SearchResponse([], [])`.

- [ ] **Step 5: Remove unused usings**

Remove references to MarkdownFile.

- [ ] **Step 6: Verify full build**

Run: `dotnet build fasolt.Server`
Expected: Build errors only in ReviewEndpoints (fixed in Task 7).

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Api/Endpoints/SearchEndpoints.cs
git commit -m "refactor: update SearchEndpoints for MCP-first pivot

Remove file search results and CardType from search response."
```

---

### Task 7: Update ReviewEndpoints

Update the DueCardDto projection in the review endpoint.

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs`

- [ ] **Step 1: Update DueCardDto projection**

Find where `DueCardDto` is constructed (the query that selects due cards) and update projection:
```csharp
new DueCardDto(c.Id, c.Front, c.Back, c.SourceFile, c.SourceHeading,
    c.State, c.EaseFactor, c.Interval, c.Repetitions)
```

Remove `c.CardType` and `c.FileId` from the projection.

- [ ] **Step 2: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Clean build.

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/Endpoints/ReviewEndpoints.cs
git commit -m "refactor: update ReviewEndpoints DueCardDto projection"
```

---

### Task 8: Add Sources Endpoint

New endpoint: `GET /api/sources` returns distinct source files with card counts.

**Files:**
- Create: `fasolt.Server/Application/Dtos/SourceDtos.cs`
- Create: `fasolt.Server/Api/Endpoints/SourceEndpoints.cs`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Create SourceDtos.cs**

```csharp
namespace Fasolt.Server.Application.Dtos;

public record SourceListResponse(List<SourceItemDto> Items);
public record SourceItemDto(string SourceFile, int CardCount, int DueCount);
```

- [ ] **Step 2: Create SourceEndpoints.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;
using System.Security.Claims;

namespace Fasolt.Server.Api.Endpoints;

public static class SourceEndpoints
{
    public static void MapSourceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sources").RequireAuthorization();
        group.MapGet("", List);
    }

    private static async Task<IResult> List(AppDbContext db, ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var now = DateTimeOffset.UtcNow;

        var sources = await db.Cards
            .Where(c => c.UserId == userId && c.SourceFile != null)
            .GroupBy(c => c.SourceFile!)
            .Select(g => new SourceItemDto(
                g.Key,
                g.Count(),
                g.Count(c => c.DueAt != null && c.DueAt <= now)))
            .OrderBy(s => s.SourceFile)
            .ToListAsync();

        return Results.Ok(new SourceListResponse(sources));
    }
}
```

- [ ] **Step 3: Register in Program.cs**

Add after `app.MapSearchEndpoints();`:
```csharp
app.MapSourceEndpoints();
```

- [ ] **Step 4: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Clean build.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Application/Dtos/SourceDtos.cs \
  fasolt.Server/Api/Endpoints/SourceEndpoints.cs \
  fasolt.Server/Program.cs
git commit -m "feat: add GET /api/sources endpoint

Returns distinct source files with card and due counts."
```

---

### Task 9: EF Migration

Generate and apply the migration for all schema changes.

**Files:**
- Auto-generated migration files

- [ ] **Step 1: Generate migration**

Run:
```bash
dotnet ef migrations add McpFirstPivot --project fasolt.Server
```

- [ ] **Step 2: Review the migration**

Open the generated migration file. Verify it:
1. Adds `SourceFile` column to `Cards`
2. Drops `FileId` column and FK from `Cards`
3. Drops `CardType` column from `Cards`
4. Drops `FileHeadings` table
5. Drops `MarkdownFiles` table
6. Adds index on `(UserId, SourceFile)`

- [ ] **Step 3: Add data migration for SourceFile population**

In the `Up` method, before dropping `FileId` and the `MarkdownFiles` table, add:
```csharp
migrationBuilder.Sql("""
    UPDATE "Cards" c
    SET "SourceFile" = m."FileName"
    FROM "MarkdownFiles" m
    WHERE c."FileId" = m."Id"
""");
```

This preserves source file provenance for existing cards.

- [ ] **Step 4: Apply migration**

Run:
```bash
dotnet ef database update --project fasolt.Server
```

Expected: Migration applies successfully.

- [ ] **Step 5: Verify full backend**

Run:
```bash
dotnet run --project fasolt.Server
```

Verify the app starts and `GET /api/health` returns 200.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat: add migration for MCP-first pivot

Adds SourceFile to Cards, populates from MarkdownFiles,
drops FileId/CardType, drops MarkdownFiles/FileHeadings tables."
```

---

### Task 10: Update MCP Server

Remove FileTools, update CardTools, remove PostFileAsync from ApiClient, add SourceTools.

**Files:**
- Delete: `fasolt.Mcp/Tools/FileTools.cs`
- Modify: `fasolt.Mcp/Tools/CardTools.cs`
- Modify: `fasolt.Mcp/ApiClient.cs`
- Create: `fasolt.Mcp/Tools/SourceTools.cs`

- [ ] **Step 1: Delete FileTools.cs**

```bash
rm fasolt.Mcp/Tools/FileTools.cs
```

- [ ] **Step 2: Remove PostFileAsync from ApiClient**

In `ApiClient.cs`, delete the `PostFileAsync` method (the multipart form upload method). Keep `GetAsync` and `PostAsync`.

- [ ] **Step 3: Update CardTools.cs**

Update `SearchCards` description to remove mention of "files":
```csharp
[McpServerTool, Description("Search existing cards and decks by query text. Use this to check for duplicates before creating cards.")]
```

Update `ListCards` — replace `fileId` param with `sourceFile`:
```csharp
[Description("Filter by source file name")] string? sourceFile = null,
```
And the query param:
```csharp
if (sourceFile is not null) queryParams.Add($"sourceFile={Uri.EscapeDataString(sourceFile)}");
```
Remove the old `fileId` query param line.

Update `CreateCards`:
```csharp
[McpServerTool, Description("Create one or more flashcards, optionally linked to a source file and/or deck. Returns created cards and any skipped duplicates.")]
public static async Task<string> CreateCards(
    ApiClient api,
    [Description("Array of cards to create. Each card needs 'front' and 'back' text, plus optional 'sourceFile' and 'sourceHeading'.")] List<CardInput> cards,
    [Description("Default source file name for all cards (individual cards can override)")] string? sourceFile = null,
    [Description("Add cards to this deck ID")] string? deckId = null)
{
    var body = new
    {
        sourceFile,
        deckId = deckId is not null ? Guid.Parse(deckId) : (Guid?)null,
        cards
    };
    var result = await api.PostAsync<JsonElement>("/api/cards/bulk", body);
    return result.GetRawText();
}
```

Update `CardInput` record:
```csharp
public record CardInput(string Front, string Back, string? SourceFile = null, string? SourceHeading = null);
```

- [ ] **Step 4: Create SourceTools.cs**

```csharp
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Fasolt.Mcp;

namespace Fasolt.Mcp.Tools;

[McpServerToolType]
public class SourceTools
{
    [McpServerTool, Description("List all source files that cards were created from, with card counts and due counts.")]
    public static async Task<string> ListSources(ApiClient api)
    {
        var result = await api.GetAsync<JsonElement>("/api/sources");
        return result.GetRawText();
    }
}
```

- [ ] **Step 5: Verify build**

Run: `dotnet build fasolt.Mcp`
Expected: Clean build.

- [ ] **Step 6: Commit**

```bash
git add -A fasolt.Mcp/
git commit -m "refactor: update MCP server for MCP-first pivot

Remove FileTools, add ListSources, update CardTools to use sourceFile."
```

---

### Task 11: Update Frontend Types and API Client

Update TypeScript types and API client to match new backend.

**Files:**
- Modify: `fasolt.client/src/types/index.ts`
- Modify: `fasolt.client/src/api/client.ts`

- [ ] **Step 1: Update types/index.ts**

Remove interfaces: `MarkdownFile`, `FileHeading`, `FileDetail`, `BulkUploadResult`, `FileUpdatePreview`, `ExtractedContent` (if present).

Update `Card` interface:
- Remove `fileId: string | null` and `cardType: string`
- Add `sourceFile: string | null`

Update `DeckCard` interface:
- Remove `cardType: string`
- Add `sourceFile: string | null`, `sourceHeading: string | null`, `back: string`

Update `DueCard` interface:
- Remove `cardType: string`, `fileId: string | null`, `dueAt: string | null`
- Add `sourceFile: string | null`
- Ensure it has: `easeFactor: number`, `interval: number`, `repetitions: number` (matching new `DueCardDto` shape)

Add new interface:
```typescript
export interface SourceItem {
  sourceFile: string
  cardCount: number
  dueCount: number
}
```

- [ ] **Step 2: Update api/client.ts**

Remove `FileSearchResult` interface.
Remove `files: FileSearchResult[]` from `SearchResponse`.
Remove the `apiUpload` function entirely.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/types/index.ts fasolt.client/src/api/client.ts
git commit -m "refactor: update frontend types and API client for pivot

Remove file types, add SourceItem, update Card/DeckCard/DueCard types."
```

---

### Task 12: Delete Frontend File Views, Components, and Store

Remove all file-related frontend code.

**Files:**
- Delete: `fasolt.client/src/views/FilesView.vue`
- Delete: `fasolt.client/src/views/FileDetailView.vue`
- Delete: `fasolt.client/src/components/FileUpdatePreviewDialog.vue`
- Delete: `fasolt.client/src/stores/files.ts`

- [ ] **Step 1: Delete files**

```bash
rm fasolt.client/src/views/FilesView.vue
rm fasolt.client/src/views/FileDetailView.vue
rm fasolt.client/src/components/FileUpdatePreviewDialog.vue
rm fasolt.client/src/stores/files.ts
```

- [ ] **Step 2: Commit**

```bash
git add -A fasolt.client/src/
git commit -m "refactor: delete file views, components, and store"
```

---

### Task 13: Update Frontend Stores and Router

Update cards/decks stores, router, and search composable.

**Files:**
- Modify: `fasolt.client/src/stores/cards.ts`
- Modify: `fasolt.client/src/stores/decks.ts`
- Modify: `fasolt.client/src/router/index.ts`
- Modify: `fasolt.client/src/composables/useSearch.ts`
- Modify: `fasolt.client/src/views/CardsView.vue`
- Modify: `fasolt.client/src/views/DeckDetailView.vue`

- [ ] **Step 1: Update stores/cards.ts**

- In `fetchCards`: rename `fileId` param to `sourceFile`, change query param from `fileId=` to `sourceFile=`
- In `createCard`: remove `fileId` and `cardType` from data param, add `sourceFile?: string`
- Delete `extractContent` method entirely

- [ ] **Step 2: Update stores/decks.ts**

- Delete `addFileCards` method entirely
- Remove it from the returned object

- [ ] **Step 3: Update router/index.ts**

Remove routes:
```typescript
{ path: '/files', name: 'files', ... }
{ path: '/files/:id', name: 'file-detail', ... }
```

Add route:
```typescript
{ path: '/sources', name: 'sources', component: () => import('@/views/SourcesView.vue') }
```

- [ ] **Step 4: Update composables/useSearch.ts**

- Remove `FileSearchResult` import
- Remove `'file'` variant from `SearchItem` union type
- Remove `case 'file'` from `navigateToResult`
- Remove file results from `hasResults` check and results mapping

- [ ] **Step 5: Update CardsView.vue**

- Remove any import of `files` store
- Replace `fileId` filter/column references with `sourceFile`
- Remove `cardType` column from card list display
- If there's a file filter dropdown, replace with sourceFile text filter or remove
- Update card display to show `sourceFile` instead of file link

- [ ] **Step 6: Update DeckDetailView.vue**

- Remove import of `files` store if present
- Remove "Add file cards" button/action (called `addFileCards`)
- Update card list display: remove `cardType`, show `sourceFile`/`sourceHeading` as metadata

- [ ] **Step 7: Update DueCard type usage**

In any review-related components that use `DueCard` type, ensure they no longer reference `cardType` or `fileId`. They should use `sourceFile` instead.

- [ ] **Step 8: Verify frontend builds**

Run:
```bash
cd fasolt.client && npm run build
```

Expected: May have errors from navigation component referencing "Files" — fixed in next task.

- [ ] **Step 9: Commit**

```bash
git add fasolt.client/src/
git commit -m "refactor: update stores, views, router, search for MCP-first pivot

Remove file references from cards/decks stores and views. Update router.
Remove file results from search."
```

---

### Task 14: Add Sources View and Update Navigation

Create the Sources view and update navigation to replace "Files" with "Sources".

**Files:**
- Create: `fasolt.client/src/views/SourcesView.vue`
- Create: `fasolt.client/src/stores/sources.ts`
- Modify: `fasolt.client/src/components/AppLayout.vue` (sidebar with "Files" nav link)
- Modify: `fasolt.client/src/components/BottomNav.vue` (mobile nav with "Files" link)

- [ ] **Step 1: Create stores/sources.ts**

```typescript
import { ref } from 'vue'
import { defineStore } from 'pinia'
import { apiFetch } from '@/api/client'
import type { SourceItem } from '@/types'

export const useSourcesStore = defineStore('sources', () => {
  const sources = ref<SourceItem[]>([])
  const loading = ref(false)

  async function fetchSources() {
    loading.value = true
    try {
      const data = await apiFetch<{ items: SourceItem[] }>('/sources')
      sources.value = data.items
    } finally {
      loading.value = false
    }
  }

  return { sources, loading, fetchSources }
})
```

- [ ] **Step 2: Create SourcesView.vue**

Build a simple list view showing source files with card count and due count. Each row links to `/cards?sourceFile=<name>`. Follow the existing view patterns in the codebase (use the same layout components, styling approach as other list views like DecksView or CardsView).

- [ ] **Step 3: Update navigation**

In `AppLayout.vue` and `BottomNav.vue`, replace the "Files" nav item (path `/files`) with "Sources" pointing to `/sources`. Update icon if appropriate.

- [ ] **Step 4: Verify frontend builds and runs**

Run:
```bash
cd fasolt.client && npm run build
```

Expected: Clean build.

- [ ] **Step 5: Manual test**

Start the full stack. Verify:
1. `/sources` page loads and shows source files (or empty state)
2. Clicking a source navigates to cards filtered by that source
3. Navigation shows "Sources" instead of "Files"
4. Old `/files` URL returns 404 / redirects appropriately

- [ ] **Step 6: Commit**

```bash
git add fasolt.client/src/stores/sources.ts \
  fasolt.client/src/views/SourcesView.vue \
  fasolt.client/src/
git commit -m "feat: add Sources view replacing Files

New sources store and view showing card provenance by source file.
Update navigation: Files -> Sources."
```

---

### Task 15: Update CLAUDE.md and Clean Up Docs

Update project documentation to reflect the pivot.

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update CLAUDE.md**

Key changes:
- Update "Core Concept" to reflect MCP-first input, no file storage
- Remove "Markdown file management" from features, add "Source tracking"
- Update "Key API Routes" — remove `/api/files`, add `/api/sources`
- Update MCP server section — new tool list (6 tools)
- Remove or update "Generating Flashcard Markers" section (the `?::` marker concept still applies but files aren't uploaded to the server)
- Update any remaining references to file upload

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md for MCP-first pivot"
```

---

### Task 16: End-to-End Verification

Verify the full stack works after all changes.

- [ ] **Step 1: Start fresh**

```bash
docker compose down -v && docker compose up -d
dotnet ef database update --project fasolt.Server
```

- [ ] **Step 2: Start the stack**

```bash
./dev.sh
```

- [ ] **Step 3: Test API endpoints**

```bash
# Health
curl http://localhost:8080/api/health

# Register and login
curl -c cookies -b cookies -X POST http://localhost:8080/api/identity/register \
  -H 'Content-Type: application/json' \
  -d '{"email":"test@test.com","password":"Test1234!"}'

# Create cards via bulk
curl -c cookies -b cookies -X POST http://localhost:8080/api/cards/bulk \
  -H 'Content-Type: application/json' \
  -d '{"sourceFile":"test.md","cards":[{"front":"Q1","back":"A1","sourceHeading":"Intro"}]}'

# List sources
curl -c cookies -b cookies http://localhost:8080/api/sources

# Search
curl -c cookies -b cookies 'http://localhost:8080/api/search?q=Q1'
```

- [ ] **Step 4: Test MCP server**

```bash
FASOLT_URL=http://localhost:8080 \
FASOLT_TOKEN=sm_dev_token_for_local_testing_only_do_not_use_in_production_0000 \
  dotnet run --project fasolt.Mcp
```

Verify it starts without errors.

- [ ] **Step 5: Test frontend via Playwright**

Use Playwright to verify:
1. Login works
2. Dashboard loads
3. Sources page loads (empty or with data)
4. Cards page loads
5. Review flow works
6. Search works (no file results)
7. `/files` URL doesn't crash the app

- [ ] **Step 6: Final commit if any fixes needed**

```bash
git add -A
git commit -m "fix: address issues found during e2e verification"
```
