# UI Readability Redesign

## Problem

The UI lacks visual hierarchy and separation. Sections (title, description, stats, tables) all run together with near-identical font sizes, no grouping, and minimal spacing. The result feels gimmicky rather than practical.

## Design Decisions

- **Keep monospace font** — the terminal aesthetic stays, the readability problems are structural
- **All 7 SRS stats stay visible** on card detail — grouped into a shaded container
- **All views get the same treatment** — full consistency pass, not selective fixes
- **"Cards" page gets a title** — consistent with every other page

## Design System Changes

These patterns apply globally across all views.

### Page Titles

Every page gets a prominent title: `text-xl font-bold tracking-tight` (~20px). Currently titles are `text-base font-semibold` (~14px) — barely distinguishable from body text.

### Stat Bars

Replace the current stat cards (bordered boxes in a grid) and inline stat text with a single shaded container: `bg-muted/50 rounded-lg px-4 py-3`. Stats inside use `text-lg font-bold` for numbers, `text-xs text-muted-foreground` for labels, separated by vertical `border-r` dividers.

Used on: Dashboard (due/total/studied), Deck Detail (card count/due/state breakdown), Card Detail (7 SRS fields).

### Section Headings

Before each content block (tables, card grids, front/back areas), add an uppercase label: `text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3`.

### Breadcrumbs on Detail Pages

Replace `← Back` ghost buttons with breadcrumb text: `text-[11px] text-muted-foreground mb-4`. Format: `Parent / Current` where parent is a RouterLink. Applied to: DeckDetailView, CardDetailView.

### Spacing

Increase `space-y` from `space-y-5`/`space-y-6` to `space-y-6`/`space-y-8` between major zones. The gap between page header and first content block should be at least 24px.

## View-by-View Changes

### DashboardView

**Current:** `text-base` title, 3 StatCard components in a grid, DeckTable with no heading.

**New:**
- Title: `text-xl font-bold`
- Replace `<StatGrid>` with inline stat bar (shaded container, 3 stats with dividers)
- Add section heading "Your decks" above DeckTable
- StatGrid and StatCard components become unused (can be removed)

### DeckDetailView

**Current:** Back button + title inline, description as muted text, stats as a `flex-wrap` line of `text-[11px]` spans, table starts immediately.

**New:**
- Breadcrumb: `Decks / {name}`
- Title: `text-xl font-bold` with description below as `text-sm text-muted-foreground`
- Actions (Study/Edit/Delete) right-aligned in header row
- Stat bar: shaded container with card count, due count (warning color), then state breakdown after a divider
- Section heading "Cards" before the table
- DeckDetailDto `createdAt` field added to stat bar is not needed — keep it focused on card counts

### CardDetailView

**Current:** Back button + badge inline, metadata as `text-[11px]` spans, 7 separate bordered boxes in a grid, Front/Back labels as tiny uppercase text above bordered content areas.

**New:**
- Breadcrumb: `Cards / {front text truncated}`
- Title: card front text at `text-base font-bold` with state badge inline
- Metadata line: Source, Section, Decks as `text-xs text-muted-foreground` with values in `text-foreground`
- SRS stats: single shaded container, 7 columns on desktop (grid-cols-4 sm:grid-cols-7), each with uppercase label + value. Replaces the 7 individual bordered boxes.
- Section heading "Front" before front content, "Back" before back content
- Front/Back content boxes: keep the bordered containers but increase padding

### CardsView

**Current:** No page title, filters immediately at top, table below.

**New:**
- Title: `text-xl font-bold` "Cards" with "New card" button right-aligned
- Toolbar (filter inputs, state dropdown, columns button) below the header row
- Table remains the same — it's already well-structured

### DecksView

**Current:** `text-base` title, card grid with minimal internal structure.

**New:**
- Title: `text-xl font-bold`
- Deck cards: add a subtle `border-t` separator inside each card between the description area and the card count line, giving internal visual structure

### SourcesView

**Current:** `text-base` title, description paragraph, card grid.

**New:**
- Title: `text-xl font-bold` with description as `text-sm text-muted-foreground` directly below (not a separate paragraph block)
- Source cards: same layout, just inherits the global spacing improvements

### SettingsView

**New:**
- Title: `text-xl font-bold` "Settings"
- Section headings above each settings card (Profile, Email, Password) using the standard section heading style

## Files to Modify

1. `src/style.css` — no changes needed, all utility classes already available in Tailwind
2. `src/views/DashboardView.vue` — inline stat bar, section heading
3. `src/views/DeckDetailView.vue` — breadcrumb, title sizing, stat bar, section heading
4. `src/views/CardDetailView.vue` — breadcrumb, stat container, section headings
5. `src/views/CardsView.vue` — add page title
6. `src/views/DecksView.vue` — title sizing, card internal separator
7. `src/views/SourcesView.vue` — title sizing, description placement
8. `src/views/SettingsView.vue` — title sizing, section headings
9. `src/components/StatGrid.vue` — can be removed (unused after dashboard change)
10. `src/components/StatCard.vue` — can be removed (unused after dashboard change)

## What Does NOT Change

- Monospace font stays
- Color palette stays (accent teal, warning orange, muted greys)
- Table structure and column definitions stay
- Dialog/modal designs stay
- ReviewView stays (it's already well-structured as a focused study experience)
- BottomNav and TopBar stay
- Landing page stays
- MCP page stays
- Dark mode stays and inherits all changes via CSS variables
