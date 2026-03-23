# Complete UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Redesign the app from a generic admin panel to a study-first tool with a neutral color palette, new navigation structure, and purpose-built Study home page.

**Architecture:** Palette change via CSS variables (Task 1) propagates to all views automatically via shadcn. Then routing + layout changes (Tasks 2-3), then individual view rewrites (Tasks 4-9). Each task produces a working, navigable state.

**Tech Stack:** Vue 3 + TypeScript, Tailwind CSS 3, shadcn-vue, Pinia, Vue Router

**Spec:** `docs/superpowers/specs/2026-03-23-complete-redesign.md`

**Testing:** Verify each task visually with Playwright after making changes. The full stack must be running.

---

### Task 1: Color palette + remove glow utilities

**Files:**
- Modify: `fasolt.client/src/style.css`

This is the foundation — every shadcn component reads these CSS variables. Converting the spec's hex colors to HSL values.

- [ ] **Step 1: Replace the `:root` light theme variables**

Replace the `:root` block inside `@layer base` with:

```css
  :root {
    /* Light theme — neutral */
    --background: 210 20% 98%;
    --foreground: 228 33% 14%;
    --card: 0 0% 100%;
    --card-foreground: 228 33% 14%;
    --popover: 0 0% 100%;
    --popover-foreground: 228 33% 14%;
    --primary: 228 33% 14%;
    --primary-foreground: 0 0% 100%;
    --secondary: 220 14% 96%;
    --secondary-foreground: 228 33% 14%;
    --muted: 220 14% 96%;
    --muted-foreground: 220 9% 46%;
    --accent: 212 92% 45%;
    --accent-foreground: 0 0% 100%;
    --destructive: 0 72% 51%;
    --destructive-foreground: 0 0% 98%;
    --warning: 40 100% 37%;
    --warning-foreground: 0 0% 9%;
    --success: 160 84% 39%;
    --success-foreground: 0 0% 98%;
    --border: 220 18% 91%;
    --input: 220 18% 91%;
    --ring: 212 92% 45%;
    --radius: 0.375rem;

    --font-mono: 'JetBrains Mono', 'SF Mono', 'Cascadia Code', ui-monospace, monospace;
  }
```

- [ ] **Step 2: Replace the `.dark` theme variables**

```css
  .dark {
    /* Dark theme — GitHub Dark */
    --background: 215 28% 5%;
    --foreground: 210 29% 95%;
    --card: 215 24% 8%;
    --card-foreground: 210 29% 95%;
    --popover: 215 24% 8%;
    --popover-foreground: 210 29% 95%;
    --primary: 210 29% 95%;
    --primary-foreground: 215 28% 5%;
    --secondary: 215 20% 10%;
    --secondary-foreground: 210 29% 95%;
    --muted: 215 20% 10%;
    --muted-foreground: 212 12% 48%;
    --accent: 212 100% 67%;
    --accent-foreground: 215 28% 5%;
    --destructive: 0 72% 63%;
    --destructive-foreground: 0 0% 98%;
    --warning: 39 86% 49%;
    --warning-foreground: 0 0% 9%;
    --success: 160 84% 39%;
    --success-foreground: 0 0% 98%;
    --border: 215 14% 16%;
    --input: 215 14% 16%;
    --ring: 212 100% 67%;
  }
```

- [ ] **Step 3: Remove glow utilities, keep bg-grid**

Remove `.glow-accent`, `.glow-accent-lg`, and `.text-glow` from the `@layer utilities` block. Keep `.bg-grid` and `.bg-grid-fade` (used by LandingView).

- [ ] **Step 4: Update border-radius**

The `--radius` changed from `0.25rem` to `0.375rem` (6px). This is already in the `:root` block above.

- [ ] **Step 5: Verify and commit**

Open the app — colors should immediately change across all pages since everything uses CSS variables. Some pages may have broken `glow-accent` classes — that's expected, we'll clean those up in later tasks.

```bash
git add fasolt.client/src/style.css
git commit -m "ui: replace color palette with neutral theme, remove glow utilities"
```

---

### Task 2: Router — rename dashboard to study, remove mcp route, add redirects

**Files:**
- Modify: `fasolt.client/src/router/index.ts`

- [ ] **Step 1: Update the router**

1. Change the static import at line 3 from `DashboardView` to `StudyView` (file doesn't exist yet — we'll create it in Task 4, but we can update the import now to point to the same file temporarily):

```typescript
import StudyView from '@/views/DashboardView.vue'
```

2. Replace the dashboard route (line 47):
```typescript
{ path: '/study', name: 'study', component: StudyView },
```

3. Remove the MCP route (line 54):
```typescript
// DELETE: { path: '/mcp', name: 'mcp', component: () => import('@/views/McpView.vue') },
```

4. Add redirect routes after the study route:
```typescript
{ path: '/dashboard', redirect: '/study' },
{ path: '/mcp', redirect: '/settings' },
```

5. Update the `beforeEach` guard — change line 76 from `{ name: 'dashboard' }` to `{ name: 'study' }`.

6. In `fasolt.client/src/views/ReviewView.vue`, update the Escape handler (line 30) to navigate to `/study` instead of `/dashboard`.

7. In `fasolt.client/src/components/SessionComplete.vue`, change the "Back to dashboard" button text (line 41) to "Back to study" and update its link target to `/study`. Also remove the `glow-accent` class from that button and `text-glow` from line 16.

- [ ] **Step 2: Verify and commit**

Navigate to `/dashboard` — should redirect to `/study`. Navigate to `/mcp` — should redirect to `/settings`. Login should redirect to `/study`.

```bash
git add fasolt.client/src/router/index.ts fasolt.client/src/views/ReviewView.vue fasolt.client/src/components/SessionComplete.vue
git commit -m "ui: rename dashboard route to study, remove mcp route, add redirects"
```

---

### Task 3: Layout + Navigation — TopBar, AppLayout, BottomNav

**Files:**
- Modify: `fasolt.client/src/layouts/AppLayout.vue`
- Modify: `fasolt.client/src/components/TopBar.vue`
- Modify: `fasolt.client/src/components/BottomNav.vue`

- [ ] **Step 1: Update AppLayout.vue**

Replace the tabs array (lines 13-20) with:
```typescript
const tabs = [
  { label: 'Study', value: '/study' },
  { label: 'Cards', value: '/cards' },
  { label: 'Decks', value: '/decks' },
  { label: 'Sources', value: '/sources' },
  { label: 'Settings', value: '/settings' },
]
```

Update the nav tab styling (line 43) — change the active state classes from teal accent to black foreground:
```
data-[state=active]:border-accent data-[state=active]:text-accent
```
to:
```
data-[state=active]:border-foreground data-[state=active]:text-foreground data-[state=active]:font-semibold
```

- [ ] **Step 2: Simplify TopBar.vue**

Remove the theme toggle button entirely (lines 128-137). Remove the related imports and computed properties (`themeIcon`, `themeLabel`, `toggle` from useDarkMode). Keep the `useDarkMode()` call only to initialize dark mode — but since AppLayout already calls it, we can remove it from TopBar entirely.

Remove the beta badge (line 97: the `<span>` with "beta").

Remove `focus:glow-accent` from the search input class (line 105).

The TopBar should end up with: logo text + search + user avatar dropdown.

- [ ] **Step 3: Update BottomNav.vue**

Replace the tabs array (lines 6-13):
```typescript
const tabs = [
  { name: 'Study', path: '/study', icon: '◉' },
  { name: 'Cards', path: '/cards', icon: '▤' },
  { name: 'Decks', path: '/decks', icon: '⊞' },
  { name: 'Sources', path: '/sources', icon: '◫' },
  { name: 'Settings', path: '/settings', icon: '⚙' },
]
```

In the template, change active class from `text-accent` to `text-foreground` (line 27), and remove `text-glow` class reference (line 29).

- [ ] **Step 4: Verify and commit**

Check that nav shows 5 tabs in correct order, active tab is black/foreground instead of teal, no glow effects, no beta badge.

```bash
git add fasolt.client/src/layouts/AppLayout.vue fasolt.client/src/components/TopBar.vue fasolt.client/src/components/BottomNav.vue
git commit -m "ui: redesign navigation — 5 tabs, neutral active state, remove beta badge"
```

---

### Task 4: StudyView — new home page replacing DashboardView

**Files:**
- Create: `fasolt.client/src/views/StudyView.vue`
- Modify: `fasolt.client/src/router/index.ts` (update import)

- [ ] **Step 1: Create StudyView.vue**

Create the file with a centered study-first layout. It needs to:
- Fetch `reviewStore.fetchStats()` for due count, total cards, studied today
- Fetch `decksStore.fetchDecks()` for the deck list
- Show large due count (56px), "Start reviewing" CTA button (links to `/review`), two stats (total, studied today)
- Show "Study by deck" section with deck list, each linking to `/review?deckId={id}`
- When 0 due: show "All caught up" instead of the CTA button
- Use the spec's design: centered max-width ~480px layout

Template structure:
```vue
<template>
  <div class="mx-auto max-w-[480px] space-y-6 py-8">
    <!-- Due count -->
    <div class="text-center">
      <div class="text-[56px] font-extrabold leading-none tracking-tighter">{{ dueCount }}</div>
      <div class="mt-2 text-sm text-muted-foreground">cards due for review</div>
    </div>

    <!-- CTA -->
    <div class="text-center">
      <Button v-if="dueCount > 0" size="lg" class="px-10" @click="router.push('/review')">
        Start reviewing
      </Button>
      <p v-else class="text-sm text-muted-foreground">All caught up</p>
    </div>

    <!-- Stats -->
    <div class="flex justify-center gap-7 text-sm text-muted-foreground">
      <div><span class="font-bold text-foreground">{{ totalCards }}</span> total</div>
      <div><span class="font-bold text-foreground">{{ studiedToday }}</span> today</div>
    </div>

    <div class="border-t border-border" />

    <!-- Study by deck -->
    <div>
      <div class="text-[11px] font-semibold uppercase tracking-[1.2px] text-muted-foreground mb-3">Study by deck</div>
      <div class="flex flex-col gap-2">
        <div
          v-for="deck in decks"
          :key="deck.id"
          class="flex cursor-pointer items-center justify-between rounded-lg border border-border bg-card px-4 py-3 transition-colors hover:border-foreground/20"
          @click="router.push(`/review?deckId=${deck.id}`)"
        >
          <div>
            <div class="text-[13px] font-semibold">{{ deck.name }}</div>
            <div class="text-[11px] text-muted-foreground">{{ deck.cardCount }} cards</div>
          </div>
          <span v-if="deck.dueCount > 0" class="rounded-full bg-warning/10 px-2.5 py-0.5 text-[10px] font-medium text-warning">
            {{ deck.dueCount }} due
          </span>
          <span v-else class="rounded-full border border-border px-2.5 py-0.5 text-[10px] text-muted-foreground">
            0 due
          </span>
        </div>
      </div>
    </div>
  </div>
</template>
```

- [ ] **Step 2: Update router import**

Change line 3 of `router/index.ts`:
```typescript
import StudyView from '@/views/StudyView.vue'
```

- [ ] **Step 3: Verify and commit**

Navigate to `/study`. Should see the big due count, CTA button, stats, and deck list.

```bash
git add fasolt.client/src/views/StudyView.vue fasolt.client/src/router/index.ts
git commit -m "ui: create StudyView as study-first home page"
```

---

### Task 5: CardsView + CardDetailView — palette/typography update

**Files:**
- Modify: `fasolt.client/src/views/CardsView.vue`
- Modify: `fasolt.client/src/views/CardDetailView.vue`

The palette change from Task 1 handles most of the color update automatically. These views mostly need:
- Remove any `glow-accent` class references
- Ensure page titles use `text-lg font-bold` (18px)
- Ensure section labels, badges, and typography match the spec

- [ ] **Step 1: Update CardsView.vue**

The view already has the page title and toolbar from the previous redesign. Main changes:
- Remove any `glow-accent` references if present
- Ensure the page title is `text-lg font-bold tracking-tight` (not `text-xl` — spec says 18px)

- [ ] **Step 2: Update CardDetailView.vue**

This view was already redesigned with breadcrumbs, stat container, and section headings. Main changes:
- Remove any `glow-accent` references if present
- Change `bg-muted/50` on the stat container to `bg-secondary` (maps to `#f4f5f7` in the new palette)
- Ensure typography sizes match spec (title at 16px → `text-base font-bold`)

- [ ] **Step 3: Verify and commit**

```bash
git add fasolt.client/src/views/CardsView.vue fasolt.client/src/views/CardDetailView.vue
git commit -m "ui: update CardsView and CardDetailView for new palette"
```

---

### Task 6: DecksView + DeckDetailView — palette/typography update

**Files:**
- Modify: `fasolt.client/src/views/DecksView.vue`
- Modify: `fasolt.client/src/views/DeckDetailView.vue`

- [ ] **Step 1: Update DecksView.vue**

- Page title: `text-lg font-bold tracking-tight`
- Deck cards already have the grid layout and internal separator from the previous redesign
- Remove any `glow-accent` references

- [ ] **Step 2: Update DeckDetailView.vue**

- Page title: `text-xl font-bold tracking-tight` (20px per spec)
- Remove `glow-accent` from the "Study this deck" button class
- Change `bg-muted/50` on the stat bar to `bg-secondary`
- Section label "Cards in this deck" instead of just "Cards"

- [ ] **Step 3: Verify and commit**

```bash
git add fasolt.client/src/views/DecksView.vue fasolt.client/src/views/DeckDetailView.vue
git commit -m "ui: update DecksView and DeckDetailView for new palette"
```

---

### Task 7: SourcesView — palette/typography update

**Files:**
- Modify: `fasolt.client/src/views/SourcesView.vue`

- [ ] **Step 1: Update title and description**

- Page title: change from `text-xl` to `text-lg font-bold tracking-tight`
- Description: update text to "Files your cards were created from." (remove the MCP mention — that info now lives in Settings)

- [ ] **Step 2: Verify and commit**

```bash
git add fasolt.client/src/views/SourcesView.vue
git commit -m "ui: update SourcesView for new palette"
```

---

### Task 8: SettingsView — add MCP setup + Appearance sections

**Files:**
- Modify: `fasolt.client/src/views/SettingsView.vue`

This is the largest single task — it needs to absorb the MCP content and add the theme toggle.

- [ ] **Step 1: Add MCP setup section**

Import the MCP-related logic from McpView.vue: the `copiedStates` ref, `origin` computed, `remoteClaudeCommand` computed, `remoteCopilotConfig` computed, and `copyToClipboard` function. Add the Copy and Check icons import from lucide-vue-next.

Add a new Card section after the password card with:
- CardTitle "MCP Setup"
- How it works explanation text
- Claude Code command with copy button
- Copilot config with copy button

- [ ] **Step 2: Add Appearance section**

Import `useDarkMode` composable. Add a Card with:
- CardTitle "Appearance"
- Three-button segmented control for Light / System / Dark
- Use `theme` ref from useDarkMode to highlight the active option
- Each button calls a function that sets the theme

```vue
<div class="flex gap-1 rounded-lg border border-border p-1">
  <button
    v-for="opt in ['light', 'system', 'dark'] as const"
    :key="opt"
    class="rounded-md px-4 py-1.5 text-xs capitalize transition-colors"
    :class="theme === opt ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground'"
    @click="setTheme(opt)"
  >
    {{ opt }}
  </button>
</div>
```

The `setTheme` function updates `theme.value` and calls `localStorage.setItem` + `apply()`. This requires exporting a `setTheme` method from `useDarkMode.ts`.

- [ ] **Step 3: Update useDarkMode.ts to expose setTheme**

Add a `setTheme(t: Theme)` function alongside `toggle`:
```typescript
function setTheme(t: Theme) {
  theme.value = t
  localStorage.setItem(STORAGE_KEY, t)
  apply()
}

return { isDark, theme, toggle, setTheme }
```

- [ ] **Step 4: Update page title**

Change the h1 from `text-xl font-bold tracking-tight` to `text-lg font-bold tracking-tight` (18px per spec).

- [ ] **Step 5: Verify and commit**

Navigate to `/settings`. Should see Profile, Email, Password, MCP Setup, and Appearance sections. Theme toggle should work.

```bash
git add fasolt.client/src/views/SettingsView.vue fasolt.client/src/composables/useDarkMode.ts
git commit -m "ui: add MCP setup and Appearance sections to Settings"
```

---

### Task 9: Cleanup — remove old files and components

**Files:**
- Delete: `fasolt.client/src/views/DashboardView.vue`
- Delete: `fasolt.client/src/views/McpView.vue`
- Delete: `fasolt.client/src/components/DeckTable.vue`

- [ ] **Step 1: Verify no references remain**

```bash
grep -r "DashboardView\|McpView\|DeckTable" fasolt.client/src/ --include="*.vue" --include="*.ts"
```

DashboardView should no longer be imported (router now imports StudyView). McpView route was removed. DeckTable was used by DashboardView which is gone.

- [ ] **Step 2: Delete the files**

```bash
rm fasolt.client/src/views/DashboardView.vue
rm fasolt.client/src/views/McpView.vue
rm fasolt.client/src/components/DeckTable.vue
```

- [ ] **Step 3: Verify the app builds**

```bash
cd fasolt.client && npx vue-tsc --noEmit
```

- [ ] **Step 4: Commit**

```bash
git add -u
git commit -m "chore: remove DashboardView, McpView, DeckTable"
```

---

### Task 10: Visual verification — full Playwright walkthrough

- [ ] **Step 1: Screenshot all redesigned pages (light mode)**

Navigate to each page and take a full-page screenshot:
1. `/study` — big due count, CTA, deck list
2. `/cards` — page title, toolbar, table
3. `/cards/{id}` — breadcrumb, stats, front/back
4. `/decks` — page title, card grid
5. `/decks/{id}` — breadcrumb, stat bar, table
6. `/sources` — page title, card grid
7. `/settings` — all sections including MCP and Appearance

- [ ] **Step 2: Check dark mode**

Go to Settings, click "Dark" in Appearance. Spot-check Study page and Card Detail. Verify the dark palette renders correctly.

- [ ] **Step 3: Check redirects**

Navigate to `/dashboard` — should redirect to `/study`.
Navigate to `/mcp` — should redirect to `/settings`.

- [ ] **Step 4: Check mobile viewport**

Resize to 375px width. Verify:
- Bottom nav shows 5 tabs
- Study page is readable
- Card detail SRS grid wraps to 4 columns
