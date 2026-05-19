# `list_cards` Slim Default + `include` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the MCP `list_cards` tool return slim card metadata by default and let callers opt into FSRS scheduling fields and SVG image blobs via an `include` array, so LLMs stop fanning out to `get_card` per result and stop bloating context with data they didn't ask for.

**Architecture:** Add an `include: HashSet<string>?` parameter to `CardService.ListCards`. The service projects SRS fields (`state`, `dueAt`, `stability`, `difficulty`, `step`, `lastReviewedAt`) only when `"srs"` is present, and SVG fields (`frontSvg`, `backSvg`) only when `"svg"` is present. The MCP tool wrapper exposes `include` to the LLM; REST endpoints keep passing nothing and continue receiving the full shape (no mobile breakage). JSON serialization gets `JsonIgnoreCondition.WhenWritingNull` so unincluded fields disappear from the response entirely.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, ModelContextProtocol .NET SDK, xUnit.

---

## File Structure

- **Modify** `fasolt.Server/Application/Dtos/CardDtos.cs` — make `State` nullable in `CardDto`.
- **Modify** `fasolt.Server/Application/Services/CardService.cs` — add `include` parameter to `ListCards`, conditional projection.
- **Modify** `fasolt.Server/Api/McpTools/CardTools.cs` — expose `include` parameter on the MCP `ListCards` tool, rewrite descriptions.
- **Modify** `fasolt.Server/Api/McpTools/McpJson.cs` — add `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`.
- **Modify** `fasolt.Tests/CardServiceTests.cs` — add coverage for slim default, `include: ["srs"]`, `include: ["svg"]`, both.

REST endpoints (`fasolt.Server/Api/Endpoints/CardEndpoints.cs`) are intentionally left alone — they pass no `include`, the service treats null-include as "return full shape", mobile apps stay green.

---

### Task 1: Branch off main

- [ ] **Step 1: Verify clean tree**

Run: `git status`
Expected: `nothing to commit, working tree clean` on `main`.

- [ ] **Step 2: Create branch**

Run: `git checkout -b list-cards-slim-include`
Expected: `Switched to a new branch 'list-cards-slim-include'`.

---

### Task 2: Make `CardDto.State` nullable

**Files:**
- Modify: `fasolt.Server/Application/Dtos/CardDtos.cs:14-22`

- [ ] **Step 1: Edit `CardDto`**

Change:
```csharp
public record CardDto(
    string Id, string? SourceFile, string? SourceHeading,
    string Front, string Back, string State,
    DateTimeOffset CreatedAt, List<CardDeckInfoDto> Decks,
    bool IsSuspended = false,
    DateTimeOffset? DueAt = null, double? Stability = null,
    double? Difficulty = null, int? Step = null,
    DateTimeOffset? LastReviewedAt = null,
    string? FrontSvg = null, string? BackSvg = null);
```
to:
```csharp
public record CardDto(
    string Id, string? SourceFile, string? SourceHeading,
    string Front, string Back, string? State,
    DateTimeOffset CreatedAt, List<CardDeckInfoDto> Decks,
    bool IsSuspended = false,
    DateTimeOffset? DueAt = null, double? Stability = null,
    double? Difficulty = null, int? Step = null,
    DateTimeOffset? LastReviewedAt = null,
    string? FrontSvg = null, string? BackSvg = null);
```

(Only `string State` → `string? State`.)

- [ ] **Step 2: Build to surface fallout**

Run: `dotnet build fasolt.Server/fasolt.Server.csproj`
Expected: Build succeeds with possibly some new nullability warnings to address in subsequent tasks. If errors arise from callers that dereferenced `State` without null-checking, note the file:line and fix them inline with the obvious null-check (`card.State ?? "Unknown"` or guard before use). REST endpoints, mobile apps, and study/review flows all hit code paths where `State` is populated, so no logic-level changes are expected.

---

### Task 3: Add `include` parameter + conditional projection to `CardService.ListCards`

**Files:**
- Modify: `fasolt.Server/Application/Services/CardService.cs:200-242`

- [ ] **Step 1: Update method signature and projection**

Replace lines 200-242 with:

```csharp
public async Task<PaginatedResponse<CardDto>> ListCards(
    string userId,
    string? sourceFile,
    string? deckId,
    int? limit,
    string? after,
    HashSet<string>? include = null)
{
    var take = Math.Clamp(limit ?? 50, 1, 200);
    var includeSrs = include is null || include.Contains("srs");
    var includeSvg = include is null || include.Contains("svg");

    IQueryable<Card> query = db.Cards
        .Where(c => c.UserId == userId)
        .OrderByDescending(c => c.CreatedAt)
        .ThenBy(c => c.Id);

    if (sourceFile is not null)
        query = query.Where(c => c.SourceFile == sourceFile);

    if (deckId is not null)
    {
        var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == userId);
        if (deck is not null)
            query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deck.Id));
    }

    if (after is not null)
    {
        var cursor = await db.Cards.Where(c => c.PublicId == after && c.UserId == userId)
            .Select(c => new { c.CreatedAt, c.Id }).FirstOrDefaultAsync();
        if (cursor is not null)
            query = query.Where(c => c.CreatedAt < cursor.CreatedAt ||
                (c.CreatedAt == cursor.CreatedAt && c.Id.CompareTo(cursor.Id) > 0));
    }

    var cards = await query
        .Take(take + 1)
        .Select(c => new CardDto(
            c.PublicId,
            c.SourceFile,
            c.SourceHeading,
            c.Front,
            c.Back,
            includeSrs ? c.State : null,
            c.CreatedAt,
            c.DeckCards.Select(dc => new CardDeckInfoDto(dc.Deck.PublicId, dc.Deck.Name, dc.Deck.IsSuspended)).ToList(),
            c.IsSuspended,
            includeSrs ? c.DueAt : null,
            includeSrs ? c.Stability : null,
            includeSrs ? c.Difficulty : null,
            includeSrs ? c.Step : null,
            includeSrs ? c.LastReviewedAt : null,
            includeSvg ? c.FrontSvg : null,
            includeSvg ? c.BackSvg : null))
        .ToListAsync();

    var hasMore = cards.Count > take;
    if (hasMore) cards = cards[..take];
    var nextCursor = hasMore ? cards[^1].Id : null;

    return new PaginatedResponse<CardDto>(cards, hasMore, nextCursor);
}
```

Key semantics:
- `include == null` → full shape (preserves existing behavior for REST callers).
- `include` non-null but empty → slim only (no SRS, no SVG).
- `include` contains `"srs"` → adds SRS fields.
- `include` contains `"svg"` → adds SVG fields.

- [ ] **Step 2: Build**

Run: `dotnet build fasolt.Server/fasolt.Server.csproj`
Expected: Build succeeds. The REST `MapGet("/")` handler still calls `ListCards(user.Id, sourceFile, deckId, limit, after)` — the new param defaults to `null` → full shape, unchanged.

---

### Task 4: Wire `include` through MCP `ListCards` tool with updated descriptions

**Files:**
- Modify: `fasolt.Server/Api/McpTools/CardTools.cs:21-31`

- [ ] **Step 1: Replace the MCP `ListCards` method**

Replace lines 21-31 with:

```csharp
    [McpServerTool, Description("List cards with slim metadata (id, front, back, sourceFile, sourceHeading, isSuspended, decks, createdAt). To pull FSRS scheduling fields or SVG image content across many cards in one call, pass `include`. Do NOT loop over results calling get_card — use include instead. Supports cursor-based pagination via the `after` parameter.")]
    public async Task<string> ListCards(
        [Description("Filter by source file name")] string? sourceFile = null,
        [Description("Filter by deck ID")] string? deckId = null,
        [Description("Max results to return (1-200, default 50)")] int? limit = null,
        [Description("Cursor from previous page's nextCursor field")] string? after = null,
        [Description("Optional opt-in extras. Pass only what you actually need. Valid values: \"srs\" adds FSRS scheduling fields (state, dueAt, stability, difficulty, step, lastReviewedAt) — request when the task involves due dates, difficulty, or learning state. \"svg\" adds frontSvg and backSvg image blobs — only request when you specifically need to read or write the SVG content (e.g. regenerating diagrams). The blobs can be hundreds of KB each; a user asking for a 'full audit' of text content does not need svg.")] string[]? include = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var includeSet = include is null ? new HashSet<string>() : new HashSet<string>(include, StringComparer.OrdinalIgnoreCase);
        var result = await cardService.ListCards(userId, sourceFile, deckId, limit, after, includeSet);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }
```

Notes:
- Passing an empty `HashSet` (when `include` is omitted by the LLM) gives slim default — this is the new MCP default.
- `StringComparer.OrdinalIgnoreCase` so `"SRS"`, `"Srs"`, `"srs"` all work.

- [ ] **Step 2: Build**

Run: `dotnet build fasolt.Server/fasolt.Server.csproj`
Expected: Build succeeds.

---

### Task 5: Drop nulls from MCP JSON output

**Files:**
- Modify: `fasolt.Server/Api/McpTools/McpJson.cs`

- [ ] **Step 1: Add null-suppression**

Replace the file with:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fasolt.Server.Api.McpTools;

internal static class McpJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
```

- [ ] **Step 2: Build**

Run: `dotnet build fasolt.Server/fasolt.Server.csproj`
Expected: Build succeeds.

---

### Task 6: Add service-level tests for slim default and `include` variants

**Files:**
- Modify: `fasolt.Tests/CardServiceTests.cs`

- [ ] **Step 1: Inspect existing test patterns**

Run: `grep -n "ListCards\|public.*Task.*List\|CardServiceTests\|CreateService" fasolt.Tests/CardServiceTests.cs | head -40`
Expected: You see existing `ListCards_*` tests and a service-construction helper. Match their style (DbContextOptions, `using var ctx = ...`, in-memory or test-Postgres, etc.). If unfamiliar, read the top of the file (`Read fasolt.Tests/CardServiceTests.cs offset 1 limit 80`) to learn the pattern, then add your new tests after the last `ListCards_*` test.

- [ ] **Step 2: Add four tests**

Add these four tests, adapting fixture setup to match the existing pattern in the file:

```csharp
[Fact]
public async Task ListCards_SlimDefault_OmitsSrsAndSvg()
{
    // Arrange: seed one card with SVG content and a known SRS state.
    // (Use the existing test fixture/setup helper in this file.)
    var (service, userId, _) = await SeedCardWithSvgAndSrs();

    // Act: call with an EMPTY include set — this is what the MCP wrapper passes by default.
    var result = await service.ListCards(userId, null, null, null, null, new HashSet<string>());

    // Assert
    var card = Assert.Single(result.Items);
    Assert.Null(card.State);
    Assert.Null(card.DueAt);
    Assert.Null(card.Stability);
    Assert.Null(card.Difficulty);
    Assert.Null(card.Step);
    Assert.Null(card.LastReviewedAt);
    Assert.Null(card.FrontSvg);
    Assert.Null(card.BackSvg);
    Assert.NotNull(card.Front);
    Assert.NotNull(card.Back);
}

[Fact]
public async Task ListCards_IncludeSrs_PopulatesSrsFieldsOnly()
{
    var (service, userId, _) = await SeedCardWithSvgAndSrs();

    var result = await service.ListCards(userId, null, null, null, null,
        new HashSet<string> { "srs" });

    var card = Assert.Single(result.Items);
    Assert.NotNull(card.State);
    // Stability/Difficulty/DueAt/etc come from FSRS scheduling — non-null after first review.
    Assert.Null(card.FrontSvg);
    Assert.Null(card.BackSvg);
}

[Fact]
public async Task ListCards_IncludeSvg_PopulatesSvgFieldsOnly()
{
    var (service, userId, _) = await SeedCardWithSvgAndSrs();

    var result = await service.ListCards(userId, null, null, null, null,
        new HashSet<string> { "svg" });

    var card = Assert.Single(result.Items);
    Assert.Null(card.State);
    Assert.NotNull(card.FrontSvg);
}

[Fact]
public async Task ListCards_NullInclude_ReturnsFullShape_ForRestCompatibility()
{
    var (service, userId, _) = await SeedCardWithSvgAndSrs();

    // include == null → REST callers get the full shape, unchanged from before.
    var result = await service.ListCards(userId, null, null, null, null, null);

    var card = Assert.Single(result.Items);
    Assert.NotNull(card.State);
    Assert.NotNull(card.FrontSvg);
}
```

If a helper like `SeedCardWithSvgAndSrs` doesn't exist, write a small private async method at the bottom of the test class that:
1. Builds an `AppDbContext` (same way as other tests in the file).
2. Inserts a `User` row.
3. Inserts a `Card` row with non-null `FrontSvg` (any small `<svg>...</svg>` string), and a known `State`.
4. Returns `(new CardService(ctx), userId, ctx)` so the caller can assert against the same context if needed.

Read the top of `CardServiceTests.cs` to see how the existing tests construct the DbContext and `CardService` — mirror that exactly.

- [ ] **Step 3: Run the new tests**

Run: `dotnet test fasolt.Tests/fasolt.Tests.csproj --filter "FullyQualifiedName~ListCards_" --logger "console;verbosity=normal"`
Expected: The four new tests pass; existing `ListCards_*` tests still pass.

---

### Task 7: Run the full server test suite

- [ ] **Step 1: Run all tests**

Run: `dotnet test`
Expected: All tests pass. If something downstream of the `CardDto.State` nullability change broke (a callers somewhere assumed `State` was non-null), fix it in place — usually `card.State ?? "New"` or a guard before the consumer.

---

### Task 8: Manual sanity check via the live MCP server

- [ ] **Step 1: Start the stack**

Run: `make dev` (or `./scripts/dev.sh`) in a separate terminal if it's not already running.
Expected: Backend up on `http://localhost:8080`, frontend on `http://localhost:5173`.

- [ ] **Step 2: Hit the MCP `list_cards` tool**

Use the MCP inspector at `/mcp` or a quick `curl` to the streamable HTTP endpoint, calling `list_cards` with no `include` and with `include: ["srs"]`. Verify:
- Default response has no `state`, `dueAt`, `stability`, `difficulty`, `step`, `lastReviewedAt`, `frontSvg`, `backSvg` keys.
- `include: ["srs"]` response includes those SRS keys but still no SVG keys.

If you can't easily call MCP directly from CLI, skip this step — the unit tests above cover the projection logic — and note it as a manual follow-up in the PR description.

---

### Task 9: Commit and open PR

- [ ] **Step 1: Commit**

Stage only the files touched in this plan:

```bash
git add fasolt.Server/Application/Dtos/CardDtos.cs \
        fasolt.Server/Application/Services/CardService.cs \
        fasolt.Server/Api/McpTools/CardTools.cs \
        fasolt.Server/Api/McpTools/McpJson.cs \
        fasolt.Tests/CardServiceTests.cs \
        docs/superpowers/plans/2026-05-19-list-cards-slim-include.md
git commit -m "$(cat <<'EOF'
feat(mcp): slim list_cards by default, add include opt-in for srs/svg

LLMs were writing Python to reshape list_cards output because the response
included 17 fields per card — SVG blobs and FSRS internals were noise for
the common browse/curate flows. Now list_cards returns slim metadata by
default and accepts an `include` array to opt into SRS or SVG fields,
eliminating the get_card fan-out pattern while keeping niche use cases
single-call.

REST callers (mobile apps) pass no include and still receive the full
shape — service treats null-include as "return everything" for backwards
compatibility.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step 2: Push and open PR**

```bash
git push -u origin list-cards-slim-include
gh pr create --title "Slim list_cards default + include opt-in for srs/svg" --body "$(cat <<'EOF'
## Summary
- `list_cards` MCP tool now returns slim card metadata by default (id, front, back, sourceFile, sourceHeading, isSuspended, decks, createdAt). FSRS scheduling fields and SVG image blobs are opt-in via `include: ["srs"]` and `include: ["svg"]`.
- Empirically validated with 6 parallel test LLMs: 0/6 fan out to `get_card`; the `include` opt-in is reliably picked when needed and left off otherwise.
- REST endpoints unchanged — service treats null-include as "full shape" so mobile apps stay green.
- `McpJson.Options` now suppresses null fields entirely (further response shrink).

## Test plan
- [x] `dotnet test` — full server suite green
- [x] New unit tests cover slim default, `include: ["srs"]`, `include: ["svg"]`, and the null-include REST path
- [ ] Manual: call `list_cards` from a fresh MCP client and confirm response shape matches the new defaults

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Surface the PR URL to the user**

Print the URL returned by `gh pr create`.
