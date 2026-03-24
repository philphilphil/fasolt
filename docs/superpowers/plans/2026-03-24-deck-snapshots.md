# Deck Snapshots Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow users to create snapshots of all decks as backups and selectively restore individual cards from them via a diff-based UI.

**Architecture:** New `DeckSnapshot` entity stores full deck+card state as a jsonb blob with a version field for forward compatibility. Diff computation and restore logic live in a `DeckSnapshotService`. Frontend adds a snapshots list page per deck with a restore dialog. iOS settings gets a create button + history list.

**Tech Stack:** .NET 10 / EF Core / Postgres jsonb, Vue 3 / Pinia / shadcn-vue, Swift / SwiftUI

---

## File Structure

### Backend — New Files
- `fasolt.Server/Domain/Entities/DeckSnapshot.cs` — entity
- `fasolt.Server/Application/Services/DeckSnapshotService.cs` — create, list, diff, restore logic
- `fasolt.Server/Application/Dtos/SnapshotDtos.cs` — request/response DTOs
- `fasolt.Server/Api/Endpoints/SnapshotEndpoints.cs` — REST endpoints
- `fasolt.Server/Api/McpTools/SnapshotTools.cs` — MCP tools

### Backend — Modified Files
- `fasolt.Server/Infrastructure/Data/AppDbContext.cs` — add DeckSnapshot DbSet + configuration
- `fasolt.Server/Program.cs` — register DeckSnapshotService, map SnapshotEndpoints

### Frontend — New Files
- `fasolt.client/src/views/DeckSnapshotsView.vue` — snapshots list page
- `fasolt.client/src/components/RestoreDialog.vue` — restore diff dialog
- `fasolt.client/src/stores/snapshots.ts` — Pinia store

### Frontend — Modified Files
- `fasolt.client/src/types/index.ts` — add snapshot types
- `fasolt.client/src/router/index.ts` — add snapshots route
- `fasolt.client/src/views/DeckDetailView.vue` — add Snapshots button in header
- `fasolt.client/src/views/StudyView.vue` — add Create Snapshot button on dashboard

### iOS — New Files
- `fasolt.ios/Fasolt/ViewModels/SnapshotViewModel.swift` — snapshot create + list logic

### iOS — Modified Files
- `fasolt.ios/Fasolt/Models/APIModels.swift` — add DeckSnapshotDTO
- `fasolt.ios/Fasolt/Views/Settings/SettingsView.swift` — add Snapshots section

---

## Task 1: DeckSnapshot Entity + Migration

**Files:**
- Create: `fasolt.Server/Domain/Entities/DeckSnapshot.cs`
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs`

- [ ] **Step 1: Create the DeckSnapshot entity**

Create `fasolt.Server/Domain/Entities/DeckSnapshot.cs`:

```csharp
namespace Fasolt.Server.Domain.Entities;

public class DeckSnapshot
{
    public Guid Id { get; set; }
    public string PublicId { get; set; } = default!;
    public Guid DeckId { get; set; }
    public Deck Deck { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public int Version { get; set; } = 1;
    public int CardCount { get; set; }
    public string Data { get; set; } = default!; // jsonb
    public DateTimeOffset CreatedAt { get; set; }
}
```

- [ ] **Step 2: Add DbSet and EF configuration to AppDbContext**

In `AppDbContext.cs`, add `DbSet<DeckSnapshot> DeckSnapshots` property.

In `OnModelCreating`, add configuration after the existing Deck configuration:

```csharp
builder.Entity<DeckSnapshot>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.PublicId).HasMaxLength(12).IsRequired();
    entity.HasIndex(e => e.PublicId).IsUnique();
    entity.Property(e => e.Data).HasColumnType("jsonb").IsRequired();
    entity.HasIndex(e => new { e.UserId, e.DeckId, e.CreatedAt });
    entity.HasOne(e => e.Deck).WithMany().HasForeignKey(e => e.DeckId)
        .OnDelete(DeleteBehavior.SetNull);
    entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

Note: `OnDelete(DeleteBehavior.SetNull)` requires `DeckId` to be nullable (`Guid?`). Update the entity to use `Guid? DeckId` and `Deck? Deck` so snapshots survive deck deletion.

- [ ] **Step 3: Generate and verify the migration**

Run:
```bash
dotnet ef migrations add AddDeckSnapshots --project fasolt.Server
```

Review the generated migration file to confirm it creates the `DeckSnapshots` table with the correct columns, indexes, and the SetNull foreign key behavior.

- [ ] **Step 4: Apply migration and verify**

Run:
```bash
dotnet ef database update --project fasolt.Server
```

Verify the table exists:
```bash
docker exec -it fasolt-db psql -U spaced -d fasolt -c '\d "DeckSnapshots"'
```

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Domain/Entities/DeckSnapshot.cs fasolt.Server/Infrastructure/Data/AppDbContext.cs fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat: add DeckSnapshot entity and migration"
```

---

## Task 2: Snapshot DTOs

**Files:**
- Create: `fasolt.Server/Application/Dtos/SnapshotDtos.cs`

- [ ] **Step 1: Create all snapshot DTOs**

Create `fasolt.Server/Application/Dtos/SnapshotDtos.cs`:

```csharp
namespace Fasolt.Server.Application.Dtos;

// Data stored inside the jsonb blob
public record SnapshotData(string DeckName, string? DeckDescription, List<SnapshotCardData> Cards);

public record SnapshotCardData(
    Guid CardId,
    string PublicId,
    string Front,
    string Back,
    string? FrontSvg,
    string? BackSvg,
    string? SourceFile,
    string? SourceHeading,
    DateTimeOffset CreatedAt,
    double? Stability,
    double? Difficulty,
    int? Step,
    DateTimeOffset? DueAt,
    string State,
    DateTimeOffset? LastReviewedAt);

// API response for listing
public record SnapshotListDto(string Id, string? DeckName, int CardCount, DateTimeOffset CreatedAt);

// API response for diff
public record SnapshotDiffDto(
    List<DiffDeletedCard> Deleted,
    List<DiffModifiedCard> Modified,
    List<DiffAddedCard> Added);

public record DiffDeletedCard(
    Guid CardId, string Front, string Back, string? SourceFile,
    double? Stability, DateTimeOffset? DueAt);

public record DiffModifiedCard(
    Guid CardId,
    string Front, string CurrentFront,
    string Back, string CurrentBack,
    double? SnapshotStability, double? CurrentStability,
    bool HasContentChanges, bool HasFsrsChanges);

public record DiffAddedCard(Guid CardId, string Front, string Back);

// Restore request
public record RestoreRequest(List<Guid> RestoreDeletedCardIds, List<Guid> RevertModifiedCardIds);

// Create response
public record SnapshotCreateResult(int Count);
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.Server/Application/Dtos/SnapshotDtos.cs
git commit -m "feat: add snapshot DTOs"
```

---

## Task 3: DeckSnapshotService — Create + Retention

**Files:**
- Create: `fasolt.Server/Application/Services/DeckSnapshotService.cs`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Create DeckSnapshotService with CreateAll method**

Create `fasolt.Server/Application/Services/DeckSnapshotService.cs`:

```csharp
using System.Text.Json;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fasolt.Server.Application.Services;

public class DeckSnapshotService(AppDbContext db)
{
    private const int MaxSnapshotsPerDeck = 10;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<int> CreateAll(string userId)
    {
        var decks = await db.Decks
            .Where(d => d.UserId == userId)
            .Include(d => d.Cards).ThenInclude(dc => dc.Card)
            .ToListAsync();

        var count = 0;
        foreach (var deck in decks)
        {
            var cards = deck.Cards.Select(dc => dc.Card).ToList();
            if (cards.Count == 0) continue; // skip empty decks

            var data = new SnapshotData(
                deck.Name,
                deck.Description,
                cards.Select(c => new SnapshotCardData(
                    c.Id, c.PublicId, c.Front, c.Back, c.FrontSvg, c.BackSvg,
                    c.SourceFile, c.SourceHeading, c.CreatedAt,
                    c.Stability, c.Difficulty, c.Step, c.DueAt, c.State, c.LastReviewedAt
                )).ToList());

            var snapshot = new DeckSnapshot
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                DeckId = deck.Id,
                UserId = userId,
                Version = 1,
                CardCount = cards.Count,
                Data = JsonSerializer.Serialize(data, JsonOptions),
                CreatedAt = DateTimeOffset.UtcNow,
            };

            db.DeckSnapshots.Add(snapshot);
            count++;
        }

        await db.SaveChangesAsync();

        // Enforce retention
        await EnforceRetention(userId);

        return count;
    }

    private async Task EnforceRetention(string userId)
    {
        var deckIds = await db.DeckSnapshots
            .Where(s => s.UserId == userId)
            .Select(s => s.DeckId)
            .Distinct()
            .ToListAsync();

        foreach (var deckId in deckIds)
        {
            var excess = await db.DeckSnapshots
                .Where(s => s.DeckId == deckId && s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .Skip(MaxSnapshotsPerDeck)
                .ToListAsync();

            if (excess.Count > 0)
                db.DeckSnapshots.RemoveRange(excess);
        }

        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 2: Register service in Program.cs**

In `fasolt.Server/Program.cs`, add alongside other service registrations:

```csharp
builder.Services.AddScoped<DeckSnapshotService>();
```

- [ ] **Step 3: Verify it compiles**

Run:
```bash
dotnet build fasolt.Server
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Application/Services/DeckSnapshotService.cs fasolt.Server/Program.cs
git commit -m "feat: add DeckSnapshotService with create and retention"
```

---

## Task 4: DeckSnapshotService — List + Diff + Restore

**Files:**
- Modify: `fasolt.Server/Application/Services/DeckSnapshotService.cs`

- [ ] **Step 1: Add ListByDeck method**

```csharp
public async Task<List<SnapshotListDto>> ListByDeck(string userId, string deckPublicId)
{
    var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);
    if (deck is null) return [];

    return await db.DeckSnapshots
        .Where(s => s.DeckId == deck.Id && s.UserId == userId)
        .OrderByDescending(s => s.CreatedAt)
        .Select(s => new SnapshotListDto(s.PublicId, s.Deck != null ? s.Deck.Name : null, s.CardCount, s.CreatedAt))
        .ToListAsync();
}
```

- [ ] **Step 2: Add ListRecent method (for MCP without deckId)**

```csharp
public async Task<List<SnapshotListDto>> ListRecent(string userId, int limit = 50)
{
    return await db.DeckSnapshots
        .Where(s => s.UserId == userId)
        .OrderByDescending(s => s.CreatedAt)
        .Take(limit)
        .Select(s => new SnapshotListDto(s.PublicId, s.Deck != null ? s.Deck.Name : null, s.CardCount, s.CreatedAt))
        .ToListAsync();
}
```

- [ ] **Step 3: Add GetById method**

```csharp
public async Task<object?> GetById(string userId, string snapshotPublicId)
{
    var snapshot = await db.DeckSnapshots
        .FirstOrDefaultAsync(s => s.PublicId == snapshotPublicId && s.UserId == userId);
    if (snapshot is null) return null;

    var data = JsonSerializer.Deserialize<SnapshotData>(snapshot.Data, JsonOptions)!;
    return new
    {
        snapshot.PublicId,
        DeckName = data.DeckName,
        DeckDescription = data.DeckDescription,
        snapshot.CardCount,
        snapshot.Version,
        snapshot.CreatedAt,
        Cards = data.Cards,
    };
}
```

- [ ] **Step 4: Add ComputeDiff method**

```csharp
public async Task<SnapshotDiffDto?> ComputeDiff(string userId, string snapshotPublicId)
{
    var snapshot = await db.DeckSnapshots
        .FirstOrDefaultAsync(s => s.PublicId == snapshotPublicId && s.UserId == userId);
    if (snapshot is null) return null;

    var data = JsonSerializer.Deserialize<SnapshotData>(snapshot.Data, JsonOptions)!;

    // Get current deck cards
    var currentCards = snapshot.DeckId.HasValue
        ? await db.DeckCards
            .Where(dc => dc.DeckId == snapshot.DeckId.Value)
            .Include(dc => dc.Card)
            .Select(dc => dc.Card)
            .ToListAsync()
        : [];

    var currentById = currentCards.ToDictionary(c => c.Id);
    var snapshotById = data.Cards.ToDictionary(c => c.CardId);

    var deleted = data.Cards
        .Where(sc => !currentById.ContainsKey(sc.CardId))
        .Select(sc => new DiffDeletedCard(sc.CardId, sc.Front, sc.Back, sc.SourceFile, sc.Stability, sc.DueAt))
        .ToList();

    var modified = data.Cards
        .Where(sc => currentById.ContainsKey(sc.CardId))
        .Select(sc =>
        {
            var cur = currentById[sc.CardId];
            var contentChanged = sc.Front != cur.Front || sc.Back != cur.Back
                || sc.FrontSvg != cur.FrontSvg || sc.BackSvg != cur.BackSvg
                || sc.SourceFile != cur.SourceFile || sc.SourceHeading != cur.SourceHeading;
            var fsrsChanged = sc.Stability != cur.Stability || sc.Difficulty != cur.Difficulty
                || sc.Step != cur.Step || sc.DueAt != cur.DueAt || sc.State != cur.State;
            if (!contentChanged && !fsrsChanged) return null;
            return new DiffModifiedCard(
                sc.CardId, sc.Front, cur.Front, sc.Back, cur.Back,
                sc.Stability, cur.Stability, contentChanged, fsrsChanged);
        })
        .Where(m => m is not null)
        .Cast<DiffModifiedCard>()
        .ToList();

    var added = currentCards
        .Where(c => !snapshotById.ContainsKey(c.Id))
        .Select(c => new DiffAddedCard(c.Id, c.Front, c.Back))
        .ToList();

    return new SnapshotDiffDto(deleted, modified, added);
}
```

- [ ] **Step 5: Add Restore method**

Note: `Card.State` is a `string` in this codebase, so assigning from the snapshot string directly works.

```csharp
public async Task<bool> Restore(string userId, string snapshotPublicId, RestoreRequest request)
{
    var snapshot = await db.DeckSnapshots
        .FirstOrDefaultAsync(s => s.PublicId == snapshotPublicId && s.UserId == userId);
    if (snapshot?.DeckId is null) return false;

    var data = JsonSerializer.Deserialize<SnapshotData>(snapshot.Data, JsonOptions)!;
    var snapshotById = data.Cards.ToDictionary(c => c.CardId);
    var deckId = snapshot.DeckId.Value;

    // Restore deleted cards
    foreach (var cardId in request.RestoreDeletedCardIds)
    {
        if (!snapshotById.TryGetValue(cardId, out var sc)) continue;

        var existingCard = await db.Cards.FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId);
        if (existingCard is not null)
        {
            // Card exists but was removed from deck — update and re-add
            ApplySnapshotToCard(existingCard, sc);
            var alreadyInDeck = await db.DeckCards.AnyAsync(dc => dc.DeckId == deckId && dc.CardId == cardId);
            if (!alreadyInDeck)
                db.DeckCards.Add(new DeckCard { DeckId = deckId, CardId = cardId });
        }
        else
        {
            // Card truly deleted — create new
            var newCard = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                UserId = userId,
                Front = sc.Front,
                Back = sc.Back,
                FrontSvg = sc.FrontSvg,
                BackSvg = sc.BackSvg,
                SourceFile = sc.SourceFile,
                SourceHeading = sc.SourceHeading,
                CreatedAt = sc.CreatedAt,
                Stability = sc.Stability,
                Difficulty = sc.Difficulty,
                Step = sc.Step,
                DueAt = sc.DueAt,
                State = sc.State,
                LastReviewedAt = sc.LastReviewedAt,
            };
            db.Cards.Add(newCard);
            db.DeckCards.Add(new DeckCard { DeckId = deckId, CardId = newCard.Id });
        }
    }

    // Revert modified cards
    foreach (var cardId in request.RevertModifiedCardIds)
    {
        if (!snapshotById.TryGetValue(cardId, out var sc)) continue;
        var card = await db.Cards.FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId);
        if (card is not null)
            ApplySnapshotToCard(card, sc);
    }

    await db.SaveChangesAsync();
    return true;
}

private static void ApplySnapshotToCard(Card card, SnapshotCardData sc)
{
    card.Front = sc.Front;
    card.Back = sc.Back;
    card.FrontSvg = sc.FrontSvg;
    card.BackSvg = sc.BackSvg;
    card.SourceFile = sc.SourceFile;
    card.SourceHeading = sc.SourceHeading;
    card.CreatedAt = sc.CreatedAt;
    card.Stability = sc.Stability;
    card.Difficulty = sc.Difficulty;
    card.Step = sc.Step;
    card.DueAt = sc.DueAt;
    card.State = sc.State;
    card.LastReviewedAt = sc.LastReviewedAt;
}
```

- [ ] **Step 6: Verify it compiles**

Run:
```bash
dotnet build fasolt.Server
```

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Application/Services/DeckSnapshotService.cs
git commit -m "feat: add snapshot list, diff, and restore logic"
```

---

## Task 5: REST Endpoints

**Files:**
- Create: `fasolt.Server/Api/Endpoints/SnapshotEndpoints.cs`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Create SnapshotEndpoints**

Create `fasolt.Server/Api/Endpoints/SnapshotEndpoints.cs`:

```csharp
using System.Security.Claims;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Fasolt.Server.Api.Endpoints;

public static class SnapshotEndpoints
{
    public static void MapSnapshotEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        group.MapPost("/snapshots", CreateAll);
        group.MapGet("/snapshots/recent", ListRecent); // must be before {id} to avoid route conflict
        group.MapGet("/decks/{deckId}/snapshots", ListByDeck);
        group.MapGet("/snapshots/{id}", GetById);
        group.MapGet("/snapshots/{id}/diff", GetDiff);
        group.MapPost("/snapshots/{id}/restore", Restore);
    }

    private static async Task<IResult> CreateAll(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var count = await snapshotService.CreateAll(user.Id);
        return Results.Ok(new SnapshotCreateResult(count));
    }

    private static async Task<IResult> ListRecent(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var list = await snapshotService.ListRecent(user.Id);
        return Results.Ok(list);
    }

    private static async Task<IResult> ListByDeck(
        string deckId,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var list = await snapshotService.ListByDeck(user.Id, deckId);
        return Results.Ok(list);
    }

    private static async Task<IResult> GetById(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var snapshot = await snapshotService.GetById(user.Id, id);
        return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
    }

    private static async Task<IResult> GetDiff(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var diff = await snapshotService.ComputeDiff(user.Id, id);
        return diff is null ? Results.NotFound() : Results.Ok(diff);
    }

    private static async Task<IResult> Restore(
        string id,
        RestoreRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSnapshotService snapshotService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var success = await snapshotService.Restore(user.Id, id, request);
        return success ? Results.Ok() : Results.NotFound();
    }
}
```

- [ ] **Step 2: Register endpoints in Program.cs**

In `fasolt.Server/Program.cs`, add alongside other endpoint mappings:

```csharp
app.MapSnapshotEndpoints();
```

- [ ] **Step 3: Verify it compiles**

Run:
```bash
dotnet build fasolt.Server
```

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/SnapshotEndpoints.cs fasolt.Server/Program.cs
git commit -m "feat: add snapshot REST endpoints"
```

---

## Task 6: MCP Tools

**Files:**
- Create: `fasolt.Server/Api/McpTools/SnapshotTools.cs`

- [ ] **Step 1: Create SnapshotTools**

Create `fasolt.Server/Api/McpTools/SnapshotTools.cs`:

```csharp
using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Api.Mcp;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class SnapshotTools(DeckSnapshotService snapshotService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("Create snapshots of all decks as backups. Captures full card state including content and FSRS data. Keeps last 10 snapshots per deck.")]
    public async Task<string> CreateSnapshot()
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var count = await snapshotService.CreateAll(userId);
        return JsonSerializer.Serialize(new { snapshotsCreated = count }, McpJson.Options);
    }

    [McpServerTool, Description("List deck snapshots. Without deckId, lists the 50 most recent across all decks.")]
    public async Task<string> ListSnapshots(
        [Description("Optional deck ID to filter snapshots for a specific deck")] string? deckId = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = deckId is not null
            ? await snapshotService.ListByDeck(userId, deckId)
            : await snapshotService.ListRecent(userId);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run:
```bash
dotnet build fasolt.Server
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/McpTools/SnapshotTools.cs
git commit -m "feat: add snapshot MCP tools (CreateSnapshot, ListSnapshots)"
```

---

## Task 7: Manual Backend Verification

**Files:** None (testing only)

- [ ] **Step 1: Start the backend**

Run:
```bash
./dev.sh
```

- [ ] **Step 2: Create test data and verify snapshot creation**

Log in as dev user and create a snapshot via curl:

```bash
# Get auth cookie (adjust if needed based on existing auth flow)
# Then create snapshot
curl -s -b cookies.txt http://localhost:8080/api/snapshots -X POST | jq .
```

Expected: `{"count": N}` where N is the number of non-empty decks.

- [ ] **Step 3: Verify list endpoint**

```bash
# Replace DECK_ID with an actual deck public ID
curl -s -b cookies.txt http://localhost:8080/api/decks/DECK_ID/snapshots | jq .
```

Expected: Array of snapshot objects with id, deckName, cardCount, createdAt.

- [ ] **Step 4: Verify diff endpoint**

```bash
# Replace SNAPSHOT_ID with an actual snapshot public ID
curl -s -b cookies.txt http://localhost:8080/api/snapshots/SNAPSHOT_ID/diff | jq .
```

Expected: Object with `deleted`, `modified`, `added` arrays (all empty if nothing changed since snapshot).

- [ ] **Step 5: Commit any fixes if needed**

---

## Task 8: Frontend Types + Store

**Files:**
- Modify: `fasolt.client/src/types/index.ts`
- Create: `fasolt.client/src/stores/snapshots.ts`

- [ ] **Step 1: Add snapshot types**

In `fasolt.client/src/types/index.ts`, add:

```typescript
export interface DeckSnapshot {
  id: string
  deckName: string | null
  cardCount: number
  createdAt: string
}

export interface SnapshotDiff {
  deleted: DiffDeletedCard[]
  modified: DiffModifiedCard[]
  added: DiffAddedCard[]
}

export interface DiffDeletedCard {
  cardId: string
  front: string
  back: string
  sourceFile: string | null
  stability: number | null
  dueAt: string | null
}

export interface DiffModifiedCard {
  cardId: string
  front: string
  currentFront: string
  back: string
  currentBack: string
  snapshotStability: number | null
  currentStability: number | null
  hasContentChanges: boolean
  hasFsrsChanges: boolean
}

export interface DiffAddedCard {
  cardId: string
  front: string
  back: string
}
```

- [ ] **Step 2: Create snapshots store**

Create `fasolt.client/src/stores/snapshots.ts`:

```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { DeckSnapshot, SnapshotDiff } from '@/types'
import { apiFetch } from '@/api/client'

export const useSnapshotsStore = defineStore('snapshots', () => {
  const snapshots = ref<DeckSnapshot[]>([])
  const loading = ref(false)

  async function createAll(): Promise<{ count: number }> {
    return apiFetch<{ count: number }>('/snapshots', { method: 'POST' })
  }

  async function fetchByDeck(deckId: string) {
    loading.value = true
    try {
      snapshots.value = await apiFetch<DeckSnapshot[]>(`/decks/${deckId}/snapshots`)
    } finally {
      loading.value = false
    }
  }

  async function getDiff(snapshotId: string): Promise<SnapshotDiff> {
    return apiFetch<SnapshotDiff>(`/snapshots/${snapshotId}/diff`)
  }

  async function restore(snapshotId: string, restoreDeletedCardIds: string[], revertModifiedCardIds: string[]) {
    await apiFetch(`/snapshots/${snapshotId}/restore`, {
      method: 'POST',
      body: JSON.stringify({ restoreDeletedCardIds, revertModifiedCardIds }),
    })
  }

  return { snapshots, loading, createAll, fetchByDeck, getDiff, restore }
})
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/types/index.ts fasolt.client/src/stores/snapshots.ts
git commit -m "feat: add snapshot types and Pinia store"
```

---

## Task 9: Snapshots List Page

**Files:**
- Create: `fasolt.client/src/views/DeckSnapshotsView.vue`
- Modify: `fasolt.client/src/router/index.ts`

- [ ] **Step 1: Add route**

In `fasolt.client/src/router/index.ts`, add after the `deck-detail` route:

```typescript
{
  path: '/decks/:id/snapshots',
  name: 'deck-snapshots',
  component: () => import('@/views/DeckSnapshotsView.vue'),
},
```

- [ ] **Step 2: Create DeckSnapshotsView**

Create `fasolt.client/src/views/DeckSnapshotsView.vue`. This page shows:

- Back link to deck detail
- Deck name as heading
- List of snapshots (date, card count, Restore button per row)
- Empty state if no snapshots

Use the same layout patterns as DeckDetailView (container, headings, table/list structure). Use shadcn-vue `Button`, `Card` components consistent with the rest of the app.

The Restore button on each row opens the RestoreDialog component (built in next task).

- [ ] **Step 3: Verify it renders**

Start the frontend (`cd fasolt.client && npm run dev`), navigate to `/decks/{some-deck-id}/snapshots`. Should show the page (empty list is fine if no snapshots exist yet).

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/views/DeckSnapshotsView.vue fasolt.client/src/router/index.ts
git commit -m "feat: add deck snapshots list page"
```

---

## Task 10: Restore Dialog

**Files:**
- Create: `fasolt.client/src/components/RestoreDialog.vue`

- [ ] **Step 1: Create RestoreDialog component**

Create `fasolt.client/src/components/RestoreDialog.vue`.

This is a dialog/modal triggered from DeckSnapshotsView when clicking "Restore" on a snapshot. It:

1. Receives `snapshotId` and `deckId` as props, plus `open` model
2. On open, fetches diff via `snapshotsStore.getDiff(snapshotId)`
3. Shows three sections:
   - **Deleted since snapshot** (red badge) — checkboxes per card, checked by default. Shows front text, source, stability, due date.
   - **Modified since snapshot** (amber badge) — checkboxes per card, unchecked by default. Shows before/after text diff, stability changes.
   - **Added since snapshot** (green badge) — info-only text, no checkboxes.
4. Footer: selected count, Cancel button, "Restore Selected" button
5. On submit: calls `snapshotsStore.restore()` with selected card IDs, emits `restored` event

Use shadcn-vue `Dialog`, `DialogContent`, `DialogHeader`, `DialogFooter`, `Checkbox`, `Button`, `Badge` components. Follow the mockup from the brainstorming session.

- [ ] **Step 2: Wire RestoreDialog into DeckSnapshotsView**

Import and add `<RestoreDialog>` to DeckSnapshotsView. Pass the selected snapshot ID. On `restored` event, refresh the deck detail or navigate back.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/components/RestoreDialog.vue fasolt.client/src/views/DeckSnapshotsView.vue
git commit -m "feat: add restore dialog with diff display and selective restore"
```

---

## Task 11: Wire Snapshot Button into Existing Pages

**Files:**
- Modify: `fasolt.client/src/views/DeckDetailView.vue`
- Modify: `fasolt.client/src/views/StudyView.vue`

- [ ] **Step 1: Add Snapshots button to DeckDetailView header**

In `fasolt.client/src/views/DeckDetailView.vue`, add a "Snapshots" button in the header actions area (alongside Edit/Delete). It should use `router.push({ name: 'deck-snapshots', params: { id: deck.id } })`.

Use a `Button` with `variant="outline"` to match the existing action buttons. Use an appropriate icon (e.g., `Camera` or `History` from lucide-vue-next).

- [ ] **Step 2: Add Create Snapshot button to StudyView**

In `fasolt.client/src/views/StudyView.vue` (the dashboard), add a "Create Snapshot" button. When clicked:

```typescript
import { useSnapshotsStore } from '@/stores/snapshots'
import { useToast } from '@/components/ui/toast' // or however toasts work in this app

const snapshots = useSnapshotsStore()

async function handleCreateSnapshot() {
  const result = await snapshots.createAll()
  // show success toast: `Created ${result.count} snapshot(s)`
}
```

Place the button in a logical location — near the top actions area or in a "Backup" section.

- [ ] **Step 3: Verify both buttons work**

- Navigate to deck detail, click Snapshots → should go to snapshots page
- On dashboard, click Create Snapshot → should show success toast

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/views/DeckDetailView.vue fasolt.client/src/views/StudyView.vue
git commit -m "feat: add snapshot buttons to deck detail and dashboard"
```

---

## Task 12: Playwright E2E Tests

**Files:** None created (uses Playwright MCP)

- [ ] **Step 1: Ensure full stack is running**

```bash
./dev.sh
```

- [ ] **Step 2: Test snapshot creation from dashboard**

Using Playwright MCP:
1. Navigate to login, log in as dev user
2. Navigate to dashboard
3. Click "Create Snapshot" button
4. Verify success toast appears

- [ ] **Step 3: Test snapshot appears in deck's list**

1. Navigate to a deck detail page
2. Click "Snapshots" button
3. Verify snapshot list shows at least one entry with date and card count

- [ ] **Step 4: Test restore flow**

1. Navigate to a deck, note a card that exists
2. Remove that card from the deck
3. Navigate to deck snapshots
4. Click "Restore" on the snapshot taken before removal
5. Verify the diff dialog shows the removed card in the "Deleted" section, checked
6. Click "Restore Selected"
7. Verify the card is back in the deck

- [ ] **Step 5: Test empty state**

1. Create a new deck with a card
2. Navigate to its snapshots page
3. Verify empty state message is shown (no snapshots taken yet)

- [ ] **Step 6: Commit any fixes**

---

## Task 13: iOS — Snapshot DTO + ViewModel

**Files:**
- Modify: `fasolt.ios/Fasolt/Models/APIModels.swift`
- Create: `fasolt.ios/Fasolt/ViewModels/SnapshotViewModel.swift`

- [ ] **Step 1: Add DeckSnapshotDTO**

In `fasolt.ios/Fasolt/Models/APIModels.swift`, add:

```swift
// MARK: - Snapshots
struct DeckSnapshotDTO: Decodable, Sendable, Identifiable {
    let id: String
    let deckName: String?
    let cardCount: Int
    let createdAt: String
}

struct SnapshotCreateResultDTO: Decodable, Sendable {
    let count: Int
}
```

- [ ] **Step 2: Create SnapshotViewModel**

Create `fasolt.ios/Fasolt/ViewModels/SnapshotViewModel.swift`:

```swift
import Foundation

@Observable
final class SnapshotViewModel {
    private let apiClient: APIClient

    var snapshots: [DeckSnapshotDTO] = []
    var isLoading = false
    var isCreating = false
    var errorMessage: String?
    var createSuccessCount: Int?

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    func createSnapshot() async {
        isCreating = true
        createSuccessCount = nil
        errorMessage = nil
        do {
            let result: SnapshotCreateResultDTO = try await apiClient.request(
                .post("/api/snapshots"))
            createSuccessCount = result.count
            await loadSnapshots()
        } catch {
            errorMessage = error.localizedDescription
        }
        isCreating = false
    }

    func loadSnapshots() async {
        isLoading = true
        do {
            // List recent across all decks
            snapshots = try await apiClient.request(
                .get("/api/snapshots/recent"))
        } catch {
            errorMessage = error.localizedDescription
        }
        isLoading = false
    }
}
```

Note: Check the exact `Endpoint` enum pattern used in the iOS app and adapt the `.post`/`.get` calls accordingly. The APIClient uses an `Endpoint` type — match that pattern.

- [ ] **Step 3: Verify it compiles**

Open Xcode or run `xcodebuild` to verify no compile errors.

- [ ] **Step 4: Commit**

```bash
git add fasolt.ios/Fasolt/Models/APIModels.swift fasolt.ios/Fasolt/ViewModels/SnapshotViewModel.swift
git commit -m "feat(ios): add snapshot DTO and ViewModel"
```

---

## Task 14: iOS — Settings Snapshots Section

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Settings/SettingsView.swift`

- [ ] **Step 1: Add Snapshots section to SettingsView**

In `fasolt.ios/Fasolt/Views/Settings/SettingsView.swift`, add a new `Section("Snapshots")` with:

1. **Create Snapshot button** — calls `snapshotViewModel.createSnapshot()`. Shows loading indicator while creating. On success, shows alert with count.
2. **Snapshot history list** — shows `snapshotViewModel.snapshots` grouped or listed by deck name, each showing date and card count. Use `.task { await snapshotViewModel.loadSnapshots() }` to load on appear.
3. **Footer text** — "To restore a snapshot, visit the web app."

Initialize `SnapshotViewModel` the same way other ViewModels are initialized in the iOS app (check the pattern — likely passed in via init or created with the APIClient).

- [ ] **Step 2: Verify in Xcode/simulator**

Build and run in iOS simulator. Navigate to Settings, verify the Snapshots section appears with the create button and the restore note.

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Settings/SettingsView.swift
git commit -m "feat(ios): add snapshots section to settings"
```

---

## Task 15: Final E2E Verification

- [ ] **Step 1: Full stack smoke test**

Start `./dev.sh`, log in as dev user.

- [ ] **Step 2: Create snapshot from dashboard, verify toast**

- [ ] **Step 3: Navigate to a deck → Snapshots → verify list**

- [ ] **Step 4: Modify a card, delete a card, then restore from snapshot → verify diff dialog → restore → verify cards restored**

- [ ] **Step 5: Test MCP tools**

Use the Fasolt MCP tools to verify:
```
CreateSnapshot → should return count
ListSnapshots → should show recent snapshots
ListSnapshots(deckId) → should show snapshots for that deck
```

- [ ] **Step 6: Move requirement to done**

```bash
mv docs/requirements/19_deck_snapshots.md docs/requirements/done/
git add docs/requirements/
git commit -m "docs: move deck snapshots requirement to done"
```
