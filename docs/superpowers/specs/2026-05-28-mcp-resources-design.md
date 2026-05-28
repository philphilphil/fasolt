# MCP Resources for Fasolt — Design

**Issue:** [#166](https://github.com/philphilphil/fasolt/issues/166)
**Date:** 2026-05-28
**Status:** Design approved, ready for implementation plan

## Problem

The Fasolt MCP server today exposes only **tools**. Clients like `codex-mcp-client` poll `resources/list` and `resources/templates/list`, both of which return "method not implemented" — visible as repeated errors in server logs. Beyond the noise, the absence of resources means users have no way to *pin* deck content into a conversation from their host's UI (Claude Code's `@`-autocomplete, Claude Desktop's paperclip picker, etc.).

The anchor use case is style transfer: *"Create 20 new cards in the style of `@fasolt:fasolt://deck/german-verbs`."* The model sees representative cards without guessing the right `search_cards` query.

## Goals

- Implement `resources/list`, `resources/templates/list`, `resources/read`, and advertise the `resources` capability.
- Expose three resources covering organizational and temporal lenses on the user's card collection.
- Eliminate the `method not implemented` log noise as a side effect.

## Non-goals

- Not exposing individual cards as resources (volume explodes the picker; `search_cards`/`get_card` already cover this).
- Not exposing source files as resources (defer until concrete need; deck workflow likely subsumes it).
- Not replacing or modifying any existing MCP tool — resources are purely additive.
- No cursor-based pagination in v1; truncation hints suffice.
- No review-state or per-card SRS data in resource payloads — that's noise for the "show me examples" workflow.

## Resource set

Three resources, all under the `fasolt://` scheme:

| URI | Listing | Lifecycle |
|---|---|---|
| `fasolt://deck/{deckId}` | One entry per active (non-suspended) deck the user owns | Dynamic, per-user |
| `fasolt://due-today` | Static singleton | Always present |
| `fasolt://recent` | Static singleton | Always present |

`resources/templates/list` advertises `fasolt://deck/{deckId}` so clients with template-autocomplete can address any deck by ID.

### Selection rationale

Decks are the *organizational* unit users think in. Due-today and recent are *attentional* lenses — "what's relevant right now." Together they cover the natural pinning workflows. Adding more in this family (forgotten-today, leeches, etc.) starts surfacing internals and was deliberately deferred.

### Naming behavior in client picker

Each deck `resources/list` entry carries `name = <DeckName>`, so the `@`-autocomplete shows the human deck name, not the opaque GUID URI. This is the path that avoids the known Claude Desktop bug ([anthropics/claude-code#13312](https://github.com/anthropics/claude-code/issues/13312)) where template-derived resources show the template's static title instead of per-resource names.

### Suspended decks

Hidden from `resources/list`, but `resources/read` on a suspended deck still returns its cards if the URI is asked for directly. No point lying about existence — and clients may have a stale list.

### `recent` semantics

Cards where `CreatedAt >= UtcNow - 7 days`, ordered `CreatedAt DESC`, suspended cards excluded.

## Content format

All resources return `mimeType = "text/markdown"`.

### Per-card block

```markdown
**Front:** essen

**Back:** to eat

*Source: notes/german/verbs.md → Common verbs*
```

- `**Back:** …` block omitted if back is empty.
- `*Source: …*` italic footer omitted if no source file. Heading appended after `→` when present.
- Cards with SVG: append `[has SVG image — use get_card for full content]`. SVG bytes are NOT inlined (text-focused, keeps content bounded).

Cards are separated by `---` (horizontal rule). The paragraph-with-bold-labels format is robust against cards whose own markdown contains headings (which would collide with `## Front` styles) or multi-line content (which would break tables).

### Deck header

```markdown
# Deck: German Verbs

50 cards · 12 due today

Common German verbs from chapter 3.

---

**Front:** …
```

- Deck description included only when present.
- "Due today" count omitted when zero.

### Due-today resource

```markdown
# Due Today

42 cards across 6 decks

## German Verbs (12 cards)

**Front:** …

**Back:** …

---

## French Vocab (8 cards)

…

## (no deck) (3 cards)

…
```

Grouped by deck for scannability. Cards in no deck go under "(no deck)" at the end. Cards in multiple decks appear once, under the first deck (alphabetical).

### Recent resource

```markdown
# Recently Created

23 cards created since 2026-05-21 (last 7 days)

---

**Created:** 2026-05-27 · **Deck:** German Verbs

**Front:** …

**Back:** …

*Source: notes/german/verbs.md*

---
```

Per-card metadata line shows creation date and first deck (if any).

### Truncation footer

```markdown
*Showing 100 of 423 cards. Use list_cards or search_cards for the full set.*
```

Only emitted when truncated.

## Truncation & filtering

**Size budget:** 80 KB markdown per resource. **Soft cap:** 100 cards. Whichever hits first triggers truncation.

**Suspended filter — per resource:**

| Resource | Suspended decks | Suspended cards |
|---|---|---|
| `fasolt://deck/{id}` listing | hidden from list | n/a |
| `fasolt://deck/{id}` read | returned if URI asked directly | excluded from cards rendered |
| `fasolt://due-today` | n/a (FSRS already skips) | excluded |
| `fasolt://recent` | n/a | excluded |

**Empty results:** never error — render the header + a `No cards.` line. Keeps the picker entry useful.

**Per-card ordering:**
- Deck: same as `list_cards` default (creation order).
- Due-today: by deck name, then by due time within deck.
- Recent: `CreatedAt DESC`.

## Architecture

The implementation follows the existing flat `Api/McpTools/` pattern.

```
fasolt.Server/
  Application/
    Services/
      McpResourceService.cs       — rendering + queries; unit-testable core
  Api/
    McpResources/
      ResourceUris.cs             — URI constants and parsing helpers
      ResourceList.cs              — WithListResourcesHandler delegate
      DeckResource.cs              — [McpServerResource(UriTemplate="fasolt://deck/{deckId}")]
      DueTodayResource.cs          — [McpServerResource(UriTemplate="fasolt://due-today")]
      RecentResource.cs            — [McpServerResource(UriTemplate="fasolt://recent")]
```

### Wiring in `Program.cs`

```csharp
builder.Services.AddMcpServer(options => { … })
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()           // picks up [McpServerResource] methods
    .WithListResourcesHandler(/* dynamic enumeration */)
    .WithListResourceTemplatesHandler(/* returns deck template */)
    .WithRequestFilters(/* existing */);
```

The `resources` capability is auto-advertised once any resource handler is registered.

### Auth pattern

Identical to tools: inject `IHttpContextAccessor`, resolve user via `McpUserResolver.GetUserId(...)`. The `EmailVerified` authorization policy already applied to `/mcp` inherits to resource handlers.

### `McpResourceService` surface

```csharp
public class McpResourceService(AppDbContext db, ReviewService reviewService, TimeProvider tp)
{
    Task<IEnumerable<ResourceListEntry>> ListUserResourcesAsync(string userId);
    Task<string?> RenderDeckAsync(string userId, string deckId);      // null = not found
    Task<string>  RenderDueTodayAsync(string userId);
    Task<string>  RenderRecentAsync(string userId);
}
```

All rendering logic lives in the service as pure-ish methods. The `Api/McpResources/*` classes are thin shims that call it and serialize results. This matches the existing tool/service separation (e.g., `DeckTools` → `DeckService`).

### Server instructions update

Append a one-line note to the `ServerInstructions` block in `Program.cs`:

> Decks, today's due queue, and recently created cards are also exposed as MCP resources — clients with a resource picker (Claude Code, Claude Desktop) can attach them via `@`.

## Testing

### Unit tests — `fasolt.Tests/Application/McpResourceServiceTests.cs`

- Deck render — markdown shape, suspended-card filter, empty-deck case, source footer, SVG note, missing-deck returns null, cross-user isolation
- Deck render — truncation at 100 cards / 80 KB with footer
- Due-today render — grouping by deck, `(no deck)` bucket, suspended cards excluded
- Recent render — 7-day window, newest-first, suspended cards excluded
- `ListUserResourcesAsync` — only active decks listed, two statics always present, deck `name` carries human name

### Integration tests — `fasolt.Tests/Api/McpResourceEndpointTests.cs`

Matching the existing MCP tool test pattern:

- `resources/list` returns the right shape for an authenticated user
- `resources/templates/list` returns the deck template
- `resources/read` on `fasolt://deck/{id}` returns rendered markdown
- `resources/read` on `fasolt://due-today` and `fasolt://recent` work
- `resources/read` on unknown URI returns the SDK's resource-not-found error
- Unauthenticated `resources/list` returns 401 (auth filter still applies)

No Playwright — MCP is not UI; CLAUDE.md's "always run Playwright" rule applies to web features.

Implementation is **TDD**: each test is written failing before its production code.

## Error handling

- Unknown deck ID in `fasolt://deck/{id}` → throw `McpException("Deck not found: {id}")`. SDK translates to the proper MCP error.
- Cross-user deck access → treated as not-found (same behavior as existing tools).
- Empty result set → never an error; renders header + `No cards.`.

## Out of scope (future)

- `fasolt://struggling` (leech-y cards) — deferred until usage signals demand.
- `fasolt://source/{file}` — defer; volume could be high and deck workflow likely subsumes it.
- Card-level resources — explicitly rejected; volume issue too severe.
- Cursor-based pagination — not needed at the proposed 100-card cap.
