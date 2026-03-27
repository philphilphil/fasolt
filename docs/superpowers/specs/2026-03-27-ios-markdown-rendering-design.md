# iOS Markdown Rendering — Design Spec

**Issues:** #30 (Markdown renderer for card content), #31 (Strip markdown in list views)
**Date:** 2026-03-27

## Summary

Card content can contain markdown (bold, italic, inline code, lists, headings, etc.) but the iOS app displays it as plain text. Add markdown rendering in full card views using the Textual library, and strip markdown in list/row views using a hand-rolled utility.

## Dependency

**Textual** ([gonzalezreal/textual](https://github.com/gonzalezreal/textual)) — added via Swift Package Manager.

- Successor to MarkdownUI (3.8k stars), from the same author
- Pure SwiftUI: `StructuredText` view for block-level markdown, `InlineText` for inline
- GitHub Flavored Markdown support: headings, lists, code blocks, block quotes, tables, bold, italic, inline code, links
- Built-in theming with `.default` and `.gitHub` presets, plus full customization
- MIT license

## Rendering Strategy

### Full markdown rendering (Textual `StructuredText`)

These views currently use SwiftUI `Text` and will switch to `StructuredText`:

| View | File | Current Style | Notes |
|------|------|---------------|-------|
| CardView (study) | `Views/Study/CardView.swift` | `.title3`, centered | Primary study experience |
| CardDetailView | `Views/Shared/CardDetailView.swift` | `.body`, left-aligned | Full card info page |
| CardDetailSheet | `Views/Shared/CardDetailSheet.swift` | `.body` | Modal preview |

### Stripped markdown (plain text)

These views will use a `stripMarkdown()` utility to remove markdown syntax:

| View | File | Notes |
|------|------|-------|
| DeckCardRow | `Views/Decks/DeckCardRow.swift` | 2-line preview, used in card lists |

## `stripMarkdown()` Utility

A hand-rolled Swift function using regex. No third-party library — keeps it simple and avoids supply chain risk.

**Strips:**
- Heading markers (`#`, `##`, etc.)
- Bold/italic markers (`**`, `__`, `*`, `_`)
- Inline code backticks (`` ` ``)
- Code block fences (`` ``` ``)
- Link syntax `[text](url)` → `text`
- Image syntax `![alt](url)` → `alt`
- List markers (`-`, `*`, `1.`)
- Block quote markers (`>`)

**Behavior:**
- Returns clean plain text suitable for list row display
- Malformed markdown degrades to showing raw text (current behavior, acceptable)
- Lives in `Views/Shared/CardHelpers.swift` or a new `Utilities/StringExtensions.swift`

## Theming

- Use Textual's `.default` theme as the base
- Override font sizes to match existing context:
  - Study view (CardView): `.title3` equivalent
  - Detail views: `.body` equivalent
- No syntax highlighting — not needed for this content
- System colors for light/dark mode are handled automatically by Textual and SwiftUI

## Scope

- **iOS only** — web stripping is mentioned in #31 but not part of this implementation
- **Display only** — no API or data model changes
- **No SVG changes** — existing `SvgView` rendering is unaffected

## Edge Cases

- **Empty markdown** — renders as empty view (same as current empty text)
- **Malformed markdown** — Textual's parser handles gracefully; worst case shows raw text
- **Very long content** — already handled by `ScrollView` in study and detail views
- **Mixed content (text + SVG)** — SVG continues to render via `SvgView`; markdown rendering only applies to the text portions
