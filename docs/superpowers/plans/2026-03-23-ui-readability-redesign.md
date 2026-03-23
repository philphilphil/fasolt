# UI Readability Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve visual hierarchy and separation across all views by applying consistent page titles, stat bars, section headings, and breadcrumbs.

**Architecture:** Pure frontend template/class changes — no logic, data, or API changes. Each task modifies one view file's `<template>` block (and occasionally removes an unused import). All changes use existing Tailwind utility classes.

**Tech Stack:** Vue 3 + TypeScript, Tailwind CSS 3, shadcn-vue

**Spec:** `docs/superpowers/specs/2026-03-23-ui-readability-redesign.md`

**Testing:** After each task, run the dev server and verify with Playwright screenshots. The full stack must be running (`./dev.sh` or backend + frontend manually).

**Design system patterns used throughout (reference):**
- Page title: `text-xl font-bold tracking-tight`
- Stat bar: `bg-muted/50 rounded-lg px-4 py-3 flex items-center gap-5`
- Stat number: `text-lg font-bold`
- Stat label: `text-xs text-muted-foreground`
- Stat divider: `<div class="w-px h-5 bg-border" />`
- Section heading: `text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3`
- Breadcrumb: `text-[11px] text-muted-foreground mb-4` with RouterLink for parent
- View spacing: `space-y-6` (was space-y-5) or `space-y-8` for major sections

---

### Task 1: DashboardView — stat bar + section heading

**Files:**
- Modify: `fasolt.client/src/views/DashboardView.vue`

- [ ] **Step 1: Update the template**

Replace the entire `<template>` block with:

```vue
<template>
  <div class="space-y-6">
    <div class="flex items-center justify-between">
      <h1 class="text-xl font-bold tracking-tight">Dashboard</h1>
      <Button v-if="dueCount > 0" class="glow-accent" @click="studyNow">
        Study now · {{ dueCount }} due
      </Button>
    </div>

    <!-- Stat bar -->
    <div class="bg-muted/50 rounded-lg px-4 py-3 flex items-center gap-5">
      <div>
        <span class="text-lg font-bold text-warning">{{ stats[0].value }}</span>
        <span class="text-xs text-muted-foreground ml-1.5">due</span>
      </div>
      <div class="w-px h-5 bg-border" />
      <div>
        <span class="text-lg font-bold">{{ stats[1].value }}</span>
        <span class="text-xs text-muted-foreground ml-1.5">total cards</span>
      </div>
      <div class="w-px h-5 bg-border" />
      <div>
        <span class="text-lg font-bold">{{ stats[2].value }}</span>
        <span class="text-xs text-muted-foreground ml-1.5">studied today</span>
      </div>
    </div>

    <!-- Decks section -->
    <div>
      <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Your decks</div>
      <DeckTable :decks="decksStore.decks" @select-deck="onSelectDeck" />
    </div>
  </div>
</template>
```

- [ ] **Step 2: Remove unused StatGrid import**

In the `<script setup>` block, remove line 6: `import StatGrid from '@/components/StatGrid.vue'`. All other imports stay (both `Stat` and `Deck` types are still used).

- [ ] **Step 3: Verify in browser**

Navigate to `http://localhost:5173/dashboard`. Confirm:
- Title is visually larger and bolder
- Stats appear in a shaded bar with dividers (not separate cards)
- "Your decks" heading appears above the deck table

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/views/DashboardView.vue
git commit -m "ui: redesign dashboard with stat bar and section heading"
```

---

### Task 2: DeckDetailView — breadcrumb, title, stat bar, section heading

**Files:**
- Modify: `fasolt.client/src/views/DeckDetailView.vue`

- [ ] **Step 1: Update the template**

Replace lines 161-196 (from `<div v-else-if="deck"` through the description and stats line). Then wrap lines 197-232 (the card table `<div v-if="deck.cards.length">` block and the empty state `<div v-else>`) inside a new "Cards section" `<div>` with a section heading. Keep all dialogs (lines 234-270) unchanged.

The complete new template body inside `<div v-else-if="deck">`:

```vue
  <div v-else-if="deck" class="space-y-6">
    <!-- Breadcrumb -->
    <div class="text-[11px] text-muted-foreground">
      <RouterLink to="/decks" class="hover:text-foreground transition-colors">Decks</RouterLink>
      <span class="mx-1.5">/</span>
      <span class="text-foreground">{{ deck.name }}</span>
    </div>

    <!-- Header -->
    <div class="flex items-start justify-between">
      <div>
        <h1 class="text-xl font-bold tracking-tight">{{ deck.name }}</h1>
        <p v-if="deck.description" class="text-sm text-muted-foreground mt-1">{{ deck.description }}</p>
      </div>
      <div class="flex items-center gap-2">
        <Button
          v-if="deck.dueCount > 0"
          size="sm"
          class="text-xs glow-accent"
          @click="router.push(`/review?deckId=${deck.id}`)"
        >
          Study this deck
        </Button>
        <Button variant="outline" size="sm" class="h-7 text-[10px]" @click="openEdit">Edit</Button>
        <Button variant="outline" size="sm" class="h-7 text-[10px] text-destructive hover:text-destructive" @click="openDelete">Delete</Button>
      </div>
    </div>

    <!-- Stat bar -->
    <div class="bg-muted/50 rounded-lg px-4 py-3 flex items-center gap-5">
      <div>
        <span class="text-lg font-bold">{{ deck.cardCount }}</span>
        <span class="text-xs text-muted-foreground ml-1.5">cards</span>
      </div>
      <div class="w-px h-5 bg-border" />
      <div>
        <span class="text-lg font-bold text-warning">{{ deck.dueCount }}</span>
        <span class="text-xs text-muted-foreground ml-1.5">due</span>
      </div>
      <div class="w-px h-5 bg-border" />
      <div class="flex items-center gap-3 text-xs text-muted-foreground">
        <span v-for="state in ['new', 'learning', 'review', 'relearning']" :key="state">
          {{ stateCounts[state] || 0 }} {{ state }}
        </span>
      </div>
    </div>

    <!-- Cards section -->
    <div>
      <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Cards</div>

      <div v-if="deck.cards.length > 0" class="rounded border border-border/60">
        <!-- KEEP the existing <Table> block (lines 198-228) exactly as-is -->
      </div>

      <div v-else class="py-12 text-center text-xs text-muted-foreground">
        No cards in this deck yet. Add cards from the Cards view.
      </div>
    </div>
    <!-- END of Cards section -->

    <!-- KEEP all dialogs (Edit dialog, Delete confirmation dialog) exactly as-is -->
```

The `<Table>` block inside the `v-if` div stays identical to what's currently on lines 198-227.

- [ ] **Step 2: Remove unused Button import for back button**

The back button (`← Decks`) is replaced by the breadcrumb RouterLink. The `Button` import is still used for Edit/Delete/Study buttons, so keep it. No import changes needed.

- [ ] **Step 3: Verify in browser**

Navigate to `http://localhost:5173/decks`, click a deck. Confirm:
- Breadcrumb shows `Decks / Demo Deck`
- Title is large and bold, description below it
- Stats in shaded bar with dividers
- "Cards" section heading above the table

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/views/DeckDetailView.vue
git commit -m "ui: redesign deck detail with breadcrumb, stat bar, section heading"
```

---

### Task 3: CardDetailView — breadcrumb, stat container, section headings

**Files:**
- Modify: `fasolt.client/src/views/CardDetailView.vue`

- [ ] **Step 1: Add computed for truncated front text**

Add after the `error` ref:

```typescript
const truncatedFront = computed(() => {
  if (!card.value) return ''
  return card.value.front.length > 60 ? card.value.front.slice(0, 60) + '…' : card.value.front
})
```

Add `computed` to the vue import (line 1): `import { ref, computed, onMounted } from 'vue'`

- [ ] **Step 2: Update the template**

Replace the content inside `<div v-else-if="card" class="space-y-6">`:

```vue
  <div v-else-if="card" class="space-y-6">
    <!-- Breadcrumb -->
    <div class="text-[11px] text-muted-foreground">
      <RouterLink to="/cards" class="hover:text-foreground transition-colors">Cards</RouterLink>
      <span class="mx-1.5">/</span>
      <span class="text-foreground">{{ truncatedFront }}</span>
    </div>

    <!-- Header -->
    <div class="flex items-start justify-between">
      <div class="flex items-center gap-2.5">
        <h1 class="text-base font-bold tracking-tight">{{ truncatedFront }}</h1>
        <Badge variant="outline" class="text-[10px]">{{ card.state }}</Badge>
      </div>
      <div class="flex items-center gap-2">
        <Button v-if="!editing" variant="outline" size="sm" class="h-7 text-[10px]" @click="startEdit">Edit</Button>
        <Button
          variant="outline"
          size="sm"
          class="h-7 text-[10px] text-destructive hover:text-destructive"
          @click="deleteOpen = true"
        >
          Delete
        </Button>
      </div>
    </div>

    <!-- Metadata -->
    <div class="flex flex-wrap gap-x-6 gap-y-1 text-xs text-muted-foreground">
      <span v-if="card.sourceFile">Source: <span class="text-foreground">{{ card.sourceFile }}</span></span>
      <span v-if="card.sourceHeading">Section: <span class="text-foreground">{{ card.sourceHeading }}</span></span>
      <span v-if="card.decks.length > 0">Decks: <span class="text-foreground">{{ card.decks.map(d => d.name).join(', ') }}</span></span>
    </div>

    <!-- SRS Stats -->
    <div class="bg-muted/50 rounded-lg px-4 py-3 grid grid-cols-4 sm:grid-cols-7 gap-4">
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">State</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.state }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Due</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.dueAt ? formatDate(card.dueAt) : '—' }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Stability</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.stability != null ? card.stability.toFixed(2) : '—' }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Difficulty</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.difficulty != null ? card.difficulty.toFixed(2) : '—' }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Step</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.step ?? '—' }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Last Review</div>
        <div class="text-sm font-semibold mt-0.5">{{ card.lastReviewedAt ? formatDate(card.lastReviewedAt) : '—' }}</div>
      </div>
      <div>
        <div class="text-[9px] uppercase tracking-widest text-muted-foreground">Created</div>
        <div class="text-sm font-semibold mt-0.5">{{ formatDate(card.createdAt) }}</div>
      </div>
    </div>

    <!-- Edit mode -->
    <div v-if="editing" class="space-y-4">
      <div class="space-y-1">
        <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Front (question)</div>
        <textarea
          v-model="front"
          class="w-full rounded border border-border bg-transparent px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
          rows="3"
        />
      </div>
      <div class="space-y-1">
        <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Back (answer)</div>
        <textarea
          v-model="back"
          class="w-full rounded border border-border bg-transparent px-3 py-2 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
          rows="8"
        />
      </div>
      <div v-if="error" class="text-xs text-destructive">{{ error }}</div>
      <div class="flex gap-2">
        <Button size="sm" class="text-xs" :disabled="saving" @click="save">
          {{ saving ? 'Saving...' : 'Save' }}
        </Button>
        <Button variant="outline" size="sm" class="text-xs" @click="editing = false">Cancel</Button>
      </div>
    </div>

    <!-- View mode -->
    <div v-else class="space-y-5">
      <div>
        <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Front</div>
        <div class="prose dark:prose-invert max-w-none rounded border border-border/60 px-5 py-4" v-html="render(card.front)" />
      </div>
      <div>
        <div class="text-[11px] font-semibold uppercase tracking-widest text-muted-foreground border-b-2 border-border pb-1.5 mb-3">Back</div>
        <div class="prose dark:prose-invert max-w-none rounded border border-border/60 px-5 py-4" v-html="render(card.back)" />
      </div>
    </div>

    <!-- Delete confirmation (unchanged) -->
```

Keep the delete Dialog unchanged.

- [ ] **Step 3: Add RouterLink import**

Add `RouterLink` to the vue-router import:

```typescript
import { useRoute, useRouter, RouterLink } from 'vue-router'
```

- [ ] **Step 4: Verify in browser**

Navigate to `http://localhost:5173/cards/{id}`. Confirm:
- Breadcrumb: `Cards / What is Big O notation?`
- Title with state badge inline
- Metadata values in foreground color
- SRS stats in shaded grid (7 columns on desktop, 4 on mobile)
- Section headings for Front/Back with border rules

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/src/views/CardDetailView.vue
git commit -m "ui: redesign card detail with breadcrumb, stat container, section headings"
```

---

### Task 4: CardsView — add page title

**Files:**
- Modify: `fasolt.client/src/views/CardsView.vue`

- [ ] **Step 1: Update the template**

Replace the `<template>` block from the opening through line 235 (end of toolbar/button area). Keep everything from line 237 (`<!-- Table -->` comment) onward unchanged. The new template opening:

```vue
<template>
  <div class="space-y-4">
    <!-- Page header -->
    <div class="flex items-center justify-between">
      <h1 class="text-xl font-bold tracking-tight">Cards</h1>
      <Button size="sm" class="h-8 text-xs" @click="createOpen = true">New card</Button>
    </div>

    <!-- Toolbar -->
    <div class="flex items-center gap-2">
      <Input
        v-model="filterValue"
        placeholder="Filter cards..."
        class="h-8 max-w-[200px] text-xs"
      />
      <Input
        :value="sourceFilter"
        placeholder="Filter by source..."
        class="h-8 max-w-[200px] text-xs"
        @input="applySourceFilter(($event.target as HTMLInputElement).value)"
      />
      <select
        :value="stateFilter"
        class="h-8 rounded border border-border bg-transparent px-2 text-xs text-foreground"
        @change="applyStateFilter(($event.target as HTMLSelectElement).value)"
      >
        <option value="">All states</option>
        <option value="new">new</option>
        <option value="learning">learning</option>
        <option value="review">review</option>
        <option value="relearning">relearning</option>
      </select>
      <DropdownMenu>
        <DropdownMenuTrigger as-child>
          <Button variant="outline" size="sm" class="h-8 text-xs ml-auto">
            Columns
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuCheckboxItem
            v-for="column in table.getAllColumns().filter(c => c.getCanHide())"
            :key="column.id"
            class="capitalize text-xs"
            :model-value="column.getIsVisible()"
            @update:model-value="(value: boolean) => column.toggleVisibility(!!value)"
          >
            {{ column.id }}
          </DropdownMenuCheckboxItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>

    <!-- Table (unchanged from here) -->
```

Everything from `<!-- Table -->` (line 238) onward stays the same.

- [ ] **Step 2: Verify in browser**

Navigate to `http://localhost:5173/cards`. Confirm:
- "Cards" title appears at top left, "New card" button at top right
- Toolbar with filters sits below the title row
- Table and pagination unchanged

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/CardsView.vue
git commit -m "ui: add page title to cards view"
```

---

### Task 5: DecksView — title sizing + card internal separator

**Files:**
- Modify: `fasolt.client/src/views/DecksView.vue`

- [ ] **Step 1: Update the template**

Change the title class and add internal separator to deck cards:

1. Line 33: Change `text-base font-semibold tracking-tight` to `text-xl font-bold tracking-tight`

2. Replace the `<CardContent>` block (lines 62-75) with:

```vue
        <CardContent class="p-4">
          <div class="flex items-start justify-between">
            <div>
              <div class="text-sm font-semibold text-foreground">{{ deck.name }}</div>
              <div v-if="deck.description" class="mt-0.5 text-[11px] text-muted-foreground">{{ deck.description }}</div>
            </div>
            <span v-if="deck.dueCount > 0" class="text-xs text-warning whitespace-nowrap ml-3">
              {{ deck.dueCount }} due
            </span>
          </div>
          <div class="mt-2 pt-2 border-t border-border/40 text-[11px] text-muted-foreground">
            {{ deck.cardCount }} cards
          </div>
        </CardContent>
```

- [ ] **Step 2: Verify in browser**

Navigate to `http://localhost:5173/decks`. Confirm:
- Title is larger and bolder
- Each deck card has an internal separator line between description and card count

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/DecksView.vue
git commit -m "ui: redesign decks view with larger title and card separators"
```

---

### Task 6: SourcesView — title sizing + description placement

**Files:**
- Modify: `fasolt.client/src/views/SourcesView.vue`

- [ ] **Step 1: Update the template header**

Replace lines 16-22 (the title wrapper div + the separate description paragraph). The replacement collapses them into one block:

```vue
    <div>
      <h1 class="text-xl font-bold tracking-tight">Sources</h1>
      <p class="text-sm text-muted-foreground mt-1">
        Source files that cards have been created from. Use the MCP agent to create cards from your markdown notes.
      </p>
    </div>
```

- [ ] **Step 2: Verify in browser**

Navigate to `http://localhost:5173/sources`. Confirm:
- Title is larger, description sits directly below it as smaller muted text

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/SourcesView.vue
git commit -m "ui: redesign sources view title and description"
```

---

### Task 7: SettingsView — title sizing

**Files:**
- Modify: `fasolt.client/src/views/SettingsView.vue`

- [ ] **Step 1: Update the title**

Line 83: Change `text-base font-semibold tracking-tight` to `text-xl font-bold tracking-tight`

- [ ] **Step 2: Verify in browser**

Navigate to `http://localhost:5173/settings`. Confirm title is larger.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/SettingsView.vue
git commit -m "ui: update settings page title sizing"
```

---

### Task 8: Cleanup — remove unused StatGrid and StatCard

**Files:**
- Delete: `fasolt.client/src/components/StatGrid.vue`
- Delete: `fasolt.client/src/components/StatCard.vue`

- [ ] **Step 1: Verify no other references exist**

```bash
grep -r "StatGrid\|StatCard" fasolt.client/src/ --include="*.vue" --include="*.ts"
```

Should return zero results (DashboardView import was removed in Task 1).

- [ ] **Step 2: Delete the files**

```bash
rm fasolt.client/src/components/StatGrid.vue fasolt.client/src/components/StatCard.vue
```

- [ ] **Step 3: Verify the app still builds**

```bash
cd fasolt.client && npx vue-tsc --noEmit
```

- [ ] **Step 4: Commit**

```bash
git add -u fasolt.client/src/components/StatGrid.vue fasolt.client/src/components/StatCard.vue
git commit -m "chore: remove unused StatGrid and StatCard components"
```

---

### Task 9: Visual verification — full Playwright walkthrough

- [ ] **Step 1: Screenshot all redesigned pages**

Using Playwright (via MCP), navigate to each page and take a full-page screenshot:

1. `http://localhost:5173/dashboard` — verify stat bar, section heading, title
2. `http://localhost:5173/decks` — verify title, card separators
3. Click into a deck — verify breadcrumb, stat bar, section heading
4. `http://localhost:5173/cards` — verify page title above toolbar
5. Click into a card — verify breadcrumb, stat container, section headings for Front/Back
6. `http://localhost:5173/sources` — verify title + description
7. `http://localhost:5173/settings` — verify title sizing

- [ ] **Step 2: Check dark mode**

Toggle to dark mode and spot-check Dashboard + Deck Detail. The shaded stat bars (`bg-muted/50`) should render correctly with the dark theme variables.

- [ ] **Step 3: Check mobile viewport**

Resize to 375px width. Verify:
- Stat bars wrap gracefully
- Card detail SRS grid shows 4 columns (not 7)
- Breadcrumbs are readable
- Page titles don't overflow
