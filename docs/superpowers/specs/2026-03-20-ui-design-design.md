# UI Design Spec: Command Center

**Date:** 2026-03-20
**Status:** Approved
**Direction chosen:** Command Center — dark, dense, keyboard-friendly, information-rich

## Design Principles

1. **Everything visible** — no hidden state. Due counts, progress, stats are always on screen.
2. **Keyboard-first, click-friendly** — shortcuts exist for power users; all actions are also clickable.
3. **Semantic color only** — every color communicates meaning. No decorative color.
4. **Responsive by default** — works equally well on desktop and mobile.

## Color System

Follows OS light/dark preference via `prefers-color-scheme`. No manual toggle (system setting is authoritative).

### Dark theme (default when OS is dark)

| Token            | Value     | Usage                              |
|------------------|-----------|------------------------------------|
| `--bg-root`      | `#0d0d0d` | Page background                    |
| `--bg-surface`   | `#111111` | Cards, panels, table rows          |
| `--bg-elevated`  | `#151515` | Search bar, hover states           |
| `--border`       | `#1a1a1a` | Subtle dividers                    |
| `--border-strong`| `#222222` | Input borders, card outlines       |
| `--text-primary` | `#ffffff` | Headings, primary content          |
| `--text-secondary`| `#888888`| Labels, secondary info             |
| `--text-muted`   | `#555555` | Hints, disabled text               |
| `--accent`       | `#3b82f6` | Active tab, selected state, links  |
| `--warning`      | `#f59e0b` | Due counts, attention items        |
| `--success`      | `#22c55e` | Positive deltas, "good" rating     |
| `--destructive`  | `#ef4444` | Delete actions, errors             |

### Light theme (default when OS is light)

Inverted luminance: white/gray backgrounds, dark text. Same accent, warning, success, destructive hues adjusted for contrast. All pairs must meet WCAG AA (4.5:1 for text, 3:1 for large text/UI).

## Typography

- **UI text:** System font stack (`-apple-system, BlinkMacSystemFont, 'Inter', 'Segoe UI', sans-serif`)
- **Data/numbers:** Monospace (`'JetBrains Mono', 'SF Mono', 'Cascadia Code', monospace`) for stat values, counts, keyboard hints, file paths
- **Scale:** 10px (labels/kbd), 11-12px (body/table), 13-14px (UI text), 17-22px (stat values), 26px (page headings, rare)
- **Weight:** 400 default, 600 headings, 700 stat values
- **Letter-spacing:** -0.02em on headings and stat values, 0.06-0.08em on uppercase labels

## Layout & Navigation

### Top bar
- Left: logo (`spaced-md`, monospace, 13px, bold)
- Center: search input with `⌘K` badge — nice-to-have shortcut, not primary navigation
- Right: user avatar/menu

### Tabs
- Below top bar: `Dashboard | Files | Groups | Settings`
- Active tab: white text, blue underline (2px)
- Inactive: `--text-muted`

### Content area
- Full-width, no sidebar
- Max-width container (1200px) centered on large screens
- Consistent padding: 20px on all sides

### Mobile (<768px)
- Tabs move to bottom navigation bar (4 icons + labels)
- Search behind icon in top bar
- Content goes full-width with 16px padding

### Tablet (768-1024px)
- Stat grid: 2x2 instead of 4-across
- Table remains, columns may truncate

## Pages

### Dashboard

**Stat grid** (4 cards, single row on desktop):

| Stat      | Format           | Detail            |
|-----------|------------------|-------------------|
| Due       | Monospace number  | `↑ N from yesterday` delta |
| Total     | Monospace number  | —                 |
| Retention | Monospace percent | `↑ N% this week` delta |
| Streak    | Monospace + `d`   | —                 |

Each stat card: `--bg-surface` background, `--border` border, 6px border-radius, 12px padding.

**Deck table** below stats:

| Column       | Width | Style                     |
|--------------|-------|---------------------------|
| Deck name    | 2fr   | White, weight 500         |
| Due          | 1fr   | `--warning` color         |
| Cards        | 1fr   | `--text-secondary`        |
| Next review  | 1fr   | `--text-muted`, relative time |

Rows are clickable — navigates to deck detail or starts review if cards are due.

### Files

- Table: file name, card count, date uploaded, file size
- Click row to expand inline: shows heading tree of the markdown file
- Per-heading "Create cards" action button
- Drag-and-drop upload zone at top (dashed border, `--border-strong`)
- Empty state: centered message with upload prompt

### Groups

- List view: group name, card count, due count
- Create/edit/delete actions
- Click to view group contents or start group-specific review
- Cards can belong to multiple groups

### Study/Review Flow

**Entry:** Click a due count on dashboard, or "Start review" from a deck/group detail.

**Layout (top to bottom):**

1. **Context bar:** Deck name · progress count (4/12) · due remaining · keyboard hints (`space` flip, `1-4` rate)
2. **Progress meter:** Horizontal segmented bar, one segment per card. Completed = `--accent` solid, current = `--accent` 40% opacity, remaining = `--border`
3. **Card panel:** Centered, `--bg-surface` background, `--border` border, 8px radius, 32px padding
   - "QUESTION" label (11px, uppercase, `--text-muted`)
   - Card text (17px, white, centered)
   - Source reference below (11px, monospace, `--text-muted`): `filename.md → ## Section`
4. **Rating buttons:** Row of 4: Again, Hard, Good, Easy
   - Each shows keyboard shortcut prefix: `1`, `2`, `3`, `4`
   - Default: `--bg-surface` bg, `--border-strong` border, `--text-secondary` text
   - "Good" highlighted: `--success` border and text
   - Monospace font for shortcut numbers

**Card flip:** Pressing space or clicking the card reveals the answer in the same panel. Question text fades to `--text-muted`, answer appears below in white.

**Session complete screen:** Cards reviewed count, average quality, breakdown by rating, next review dates summary, "Back to dashboard" button.

**Mobile adjustments:**
- Progress meter uses dots instead of segments
- Rating buttons: larger touch targets (48px height), no keyboard hints shown
- Card panel: full-width, 20px padding
- Swipe gestures: left = Again, right = Good (stretch goal)

## Keyboard Shortcuts

| Context    | Key       | Action              |
|------------|-----------|---------------------|
| Global     | `⌘K`     | Focus search        |
| Global     | `1-4`     | Navigate tabs       |
| Review     | `Space`   | Flip card           |
| Review     | `1`       | Rate: Again         |
| Review     | `2`       | Rate: Hard          |
| Review     | `3`       | Rate: Good          |
| Review     | `4`       | Rate: Easy          |
| Review     | `Esc`     | Exit review session |

## Component Inventory

shadcn-vue components to install/use:

- **Button** (already installed) — actions, rating buttons
- **Card** — stat cards, deck rows
- **Table** — deck list, file list
- **Input** — search bar, forms
- **Dialog** — confirmations, create group
- **DropdownMenu** — user menu, actions
- **Tabs** — main navigation
- **Progress** — review progress bar
- **Badge** — due counts, deltas
- **Tooltip** — keyboard shortcut hints on hover

## Accessibility

- All interactive elements keyboard-navigable with visible focus rings
- ARIA labels on stat cards (`aria-label="12 cards due today"`)
- Rating buttons use `aria-pressed` state
- Progress meter has `role="progressbar"` with `aria-valuenow`/`aria-valuemax`
- Color is never the sole indicator — icons or text accompany colored states
- Minimum contrast: WCAG AA (4.5:1 body text, 3:1 UI components)
