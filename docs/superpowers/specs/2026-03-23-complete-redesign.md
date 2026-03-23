# Complete UI Redesign

## Problem

The app looks like a generic admin panel. It doesn't feel purpose-built for studying — the Dashboard is a data overview, not a study launcher. The visual style (teal accents, glow effects, terminal theme) feels gimmicky rather than practical.

## Design Decisions

- **Study-first** — the home page is a study launcher, not a dashboard
- **5 tabs** — Study, Cards, Decks, Sources, Settings. Dashboard is replaced by Study. MCP page moves into Settings.
- **Neutral palette** — black/grey/white with blue for links, amber for "due" warnings. No teal accent, no glow effects.
- **Keep monospace** — the font stays, the aesthetic around it changes
- **Light-first** — light is the primary theme. Dark mode stays available.
- **Review view unchanged** — the actual card studying experience is fine as-is

## Navigation

### Current
Dashboard, Sources, Cards, Decks, MCP, Settings (6 tabs)

### New
Study, Cards, Decks, Sources, Settings (5 tabs)

- **Study** replaces Dashboard as the home/default route
- **MCP** content moves into Settings (it's a one-time setup page)
- Tab order reflects priority: study first, then management, then config

### Nav bar style
- White background, bottom border
- Logo ("fasolt") left-aligned, bold, black text — no icon needed
- Active tab: black text, `font-weight: 600`, 2px black bottom border
- Inactive tab: `#6b7280` text, no border
- Right side: command palette hint (Cmd+K), user avatar circle
- No theme toggle in nav bar — move to Settings page

## Color Palette

### Light theme
- Background: `#fafbfc`
- Card/surface: `#ffffff`
- Text primary: `#1a1f2e`
- Text secondary: `#6b7280`
- Text tertiary: `#9ca3af`
- Link: `#0969da`
- Border: `#e2e6ed`
- Border subtle: `#f0f2f5`
- Warning/due: `#bf8700`
- Warning bg: `#fff8e1`
- Destructive: `#dc2626`
- Stats background: `#f4f5f7`
- Primary button bg: `#1a1f2e`
- Primary button text: `#ffffff`

### Dark theme
- Background: `#0d1117`
- Card/surface: `#161b22`
- Text primary: `#f0f6fc`
- Text secondary: `#8b949e`
- Text tertiary: `#6e7681`
- Link: `#58a6ff`
- Border: `#21262d`
- Border subtle: `#1c2028`
- Warning/due: `#d29922`
- Warning bg: `#2a2000`
- Destructive: `#f85149`
- Stats background: `#161b22`
- Primary button bg: `#f0f6fc`
- Primary button text: `#0d1117`

## Typography

- Font: monospace (JetBrains Mono, SF Mono, Cascadia Code, ui-monospace)
- Page title: 18-20px, font-weight 700
- Card title / row link: 13px, font-weight 600
- Body text: 13px
- Small/meta: 11px
- Badge: 10px
- Section label: 11px, uppercase, letter-spacing 1.2px, font-weight 600, color tertiary
- Table header: 10px, uppercase, letter-spacing 1px, font-weight 600, color tertiary

## View-by-View Design

### StudyView (new — replaces DashboardView)

The home page. Centered layout, max-width ~480px.

- **Due count**: large number (56px, weight 800, tight letter-spacing), with "cards due for review" label below
- **CTA button**: "Start reviewing" — primary button, prominent, centered. Links to `/review`
- **Stats row**: two inline stats below the CTA — total cards, studied today. Numbers in primary color/weight, labels in tertiary. (No streak — the backend doesn't provide streak data, and adding it is out of scope for this redesign.)
- **Divider line**
- **"Study by deck" section**: section label, then a vertical list of deck cards. Each shows deck name, card count, and a "due" badge. Clicking a deck links to `/review?deckId={id}`. Only decks with due cards show the warning badge; others show an outline "0 due" badge.
- **When 0 cards are due**: show "0" as the big number, replace the CTA with "All caught up" text, keep the deck list below.

Route: `/study` (default redirect from `/`)

### CardsView

Same data table as current. Changes:

- Page title: "Cards" at 18px bold, "New card" button right-aligned
- Toolbar below title: filter inputs + state dropdown + columns dropdown. Same functionality.
- Table: same columns and behavior
- No visual style changes beyond the new color palette and typography

### DecksView

- Page title: "Decks" at 18px bold, "New deck" button right-aligned
- 2-column grid of deck cards
- Each card: white background, 1px border, 8px radius. Title (13px bold), description (11px muted), stat row at bottom with border-top separator showing card count and state counts. Due badge top-right if > 0.
- Clicking a card navigates to deck detail

### DeckDetailView

- Breadcrumb: `Decks / {name}`
- Title: 20px bold, description below as 12px muted
- Actions: "Study this deck" primary button (only shown when dueCount > 0) + "Edit" outline button, right-aligned
- Stats: inline — card count and due count as large numbers (18px bold), state breakdown after a vertical border separator in 11px muted
- Section label "Cards in this deck" above the table
- Table: same columns as current (Front, State, Due, Remove action)

### CardDetailView

- Breadcrumb: `Cards / {truncated front}`
- Title: 16px bold with state badge inline
- Metadata: Source, Section, Deck as 11px labels with values in primary color
- SRS stats: 7-column grid in a `#f4f5f7` rounded container. Labels 9px uppercase, values 12px semibold.
- Section labels "Front" and "Back" above content boxes
- Content boxes: white background, 1px border, 8px radius, 14px text with comfortable padding
- Edit/Delete buttons top-right

### SourcesView

- Page title: "Sources" at 18px bold, subtitle "Files your cards were created from." in 12px muted
- 2-column grid of source cards, same style as deck cards but simpler (name + card count + due badge)

### SettingsView

- Page title: "Settings" at 18px bold
- Keep existing Card/CardHeader/CardContent structure for Profile, Email, Password sections
- Add new section: "MCP Setup" — move the content from the current McpView into a settings card. Show the MCP endpoint URL, setup instructions for Claude Code and Copilot, copy buttons.
- Add "Appearance" section with three-button segmented control for theme (Light / System / Dark) — replaces the nav bar theme toggle

## Components to Remove

- `StatGrid.vue` — already removed
- `StatCard.vue` — already removed
- `DeckTable.vue` — replaced by inline deck list on Study page; Decks page uses its own grid

## Routes

| Old | New | Notes |
|-----|-----|-------|
| `/dashboard` | `/study` | Default route, redirect `/` → `/study` |
| `/cards` | `/cards` | Unchanged |
| `/cards/:id` | `/cards/:id` | Unchanged |
| `/decks` | `/decks` | Unchanged |
| `/decks/:id` | `/decks/:id` | Unchanged |
| `/sources` | `/sources` | Unchanged |
| `/review` | `/review` | Unchanged |
| `/mcp` | removed | Content moves to Settings |
| `/settings` | `/settings` | Gains MCP setup + appearance sections |
| `/dashboard` | redirect → `/study` | Backwards compat |

## Files to Modify

1. `src/style.css` — convert hex colors to HSL and update the existing shadcn CSS variables (`:root` and `.dark` blocks). Remove `.glow-accent`, `.glow-accent-lg`, `.text-glow` utilities. Keep `.bg-grid` for LandingView.
2. `src/views/DashboardView.vue` → rename/replace with `src/views/StudyView.vue`
3. `src/views/CardsView.vue` — new palette, typography
4. `src/views/CardDetailView.vue` — new palette, typography
5. `src/views/DecksView.vue` — new palette, card style
6. `src/views/DeckDetailView.vue` — new palette, typography
7. `src/views/SourcesView.vue` — new palette, typography
8. `src/views/SettingsView.vue` — add MCP setup and appearance sections
9. `src/views/McpView.vue` — remove (content moves to Settings)
10. `src/layouts/AppLayout.vue` — new nav bar design, remove theme toggle from TopBar
11. `src/components/TopBar.vue` — simplify: logo + nav tabs + search + avatar
12. `src/components/BottomNav.vue` — update to 5 tabs matching new nav
13. `src/components/DeckTable.vue` — remove (unused after Study page redesign)
14. `src/router/index.ts` — update routes, add redirects, update `beforeEach` guard to redirect authenticated users to `{ name: 'study' }` instead of `{ name: 'dashboard' }`
15. `src/composables/useDarkMode.ts` — keep, but trigger from Settings instead of nav bar

## What Does NOT Change

- ReviewView — the card review experience stays as-is
- LandingView / AlgorithmView — public pages stay unchanged
- Auth views (login, register, forgot-password, reset-password) — unchanged
- All backend API endpoints and data contracts — unchanged
- Card/Deck/Source data models and stores — unchanged
- Dialog components (create, edit, delete) — functionality stays, styling inherits new palette
- Search functionality — unchanged (SearchResults.vue uses `accent` CSS variable which will be remapped via style.css)
- TerminalDemo.vue — unchanged (lives on LandingView which is out of scope)
- NotFoundView — unchanged
- Beta badge in nav — remove (clean look, no need for a badge)
