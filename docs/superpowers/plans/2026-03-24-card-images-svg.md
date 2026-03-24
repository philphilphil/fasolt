# Card Images (SVG) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add SVG image support to flashcards via dedicated `FrontSvg`/`BackSvg` fields on the Card entity.

**Architecture:** Two nullable text columns on Card for SVG content. Server-side sanitization via XDocument. Frontend renders SVGs with DOMPurify allowlist. New `AddSvgToCard` MCP tool for agent discoverability. Web editor with paste + live preview.

**Tech Stack:** .NET 10, EF Core, System.Xml.Linq, Vue 3 + DOMPurify, Swift/SwiftUI + WKWebView (iOS)

**Spec:** `docs/superpowers/specs/2026-03-24-card-images-svg-design.md`

---

## File Structure

**Backend — Create:**
- `fasolt.Server/Application/Services/SvgSanitizer.cs` — Shared SVG sanitization helper

**Backend — Modify:**
- `fasolt.Server/Domain/Entities/Card.cs` — Add `FrontSvg`, `BackSvg` properties
- `fasolt.Server/Application/Dtos/CardDtos.cs` — Add SVG fields to DTOs
- `fasolt.Server/Application/Dtos/BulkCardDtos.cs` — Add SVG fields to BulkCardItem
- `fasolt.Server/Application/Dtos/ReviewDtos.cs` — Add SVG fields to DueCardDto
- `fasolt.Server/Application/Dtos/DeckDtos.cs` — Add SVG fields to DeckCardDto
- `fasolt.Server/Application/Services/CardService.cs` — Accept/store/return SVG fields
- `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs` — Include SVG in DueCardDto projection
- `fasolt.Server/Application/Services/DeckService.cs` — Include SVG in DeckCardDto projection
- `fasolt.Server/Api/McpTools/CardTools.cs` — Add SVG params + AddSvgToCard tool

**Frontend — Create:**
- `fasolt.client/src/composables/useSvgSanitizer.ts` — DOMPurify SVG-specific config

**Frontend — Modify:**
- `fasolt.client/src/types/index.ts` — Add SVG fields to Card/DeckCard/DueCard
- `fasolt.client/src/components/ReviewCard.vue` — Render SVGs above text
- `fasolt.client/src/views/CardDetailView.vue` — SVG display + paste editor with preview

**iOS — Modify:**
- `fasolt.ios/Fasolt/Models/APIModels.swift` — Add SVG fields to DTOs
- `fasolt.ios/Fasolt/Models/Card.swift` — Add SVG stored properties
- `fasolt.ios/Fasolt/Views/Study/CardView.swift` — Render SVGs via WKWebView

**Database:**
- New EF Core migration for `FrontSvg`/`BackSvg` columns

---

### Task 1: Add SVG Fields to Card Entity and Migration

**Files:**
- Modify: `fasolt.Server/Domain/Entities/Card.cs`
- New migration

- [ ] **Step 1: Add FrontSvg and BackSvg to Card entity**

In `fasolt.Server/Domain/Entities/Card.cs`, add after `Back` (line 14):

```csharp
public string? FrontSvg { get; set; }
public string? BackSvg { get; set; }
```

- [ ] **Step 2: Generate and apply migration**

```bash
dotnet ef migrations add AddCardSvgFields --project fasolt.Server
dotnet ef database update --project fasolt.Server
```

- [ ] **Step 3: Verify build**

Run: `dotnet build fasolt.Server`

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Domain/Entities/Card.cs fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat(cards): add FrontSvg and BackSvg fields to Card entity"
```

---

### Task 2: Create SVG Sanitizer

**Files:**
- Create: `fasolt.Server/Application/Services/SvgSanitizer.cs`

- [ ] **Step 1: Create the SVG sanitizer**

Create `fasolt.Server/Application/Services/SvgSanitizer.cs`:

```csharp
using System.Xml.Linq;

namespace Fasolt.Server.Application.Services;

public static class SvgSanitizer
{
    private static readonly int MaxSvgLength = 1_048_576; // ~1MB (char count, not bytes)

    private static readonly HashSet<string> AllowedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg", "path", "circle", "rect", "line", "polyline", "polygon", "ellipse",
        "g", "defs", "use", "text", "tspan", "clipPath", "mask", "pattern",
        "linearGradient", "radialGradient", "stop", "filter",
        "feGaussianBlur", "feOffset", "feMerge", "feMergeNode",
        "title", "desc", "marker", "symbol",
    };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "viewBox", "width", "height", "fill", "stroke", "stroke-width", "d",
        "cx", "cy", "r", "rx", "ry", "x", "y", "x1", "y1", "x2", "y2",
        "points", "transform", "opacity", "font-size", "font-family",
        "text-anchor", "dominant-baseline", "class", "id", "xmlns",
        "preserveAspectRatio", "gradientUnits", "offset",
        "stop-color", "stop-opacity", "stroke-dasharray",
        "stroke-linecap", "stroke-linejoin", "fill-opacity", "stroke-opacity",
        "marker-start", "marker-mid", "marker-end", "fill-rule", "clip-rule",
        "dx", "dy", "textLength", "lengthAdjust",
    };

    /// <summary>
    /// Validates and sanitizes SVG content. Returns null if invalid.
    /// </summary>
    public static string? Sanitize(string? svg)
    {
        if (string.IsNullOrWhiteSpace(svg)) return null;

        var trimmed = svg.Trim();
        if (trimmed.Length > MaxSvgLength) return null;
        if (!trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            var doc = XDocument.Parse(trimmed);
            if (doc.Root is null) return null;

            SanitizeElement(doc.Root);
            return doc.Root.ToString();
        }
        catch
        {
            return null; // Invalid XML
        }
    }

    private static void SanitizeElement(XElement element)
    {
        var localName = element.Name.LocalName;

        // Remove disallowed elements
        var childrenToRemove = element.Elements()
            .Where(e => !AllowedElements.Contains(e.Name.LocalName))
            .ToList();
        foreach (var child in childrenToRemove)
            child.Remove();

        // Remove disallowed attributes (including all on* event handlers)
        var attrsToRemove = element.Attributes()
            .Where(a =>
            {
                var name = a.Name.LocalName;
                // Always strip event handlers
                if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase)) return true;
                // Always strip style attribute
                if (name.Equals("style", StringComparison.OrdinalIgnoreCase)) return true;
                // Strip href/xlink:href unless it's a fragment reference
                if (name.Equals("href", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.ToString().Contains("href", StringComparison.OrdinalIgnoreCase))
                {
                    return !a.Value.StartsWith('#');
                }
                // Allow namespace declarations
                if (a.IsNamespaceDeclaration) return false;
                // Check allowlist
                return !AllowedAttributes.Contains(name);
            })
            .ToList();
        foreach (var attr in attrsToRemove)
            attr.Remove();

        // Recurse into remaining children
        foreach (var child in element.Elements())
            SanitizeElement(child);
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build fasolt.Server`

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Application/Services/SvgSanitizer.cs
git commit -m "feat(cards): add SVG sanitizer with XDocument-based allowlist"
```

---

### Task 3: Update All DTOs with SVG Fields

**Files:**
- Modify: `fasolt.Server/Application/Dtos/CardDtos.cs`
- Modify: `fasolt.Server/Application/Dtos/BulkCardDtos.cs`
- Modify: `fasolt.Server/Application/Dtos/ReviewDtos.cs`
- Modify: `fasolt.Server/Application/Dtos/DeckDtos.cs`

- [ ] **Step 1: Update CardDtos.cs**

Add SVG fields to `CreateCardRequest` (line 3):
```csharp
public record CreateCardRequest(string? SourceFile, string? SourceHeading, string Front, string Back, string? FrontSvg = null, string? BackSvg = null);
```

Add SVG fields to `CardDto` (lines 5-11) — add at end before closing paren:
```csharp
public record CardDto(
    string Id, string? SourceFile, string? SourceHeading,
    string Front, string Back, string State,
    DateTimeOffset CreatedAt, List<CardDeckInfoDto> Decks,
    DateTimeOffset? DueAt = null, double? Stability = null,
    double? Difficulty = null, int? Step = null,
    DateTimeOffset? LastReviewedAt = null,
    string? FrontSvg = null, string? BackSvg = null);
```

Add SVG fields to `UpdateCardFieldsRequest` (lines 14-18):
```csharp
public record UpdateCardFieldsRequest(
    string? NewFront = null,
    string? NewBack = null,
    string? NewSourceFile = null,
    string? NewSourceHeading = null,
    string? NewFrontSvg = null,
    string? NewBackSvg = null);
```

- [ ] **Step 2: Update BulkCardDtos.cs**

Add SVG fields to `BulkCardItem` (line 4):
```csharp
public record BulkCardItem(string Front, string Back, string? SourceFile = null, string? SourceHeading = null, string? FrontSvg = null, string? BackSvg = null);
```

- [ ] **Step 3: Update ReviewDtos.cs**

Add SVG fields to `DueCardDto` (lines 7-10):
```csharp
public record DueCardDto(
    string Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State,
    string? FrontSvg = null, string? BackSvg = null);
```

- [ ] **Step 4: Update DeckDtos.cs**

Add SVG fields to `DeckCardDto` — add at the end of the existing record. The current DeckCardDto (in DeckDtos.cs) ends with `DateTimeOffset? LastReviewedAt = null`. Add after:
```csharp
    string? FrontSvg = null, string? BackSvg = null);
```

- [ ] **Step 5: Verify build fails (expected — construction sites need updating)**

Run: `dotnet build fasolt.Server` — may have compilation errors at construction sites that don't include the new fields (though since they have defaults, positional records with defaults should compile). Verify.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Application/Dtos/
git commit -m "feat(cards): add SVG fields to all card DTOs"
```

---

### Task 4: Update CardService

**Files:**
- Modify: `fasolt.Server/Application/Services/CardService.cs`

- [ ] **Step 1: Update BulkCreateCards to store SVG**

In `CardService.cs`, in the card construction block (lines 101-112), after `State = "new"` add:

```csharp
FrontSvg = SvgSanitizer.Sanitize(item.FrontSvg),
BackSvg = SvgSanitizer.Sanitize(item.BackSvg),
```

- [ ] **Step 2: Update BulkCreateCards DTO construction to include SVG**

In the `createdDtos` construction (lines 132-138), add SVG fields. Change to:

```csharp
var createdDtos = created.Select(c => new CardDto(
    c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back,
    c.State, c.CreatedAt,
    deckId is not null
        ? [new CardDeckInfoDto(deckId, "")]
        : [],
    c.DueAt, c.Stability, c.Difficulty, c.Step, c.LastReviewedAt,
    c.FrontSvg, c.BackSvg)).ToList();
```

- [ ] **Step 3: Update ListCards LINQ projection to include SVG**

In the `ListCards` method, the `.Select()` (lines 173-175). Add SVG at end:

```csharp
.Select(c => new CardDto(c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back, c.State, c.CreatedAt,
    c.DeckCards.Select(dc => new CardDeckInfoDto(dc.Deck.PublicId, dc.Deck.Name)).ToList(),
    c.DueAt, c.Stability, c.Difficulty, c.Step, c.LastReviewedAt,
    c.FrontSvg, c.BackSvg))
```

- [ ] **Step 4: Update ToDto helper to include SVG**

Update `ToDto` (lines 289-292):

```csharp
private static CardDto ToDto(Card c) =>
    new(c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back, c.State, c.CreatedAt,
        c.DeckCards.Select(dc => new CardDeckInfoDto(dc.Deck.PublicId, dc.Deck.Name)).ToList(),
        c.DueAt, c.Stability, c.Difficulty, c.Step, c.LastReviewedAt,
        c.FrontSvg, c.BackSvg);
```

- [ ] **Step 5: Update ApplyCardFieldUpdates to handle SVG fields**

In `ApplyCardFieldUpdates` (around lines 255-258), after the existing field updates, add:

```csharp
if (req.NewFrontSvg is not null)
    card.FrontSvg = req.NewFrontSvg == "" ? null : SvgSanitizer.Sanitize(req.NewFrontSvg);
if (req.NewBackSvg is not null)
    card.BackSvg = req.NewBackSvg == "" ? null : SvgSanitizer.Sanitize(req.NewBackSvg);
```

Empty string `""` clears the SVG, null means leave unchanged.

- [ ] **Step 6: Update CreateCard method**

In `CardService.cs`, the `CreateCard` method (lines 11-29) needs SVG parameters. Update the signature:

```csharp
public async Task<CardDto> CreateCard(string userId, string front, string back, string? sourceFile, string? sourceHeading, string? frontSvg = null, string? backSvg = null)
```

In the card construction (lines 13-23), add after `CreatedAt`:
```csharp
FrontSvg = SvgSanitizer.Sanitize(frontSvg),
BackSvg = SvgSanitizer.Sanitize(backSvg),
```

Also update `CardEndpoints.Create` to pass the SVG fields from `CreateCardRequest` to `CreateCard`.

- [ ] **Step 7: Verify build**

Run: `dotnet build fasolt.Server`

- [ ] **Step 8: Commit**

```bash
git add fasolt.Server/Application/Services/CardService.cs
git commit -m "feat(cards): accept and store SVG in CardService"
```

---

### Task 5: Update LINQ Projections in ReviewEndpoints and DeckService

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs`
- Modify: `fasolt.Server/Application/Services/DeckService.cs`

- [ ] **Step 1: Update DueCardDto projection in ReviewEndpoints**

In `ReviewEndpoints.cs`, the `.Select()` at line 75:

```csharp
.Select(c => new DueCardDto(c.PublicId, c.Front, c.Back, c.SourceFile, c.SourceHeading, c.State))
```

Change to:

```csharp
.Select(c => new DueCardDto(c.PublicId, c.Front, c.Back, c.SourceFile, c.SourceHeading, c.State, c.FrontSvg, c.BackSvg))
```

- [ ] **Step 2: Update DeckCardDto projection in DeckService**

In `DeckService.cs`, the `.Select()` at line 59:

```csharp
.Select(dc => new DeckCardDto(dc.Card.PublicId, dc.Card.Front, dc.Card.Back, dc.Card.SourceFile, dc.Card.SourceHeading, dc.Card.State, dc.Card.DueAt, dc.Card.Stability, dc.Card.Difficulty, dc.Card.Step, dc.Card.LastReviewedAt))
```

Change to:

```csharp
.Select(dc => new DeckCardDto(dc.Card.PublicId, dc.Card.Front, dc.Card.Back, dc.Card.SourceFile, dc.Card.SourceHeading, dc.Card.State, dc.Card.DueAt, dc.Card.Stability, dc.Card.Difficulty, dc.Card.Step, dc.Card.LastReviewedAt, dc.Card.FrontSvg, dc.Card.BackSvg))
```

- [ ] **Step 3: Verify build**

Run: `dotnet build fasolt.Server`

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/ReviewEndpoints.cs fasolt.Server/Application/Services/DeckService.cs
git commit -m "feat(cards): include SVG in due card and deck card projections"
```

---

### Task 6: Update MCP CardTools + Add AddSvgToCard

**Files:**
- Modify: `fasolt.Server/Api/McpTools/CardTools.cs`

- [ ] **Step 1: Add SVG params to CreateCards tool**

In `CardTools.cs`, the `CreateCards` method (line 34). The `cards` parameter already accepts `BulkCardItem` which now has `FrontSvg`/`BackSvg`. Update the description to mention SVG support:

```csharp
[McpServerTool, Description("Create one or more flashcards, optionally linked to a source file and/or deck. Each card can include SVG images for front and/or back. Returns created cards, any skipped duplicates, and a deckUrl deep link if a deck was used.")]
```

- [ ] **Step 2: Add SVG params to UpdateCard tool**

In the `UpdateCard` method (line 69-107), add new parameters after `newSourceHeading` (line 77):

```csharp
[Description("New SVG image for front (raw SVG markup). Empty string clears.")] string? newFrontSvg = null,
[Description("New SVG image for back (raw SVG markup). Empty string clears.")] string? newBackSvg = null)
```

Update the validation check (line 81) to include the new fields:
```csharp
if (newFront is null && newBack is null && newSourceFile is null && newSourceHeading is null && newFrontSvg is null && newBackSvg is null)
    return JsonSerializer.Serialize(new { error = "Provide at least one field to update" }, McpJson.Options);
```

Update the `UpdateCardFieldsRequest` construction (line 84):
```csharp
var req = new UpdateCardFieldsRequest(newFront, newBack, newSourceFile, newSourceHeading, newFrontSvg, newBackSvg);
```

- [ ] **Step 3: Add AddSvgToCard tool**

Add after the `UpdateCard` method:

```csharp
[McpServerTool, Description("Add an SVG image to a card. LLMs can generate SVG diagrams, charts, chemical structures, math visualizations, etc. The SVG is sanitized server-side for security.")]
public async Task<string> AddSvgToCard(
    [Description("Card ID")] string cardId,
    [Description("Which side: 'front' or 'back'")] string side,
    [Description("Raw SVG markup (must start with <svg)")] string svg)
{
    var userId = McpUserResolver.GetUserId(httpContextAccessor);

    if (side is not "front" and not "back")
        return JsonSerializer.Serialize(new { error = "Side must be 'front' or 'back'" }, McpJson.Options);

    var req = side == "front"
        ? new UpdateCardFieldsRequest(NewFrontSvg: svg)
        : new UpdateCardFieldsRequest(NewBackSvg: svg);

    var result = await cardService.UpdateCardFields(userId, cardId, req);

    return result.Status switch
    {
        UpdateCardStatus.Success => JsonSerializer.Serialize(result.Card, McpJson.Options),
        UpdateCardStatus.NotFound => JsonSerializer.Serialize(new { error = "Card not found" }, McpJson.Options),
        _ => JsonSerializer.Serialize(new { error = "Unexpected error" }, McpJson.Options),
    };
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build fasolt.Server`

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Api/McpTools/CardTools.cs
git commit -m "feat(cards): add SVG params to MCP tools + AddSvgToCard tool"
```

---

### Task 7: Update Frontend Types and Create SVG Sanitizer Composable

**Files:**
- Modify: `fasolt.client/src/types/index.ts`
- Create: `fasolt.client/src/composables/useSvgSanitizer.ts`

- [ ] **Step 1: Add SVG fields to TypeScript types**

In `types/index.ts`, add to the `Card` interface (after `back`):
```typescript
frontSvg: string | null
backSvg: string | null
```

Add to `DeckCard` interface (after `dueAt`):
```typescript
frontSvg: string | null
backSvg: string | null
```

Add to `DueCard` interface (after `state`):
```typescript
frontSvg: string | null
backSvg: string | null
```

- [ ] **Step 2: Create SVG sanitizer composable**

Create `fasolt.client/src/composables/useSvgSanitizer.ts`:

```typescript
import DOMPurify from 'dompurify'

const SVG_CONFIG: DOMPurify.Config = {
  USE_PROFILES: { svg: true, svgFilters: true },
  ADD_TAGS: [],
  FORBID_TAGS: ['foreignObject', 'script', 'style'],
  FORBID_ATTR: ['style'],
  ALLOW_DATA_ATTR: false,
}
// Note: xlink:href is NOT forbidden because internal #fragment references
// (e.g., <use href="#id">) are legitimate. The server-side sanitizer
// already strips external href values — this is defense-in-depth only.

export function sanitizeSvg(svg: string): string {
  return DOMPurify.sanitize(svg, SVG_CONFIG)
}
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/types/index.ts fasolt.client/src/composables/useSvgSanitizer.ts
git commit -m "feat(cards): add SVG types and sanitizer composable"
```

---

### Task 8: Update ReviewCard to Render SVGs

**Files:**
- Modify: `fasolt.client/src/components/ReviewCard.vue`

- [ ] **Step 1: Add SVG rendering above text content**

In `ReviewCard.vue`, import the sanitizer:

```typescript
import { sanitizeSvg } from '@/composables/useSvgSanitizer'
```

Add SVG rendering above the front text (before line 23). After the "Question"/"Answer" label div (line 16-18), add front SVG:

```vue
<div v-if="card.frontSvg" class="mt-4 flex w-full max-w-lg justify-center">
  <div class="max-h-[300px] max-w-full [&>svg]:max-h-[300px] [&>svg]:max-w-full" v-html="sanitizeSvg(card.frontSvg)" />
</div>
```

Add back SVG above the back text (inside the `v-if="isFlipped"` block, before line 26):

```vue
<div v-if="isFlipped && card.backSvg" class="mt-4 flex w-full max-w-lg justify-center">
  <div class="max-h-[300px] max-w-full [&>svg]:max-h-[300px] [&>svg]:max-w-full" v-html="sanitizeSvg(card.backSvg)" />
</div>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/components/ReviewCard.vue
git commit -m "feat(cards): render SVG images in review cards"
```

---

### Task 9: Update CardDetailView with SVG Display and Paste Editor

**Files:**
- Modify: `fasolt.client/src/views/CardDetailView.vue`

- [ ] **Step 1: Add SVG display in view mode**

Import the sanitizer in `<script setup>`:
```typescript
import { sanitizeSvg } from '@/composables/useSvgSanitizer'
```

In view mode, above the front text display, add:
```vue
<div v-if="card.frontSvg" class="mb-3 flex justify-center rounded border border-border/40 bg-muted/30 p-4">
  <div class="max-h-[300px] max-w-full [&>svg]:max-h-[300px] [&>svg]:max-w-full" v-html="sanitizeSvg(card.frontSvg)" />
</div>
```

Same pattern for back SVG above the back text display.

- [ ] **Step 2: Add SVG paste editor in edit mode**

Add refs for SVG editing:
```typescript
const editFrontSvg = ref('')
const editBackSvg = ref('')
```

Initialize them when entering edit mode (where editFront/editBack are set):
```typescript
editFrontSvg.value = card.value?.frontSvg ?? ''
editBackSvg.value = card.value?.backSvg ?? ''
```

In the edit mode template, after the front textarea, add a collapsible SVG section:

```vue
<details class="mt-2">
  <summary class="cursor-pointer text-xs text-muted-foreground">SVG (front)</summary>
  <div class="mt-2 grid grid-cols-2 gap-2">
    <textarea
      v-model="editFrontSvg"
      class="min-h-[100px] rounded border border-border bg-background px-3 py-2 text-xs font-mono"
      placeholder="Paste SVG markup here..."
    />
    <div class="flex items-center justify-center rounded border border-border/40 bg-muted/30 p-2 min-h-[100px]">
      <div v-if="editFrontSvg" class="max-h-[200px] max-w-full [&>svg]:max-h-[200px] [&>svg]:max-w-full" v-html="sanitizeSvg(editFrontSvg)" />
      <span v-else class="text-xs text-muted-foreground">Preview</span>
    </div>
  </div>
  <Button v-if="editFrontSvg" variant="ghost" size="sm" class="mt-1 text-xs" @click="editFrontSvg = ''">Clear SVG</Button>
</details>
```

Same pattern for back SVG after the back textarea.

- [ ] **Step 3: Include SVG in save handler**

When saving edits, include SVG fields in the update request. The save handler should pass `frontSvg` and `backSvg` (empty string to clear, current value to keep/update).

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/views/CardDetailView.vue
git commit -m "feat(cards): add SVG display and paste editor to card detail"
```

---

### Task 10: Add SVG Indicator in Deck Detail Card List

**Files:**
- Modify: `fasolt.client/src/views/DeckDetailView.vue`

- [ ] **Step 1: Add SVG icon indicator to card rows**

In `DeckDetailView.vue`, in the card table rows, next to the card front text (or source file metadata), add a small image icon when the card has SVG content:

```vue
<span v-if="card.frontSvg || card.backSvg" class="text-muted-foreground" title="Has SVG image">
  ◆
</span>
```

Use a small inline indicator (diamond, image icon, etc.) — not the SVG itself.

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/views/DeckDetailView.vue
git commit -m "feat(cards): add SVG indicator icon in deck detail card list"
```

---

### Task 11: Update iOS Models

**Files:**
- Modify: `fasolt.ios/Fasolt/Models/APIModels.swift`
- Modify: `fasolt.ios/Fasolt/Models/Card.swift`

- [ ] **Step 1: Add SVG fields to iOS DTOs**

In `APIModels.swift`, add to `DeckCardDTO` (after `lastReviewedAt`):
```swift
let frontSvg: String?
let backSvg: String?
```

- [ ] **Step 2: Add SVG fields to Card SwiftData model**

In `Card.swift`, add stored properties (nullable, default nil for SwiftData migration):
```swift
var frontSvg: String?
var backSvg: String?
```

Update the initializer if it has one to include `frontSvg`/`backSvg` with `nil` defaults.

Update any cache write/read methods in repositories that construct Card objects.

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/
git commit -m "feat(cards): add SVG fields to iOS models"
```

---

### Task 12: Update iOS CardView to Render SVGs

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Study/CardView.swift`

- [ ] **Step 1: Add WKWebView wrapper for SVG rendering**

Create a SwiftUI wrapper for rendering SVG via WKWebView. Add a helper view (can be in the same file or a new `SvgView.swift`):

```swift
import WebKit
import SwiftUI

struct SvgView: UIViewRepresentable {
    let svg: String

    func makeUIView(context: Context) -> WKWebView {
        let webView = WKWebView()
        webView.isOpaque = false
        webView.backgroundColor = .clear
        webView.scrollView.isScrollEnabled = false
        return webView
    }

    func updateUIView(_ webView: WKWebView, context: Context) {
        let html = """
        <html><head><meta name="viewport" content="width=device-width,initial-scale=1">
        <style>body{margin:0;display:flex;justify-content:center;align-items:center;background:transparent}
        svg{max-width:100%;max-height:300px}</style></head>
        <body>\(svg)</body></html>
        """
        webView.loadHTMLString(html, baseURL: nil)
    }
}
```

- [ ] **Step 2: Render SVGs in CardView**

In `CardView.swift`, check if the current card has frontSvg/backSvg and render the `SvgView` above the text.

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/
git commit -m "feat(cards): render SVG images in iOS card views"
```

---

### Task 13: End-to-End Testing with Playwright

**Files:** None (browser testing)

- [ ] **Step 1: Start the full stack**

Run: `./dev.sh`

- [ ] **Step 2: Test SVG via API**

Create a card with SVG using curl:
```bash
curl -X POST http://localhost:8080/api/cards -H "Content-Type: application/json" -b cookies.txt -d '{
  "front": "What shape is this?",
  "back": "A circle",
  "frontSvg": "<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\"><circle cx=\"50\" cy=\"50\" r=\"40\" fill=\"blue\"/></svg>"
}'
```

- [ ] **Step 3: Test SVG renders in review**

Using Playwright MCP:
1. Login and navigate to study/review
2. Verify the SVG circle renders above the question text
3. Flip the card — verify no back SVG (we only set front)

- [ ] **Step 4: Test SVG paste editor in card detail**

1. Navigate to the card detail
2. Open the SVG section in edit mode
3. Paste SVG markup
4. Verify live preview shows the SVG
5. Save and verify it persists

- [ ] **Step 5: Commit any fixes**

---

### Task 14: Move Requirement to Done

**Files:**
- Move: `docs/requirements/14_images.md` → `docs/requirements/done/14_images.md`

Note: This is a partial completion — SVG only. Add a note to the moved file or create `14b_raster_images.md` for the deferred PNG/JPG scope.

- [ ] **Step 1: Move and commit**

```bash
mv docs/requirements/14_images.md docs/requirements/done/14_images.md
git add docs/requirements/14_images.md docs/requirements/done/14_images.md
git commit -m "docs: move images requirement to done (SVG implemented, PNG/JPG deferred)"
```
