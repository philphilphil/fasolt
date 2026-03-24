# Card Images (SVG) Design

## Overview

Add SVG image support to flashcards. SVGs are stored as dedicated text fields on the Card entity (separate from front/back text). AI agents generate SVGs via MCP tools. The web app supports paste-with-preview editing. This is SVG-only — raster image support (PNG/JPG with file upload) is deferred to a future requirement.

## Requirements

From `docs/requirements/14_images.md`:
- Cards can contain images
- Support SVG (special mode where the agent can provide SVG)
- Addable via MCP and in the web (SVG paste)

Note: This spec covers SVG only. PNG/JPG file upload is deferred.

## Data Model

### Card Entity

Add two nullable string fields:

```csharp
public string? FrontSvg { get; set; }
public string? BackSvg { get; set; }
```

One EF Core migration to add two nullable text columns. No default value needed.

### Validation

- Max **1MB** per SVG field
- Must start with `<svg` (basic sanity check)
- Server-side sanitization (see Security section)

## Backend

### DTOs — Add SVG fields

All card-related DTOs gain optional SVG fields. Add at the end of each record's parameter list with `= null` default:

- `BulkCardItem` — add `string? FrontSvg = null`, `string? BackSvg = null`
- `CreateCardRequest` — add `string? FrontSvg = null`, `string? BackSvg = null`
- `CardDto` — add `string? FrontSvg = null`, `string? BackSvg = null`
- `DeckCardDto` — add `string? FrontSvg = null`, `string? BackSvg = null`
- `DueCardDto` — add `string? FrontSvg = null`, `string? BackSvg = null`
- `UpdateCardFieldsRequest` — add `string? NewFrontSvg = null`, `string? NewBackSvg = null` (follows existing `New*` naming convention)

### DTO Construction Sites (must all be updated)

- `CardService.ToDto()` — add SVG fields to CardDto construction
- `CardService.BulkCreateCards` — accept SVG from BulkCardItem, store on Card, include in result DTO
- `CardService.ListCards` — LINQ `.Select()` projection must include FrontSvg/BackSvg
- `DeckService.GetDeck` — LINQ `.Select()` for DeckCardDto must include FrontSvg/BackSvg
- `ReviewEndpoints.GetDueCards` — LINQ `.Select()` for DueCardDto must include FrontSvg/BackSvg

### Services

- `CardService.BulkCreateCards` — accept and store SVG fields from BulkCardItem
- `CardService.CreateCard` — accept and store SVG fields
- `CardService.UpdateCardFields` — accept `NewFrontSvg`/`NewBackSvg`, apply same null-means-unchanged pattern as existing fields
- **SVG clearing convention**: empty string `""` means "clear the SVG", `null` means "leave unchanged" (consistent with how other nullable fields work via the `New*` pattern)

### SVG Sanitization Helper

Shared static method used by all creation/update paths. Use `System.Xml.Linq.XDocument` to parse and sanitize:

1. Strip `<script>` elements
2. Strip `<style>` elements
3. Strip all `on*` event attributes (onload, onclick, onerror, etc.)
4. Strip or sanitize `href` and `xlink:href` attributes — allow only `#fragment` references, disallow `javascript:`, `data:`, and external URLs
5. Strip `<use>` elements with external references (allow only `#fragment` hrefs)
6. Strip `<animate>`, `<set>`, `<animateTransform>` elements that target `href` attributes

### Endpoints

No new REST endpoints — existing create/update/get endpoints carry the new fields through DTOs.

### MCP Tools

- `CreateCards` — each card item gets optional `frontSvg` / `backSvg` parameters with descriptions explaining SVG support
- `UpdateCard` — add optional `newFrontSvg` / `newBackSvg` parameters. Empty string `""` clears the SVG, omitting leaves unchanged.
- **New tool: `AddSvgToCard`** — dedicated tool for agent discoverability:
  - Parameters: `cardId` (string), `side` ("front" | "back"), `svg` (string)
  - Sets the SVG for the specified side of a card
  - Description explains that LLMs can generate SVG diagrams, charts, visualizations, etc.

## Frontend (Web)

### Types (`types/index.ts`)

Add optional SVG fields to all card-related interfaces:
- `Card` — add `frontSvg: string | null`, `backSvg: string | null`
- `DeckCard` — add `frontSvg: string | null`, `backSvg: string | null`
- `DueCard` — add `frontSvg: string | null`, `backSvg: string | null`

### SVG Rendering

SVG fields are rendered separately from markdown text — they use their own DOMPurify invocation with SVG-specific configuration:

- Allowed elements: `svg`, `path`, `circle`, `rect`, `line`, `polyline`, `polygon`, `ellipse`, `g`, `defs`, `use`, `text`, `tspan`, `clipPath`, `mask`, `pattern`, `linearGradient`, `radialGradient`, `stop`, `filter`, `feGaussianBlur`, `feOffset`, `feMerge`, `feMergeNode`, `title`, `desc`, `marker`, `symbol`
- **Not allowed**: `foreignObject` (XSS vector — allows arbitrary HTML inside SVG), `script`, `style` elements
- Allowed attributes: `viewBox`, `width`, `height`, `fill`, `stroke`, `stroke-width`, `d`, `cx`, `cy`, `r`, `rx`, `ry`, `x`, `y`, `x1`, `y1`, `x2`, `y2`, `points`, `transform`, `opacity`, `font-size`, `font-family`, `text-anchor`, `dominant-baseline`, `class`, `id`, `xmlns`, `preserveAspectRatio`, `gradientUnits`, `offset`, `stop-color`, `stop-opacity`, `stroke-dasharray`, `stroke-linecap`, `stroke-linejoin`, `fill-opacity`, `stroke-opacity`, `marker-start`, `marker-mid`, `marker-end`
- **Not allowed**: `style` attribute (use SVG presentation attributes like `fill`, `stroke` instead — avoids CSS injection vectors), all `on*` event attributes, `href`/`xlink:href` with non-fragment values

### Markdown Renderer (`useMarkdown.ts`)

Leave the existing placeholder image rule as-is — it is unrelated to the SVG fields feature (SVGs are in dedicated fields, not inline in markdown).

### Review Card (`ReviewCard.vue`)

- If `frontSvg` / `backSvg` is present on the current card, render it above the text content
- SVG displayed in a constrained container (`max-width: 100%`, `max-height: 300px`, centered)
- SVG content sanitized via DOMPurify with SVG-specific config before `v-html` rendering

### Card Detail View (`CardDetailView.vue`)

- View mode: display SVGs above text (same as review)
- Edit mode: for each side (front/back), a collapsible "SVG" section with:
  - Textarea for pasting raw SVG markup
  - Live preview panel beside it showing the rendered SVG
  - Clear button to remove the SVG (sends empty string `""` via update)
- Save sends SVG via the existing update endpoint

### Deck Detail Card List

- Small icon indicator on cards that have SVGs (e.g., an image icon next to the card front text)
- Not the SVG itself — too heavy for a list view

## iOS

### Models

- `DeckCardDTO` in `APIModels.swift` — add `let frontSvg: String?`, `let backSvg: String?`
- `Card` SwiftData model — add `var frontSvg: String?`, `var backSvg: String?` stored properties (nullable, no migration issue)

### Study View (`CardView.swift`)

- If frontSvg/backSvg is present, render via a `WKWebView` wrapper (native SwiftUI SVG rendering is limited)
- Constrained to reasonable size within the card layout

### Card Detail

- Display SVGs if present using same web view approach

## Security

SVG is sanitized at two layers:

1. **Server-side** (on write): Structural sanitization using `XDocument` parser. Strips dangerous elements and attributes before storage. This is the security boundary — even if frontend sanitization is bypassed, stored SVGs are safe.

2. **Client-side** (on render): DOMPurify with SVG-specific allowlist config. Defense in depth.

Key threats mitigated:
- **Script injection**: `<script>` elements stripped server-side and blocked client-side
- **Event handler injection**: All `on*` attributes stripped
- **foreignObject escape**: `foreignObject` not in allowlist (allows arbitrary HTML inside SVG)
- **CSS injection**: `<style>` elements and `style` attribute not allowed
- **External resource loading**: `href`/`xlink:href` restricted to `#fragment` references only
- **Animation-based attacks**: Dangerous `<animate>`/`<set>` targeting `href` stripped

## Database

One migration: add `FrontSvg` and `BackSvg` nullable text columns to `Cards` table.

## Out of Scope

- PNG/JPG raster image support (requires file upload + blob storage — future requirement)
- Image generation (AI generates SVG, user pastes — no server-side generation)
- SVG optimization/minification
- Multiple images per card side
- Image search/indexing
- Markdown inline image rendering (unrelated to SVG fields)
