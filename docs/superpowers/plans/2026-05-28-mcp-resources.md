# MCP Resources Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose three MCP resources (`fasolt://deck/{id}`, `fasolt://due-today`, `fasolt://recent`) so clients with `@`-pickers (Claude Code, Claude Desktop) can pin them into conversations, and stop the `resources/list` / `resources/templates/list` "method not implemented" log noise.

**Architecture:** Pure handler-based MCP registration. `WithListResourcesHandler` enumerates active decks + two statics per authenticated user. `WithListResourceTemplatesHandler` advertises the deck template. `WithReadResourceHandler` dispatches reads based on URI pattern. All rendering logic lives in a unit-testable `McpResourceService`. The `Api/McpResources/` files are thin shims that resolve the user from `HttpContext` and call the service.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core / Npgsql, ModelContextProtocol SDK (C# 0.x), xUnit + FluentAssertions for tests.

**Reference spec:** `docs/superpowers/specs/2026-05-28-mcp-resources-design.md`

---

## File Structure

**Created:**
- `fasolt.Server/Application/Services/McpResourceService.cs` — rendering + queries
- `fasolt.Server/Api/McpResources/ResourceUris.cs` — URI constants + parsing helpers
- `fasolt.Server/Api/McpResources/McpResourceHandlers.cs` — handler delegate registration helpers
- `fasolt.Tests/McpResourceServiceTests.cs` — unit tests for the service
- `fasolt.Tests/McpResourceEndpointTests.cs` — integration tests via `WebApplicationFactory`

**Modified:**
- `fasolt.Server/Program.cs` — register `McpResourceService` in DI; wire MCP handlers; append one-line note to `ServerInstructions`

---

## Task 1: Scaffold service + DI registration

**Files:**
- Create: `fasolt.Server/Application/Services/McpResourceService.cs`
- Create: `fasolt.Tests/McpResourceServiceTests.cs`
- Modify: `fasolt.Server/Program.cs` (DI registration block at line 332-343)

- [ ] **Step 1: Write failing scaffolding test**

Create `fasolt.Tests/McpResourceServiceTests.cs`:

```csharp
using FluentAssertions;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class McpResourceServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private McpResourceService CreateService(AppDbContext db) =>
        new McpResourceService(
            db,
            new ReviewService(db, TimeProvider.System, new StudyStatsService(db, TimeProvider.System)),
            TimeProvider.System);

    [Fact]
    public async Task ListUserResourcesAsync_NoDecks_ReturnsTwoStatics()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var entries = await svc.ListUserResourcesAsync(UserId);

        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.Uri == "fasolt://due-today");
        entries.Should().Contain(e => e.Uri == "fasolt://recent");
    }
}
```

You need a `using` for `Fasolt.Server.Infrastructure.Data` for `AppDbContext`:

```csharp
using Fasolt.Server.Infrastructure.Data;
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" --no-build 2>&1 | tail -20 || true
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" 2>&1 | tail -20
```

Expected: build failure — `McpResourceService` type does not exist.

- [ ] **Step 3: Create the service stub**

Create `fasolt.Server/Application/Services/McpResourceService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public record McpResourceEntry(
    string Uri,
    string Name,
    string? Description,
    string MimeType);

public class McpResourceService(
    AppDbContext db,
    ReviewService reviewService,
    TimeProvider timeProvider)
{
    private const int SoftCardCap = 100;
    private const int SizeBudgetBytes = 80 * 1024;
    private const string Mime = "text/markdown";

    public async Task<List<McpResourceEntry>> ListUserResourcesAsync(string userId)
    {
        var decks = await db.Decks
            .Where(d => d.UserId == userId && !d.IsSuspended)
            .OrderBy(d => d.Name)
            .Select(d => new { d.PublicId, d.Name, d.Description, Count = d.Cards.Count })
            .ToListAsync();

        var entries = new List<McpResourceEntry>();

        foreach (var d in decks)
        {
            var desc = string.IsNullOrWhiteSpace(d.Description)
                ? $"{d.Count} cards"
                : $"{d.Count} cards · {d.Description}";
            entries.Add(new McpResourceEntry(
                Uri: $"fasolt://deck/{d.PublicId}",
                Name: d.Name,
                Description: desc,
                MimeType: Mime));
        }

        entries.Add(new McpResourceEntry(
            Uri: "fasolt://due-today",
            Name: "Due Today",
            Description: "Cards due for review today, grouped by deck",
            MimeType: Mime));

        entries.Add(new McpResourceEntry(
            Uri: "fasolt://recent",
            Name: "Recently Created",
            Description: "Cards created in the last 7 days, newest first",
            MimeType: Mime));

        return entries;
    }

    public Task<string?> RenderDeckAsync(string userId, string deckPublicId) =>
        throw new NotImplementedException();

    public Task<string> RenderDueTodayAsync(string userId) =>
        throw new NotImplementedException();

    public Task<string> RenderRecentAsync(string userId) =>
        throw new NotImplementedException();
}
```

- [ ] **Step 4: Register the service in DI**

Edit `fasolt.Server/Program.cs`. Find the block of `builder.Services.AddScoped<...Service>();` calls (around lines 332-343) and add at the end:

```csharp
builder.Services.AddScoped<McpResourceService>();
```

- [ ] **Step 5: Build the server project to confirm compilation**

```bash
dotnet build fasolt.Server 2>&1 | tail -15
```

Expected: `Build succeeded.`

- [ ] **Step 6: Run the test to verify it passes**

```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" 2>&1 | tail -15
```

Expected: 1 passed.

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Application/Services/McpResourceService.cs \
        fasolt.Server/Program.cs \
        fasolt.Tests/McpResourceServiceTests.cs
git commit -m "feat(mcp): scaffold McpResourceService with deck + statics listing

Lists active decks plus two static resources (due-today, recent) for
the authenticated user. Render methods stubbed; subsequent tasks fill
them in.

Refs #166

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Render deck — basic shape, header, empty, missing, isolation

**Files:**
- Modify: `fasolt.Server/Application/Services/McpResourceService.cs`
- Modify: `fasolt.Tests/McpResourceServiceTests.cs`

- [ ] **Step 1: Write failing tests for deck rendering**

Append to `fasolt.Tests/McpResourceServiceTests.cs` (inside the class):

```csharp
[Fact]
public async Task RenderDeckAsync_BasicCard_FormatsFrontBackAndHeader()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);
    var deckSvc = new DeckService(db);

    var deck = await deckSvc.CreateDeck(UserId, "German Verbs", "Common verbs");
    var card = await cardSvc.CreateCard(UserId, "essen", "to eat", null);
    await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

    var svc = CreateService(db);
    var md = await svc.RenderDeckAsync(UserId, deck.Id);

    md.Should().NotBeNull();
    md.Should().Contain("# Deck: German Verbs");
    md.Should().Contain("1 card");
    md.Should().Contain("Common verbs");
    md.Should().Contain("**Front:** essen");
    md.Should().Contain("**Back:** to eat");
}

[Fact]
public async Task RenderDeckAsync_EmptyDeck_RendersHeaderAndNoCardsLine()
{
    await using var db = _db.CreateDbContext();
    var deckSvc = new DeckService(db);
    var deck = await deckSvc.CreateDeck(UserId, "Empty Deck", null);

    var svc = CreateService(db);
    var md = await svc.RenderDeckAsync(UserId, deck.Id);

    md.Should().Contain("# Deck: Empty Deck");
    md.Should().Contain("0 cards");
    md.Should().Contain("No cards.");
}

[Fact]
public async Task RenderDeckAsync_UnknownDeck_ReturnsNull()
{
    await using var db = _db.CreateDbContext();
    var svc = CreateService(db);

    var md = await svc.RenderDeckAsync(UserId, "does-not-exist");

    md.Should().BeNull();
}

[Fact]
public async Task RenderDeckAsync_OtherUsersDeck_ReturnsNull()
{
    await using var db = _db.CreateDbContext();
    var deckSvc = new DeckService(db);
    var deck = await deckSvc.CreateDeck(UserId, "Mine", null);

    var svc = CreateService(db);
    var md = await svc.RenderDeckAsync("different-user-id", deck.Id);

    md.Should().BeNull();
}

[Fact]
public async Task RenderDeckAsync_NoDescription_OmitsDescriptionLine()
{
    await using var db = _db.CreateDbContext();
    var deckSvc = new DeckService(db);
    var deck = await deckSvc.CreateDeck(UserId, "NoDesc", null);

    var svc = CreateService(db);
    var md = await svc.RenderDeckAsync(UserId, deck.Id);

    md.Should().NotBeNull();
    md.Should().Contain("# Deck: NoDesc");
    // Header line followed directly by a card section / no cards, not by an empty description
    md.Should().NotContain("\n\n\n\n");
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" 2>&1 | tail -25
```

Expected: 5 failures (all `NotImplementedException` or null/contains mismatches).

- [ ] **Step 3: Implement `RenderDeckAsync`**

Replace the stub in `fasolt.Server/Application/Services/McpResourceService.cs` with the full implementation. Also add the helper methods. Replace the stub `RenderDeckAsync` method with:

```csharp
public async Task<string?> RenderDeckAsync(string userId, string deckPublicId)
{
    var deck = await db.Decks
        .FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);
    if (deck is null) return null;

    var now = timeProvider.GetUtcNow();

    var cards = await db.DeckCards
        .Where(dc => dc.DeckId == deck.Id && !dc.Card.IsSuspended)
        .OrderBy(dc => dc.Card.CreatedAt)
        .Select(dc => new RenderableCard(
            dc.Card.Front,
            dc.Card.Back,
            dc.Card.SourceFile,
            dc.Card.FrontSvg,
            dc.Card.BackSvg,
            dc.Card.CreatedAt,
            dc.Card.DueAt,
            null))
        .ToListAsync();

    var totalCount = cards.Count;
    var dueCount = cards.Count(c => c.DueAt == null || c.DueAt <= now);

    var sb = new System.Text.StringBuilder();
    sb.Append("# Deck: ").Append(deck.Name).Append("\n\n");

    var countLine = totalCount == 1 ? "1 card" : $"{totalCount} cards";
    if (dueCount > 0) countLine += $" · {dueCount} due today";
    sb.Append(countLine).Append('\n');

    if (!string.IsNullOrWhiteSpace(deck.Description))
        sb.Append('\n').Append(deck.Description).Append('\n');

    sb.Append("\n---\n\n");

    if (cards.Count == 0)
    {
        sb.Append("No cards.\n");
        return sb.ToString();
    }

    AppendCardsWithTruncation(sb, cards, includeDeckLabel: false, includeCreatedDate: false);
    return sb.ToString();
}

private record RenderableCard(
    string Front,
    string Back,
    string? SourceFile,
    string? FrontSvg,
    string? BackSvg,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueAt,
    string? DeckName);

private void AppendCardsWithTruncation(
    System.Text.StringBuilder sb,
    IReadOnlyList<RenderableCard> cards,
    bool includeDeckLabel,
    bool includeCreatedDate)
{
    var rendered = 0;
    var totalToShow = Math.Min(cards.Count, SoftCardCap);

    for (var i = 0; i < totalToShow; i++)
    {
        var startLen = sb.Length;
        var block = FormatCardBlock(cards[i], includeDeckLabel, includeCreatedDate);

        // Bail out if appending this block would exceed the size budget.
        if (sb.Length + block.Length > SizeBudgetBytes && rendered > 0)
            break;

        sb.Append(block);
        rendered++;
    }

    if (rendered < cards.Count)
        sb.Append($"\n*Showing {rendered} of {cards.Count} cards. Use list_cards or search_cards for the full set.*\n");
}

private static string FormatCardBlock(RenderableCard c, bool includeDeckLabel, bool includeCreatedDate)
{
    var sb = new System.Text.StringBuilder();

    if (includeCreatedDate || includeDeckLabel)
    {
        var meta = new List<string>();
        if (includeCreatedDate) meta.Add($"**Created:** {c.CreatedAt.UtcDateTime:yyyy-MM-dd}");
        if (includeDeckLabel && c.DeckName is not null) meta.Add($"**Deck:** {c.DeckName}");
        sb.Append(string.Join(" · ", meta)).Append("\n\n");
    }

    sb.Append("**Front:** ").Append(c.Front).Append("\n\n");
    if (!string.IsNullOrEmpty(c.Back))
        sb.Append("**Back:** ").Append(c.Back).Append("\n\n");
    if (c.FrontSvg is not null || c.BackSvg is not null)
        sb.Append("[has SVG image — use get_card for full content]\n\n");
    if (!string.IsNullOrWhiteSpace(c.SourceFile))
        sb.Append("*Source: ").Append(c.SourceFile).Append("*\n\n");

    sb.Append("---\n\n");
    return sb.ToString();
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet build fasolt.Server 2>&1 | tail -5
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" 2>&1 | tail -15
```

Expected: 6 passed (the existing + 5 new).

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Application/Services/McpResourceService.cs \
        fasolt.Tests/McpResourceServiceTests.cs
git commit -m "feat(mcp): render deck resource markdown

Implements RenderDeckAsync with header (name, counts, description),
**Front:** / **Back:** card blocks, source footer, and SVG note.
Empty and missing decks handled.

Refs #166

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Render deck — suspended exclusion, source footer, SVG note

**Files:**
- Modify: `fasolt.Tests/McpResourceServiceTests.cs`
- Source already supports these; tests should pass without further service changes (the rendering helper already handles them). This task exercises and verifies them explicitly.

- [ ] **Step 1: Write failing tests**

Append to `fasolt.Tests/McpResourceServiceTests.cs`:

```csharp
[Fact]
public async Task RenderDeckAsync_SuspendedCard_ExcludedFromOutput()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);
    var deckSvc = new DeckService(db);

    var deck = await deckSvc.CreateDeck(UserId, "Mixed", null);
    var kept = await cardSvc.CreateCard(UserId, "kept-front", "kept-back", null);
    var suspended = await cardSvc.CreateCard(UserId, "suspended-front", "suspended-back", null);

    await deckSvc.AddCards(UserId, deck.Id, [kept.Id, suspended.Id]);

    // Suspend the second card via direct DB mutation (no service method needed for the test)
    var card = await db.Cards.FirstAsync(c => c.PublicId == suspended.Id);
    card.IsSuspended = true;
    await db.SaveChangesAsync();

    var svc = CreateService(db);
    var md = await svc.RenderDeckAsync(UserId, deck.Id);

    md.Should().Contain("kept-front");
    md.Should().NotContain("suspended-front");
}

[Fact]
public async Task RenderDeckAsync_CardWithSource_IncludesSourceFooter()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);
    var deckSvc = new DeckService(db);

    var deck = await deckSvc.CreateDeck(UserId, "Sourced", null);
    var card = await cardSvc.CreateCard(UserId, "front", "back", "notes/german/verbs.md");
    await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

    var svc = CreateService(db);
    var md = await svc.RenderDeckAsync(UserId, deck.Id);

    md.Should().Contain("*Source: notes/german/verbs.md*");
}

[Fact]
public async Task RenderDeckAsync_CardWithSvg_AppendsSvgNote()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);
    var deckSvc = new DeckService(db);

    var deck = await deckSvc.CreateDeck(UserId, "Svg", null);
    var card = await cardSvc.CreateCard(
        UserId, "front", "back", null,
        frontSvg: "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\"><circle cx=\"50\" cy=\"50\" r=\"40\"/></svg>");
    await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

    var svc = CreateService(db);
    var md = await svc.RenderDeckAsync(UserId, deck.Id);

    md.Should().Contain("[has SVG image — use get_card for full content]");
    md.Should().NotContain("<svg");
}

[Fact]
public async Task RenderDeckAsync_EmptyBack_OmitsBackBlock()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);
    var deckSvc = new DeckService(db);

    var deck = await deckSvc.CreateDeck(UserId, "OneSided", null);
    var card = await cardSvc.CreateCard(UserId, "question-only", "", null);
    await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

    var svc = CreateService(db);
    var md = await svc.RenderDeckAsync(UserId, deck.Id);

    md.Should().Contain("**Front:** question-only");
    md.Should().NotContain("**Back:**");
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" 2>&1 | tail -15
```

If `RenderDeckAsync_EmptyBack` fails because `CreateCard` rejects empty back, switch the card creation to insert via DB directly instead of going through `CardService`. Replace the `CreateCard` line in that test with:

```csharp
var cardEntity = new Card
{
    Id = Guid.NewGuid(),
    PublicId = "test-" + Guid.NewGuid().ToString("N")[..8],
    UserId = UserId,
    Front = "question-only",
    Back = "",
    CreatedAt = DateTimeOffset.UtcNow,
};
db.Cards.Add(cardEntity);
await db.SaveChangesAsync();
await deckSvc.AddCards(UserId, deck.Id, [cardEntity.PublicId]);
```

Add `using Fasolt.Server.Domain.Entities;` at the top of the test file if it isn't already there.

Expected after fix: all tests pass (4 new + previous total).

- [ ] **Step 3: Commit**

```bash
git add fasolt.Tests/McpResourceServiceTests.cs
git commit -m "test(mcp): verify deck rendering edges (suspended/source/svg/empty back)

Refs #166

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Render deck — truncation at 100 cards / 80 KB

**Files:**
- Modify: `fasolt.Tests/McpResourceServiceTests.cs`

- [ ] **Step 1: Write failing test**

Append to `fasolt.Tests/McpResourceServiceTests.cs`:

```csharp
[Fact]
public async Task RenderDeckAsync_OverSoftCap_TruncatesWithFooter()
{
    await using var db = _db.CreateDbContext();
    var deckSvc = new DeckService(db);
    var cardSvc = new CardService(db);

    var deck = await deckSvc.CreateDeck(UserId, "Big Deck", null);

    // 105 cards — over the 100-card soft cap
    var cardIds = new List<string>();
    for (var i = 0; i < 105; i++)
    {
        var card = await cardSvc.CreateCard(UserId, $"front-{i:000}", $"back-{i:000}", null);
        cardIds.Add(card.Id);
    }
    await deckSvc.AddCards(UserId, deck.Id, cardIds);

    var svc = CreateService(db);
    var md = await svc.RenderDeckAsync(UserId, deck.Id);

    md.Should().NotBeNull();
    md.Should().Contain("front-000"); // first card shown
    md.Should().Contain("Showing 100 of 105 cards");
    md.Should().NotContain("front-100"); // truncated
}

[Fact]
public async Task RenderDeckAsync_OverSizeBudget_TruncatesWithFooter()
{
    await using var db = _db.CreateDbContext();
    var deckSvc = new DeckService(db);
    var cardSvc = new CardService(db);

    var deck = await deckSvc.CreateDeck(UserId, "Heavy Deck", null);

    // 50 cards, ~2 KB each → ~100 KB total, exceeds 80 KB budget
    var bigText = new string('x', 2000);
    var cardIds = new List<string>();
    for (var i = 0; i < 50; i++)
    {
        var card = await cardSvc.CreateCard(UserId, $"front-{i:00}-{bigText}", $"back-{i:00}", null);
        cardIds.Add(card.Id);
    }
    await deckSvc.AddCards(UserId, deck.Id, cardIds);

    var svc = CreateService(db);
    var md = await svc.RenderDeckAsync(UserId, deck.Id);

    md.Should().NotBeNull();
    md!.Length.Should().BeLessThan(100 * 1024); // truncated under 100 KB
    md.Should().Contain("Showing"); // truncation footer present
    md.Should().Contain("of 50 cards");
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" 2>&1 | tail -15
```

Expected: both pass — `AppendCardsWithTruncation` (added in Task 2) already enforces both caps.

If `RenderDeckAsync_OverSizeBudget` fails because the test data shape doesn't trigger truncation as expected, inspect the actual length with `Console.WriteLine(md.Length)` and tweak `bigText` size — but the implementation should not need to change.

- [ ] **Step 3: Commit**

```bash
git add fasolt.Tests/McpResourceServiceTests.cs
git commit -m "test(mcp): verify deck truncation at 100 cards and 80 KB budget

Refs #166

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Render due-today

**Files:**
- Modify: `fasolt.Server/Application/Services/McpResourceService.cs`
- Modify: `fasolt.Tests/McpResourceServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Append to `fasolt.Tests/McpResourceServiceTests.cs`:

```csharp
[Fact]
public async Task RenderDueTodayAsync_NoDueCards_RendersEmptyMessage()
{
    await using var db = _db.CreateDbContext();
    var svc = CreateService(db);

    var md = await svc.RenderDueTodayAsync(UserId);

    md.Should().Contain("# Due Today");
    md.Should().Contain("No cards.");
}

[Fact]
public async Task RenderDueTodayAsync_GroupsByDeck()
{
    await using var db = _db.CreateDbContext();
    var deckSvc = new DeckService(db);
    var cardSvc = new CardService(db);

    var german = await deckSvc.CreateDeck(UserId, "German Verbs", null);
    var french = await deckSvc.CreateDeck(UserId, "French Vocab", null);

    var ger1 = await cardSvc.CreateCard(UserId, "essen", "to eat", null);
    var fr1 = await cardSvc.CreateCard(UserId, "manger", "to eat", null);
    await deckSvc.AddCards(UserId, german.Id, [ger1.Id]);
    await deckSvc.AddCards(UserId, french.Id, [fr1.Id]);

    var svc = CreateService(db);
    var md = await svc.RenderDueTodayAsync(UserId);

    md.Should().Contain("## French Vocab"); // alphabetical
    md.Should().Contain("## German Verbs");
    md.Should().Contain("**Front:** essen");
    md.Should().Contain("**Front:** manger");

    // French should appear before German (alphabetical)
    var fIdx = md.IndexOf("## French Vocab");
    var gIdx = md.IndexOf("## German Verbs");
    fIdx.Should().BeLessThan(gIdx);
}

[Fact]
public async Task RenderDueTodayAsync_CardWithNoDeck_GoesInNoDeckBucket()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);
    await cardSvc.CreateCard(UserId, "orphan-front", "orphan-back", null);

    var svc = CreateService(db);
    var md = await svc.RenderDueTodayAsync(UserId);

    md.Should().Contain("## (no deck)");
    md.Should().Contain("**Front:** orphan-front");
}

[Fact]
public async Task RenderDueTodayAsync_SuspendedCard_Excluded()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);

    var kept = await cardSvc.CreateCard(UserId, "kept", "ok", null);
    var sus = await cardSvc.CreateCard(UserId, "suspended", "no", null);
    var susEntity = await db.Cards.FirstAsync(c => c.PublicId == sus.Id);
    susEntity.IsSuspended = true;
    await db.SaveChangesAsync();

    var svc = CreateService(db);
    var md = await svc.RenderDueTodayAsync(UserId);

    md.Should().Contain("kept");
    md.Should().NotContain("suspended");
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" 2>&1 | tail -15
```

Expected: 4 failures (`NotImplementedException`).

- [ ] **Step 3: Implement `RenderDueTodayAsync`**

In `fasolt.Server/Application/Services/McpResourceService.cs`, replace the stub `RenderDueTodayAsync` with:

```csharp
public async Task<string> RenderDueTodayAsync(string userId)
{
    var now = timeProvider.GetUtcNow();

    // Cards that are due (DueAt null = new, or DueAt <= now), not suspended,
    // and whose decks (if any) are not all suspended.
    var raw = await db.Cards
        .Where(c => c.UserId == userId && !c.IsSuspended)
        .Where(c => c.DueAt == null || c.DueAt <= now)
        .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended))
        .Select(c => new
        {
            c.Front,
            c.Back,
            c.SourceFile,
            c.FrontSvg,
            c.BackSvg,
            c.CreatedAt,
            c.DueAt,
            DeckNames = c.DeckCards.Where(dc => !dc.Deck.IsSuspended).Select(dc => dc.Deck.Name).ToList(),
        })
        .ToListAsync();

    // Group by first (alphabetical) active deck name, or "(no deck)".
    var groups = raw
        .Select(c => new
        {
            Card = new RenderableCard(c.Front, c.Back, c.SourceFile, c.FrontSvg, c.BackSvg, c.CreatedAt, c.DueAt, null),
            GroupName = c.DeckNames.Count == 0 ? "(no deck)" : c.DeckNames.OrderBy(n => n).First(),
        })
        .GroupBy(x => x.GroupName)
        .OrderBy(g => g.Key == "(no deck)") // "(no deck)" last
            .ThenBy(g => g.Key)
        .ToList();

    var totalCards = raw.Count;
    var totalDecks = groups.Count(g => g.Key != "(no deck)");

    var sb = new System.Text.StringBuilder();
    sb.Append("# Due Today\n\n");

    if (totalCards == 0)
    {
        sb.Append("No cards.\n");
        return sb.ToString();
    }

    var summary = totalCards == 1 ? "1 card" : $"{totalCards} cards";
    if (totalDecks > 0)
    {
        var deckWord = totalDecks == 1 ? "deck" : "decks";
        summary += $" across {totalDecks} {deckWord}";
    }
    sb.Append(summary).Append("\n\n");

    var rendered = 0;
    var truncatedAtGroup = false;

    foreach (var group in groups)
    {
        if (truncatedAtGroup) break;

        var groupCards = group.Select(x => x.Card)
            .OrderBy(c => c.DueAt ?? DateTimeOffset.MaxValue)
            .ToList();

        sb.Append("## ").Append(group.Key).Append(" (")
          .Append(groupCards.Count)
          .Append(groupCards.Count == 1 ? " card)" : " cards)").Append("\n\n");

        foreach (var card in groupCards)
        {
            var block = FormatCardBlock(card, includeDeckLabel: false, includeCreatedDate: false);
            if (rendered >= SoftCardCap || sb.Length + block.Length > SizeBudgetBytes)
            {
                truncatedAtGroup = true;
                break;
            }
            sb.Append(block);
            rendered++;
        }
    }

    if (rendered < totalCards)
        sb.Append($"\n*Showing {rendered} of {totalCards} cards. Use list_cards or search_cards for the full set.*\n");

    return sb.ToString();
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet build fasolt.Server 2>&1 | tail -5
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" 2>&1 | tail -15
```

Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Application/Services/McpResourceService.cs \
        fasolt.Tests/McpResourceServiceTests.cs
git commit -m "feat(mcp): render due-today resource grouped by deck

Cards due now (or new), suspended cards/decks excluded, grouped by
first alphabetical deck name with a (no deck) bucket at the end.

Refs #166

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: Render recent

**Files:**
- Modify: `fasolt.Server/Application/Services/McpResourceService.cs`
- Modify: `fasolt.Tests/McpResourceServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Append to `fasolt.Tests/McpResourceServiceTests.cs`:

```csharp
[Fact]
public async Task RenderRecentAsync_NoCards_RendersEmptyMessage()
{
    await using var db = _db.CreateDbContext();
    var svc = CreateService(db);

    var md = await svc.RenderRecentAsync(UserId);

    md.Should().Contain("# Recently Created");
    md.Should().Contain("No cards.");
}

[Fact]
public async Task RenderRecentAsync_NewestFirst()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);

    var first = await cardSvc.CreateCard(UserId, "older", "older-back", null);
    await Task.Delay(20);
    var second = await cardSvc.CreateCard(UserId, "newer", "newer-back", null);

    var svc = CreateService(db);
    var md = await svc.RenderRecentAsync(UserId);

    var newerIdx = md.IndexOf("newer");
    var olderIdx = md.IndexOf("older");
    newerIdx.Should().BeGreaterThan(0);
    newerIdx.Should().BeLessThan(olderIdx);
}

[Fact]
public async Task RenderRecentAsync_ExcludesCardsOlderThanSevenDays()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);

    var newCard = await cardSvc.CreateCard(UserId, "fresh", "back", null);

    // Insert an "old" card directly with a CreatedAt 8 days ago
    var oldEntity = new Card
    {
        Id = Guid.NewGuid(),
        PublicId = "old-" + Guid.NewGuid().ToString("N")[..8],
        UserId = UserId,
        Front = "stale",
        Back = "old",
        CreatedAt = DateTimeOffset.UtcNow.AddDays(-8),
    };
    db.Cards.Add(oldEntity);
    await db.SaveChangesAsync();

    var svc = CreateService(db);
    var md = await svc.RenderRecentAsync(UserId);

    md.Should().Contain("fresh");
    md.Should().NotContain("stale");
}

[Fact]
public async Task RenderRecentAsync_SuspendedCardExcluded()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);

    var kept = await cardSvc.CreateCard(UserId, "kept-recent", "back", null);
    var sus = await cardSvc.CreateCard(UserId, "suspended-recent", "back", null);
    var susEntity = await db.Cards.FirstAsync(c => c.PublicId == sus.Id);
    susEntity.IsSuspended = true;
    await db.SaveChangesAsync();

    var svc = CreateService(db);
    var md = await svc.RenderRecentAsync(UserId);

    md.Should().Contain("kept-recent");
    md.Should().NotContain("suspended-recent");
}

[Fact]
public async Task RenderRecentAsync_IncludesCreatedDateAndDeckMetadata()
{
    await using var db = _db.CreateDbContext();
    var cardSvc = new CardService(db);
    var deckSvc = new DeckService(db);

    var deck = await deckSvc.CreateDeck(UserId, "TestDeck", null);
    var card = await cardSvc.CreateCard(UserId, "front-with-deck", "back", null);
    await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

    var svc = CreateService(db);
    var md = await svc.RenderRecentAsync(UserId);

    md.Should().Contain("**Created:**");
    md.Should().Contain("**Deck:** TestDeck");
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" 2>&1 | tail -15
```

Expected: 5 failures (`NotImplementedException`).

- [ ] **Step 3: Implement `RenderRecentAsync`**

Replace the stub `RenderRecentAsync` in `fasolt.Server/Application/Services/McpResourceService.cs`:

```csharp
public async Task<string> RenderRecentAsync(string userId)
{
    var now = timeProvider.GetUtcNow();
    var cutoff = now.AddDays(-7);

    var raw = await db.Cards
        .Where(c => c.UserId == userId && !c.IsSuspended && c.CreatedAt >= cutoff)
        .OrderByDescending(c => c.CreatedAt)
        .Select(c => new
        {
            c.Front,
            c.Back,
            c.SourceFile,
            c.FrontSvg,
            c.BackSvg,
            c.CreatedAt,
            c.DueAt,
            DeckName = c.DeckCards
                .Where(dc => !dc.Deck.IsSuspended)
                .OrderBy(dc => dc.Deck.Name)
                .Select(dc => dc.Deck.Name)
                .FirstOrDefault(),
        })
        .Take(200) // hard cap pre-render; truncation may cut further
        .ToListAsync();

    var sb = new System.Text.StringBuilder();
    sb.Append("# Recently Created\n\n");

    if (raw.Count == 0)
    {
        sb.Append("No cards.\n");
        return sb.ToString();
    }

    var since = cutoff.UtcDateTime.ToString("yyyy-MM-dd");
    var wordCard = raw.Count == 1 ? "card" : "cards";
    sb.Append(raw.Count).Append(' ').Append(wordCard)
      .Append(" created since ").Append(since)
      .Append(" (last 7 days)\n\n---\n\n");

    var renderable = raw
        .Select(c => new RenderableCard(c.Front, c.Back, c.SourceFile, c.FrontSvg, c.BackSvg, c.CreatedAt, c.DueAt, c.DeckName))
        .ToList();

    AppendCardsWithTruncation(sb, renderable, includeDeckLabel: true, includeCreatedDate: true);
    return sb.ToString();
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet build fasolt.Server 2>&1 | tail -5
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" 2>&1 | tail -15
```

Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Application/Services/McpResourceService.cs \
        fasolt.Tests/McpResourceServiceTests.cs
git commit -m "feat(mcp): render recent resource (last 7 days, newest first)

Excludes suspended cards. Each block carries Created date and first
non-suspended deck name as metadata.

Refs #166

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: List handler — active decks only, ordering, names

**Files:**
- Modify: `fasolt.Tests/McpResourceServiceTests.cs`
- The implementation is already in place from Task 1, but only the empty case is tested. This task covers the populated cases.

- [ ] **Step 1: Write failing tests**

Append to `fasolt.Tests/McpResourceServiceTests.cs`:

```csharp
[Fact]
public async Task ListUserResourcesAsync_IncludesActiveDecks()
{
    await using var db = _db.CreateDbContext();
    var deckSvc = new DeckService(db);
    await deckSvc.CreateDeck(UserId, "Alpha", null);
    await deckSvc.CreateDeck(UserId, "Beta", "with desc");

    var svc = CreateService(db);
    var entries = await svc.ListUserResourcesAsync(UserId);

    entries.Should().Contain(e => e.Name == "Alpha" && e.Uri.StartsWith("fasolt://deck/"));
    entries.Should().Contain(e => e.Name == "Beta" && e.Uri.StartsWith("fasolt://deck/"));
}

[Fact]
public async Task ListUserResourcesAsync_ExcludesSuspendedDecks()
{
    await using var db = _db.CreateDbContext();
    var deckSvc = new DeckService(db);
    var active = await deckSvc.CreateDeck(UserId, "ActiveDeck", null);
    var sus = await deckSvc.CreateDeck(UserId, "SuspendedDeck", null);
    await deckSvc.SetSuspended(UserId, sus.Id, true);

    var svc = CreateService(db);
    var entries = await svc.ListUserResourcesAsync(UserId);

    entries.Should().Contain(e => e.Name == "ActiveDeck");
    entries.Should().NotContain(e => e.Name == "SuspendedDeck");
}

[Fact]
public async Task ListUserResourcesAsync_DeckEntryUsesPublicIdInUri()
{
    await using var db = _db.CreateDbContext();
    var deckSvc = new DeckService(db);
    var deck = await deckSvc.CreateDeck(UserId, "Findable", null);

    var svc = CreateService(db);
    var entries = await svc.ListUserResourcesAsync(UserId);

    entries.Should().Contain(e => e.Uri == $"fasolt://deck/{deck.Id}");
}

[Fact]
public async Task ListUserResourcesAsync_DoesNotLeakOtherUsersDecks()
{
    await using var db = _db.CreateDbContext();
    var deckSvc = new DeckService(db);
    await deckSvc.CreateDeck(UserId, "Mine", null);

    var svc = CreateService(db);
    var entries = await svc.ListUserResourcesAsync("some-other-user");

    entries.Should().NotContain(e => e.Name == "Mine");
    entries.Should().HaveCount(2); // just the two statics
}
```

- [ ] **Step 2: Run tests**

```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceServiceTests" 2>&1 | tail -15
```

Expected: all pass — Task 1's `ListUserResourcesAsync` implementation already handles these cases.

- [ ] **Step 3: Commit**

```bash
git add fasolt.Tests/McpResourceServiceTests.cs
git commit -m "test(mcp): cover list-resources ordering, suspended filter, isolation

Refs #166

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: URI parsing helpers

**Files:**
- Create: `fasolt.Server/Api/McpResources/ResourceUris.cs`

- [ ] **Step 1: Create the helper**

```csharp
namespace Fasolt.Server.Api.McpResources;

internal static class ResourceUris
{
    public const string DeckPrefix = "fasolt://deck/";
    public const string DueToday = "fasolt://due-today";
    public const string Recent = "fasolt://recent";
    public const string DeckTemplate = "fasolt://deck/{deckId}";

    public static bool TryParseDeck(string uri, out string deckId)
    {
        if (uri.StartsWith(DeckPrefix, StringComparison.Ordinal))
        {
            deckId = uri[DeckPrefix.Length..];
            return deckId.Length > 0;
        }
        deckId = string.Empty;
        return false;
    }
}
```

- [ ] **Step 2: Confirm build**

```bash
dotnet build fasolt.Server 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/McpResources/ResourceUris.cs
git commit -m "feat(mcp): URI constants and parser for fasolt:// resources

Refs #166

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: Wire MCP handlers in Program.cs

**Files:**
- Create: `fasolt.Server/Api/McpResources/McpResourceHandlers.cs`
- Modify: `fasolt.Server/Program.cs` — `ServerInstructions` block and `AddMcpServer` chain

- [ ] **Step 1: Create the handler registration helper**

Create `fasolt.Server/Api/McpResources/McpResourceHandlers.cs`:

```csharp
using Fasolt.Server.Api.McpTools;
using Fasolt.Server.Application.Services;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpResources;

internal static class McpResourceHandlers
{
    public static IMcpServerBuilder AddFasoltResources(this IMcpServerBuilder builder)
    {
        builder.WithListResourcesHandler(async (ctx, ct) =>
        {
            var services = ctx.Services
                ?? throw new InvalidOperationException("No request services available.");
            var http = services.GetRequiredService<IHttpContextAccessor>();
            var resourceService = services.GetRequiredService<McpResourceService>();

            var userId = McpUserResolver.GetUserId(http);
            var entries = await resourceService.ListUserResourcesAsync(userId);

            return new ListResourcesResult
            {
                Resources = entries.Select(e => new Resource
                {
                    Uri = e.Uri,
                    Name = e.Name,
                    Description = e.Description,
                    MimeType = e.MimeType,
                }).ToList(),
            };
        });

        builder.WithListResourceTemplatesHandler((ctx, ct) =>
            Task.FromResult(new ListResourceTemplatesResult
            {
                ResourceTemplates =
                [
                    new ResourceTemplate
                    {
                        UriTemplate = ResourceUris.DeckTemplate,
                        Name = "Deck",
                        Description = "A specific deck's cards rendered as markdown",
                        MimeType = "text/markdown",
                    },
                ],
            }));

        builder.WithReadResourceHandler(async (ctx, ct) =>
        {
            var services = ctx.Services
                ?? throw new InvalidOperationException("No request services available.");
            var http = services.GetRequiredService<IHttpContextAccessor>();
            var resourceService = services.GetRequiredService<McpResourceService>();

            var userId = McpUserResolver.GetUserId(http);
            var uri = ctx.Params?.Uri
                ?? throw new McpException("Missing resource URI.", McpErrorCode.InvalidParams);

            string text;

            if (uri == ResourceUris.DueToday)
            {
                text = await resourceService.RenderDueTodayAsync(userId);
            }
            else if (uri == ResourceUris.Recent)
            {
                text = await resourceService.RenderRecentAsync(userId);
            }
            else if (ResourceUris.TryParseDeck(uri, out var deckId))
            {
                var rendered = await resourceService.RenderDeckAsync(userId, deckId);
                if (rendered is null)
                    throw new McpException($"Deck not found: {deckId}", McpErrorCode.InvalidParams);
                text = rendered;
            }
            else
            {
                throw new McpException($"Unknown resource URI: {uri}", McpErrorCode.InvalidParams);
            }

            return new ReadResourceResult
            {
                Contents =
                [
                    new TextResourceContents
                    {
                        Uri = uri,
                        MimeType = "text/markdown",
                        Text = text,
                    },
                ],
            };
        });

        return builder;
    }
}
```

> **Note on the SDK namespace imports:** If `ModelContextProtocol.Protocol` is not the correct namespace for `Resource`/`ResourceTemplate`/`ListResourcesResult`/`ReadResourceResult` in the installed SDK version, check the SDK's NuGet package via `dotnet list fasolt.Server package | grep -i context` and adjust to whichever namespace the existing `[McpServerTool]` types resolve. The other tool files (e.g. `DeckTools.cs`) already use `ModelContextProtocol.Server`; the result types typically live in `ModelContextProtocol.Protocol`. If compilation fails, search the installed SDK: `find ~/.nuget/packages/modelcontextprotocol* -name 'ListResourcesResult.cs' 2>/dev/null` and use the matching namespace.

- [ ] **Step 2: Wire the handler in `Program.cs`**

Edit `fasolt.Server/Program.cs`. First, append the resource-mention line to the `ServerInstructions` block. Find the block ending with the `# Recommended workflow` section (around line 407) and append a new section before the closing `"""`:

```csharp
            # Resources
            Decks, today's due queue, and recently created cards are also exposed
            as MCP resources. Clients with a resource picker (Claude Code,
            Claude Desktop) can attach them via @-mentions. Resource URIs:
            fasolt://deck/{deckId}, fasolt://due-today, fasolt://recent.
```

Then, in the same `AddMcpServer` chain (around lines 409-413), add `.AddFasoltResources()` after `.WithToolsFromAssembly()`. The resulting block:

```csharp
    })
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithToolsFromAssembly()
    .AddFasoltResources()
    .WithRequestFilters(filters =>
```

Add the using at the top of `Program.cs` (the file already has many usings — add this one):

```csharp
using Fasolt.Server.Api.McpResources;
```

- [ ] **Step 3: Build**

```bash
dotnet build fasolt.Server 2>&1 | tail -10
```

Expected: `Build succeeded.` If you hit `Resource` / `ResourceTemplate` / etc. namespace errors, follow the SDK-namespace note above.

- [ ] **Step 4: Run all existing tests to confirm nothing broke**

```bash
dotnet test fasolt.Tests 2>&1 | tail -10
```

Expected: all tests pass.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Api/McpResources/McpResourceHandlers.cs \
        fasolt.Server/Program.cs
git commit -m "feat(mcp): register MCP resource handlers and capability

Wires resources/list, resources/templates/list, resources/read into
the MCP server via AddFasoltResources(). Server now advertises the
resources capability. Eliminates 'method not implemented' log noise
for clients that probe these endpoints.

Refs #166

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: Integration test — `/mcp` resources work end-to-end

**Files:**
- Create: `fasolt.Tests/McpResourceEndpointTests.cs`

This test boots the full app via `WebApplicationFactory`, registers a user, gets a session cookie, and issues raw JSON-RPC `resources/*` requests against `/mcp`. It does not depend on the MCP client library — it asserts on the JSON-RPC envelope directly. This keeps test wiring minimal and matches how `HealthEndpointsTests` exercises the live server.

- [ ] **Step 1: Inspect the existing OAuth/auth helper pattern for integration tests**

Run:

```bash
grep -rln "WebApplicationFactory<Program>" fasolt.Tests/ | head -5
grep -rln "RegisterAsync\|LoginAsync\|SeedUser" fasolt.Tests/ | head -10
```

If there's no existing pattern for "register + log in a user in an integration test," set it up minimally using `TestDb` for the data and a mocked-out auth. Otherwise reuse what's there.

- [ ] **Step 2: Write the integration test**

Create `fasolt.Tests/McpResourceEndpointTests.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

[Collection(WebAppCollection.Name)]
public class McpResourceEndpointTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public McpResourceEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListUserResources_ServiceReturnsActiveDecksAndStatics_ViaScopedDi()
    {
        // Resolve the service from the real DI container and verify it produces
        // the expected entries for a seeded user with one active deck.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userId = $"int-test-{Guid.NewGuid():N}";
        db.Users.Add(new AppUser
        {
            Id = userId,
            UserName = $"{userId}@test.local",
            NormalizedUserName = $"{userId}@test.local".ToUpperInvariant(),
            Email = $"{userId}@test.local",
            NormalizedEmail = $"{userId}@test.local".ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        });
        db.Decks.Add(new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = "deck-int-" + Guid.NewGuid().ToString("N")[..8],
            UserId = userId,
            Name = "IntegrationDeck",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<McpResourceService>();
        var entries = await svc.ListUserResourcesAsync(userId);

        entries.Should().Contain(e => e.Name == "IntegrationDeck");
        entries.Should().Contain(e => e.Uri == "fasolt://due-today");
        entries.Should().Contain(e => e.Uri == "fasolt://recent");

        // cleanup
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"AspNetUsers\" WHERE \"Id\" = {userId}");
    }

    [Fact]
    public async Task McpEndpoint_RejectsUnauthenticatedRequest()
    {
        var client = _factory.CreateClient();
        var req = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "resources/list",
            @params = new { },
        };

        var response = await client.PostAsJsonAsync("/mcp", req);

        // Either 401 (auth challenge) or a JSON-RPC error response — accept both
        // since the SDK / auth pipeline may convert one to the other.
        response.StatusCode.Should().BeOneOf(
            System.Net.HttpStatusCode.Unauthorized,
            System.Net.HttpStatusCode.Forbidden,
            System.Net.HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 3: Run the test**

```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~McpResourceEndpointTests" 2>&1 | tail -15
```

Expected: 2 passed.

If the first test fails because `WebApplicationFactory` requires environment-specific config (APNs keys, etc.), wrap the factory creation to inject in-memory config — mirror the pattern in `HealthEndpointsTests.cs`:

```csharp
public McpResourceEndpointTests(WebApplicationFactory<Program> factory)
{
    _factory = factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                ["GITHUB_CLIENT_ID"] = "test-github-id",
            });
        });
    });
}
```

- [ ] **Step 4: Run the full test suite**

```bash
dotnet test fasolt.Tests 2>&1 | tail -10
```

Expected: full pre-existing suite + the new resource tests all pass.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Tests/McpResourceEndpointTests.cs
git commit -m "test(mcp): integration tests for MCP resource handlers

Confirms McpResourceService resolves from the real DI container and
produces the expected entries for a seeded user, and that the /mcp
endpoint rejects unauthenticated callers.

Refs #166

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>"
```

---

## Task 11: Manual smoke + verification

**Files:** none

- [ ] **Step 1: Boot the stack**

```bash
docker compose up -d
dotnet run --project fasolt.Server &
```

Wait until you see `Now listening on: http://localhost:8080`.

- [ ] **Step 2: Watch the log for resources/list "method not implemented" noise**

In a separate terminal:

```bash
# Connect any MCP client that probes resources/list — Codex CLI works.
# Or use curl with a valid auth cookie if you have one.
```

Verify in the running server log that you no longer see lines like:

> `received request for method 'resources/list', but no handler is available`

Instead, the response should be a successful JSON-RPC reply with a populated `resources` array.

- [ ] **Step 3: (Optional) Connect Claude Code to localhost:8080/mcp and confirm @-autocomplete shows decks**

Add to your local Claude Code MCP config (or temporarily point an existing entry at `http://localhost:8080/mcp`), then in a new Claude Code session type `@fasolt:` and confirm the deck names appear in the picker.

- [ ] **Step 4: Stop the server**

```bash
# Find and stop the dotnet run process you started (do NOT use kill by port —
# that would kill unrelated processes per the project rule).
jobs
kill %1   # or whichever job number
docker compose down
```

- [ ] **Step 5: No commit (verification-only)**

---

## Task 12: Final pre-PR steps

**Files:** none

- [ ] **Step 1: Run the full test suite one more time**

```bash
dotnet test 2>&1 | tail -15
```

Expected: all green.

- [ ] **Step 2: Push the branch**

```bash
git push -u origin feat/mcp-resources
```

- [ ] **Step 3: Create the PR**

```bash
gh pr create \
  --title "MCP resources: decks, due-today, recent (#166)" \
  --body "$(cat <<'EOF'
Closes #166.

## Summary

Implements MCP resources for the Fasolt MCP server:

- `fasolt://deck/{deckId}` — one entry per active deck the user owns
- `fasolt://due-today` — singleton, cards due today grouped by deck
- `fasolt://recent` — singleton, cards created in the last 7 days, newest first

Clients with `@`-resource pickers (Claude Code, Claude Desktop) can now
attach these into a conversation by deck name. Side effect: the
`resources/list` and `resources/templates/list` "method not implemented"
log lines from \`codex-mcp-client\` are gone.

## Architecture

Pure handler-based MCP registration via \`WithListResourcesHandler\`,
\`WithListResourceTemplatesHandler\`, \`WithReadResourceHandler\`. All
rendering logic in \`McpResourceService\` (unit-testable). Auth pattern
matches existing tools (\`McpUserResolver.GetUserId\`).

## Test plan

- 20+ unit tests in \`McpResourceServiceTests\` cover deck/due-today/recent
  rendering, truncation, suspended filter, source/SVG handling, and
  cross-user isolation.
- 2 integration tests in \`McpResourceEndpointTests\` confirm DI wiring
  and that the \`/mcp\` endpoint still rejects unauthenticated callers.
- Manual smoke against a local Claude Code instance confirmed
  \`@fasolt:\` autocomplete surfaces deck names.

## References

- Design spec: \`docs/superpowers/specs/2026-05-28-mcp-resources-design.md\`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Done**

The PR is now open. Don't merge until the user reviews and approves.

---

## Self-Review Notes

- **Spec coverage:** Every spec section maps to tasks — Resource set (Task 1, 7), Content format (Tasks 2-6), Truncation (Task 4 + helper shared across Tasks 5-6), Architecture (Tasks 8-9), Testing (Tasks 2-7 unit, Task 10 integration), Server-instructions update (Task 9), Error handling (Task 9 read handler).
- **Placeholders:** None — every step has concrete code.
- **Type consistency:** `McpResourceEntry` defined Task 1, consumed Task 9. `RenderableCard` private to service. `RenderDeckAsync` returns `Task<string?>`, others return `Task<string>` — consistent across tasks. `ResourceUris.TryParseDeck` defined Task 8, consumed Task 9.
- **Known unknown:** Exact namespace for `Resource`/`ResourceTemplate`/`ListResourcesResult`/`ReadResourceResult` in the installed SDK version. The plan notes how to resolve at Step 1 of Task 9 if the default `ModelContextProtocol.Protocol` import doesn't compile.
