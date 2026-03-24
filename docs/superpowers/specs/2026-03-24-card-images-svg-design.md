# Card Images (SVG) Design

## Overview

Add SVG image support to flashcards. SVGs are stored as dedicated text fields on the Card entity (separate from front/back text). AI agents generate SVGs via MCP tools. The web app supports paste-with-preview editing. This is SVG-only — raster image support (PNG/JPG with file upload) is deferred.

## Requirements

From `docs/requirements/14_images.md`:
- Cards can contain images
- Support SVG (special mode where the agent can provide SVG)
- Addable via MCP and in the web (SVG paste)

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
- Server-side sanitization: strip `<script>` tags and event handler attributes (`onload`, `onclick`, `onerror`, etc.)

## Backend

### DTOs — Add `FrontSvg?` / `BackSvg?`

All card-related DTOs gain optional SVG fields:

- `BulkCardItem` — add `string? FrontSvg`, `string? BackSvg` (card creation)
- `CardDto` — add `string? FrontSvg`, `string? BackSvg` (card detail response)
- `DeckCardDto` — add `string? FrontSvg`, `string? BackSvg` (deck detail card list)
- `DueCardDto` — add `string? FrontSvg`, `string? BackSvg` (review/study)
- `UpdateCardFieldsRequest` — add `string? FrontSvg`, `string? BackSvg`

### Services

- `CardService.BulkCreateCards` — accept and store SVG fields
- `CardService.UpdateCardFields` — accept and store SVG fields
- SVG sanitization as a shared helper method (strip scripts, event handlers)

### Endpoints

No new REST endpoints — existing create/update/get endpoints carry the new fields through DTOs.

### MCP Tools

- `CreateCards` — each card item gets optional `frontSvg` / `backSvg` parameters with descriptions explaining SVG support
- `UpdateCard` — add optional `newFrontSvg` / `newBackSvg` parameters
- **New tool: `AddSvgToCard`** — dedicated tool for agent discoverability:
  - Parameters: `cardId` (string), `side` ("front" | "back"), `svg` (string)
  - Sets the SVG for the specified side of a card
  - Description explains that LLMs can generate SVG diagrams, charts, etc.

## Frontend (Web)

### Markdown Renderer (`useMarkdown.ts`)

- Remove the placeholder image rule that shows `[alt-text]` badges
- Configure DOMPurify to allow `<svg>` and safe SVG elements/attributes:
  - Allowed elements: `svg`, `path`, `circle`, `rect`, `line`, `polyline`, `polygon`, `ellipse`, `g`, `defs`, `use`, `text`, `tspan`, `clipPath`, `mask`, `pattern`, `linearGradient`, `radialGradient`, `stop`, `filter`, `feGaussianBlur`, `feOffset`, `feMerge`, `feMergeNode`, `title`, `desc`, `marker`, `symbol`, `foreignObject`
  - Allowed attributes: `viewBox`, `width`, `height`, `fill`, `stroke`, `stroke-width`, `d`, `cx`, `cy`, `r`, `rx`, `ry`, `x`, `y`, `x1`, `y1`, `x2`, `y2`, `points`, `transform`, `opacity`, `font-size`, `font-family`, `text-anchor`, `dominant-baseline`, `class`, `id`, `style`, `xmlns`, `preserveAspectRatio`, `gradientUnits`, `offset`, `stop-color`, `stop-opacity`, `stroke-dasharray`, `stroke-linecap`, `stroke-linejoin`, `fill-opacity`, `stroke-opacity`, `marker-start`, `marker-mid`, `marker-end`
  - Stripped: `<script>`, `onload`, `onclick`, `onerror`, and all `on*` event attributes

### Review Card (`ReviewCard.vue`)

- If `frontSvg` / `backSvg` is present on the current card, render it above the text content
- SVG displayed in a constrained container (`max-width: 100%`, `max-height: 300px`, centered)
- SVG content sanitized via DOMPurify before `v-html` rendering

### Card Detail View (`CardDetailView.vue`)

- View mode: display SVGs above text (same as review)
- Edit mode: for each side (front/back), a collapsible "SVG" section with:
  - Textarea for pasting raw SVG markup
  - Live preview panel beside it showing the rendered SVG
  - Clear button to remove the SVG
- Save sends SVG via the existing update endpoint

### Deck Detail Card List

- Small icon indicator on cards that have SVGs (e.g., an image icon next to the card front text)
- Not the SVG itself — too heavy for a list view

## iOS

### Models

- `DeckCardDTO`, `CardDTO` in `APIModels.swift` — add `let frontSvg: String?`, `let backSvg: String?`
- `Card` SwiftData model — add `var frontSvg: String?`, `var backSvg: String?` stored properties (nullable, no migration issue)

### Study View (`CardView.swift`)

- If frontSvg/backSvg is present, render via a `WKWebView` wrapper (native SwiftUI SVG rendering is limited)
- Constrained to reasonable size within the card layout

### Card Detail

- Display SVGs if present using same web view approach

## Database

One migration: add `FrontSvg` and `BackSvg` nullable text columns to `Cards` table.

## Out of Scope

- PNG/JPG raster image support (requires file upload + blob storage)
- Image generation (AI generates SVG, user pastes — no server-side generation)
- SVG optimization/minification
- Multiple images per card side
- Image search/indexing
