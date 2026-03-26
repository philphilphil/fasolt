# MCP Tool Consolidation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Consolidate 16 MCP tools down to 13 by merging deck-membership tools into `AssignCardsToDeck`, making `UpdateCard` bulk as `UpdateCards`, merging `DeleteCardsBySource` into `DeleteCards`, and adding `UpdateDeck`.

**Architecture:** Pure MCP tool layer refactor — service layer already has all needed methods. Changes are in `DeckTools.cs` and `CardTools.cs` only, plus a new `RemoveCards` service method for bulk removal from deck.

**Tech Stack:** .NET 10, ModelContextProtocol.AspNetCore, ASP.NET Core

---

## File Map

- Modify: `fasolt.Server/Api/McpTools/DeckTools.cs` — replace AddCardsToDeck/RemoveCardsFromDeck/MoveCards with AssignCardsToDeck, add UpdateDeck
- Modify: `fasolt.Server/Api/McpTools/CardTools.cs` — make UpdateCard bulk as UpdateCards, merge DeleteCardsBySource into DeleteCards
- Modify: `fasolt.Server/Application/Services/DeckService.cs` — add bulk RemoveCards method
- Modify: `fasolt.Server/Application/Services/CardService.cs` — add BulkUpdateCards method
- Modify: `fasolt.Server/Application/Dtos/CardDtos.cs` — add BulkUpdateCardsResult DTO
- Modify: `CLAUDE.md` — update MCP tools list

---

### Task 1: Add bulk RemoveCards to DeckService

`AssignCardsToDeck` needs to remove multiple cards in one call (when deckId is null). The existing `RemoveCard` method handles one card at a time. Add a bulk version.

**Files:**
- Modify: `fasolt.Server/Application/Services/DeckService.cs`

- [ ] **Step 1: Add RemoveCards method to DeckService**

Add after the existing `RemoveCard` method (line 188):

```csharp
public async Task<RemoveCardsResult> RemoveCards(string userId, string deckPublicId, List<string> cardPublicIds)
{
    var deck = await db.Decks
        .FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);

    if (deck is null) return new RemoveCardsResult(false, 0);

    var cardIds = await db.Cards
        .Where(c => c.UserId == userId && cardPublicIds.Contains(c.PublicId))
        .Select(c => c.Id)
        .ToListAsync();

    var removed = await db.DeckCards
        .Where(dc => dc.DeckId == deck.Id && cardIds.Contains(dc.CardId))
        .ExecuteDeleteAsync();

    return new RemoveCardsResult(true, removed);
}
```

Add the result record next to `RemoveCardResult` (near line 193):

```csharp
public record RemoveCardsResult(bool DeckFound, int RemovedCount);
```

- [ ] **Step 2: Verify it builds**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Application/Services/DeckService.cs
git commit -m "feat(mcp): add bulk RemoveCards to DeckService"
```

---

### Task 2: Replace AddCardsToDeck/RemoveCardsFromDeck/MoveCards with AssignCardsToDeck

Replace the three deck-membership tools with a single `AssignCardsToDeck` that takes a list of `{ cardId, deckId }` pairs. Setting `deckId` assigns, setting `null` removes from all decks.

**Files:**
- Modify: `fasolt.Server/Api/McpTools/DeckTools.cs`

- [ ] **Step 1: Remove the three old tools**

Delete the `AddCardsToDeck` method (lines 29-43), `RemoveCardsFromDeck` method (lines 45-61), and `MoveCards` method (lines 63-85) from `DeckTools.cs`.

- [ ] **Step 2: Add AssignCardsToDeck**

Add in place of the removed methods:

```csharp
[McpServerTool, Description("Assign cards to a deck, or remove them. Pass a deckId to assign cards to that deck. Pass null as deckId to remove cards from a specific deck (requires fromDeckId).")]
public async Task<string> AssignCardsToDeck(
    [Description("Target deck ID to assign cards to (null to remove cards from fromDeckId)")] string? deckId,
    [Description("List of card IDs")] List<string> cardIds,
    [Description("Deck ID to remove cards from (required when deckId is null, optional when moving cards between decks)")] string? fromDeckId = null)
{
    var userId = McpUserResolver.GetUserId(httpContextAccessor);

    // Remove from source deck if specified
    if (fromDeckId is not null)
    {
        var removeResult = await deckService.RemoveCards(userId, fromDeckId, cardIds);
        if (!removeResult.DeckFound)
            return JsonSerializer.Serialize(new { error = "Source deck not found" }, McpJson.Options);
    }

    // Add to target deck if specified
    if (deckId is not null)
    {
        var addResult = await deckService.AddCards(userId, deckId, cardIds);
        return addResult switch
        {
            AddCardsResult.Success => JsonSerializer.Serialize(new { success = true }, McpJson.Options),
            AddCardsResult.DeckNotFound => JsonSerializer.Serialize(new { error = "Target deck not found" }, McpJson.Options),
            AddCardsResult.CardsNotFound => JsonSerializer.Serialize(new { error = "One or more cards not found" }, McpJson.Options),
            _ => JsonSerializer.Serialize(new { error = "Unexpected error" }, McpJson.Options),
        };
    }

    // deckId is null — this was a remove-only operation
    if (fromDeckId is null)
        return JsonSerializer.Serialize(new { error = "Provide deckId to assign, or fromDeckId to remove" }, McpJson.Options);

    return JsonSerializer.Serialize(new { success = true }, McpJson.Options);
}
```

- [ ] **Step 3: Verify it builds**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/McpTools/DeckTools.cs
git commit -m "feat(mcp): replace Add/Remove/MoveCards with AssignCardsToDeck"
```

---

### Task 3: Add UpdateDeck tool

`DeckService.UpdateDeck` already exists. Just expose it as an MCP tool.

**Files:**
- Modify: `fasolt.Server/Api/McpTools/DeckTools.cs`

- [ ] **Step 1: Add UpdateDeck tool**

Add after the `CreateDeck` method:

```csharp
[McpServerTool, Description("Update a deck's name or description.")]
public async Task<string> UpdateDeck(
    [Description("ID of the deck to update")] string deckId,
    [Description("New deck name (max 100 characters)")] string name,
    [Description("New deck description (null to clear)")] string? description = null)
{
    var userId = McpUserResolver.GetUserId(httpContextAccessor);
    var result = await deckService.UpdateDeck(userId, deckId, name, description);
    if (result is null)
        return JsonSerializer.Serialize(new { error = "Deck not found" }, McpJson.Options);
    return JsonSerializer.Serialize(result, McpJson.Options);
}
```

- [ ] **Step 2: Verify it builds**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/McpTools/DeckTools.cs
git commit -m "feat(mcp): add UpdateDeck tool"
```

---

### Task 4: Add BulkUpdateCards to CardService

`UpdateCards` (bulk) needs a service method that processes multiple card updates in one call. Each item can look up by cardId or sourceFile+front natural key.

**Files:**
- Modify: `fasolt.Server/Application/Services/CardService.cs`
- Modify: `fasolt.Server/Application/Dtos/CardDtos.cs`

- [ ] **Step 1: Add DTOs**

Add to `CardDtos.cs`:

```csharp
public record BulkUpdateCardItem(
    string? CardId = null,
    string? SourceFile = null,
    string? Front = null,
    string? NewFront = null,
    string? NewBack = null,
    string? NewSourceFile = null,
    string? NewSourceHeading = null,
    string? NewFrontSvg = null,
    string? NewBackSvg = null);

public record BulkUpdateCardResult(string? CardId, string? SourceFile, string? Front, UpdateCardStatus Status, CardDto? Card = null);
```

- [ ] **Step 2: Add BulkUpdateCards method to CardService**

Add after the `UpdateCardByNaturalKey` method:

```csharp
public async Task<List<BulkUpdateCardResult>> BulkUpdateCards(string userId, List<BulkUpdateCardItem> items)
{
    var results = new List<BulkUpdateCardResult>();

    foreach (var item in items)
    {
        var req = new UpdateCardFieldsRequest(item.NewFront, item.NewBack, item.NewSourceFile, item.NewSourceHeading, item.NewFrontSvg, item.NewBackSvg);

        UpdateCardResult result;
        if (item.CardId is not null)
        {
            result = await UpdateCardFields(userId, item.CardId, req);
        }
        else if (item.SourceFile is not null && item.Front is not null)
        {
            result = await UpdateCardByNaturalKey(userId, item.SourceFile, item.Front, req);
        }
        else
        {
            results.Add(new BulkUpdateCardResult(item.CardId, item.SourceFile, item.Front, UpdateCardStatus.NotFound));
            continue;
        }

        results.Add(new BulkUpdateCardResult(item.CardId, item.SourceFile, item.Front, result.Status, result.Card));
    }

    return results;
}
```

- [ ] **Step 3: Verify it builds**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Application/Services/CardService.cs fasolt.Server/Application/Dtos/CardDtos.cs
git commit -m "feat(mcp): add BulkUpdateCards to CardService"
```

---

### Task 5: Replace UpdateCard with bulk UpdateCards and merge DeleteCardsBySource into DeleteCards

**Files:**
- Modify: `fasolt.Server/Api/McpTools/CardTools.cs`

- [ ] **Step 1: Replace UpdateCard with UpdateCards**

Remove the existing `UpdateCard` method (lines 69-109) and replace with:

```csharp
[McpServerTool, Description("Update one or more existing cards' text or source metadata. Preserves all review/SRS history. Each card can be looked up by cardId, or by sourceFile + front (case-insensitive natural key).")]
public async Task<string> UpdateCards(
    [Description("Array of card updates. Each needs a lookup key (cardId, or sourceFile + front) and at least one field to update (newFront, newBack, newSourceFile, newSourceHeading, newFrontSvg, newBackSvg).")] List<BulkUpdateCardItem> cards)
{
    var userId = McpUserResolver.GetUserId(httpContextAccessor);

    if (cards.Count == 0)
        return JsonSerializer.Serialize(new { error = "Provide at least one card to update" }, McpJson.Options);

    var results = await cardService.BulkUpdateCards(userId, cards);
    return JsonSerializer.Serialize(new
    {
        updated = results.Count(r => r.Status == UpdateCardStatus.Success),
        results
    }, McpJson.Options);
}
```

- [ ] **Step 2: Merge DeleteCardsBySource into DeleteCards**

Remove the `DeleteCardsBySource` method (lines 111-118) and update `DeleteCards` to:

```csharp
[McpServerTool, Description("Delete cards by IDs or by source file. Provide cardIds to delete specific cards, or sourceFile to delete all cards from that source.")]
public async Task<string> DeleteCards(
    [Description("List of card IDs to delete")] List<string>? cardIds = null,
    [Description("Delete all cards from this source file")] string? sourceFile = null)
{
    var userId = McpUserResolver.GetUserId(httpContextAccessor);

    if (cardIds is null && sourceFile is null)
        return JsonSerializer.Serialize(new { error = "Provide cardIds or sourceFile" }, McpJson.Options);

    var count = 0;
    if (cardIds is not null && cardIds.Count > 0)
        count += await cardService.DeleteCards(userId, cardIds);
    if (sourceFile is not null)
        count += await cardService.DeleteCardsBySource(userId, sourceFile);

    return JsonSerializer.Serialize(new { deleted = count > 0, deletedCount = count }, McpJson.Options);
}
```

- [ ] **Step 3: Add missing import for BulkUpdateCardItem**

Ensure `CardTools.cs` has `using Fasolt.Server.Application.Dtos;` (already present on line 3).

- [ ] **Step 4: Verify it builds**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Api/McpTools/CardTools.cs
git commit -m "feat(mcp): bulk UpdateCards, merge DeleteCardsBySource into DeleteCards"
```

---

### Task 6: Update CLAUDE.md tool list

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update the MCP tools section**

Replace the `### Available MCP Tools` section with:

```markdown
### Available MCP Tools

- `CreateCards` — create one or more flashcards, optionally linked to a source file and/or deck
- `SearchCards` — search existing cards by query text (use before creating to detect duplicates)
- `ListCards` — list cards, optionally filtered by source file or deck; supports pagination
- `UpdateCards` — bulk update cards' text or source metadata by ID or natural key (sourceFile + front); preserves SRS history
- `DeleteCards` — delete cards by IDs or by source file
- `AddSvgToCard` — add an SVG image to a card's front or back
- `ListSources` — list all source files that cards were created from, with card and due counts
- `ListDecks` — list all decks with card counts and due counts
- `CreateDeck` — create a new deck for organizing flashcards
- `UpdateDeck` — update a deck's name or description
- `DeleteDeck` — delete a deck, optionally deleting all its cards too
- `AssignCardsToDeck` — assign cards to a deck, remove from a deck, or move between decks
- `SetDeckActive` — activate or deactivate a deck for study
- `GetOverview` — get account overview: total cards, due cards, cards by state, deck and source counts
```

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update MCP tools list after consolidation"
```

---

### Task 7: Smoke test

- [ ] **Step 1: Start the backend**

Run: `dotnet run --project fasolt.Server`

- [ ] **Step 2: Verify MCP tools list**

Use curl or the MCP client to hit the `/mcp` endpoint and verify the tool list shows the 14 expected tools (CreateCards, SearchCards, ListCards, UpdateCards, DeleteCards, AddSvgToCard, ListSources, ListDecks, CreateDeck, UpdateDeck, DeleteDeck, AssignCardsToDeck, SetDeckActive, GetOverview).

- [ ] **Step 3: Test AssignCardsToDeck flow**

1. Create a deck via CreateDeck
2. Create cards via CreateCards
3. Assign cards to deck via AssignCardsToDeck (deckId + cardIds)
4. Remove cards via AssignCardsToDeck (deckId: null, fromDeckId + cardIds)
5. Verify cards still exist but are unlinked

- [ ] **Step 4: Test UpdateCards bulk flow**

1. Create cards with a sourceFile
2. Update multiple cards via UpdateCards (by natural key)
3. Verify all cards updated

- [ ] **Step 5: Test DeleteCards with sourceFile**

1. Create cards with sourceFile "test.md"
2. Delete via DeleteCards(sourceFile: "test.md")
3. Verify cards deleted
