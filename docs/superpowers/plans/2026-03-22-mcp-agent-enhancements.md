# MCP Agent Enhancements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three MCP tools (UpdateCard, DeleteCardsBySource, GetOverview) to improve the agent workflow experience.

**Architecture:** Extend existing CardService and CardTools with UpdateCard and DeleteCardsBySource. Add new OverviewService + OverviewTools for the GetOverview tool. All follow the established pattern: MCP tool → service → EF Core query.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, ModelContextProtocol, xUnit + FluentAssertions

**Spec:** `docs/superpowers/specs/2026-03-22-mcp-agent-enhancements-design.md`

---

### Task 1: UpdateCard — Service Layer

**Files:**
- Modify: `fasolt.Server/Application/Services/CardService.cs`
- Modify: `fasolt.Server/Application/Dtos/CardDtos.cs`
- Test: `fasolt.Tests/CardServiceTests.cs`

- [ ] **Step 1: Add UpdateCardFieldsRequest DTO**

In `CardDtos.cs`, add:

```csharp
public record UpdateCardFieldsRequest(
    string? NewFront = null,
    string? NewBack = null,
    string? NewSourceFile = null,
    string? NewSourceHeading = null);

public enum UpdateCardStatus { Success, NotFound, Collision }

public record UpdateCardResult(UpdateCardStatus Status, CardDto? Card = null)
{
    public static UpdateCardResult Success(CardDto card) => new(UpdateCardStatus.Success, card);
    public static UpdateCardResult NotFound() => new(UpdateCardStatus.NotFound);
    public static UpdateCardResult Collision() => new(UpdateCardStatus.Collision);
}
```

- [ ] **Step 2: Write failing test — update by ID preserves SRS state**

In `CardServiceTests.cs`, add:

```csharp
[Fact]
public async Task UpdateCardFields_ById_PreservesSrsState()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    var card = await svc.CreateCard(UserId, "Old front", "Old back", "notes.md", "Heading");

    // Simulate some SRS state by updating directly
    var entity = await db.Cards.FindAsync(card.Id);
    entity!.EaseFactor = 2.1;
    entity.Interval = 10;
    entity.Repetitions = 3;
    entity.State = "review";
    await db.SaveChangesAsync();

    var result = await svc.UpdateCardFields(UserId, card.Id,
        new UpdateCardFieldsRequest(NewFront: "New front", NewBack: "New back"));

    result.Status.Should().Be(UpdateCardStatus.Success);
    result.Card!.Front.Should().Be("New front");
    result.Card.Back.Should().Be("New back");

    // Verify SRS state preserved
    await using var db2 = _db.CreateDbContext();
    var reloaded = await db2.Cards.FindAsync(card.Id);
    reloaded!.EaseFactor.Should().Be(2.1);
    reloaded.Interval.Should().Be(10);
    reloaded.Repetitions.Should().Be(3);
    reloaded.State.Should().Be("review");
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test fasolt.Tests --filter "UpdateCardFields_ById_PreservesSrsState" -v n`
Expected: FAIL — `UpdateCardFields` method does not exist.

- [ ] **Step 4: Implement UpdateCardFields in CardService**

In `CardService.cs`, add:

```csharp
public async Task<UpdateCardResult> UpdateCardFields(string userId, Guid cardId, UpdateCardFieldsRequest req)
{
    var card = await db.Cards
        .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
        .FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId);

    if (card is null) return UpdateCardResult.NotFound();

    return await ApplyCardFieldUpdates(userId, card, req);
}

public async Task<UpdateCardResult> UpdateCardByNaturalKey(string userId, string sourceFile, string front, UpdateCardFieldsRequest req)
{
    var card = await db.Cards
        .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
        .FirstOrDefaultAsync(c => c.UserId == userId
            && c.SourceFile != null
            && c.SourceFile.ToLower() == sourceFile.ToLower()
            && c.Front.ToLower() == front.ToLower());

    if (card is null) return UpdateCardResult.NotFound();

    return await ApplyCardFieldUpdates(userId, card, req);
}

private async Task<UpdateCardResult> ApplyCardFieldUpdates(string userId, Card card, UpdateCardFieldsRequest req)
{
    var effectiveFront = req.NewFront?.Trim() ?? card.Front;
    var effectiveSourceFile = req.NewSourceFile?.Trim() ?? card.SourceFile;

    // Check for natural key collision if front or sourceFile is changing
    if (effectiveFront != card.Front || effectiveSourceFile != card.SourceFile)
    {
        if (effectiveSourceFile is not null)
        {
            var collision = await db.Cards.AnyAsync(c =>
                c.UserId == userId
                && c.Id != card.Id
                && c.SourceFile != null
                && c.SourceFile.ToLower() == effectiveSourceFile.ToLower()
                && c.Front.ToLower() == effectiveFront.ToLower());

            if (collision) return UpdateCardResult.Collision();
        }
    }

    if (req.NewFront is not null) card.Front = req.NewFront.Trim();
    if (req.NewBack is not null) card.Back = req.NewBack.Trim();
    if (req.NewSourceFile is not null) card.SourceFile = req.NewSourceFile.Trim();
    if (req.NewSourceHeading is not null) card.SourceHeading = req.NewSourceHeading.Trim();

    await db.SaveChangesAsync();

    return UpdateCardResult.Success(ToDto(card));
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test fasolt.Tests --filter "UpdateCardFields_ById_PreservesSrsState" -v n`
Expected: PASS

- [ ] **Step 6: Write failing test — update by natural key (case-insensitive)**

```csharp
[Fact]
public async Task UpdateCardByNaturalKey_CaseInsensitive()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    await svc.CreateCard(UserId, "What is DNA?", "Deoxyribonucleic acid", "biology.md", "Basics");

    // Look up with different casing
    var result = await svc.UpdateCardByNaturalKey(UserId, "Biology.MD", "what is dna?",
        new UpdateCardFieldsRequest(NewBack: "Updated answer"));

    result.Status.Should().Be(UpdateCardStatus.Success);
    result.Card!.Back.Should().Be("Updated answer");
    result.Card.Front.Should().Be("What is DNA?"); // original casing preserved
}
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test fasolt.Tests --filter "UpdateCardByNaturalKey_CaseInsensitive" -v n`
Expected: PASS (implementation already handles this)

- [ ] **Step 8: Write failing test — collision detection**

```csharp
[Fact]
public async Task UpdateCardFields_RejectsCollision()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    await svc.CreateCard(UserId, "Existing front", "Back A", "notes.md", null);
    var cardB = await svc.CreateCard(UserId, "Other front", "Back B", "notes.md", null);

    // Try to rename cardB's front to collide with existing card
    var result = await svc.UpdateCardFields(UserId, cardB.Id,
        new UpdateCardFieldsRequest(NewFront: "Existing front"));

    result.Status.Should().Be(UpdateCardStatus.Collision);
    result.Card.Should().BeNull();
}
```

- [ ] **Step 9: Run test to verify it passes**

Run: `dotnet test fasolt.Tests --filter "UpdateCardFields_RejectsCollision" -v n`
Expected: PASS

- [ ] **Step 10: Write test — sourceFile-only change collision**

```csharp
[Fact]
public async Task UpdateCardFields_SourceFileChangeCollision()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    await svc.CreateCard(UserId, "Same front", "Back A", "notes.md", null);
    var cardB = await svc.CreateCard(UserId, "Same front", "Back B", "other.md", null);

    // Move cardB to notes.md — collides with existing card
    var result = await svc.UpdateCardFields(UserId, cardB.Id,
        new UpdateCardFieldsRequest(NewSourceFile: "notes.md"));

    result.Status.Should().Be(UpdateCardStatus.Collision);
}
```

- [ ] **Step 11: Run test to verify it passes**

Run: `dotnet test fasolt.Tests --filter "UpdateCardFields_SourceFileChangeCollision" -v n`
Expected: PASS

- [ ] **Step 12: Commit**

```bash
git add fasolt.Server/Application/Services/CardService.cs fasolt.Server/Application/Dtos/CardDtos.cs fasolt.Tests/CardServiceTests.cs
git commit -m "feat: add UpdateCardFields and UpdateCardByNaturalKey to CardService"
```

---

### Task 2: DeleteCardsBySource — Service Layer

**Files:**
- Modify: `fasolt.Server/Application/Services/CardService.cs`
- Test: `fasolt.Tests/CardServiceTests.cs`

- [ ] **Step 1: Write failing test — delete cards by source**

In `CardServiceTests.cs`, add:

```csharp
[Fact]
public async Task DeleteCardsBySource_DeletesMatchingCards()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    await svc.CreateCard(UserId, "Q1", "A1", "target.md", null);
    await svc.CreateCard(UserId, "Q2", "A2", "target.md", null);
    await svc.CreateCard(UserId, "Q3", "A3", "other.md", null);

    var count = await svc.DeleteCardsBySource(UserId, "target.md");

    count.Should().Be(2);

    var remaining = await svc.ListCards(UserId, sourceFile: null, deckId: null, limit: null, after: null);
    remaining.Items.Should().HaveCount(1);
    remaining.Items[0].SourceFile.Should().Be("other.md");
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test fasolt.Tests --filter "DeleteCardsBySource_DeletesMatchingCards" -v n`
Expected: FAIL — method does not exist.

- [ ] **Step 3: Implement DeleteCardsBySource**

In `CardService.cs`, add:

```csharp
public async Task<int> DeleteCardsBySource(string userId, string sourceFile)
{
    return await db.Cards
        .Where(c => c.UserId == userId && c.SourceFile == sourceFile)
        .ExecuteDeleteAsync();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test fasolt.Tests --filter "DeleteCardsBySource_DeletesMatchingCards" -v n`
Expected: PASS

- [ ] **Step 5: Write test — no matches returns zero**

```csharp
[Fact]
public async Task DeleteCardsBySource_NoMatch_ReturnsZero()
{
    await using var db = _db.CreateDbContext();
    var svc = new CardService(db);

    var count = await svc.DeleteCardsBySource(UserId, "nonexistent.md");

    count.Should().Be(0);
}
```

- [ ] **Step 6: Run test to verify it passes**

Run: `dotnet test fasolt.Tests --filter "DeleteCardsBySource_NoMatch_ReturnsZero" -v n`
Expected: PASS

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Application/Services/CardService.cs fasolt.Tests/CardServiceTests.cs
git commit -m "feat: add DeleteCardsBySource to CardService"
```

---

### Task 3: UpdateCard and DeleteCardsBySource — MCP Tools

**Files:**
- Modify: `fasolt.Server/Api/McpTools/CardTools.cs`

- [ ] **Step 1: Add UpdateCard MCP tool**

In `CardTools.cs`, add to the constructor parameters: inject `CardService` is already there. Add:

```csharp
[McpServerTool, Description("Update an existing card's text or source metadata. Preserves all review/SRS history. Look up by card ID, or by sourceFile + front (case-insensitive).")]
public async Task<string> UpdateCard(
    [Description("Card ID (provide this or sourceFile + front)")] Guid? cardId = null,
    [Description("Source file for natural key lookup (with front)")] string? sourceFile = null,
    [Description("Current front text for natural key lookup (with sourceFile)")] string? front = null,
    [Description("New front text")] string? newFront = null,
    [Description("New back text")] string? newBack = null,
    [Description("New source file")] string? newSourceFile = null,
    [Description("New source heading")] string? newSourceHeading = null)
{
    var userId = McpUserResolver.GetUserId(httpContextAccessor);

    if (newFront is null && newBack is null && newSourceFile is null && newSourceHeading is null)
        return JsonSerializer.Serialize(new { error = "Provide at least one field to update (newFront, newBack, newSourceFile, newSourceHeading)" });

    var req = new UpdateCardFieldsRequest(newFront, newBack, newSourceFile, newSourceHeading);

    UpdateCardResult result;
    if (cardId.HasValue)
    {
        result = await cardService.UpdateCardFields(userId, cardId.Value, req);
    }
    else if (sourceFile is not null && front is not null)
    {
        result = await cardService.UpdateCardByNaturalKey(userId, sourceFile, front, req);
    }
    else
    {
        return JsonSerializer.Serialize(new { error = "Provide cardId or both sourceFile and front" });
    }

    return result.Status switch
    {
        UpdateCardStatus.Success => JsonSerializer.Serialize(result.Card),
        UpdateCardStatus.NotFound => JsonSerializer.Serialize(new { error = "Card not found" }),
        UpdateCardStatus.Collision => JsonSerializer.Serialize(new { error = "A card with this front text already exists for this source" }),
        _ => JsonSerializer.Serialize(new { error = "Unexpected error" }),
    };
}
```

- [ ] **Step 2: Add DeleteCardsBySource MCP tool**

In `CardTools.cs`, add:

```csharp
[McpServerTool, Description("Delete all cards from a specific source file. Use when a source file has been deleted or needs to be fully re-synced.")]
public async Task<string> DeleteCardsBySource(
    [Description("Exact source file name to match")] string sourceFile)
{
    var userId = McpUserResolver.GetUserId(httpContextAccessor);
    var count = await cardService.DeleteCardsBySource(userId, sourceFile);
    return JsonSerializer.Serialize(new { deleted = count > 0, deletedCount = count });
}
```

- [ ] **Step 3: Build to verify compilation**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/McpTools/CardTools.cs
git commit -m "feat: add UpdateCard and DeleteCardsBySource MCP tools"
```

---

### Task 4: GetOverview — Service, DTO, and MCP Tool

**Files:**
- Create: `fasolt.Server/Application/Dtos/OverviewDtos.cs`
- Create: `fasolt.Server/Application/Services/OverviewService.cs`
- Create: `fasolt.Server/Api/McpTools/OverviewTools.cs`
- Modify: `fasolt.Server/Program.cs` (DI registration)
- Test: `fasolt.Tests/OverviewServiceTests.cs`

- [ ] **Step 1: Create OverviewDto**

Create `fasolt.Server/Application/Dtos/OverviewDtos.cs`:

```csharp
namespace Fasolt.Server.Application.Dtos;

public record OverviewDto(
    int TotalCards,
    int DueCards,
    Dictionary<string, int> CardsByState,
    int TotalDecks,
    int TotalSources);
```

- [ ] **Step 2: Write failing test**

Create `fasolt.Tests/OverviewServiceTests.cs`:

```csharp
using FluentAssertions;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class OverviewServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GetOverview_ReturnsCorrectCounts()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        await cardSvc.CreateCard(UserId, "Q1", "A1", "file-a.md", null);
        await cardSvc.CreateCard(UserId, "Q2", "A2", "file-b.md", null);
        await deckSvc.CreateDeck(UserId, "Deck 1", null);

        var svc = new OverviewService(db);
        var overview = await svc.GetOverview(UserId);

        overview.TotalCards.Should().Be(2);
        overview.DueCards.Should().Be(2); // new cards have DueAt = null, which counts as due
        overview.CardsByState["new"].Should().Be(2);
        overview.CardsByState["learning"].Should().Be(0);
        overview.CardsByState["review"].Should().Be(0);
        overview.TotalDecks.Should().Be(1);
        overview.TotalSources.Should().Be(2);
    }

    [Fact]
    public async Task GetOverview_EmptyAccount()
    {
        await using var db = _db.CreateDbContext();
        var svc = new OverviewService(db);

        var overview = await svc.GetOverview(UserId);

        overview.TotalCards.Should().Be(0);
        overview.DueCards.Should().Be(0);
        overview.CardsByState["new"].Should().Be(0);
        overview.CardsByState["learning"].Should().Be(0);
        overview.CardsByState["review"].Should().Be(0);
        overview.TotalDecks.Should().Be(0);
        overview.TotalSources.Should().Be(0);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test fasolt.Tests --filter "OverviewServiceTests" -v n`
Expected: FAIL — `OverviewService` does not exist.

- [ ] **Step 4: Implement OverviewService**

Create `fasolt.Server/Application/Services/OverviewService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class OverviewService(AppDbContext db)
{
    private static readonly string[] AllStates = ["new", "learning", "review"];

    public async Task<OverviewDto> GetOverview(string userId)
    {
        var now = DateTimeOffset.UtcNow;

        var totalCards = await db.Cards.CountAsync(c => c.UserId == userId);

        var dueCards = await db.Cards.CountAsync(c =>
            c.UserId == userId && (c.DueAt == null || c.DueAt <= now));

        var stateCounts = await db.Cards
            .Where(c => c.UserId == userId)
            .GroupBy(c => c.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync();

        var cardsByState = AllStates.ToDictionary(
            s => s,
            s => stateCounts.FirstOrDefault(x => x.State == s)?.Count ?? 0);

        var totalDecks = await db.Decks.CountAsync(d => d.UserId == userId);

        var totalSources = await db.Cards
            .Where(c => c.UserId == userId && c.SourceFile != null)
            .Select(c => c.SourceFile)
            .Distinct()
            .CountAsync();

        return new OverviewDto(totalCards, dueCards, cardsByState, totalDecks, totalSources);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test fasolt.Tests --filter "OverviewServiceTests" -v n`
Expected: PASS

- [ ] **Step 6: Register OverviewService in DI**

In `Program.cs`, after the existing service registrations (line ~145), add:

```csharp
builder.Services.AddScoped<OverviewService>();
```

- [ ] **Step 7: Create OverviewTools MCP tool**

Create `fasolt.Server/Api/McpTools/OverviewTools.cs`:

```csharp
using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class OverviewTools(OverviewService overviewService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("Get an overview of the user's account: total cards, due cards, cards by state, deck count, and source file count. Call this first to orient yourself.")]
    public async Task<string> GetOverview()
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await overviewService.GetOverview(userId);
        return JsonSerializer.Serialize(result);
    }
}
```

- [ ] **Step 8: Build to verify compilation**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded.

- [ ] **Step 9: Commit**

```bash
git add fasolt.Server/Application/Dtos/OverviewDtos.cs fasolt.Server/Application/Services/OverviewService.cs fasolt.Server/Api/McpTools/OverviewTools.cs fasolt.Server/Program.cs fasolt.Tests/OverviewServiceTests.cs
git commit -m "feat: add GetOverview MCP tool with OverviewService"
```

---

### Task 5: Update CLAUDE.md and Run All Tests

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update CLAUDE.md MCP tools list**

In the `### Available MCP Tools` section, add these entries:

```
- `UpdateCard` — update a card's front, back, source file, or source heading (by ID or source+front natural key); preserves SRS history
- `DeleteCardsBySource` — delete all cards from a specific source file
- `GetOverview` — get account overview: total cards, due cards, cards by state, deck and source counts
```

- [ ] **Step 2: Run full test suite**

Run: `dotnet test fasolt.Tests -v n`
Expected: All tests pass.

- [ ] **Step 3: Start full stack and test MCP tools via Playwright**

Start: `./dev.sh` (in background)
Test the MCP endpoint works by verifying the server starts without errors.

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md with new MCP tools"
```
