# Mobile & Sizing Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix undersized text and study mode layout issues across the entire frontend, establishing a 12px text floor and making study mode fit in one viewport on mobile.

**Architecture:** Pure CSS/Tailwind class changes across 15 Vue files. No new components, no structural changes. Study mode gets a wrapper div to decouple card sizing from available space.

**Tech Stack:** Vue 3, Tailwind CSS 3, shadcn-vue

---

### Task 1: Study Mode — ReviewCard layout and text

**Files:**
- Modify: `fasolt.client/src/components/ReviewCard.vue`

- [ ] **Step 1: Update ReviewCard classes**

Replace the entire template with these changes:
- Remove `flex-1` from root div
- Add `min-h-[180px]` and `max-h-[60vh] overflow-y-auto` to root div
- Change `text-[11px]` → `text-xs` on the question/answer label and source heading
- Change `prose prose-sm` → `prose` on both content divs

```vue
<template>
  <div
    class="flex min-h-[180px] max-h-[60vh] overflow-y-auto cursor-pointer flex-col items-center justify-center rounded-lg border border-border bg-card p-5 sm:p-8"
    @click="$emit('flip')"
  >
    <div class="text-xs uppercase tracking-widest text-muted-foreground">
      {{ isFlipped ? 'Answer' : 'Question' }}
    </div>
    <div
      class="mt-3 w-full max-w-lg text-center"
      :class="isFlipped ? 'text-muted-foreground' : 'text-foreground'"
    >
      <div class="prose dark:prose-invert max-w-none" v-html="render(card.front)" />
    </div>
    <div v-if="isFlipped" class="mt-4 w-full max-w-lg text-center">
      <div class="prose dark:prose-invert max-w-none" v-html="render(card.back)" />
    </div>
    <div v-if="card.sourceHeading" class="mt-3 font-mono text-xs text-muted-foreground">
      {{ card.sourceHeading }}
    </div>
  </div>
</template>
```

- [ ] **Step 2: Verify dev server shows the change**

Run: `cd fasolt.client && npm run dev` (if not already running)
Open the review page on a 390px viewport. Card should size to content, not fill the screen.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/components/ReviewCard.vue
git commit -m "fix: ReviewCard sizes to content, bump text to 12px/16px"
```

---

### Task 2: Study Mode — ReviewView wrapper and text

**Files:**
- Modify: `fasolt.client/src/views/ReviewView.vue`

- [ ] **Step 1: Update ReviewView layout**

Change the outer container from `min-h-[calc(100vh-8rem)]` to `min-h-0 flex-1`. Wrap the `<ReviewCard>` in a centering div with `flex-1 flex flex-col items-center justify-center`. Add `class="w-full"` to ReviewCard so it fills the wrapper. Bump `text-[11px]` → `text-xs` on context bar and flip hint.

The active review section template becomes:

```html
<template v-if="review.isActive && !review.isComplete">
  <!-- Context bar -->
  <div class="mb-3 flex items-center justify-between text-xs text-muted-foreground">
    <div class="flex items-center gap-2">
      <span class="text-foreground">Review session</span>
      <span>·</span>
      <span>{{ review.sessionStats.reviewed }} reviewed</span>
    </div>
    <div class="hidden items-center gap-2 sm:flex">
      <KbdHint keys="space" /> flip
      <span>·</span>
      <KbdHint keys="1-4" /> rate
    </div>
  </div>

  <!-- Progress meter -->
  <ProgressMeter :total="100" :current="review.progress" class="mb-5" />

  <!-- Card wrapper — fills remaining space, centers card -->
  <div class="flex flex-1 flex-col items-center justify-center">
    <ReviewCard
      v-if="review.currentCard"
      :card="review.currentCard"
      :is-flipped="review.isFlipped"
      class="w-full"
      @flip="review.flipCard()"
    />
  </div>

  <!-- Rating buttons (only when flipped) -->
  <div v-if="review.isFlipped" class="mt-4">
    <RatingButtons @rate="onRate" />
  </div>

  <div v-else class="mt-4 text-center text-xs text-muted-foreground">
    Click the card or press <KbdHint keys="space" /> to reveal the answer
  </div>
</template>
```

And the outer div changes to:

```html
<div class="flex min-h-0 flex-1 flex-col pb-16 sm:pb-0">
```

- [ ] **Step 2: Verify on mobile viewport**

Card + rating buttons should both be visible without scrolling on a 390px × 844px viewport.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/ReviewView.vue
git commit -m "fix: study mode fits in one viewport, bump context text to 12px"
```

---

### Task 3: Study Mode — RatingButtons and SessionComplete

**Files:**
- Modify: `fasolt.client/src/components/RatingButtons.vue`
- Modify: `fasolt.client/src/components/SessionComplete.vue`

- [ ] **Step 1: Update RatingButtons**

Change gap, button size, and text size:

```vue
<template>
  <div class="flex justify-center gap-2">
    <Button
      v-for="r in ratings"
      :key="r.rating"
      variant="outline"
      class="font-mono text-sm py-3"
      :class="r.highlight ? 'border-success text-success' : ''"
      @click="$emit('rate', r.rating)"
    >
      <span class="hidden text-muted-foreground sm:inline mr-1">{{ r.key }}</span>
      {{ r.label }}
    </Button>
  </div>
</template>
```

Key changes: `size="sm"` removed (defaults to default size), `text-[11px]` → `text-sm`, `gap-1.5` → `gap-2`, added `py-3` for taller touch targets.

- [ ] **Step 2: Update SessionComplete labels**

In `SessionComplete.vue`, change all four `text-[10px]` rating labels to `text-xs`:

```html
<div class="text-xs text-muted-foreground">Again</div>
```

(Same for Hard, Good, Easy)

- [ ] **Step 3: Verify study flow**

Complete a review session to check both rating buttons and session complete screen.

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/components/RatingButtons.vue fasolt.client/src/components/SessionComplete.vue
git commit -m "fix: larger rating buttons and session complete labels"
```

---

### Task 4: Global Chrome — StatCard, BottomNav, KbdHint

**Files:**
- Modify: `fasolt.client/src/components/StatCard.vue`
- Modify: `fasolt.client/src/components/BottomNav.vue`
- Modify: `fasolt.client/src/components/KbdHint.vue`

- [ ] **Step 1: Update StatCard**

Change both `text-[10px]` instances to `text-xs`:

```html
<div class="mt-0.5 text-xs uppercase tracking-widest text-muted-foreground">
  {{ stat.label }}
</div>
<div v-if="stat.delta" class="mt-1 text-xs text-success">
  {{ stat.delta }}
</div>
```

- [ ] **Step 2: Update BottomNav**

Change `text-[10px]` → `text-xs` on the nav link, `text-lg` → `text-xl` on the icon, `py-2` → `py-2.5` on the container:

```html
<nav class="fixed bottom-0 left-0 right-0 flex items-center justify-around border-t border-border bg-background py-2.5 sm:hidden">
  <RouterLink
    v-for="tab in tabs"
    :key="tab.path"
    :to="tab.path"
    class="flex flex-col items-center gap-0.5 px-3 py-1 text-xs"
    :class="isActive(tab.path) ? 'text-accent' : 'text-muted-foreground'"
  >
    <span class="text-xl">{{ tab.icon }}</span>
    <span>{{ tab.name }}</span>
  </RouterLink>
</nav>
```

- [ ] **Step 3: Update KbdHint**

Change `text-[10px]` → `text-xs`:

```html
<kbd class="inline-flex items-center justify-center rounded bg-secondary px-1.5 py-0.5 font-mono text-xs text-muted-foreground">
  {{ keys }}
</kbd>
```

- [ ] **Step 4: Verify dashboard and bottom nav**

Check dashboard stat cards and bottom nav at 390px viewport.

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/src/components/StatCard.vue fasolt.client/src/components/BottomNav.vue fasolt.client/src/components/KbdHint.vue
git commit -m "fix: bump StatCard, BottomNav, KbdHint text to 12px minimum"
```

---

### Task 5: TopBar and SearchResults

**Files:**
- Modify: `fasolt.client/src/components/TopBar.vue`
- Modify: `fasolt.client/src/components/SearchResults.vue`

- [ ] **Step 1: Update TopBar**

Change beta badge `text-[10px]` → `text-xs` (line 96):
```html
<span class="rounded-full border border-border bg-muted px-1.5 py-0.5 text-xs font-medium text-muted-foreground">beta</span>
```

Change `⌘K` kbd hint `text-[10px]` → `text-xs` (line 111):
```html
<kbd class="rounded border border-border bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">⌘K</kbd>
```

Change search input `text-xs` → `text-sm` (line 104):
```html
class="h-8 w-[260px] bg-secondary pl-8 text-sm"
```

- [ ] **Step 2: Update SearchResults**

Change all three `text-[10px]` instances to `text-xs`:

Section headers (lines 50, 71):
```html
<div class="px-3 py-1.5 text-xs font-medium uppercase tracking-wider text-muted-foreground">
```

State badge (line 63):
```html
<span class="ml-auto shrink-0 rounded bg-secondary px-1.5 py-0.5 text-xs text-muted-foreground">
```

Footer kbd hints (line 92):
```html
<div class="flex gap-3 border-t border-border px-3 py-1.5 text-xs text-muted-foreground">
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/components/TopBar.vue fasolt.client/src/components/SearchResults.vue
git commit -m "fix: bump TopBar and SearchResults text to 12px minimum"
```

---

### Task 6: Table Views — CardsView, DeckDetailView, DeckTable

**Files:**
- Modify: `fasolt.client/src/views/CardsView.vue`
- Modify: `fasolt.client/src/views/DeckDetailView.vue`
- Modify: `fasolt.client/src/components/DeckTable.vue`

- [ ] **Step 1: Update CardsView**

Table header row (line 236): `text-[10px]` → `text-xs`
```html
class="h-9 text-xs uppercase tracking-wider text-muted-foreground cursor-pointer select-none"
```

Sort arrows (lines 245-246): `text-[10px]` → `text-xs`

Table body rows (line 253): `text-xs` → `text-sm`
```html
<TableRow v-for="row in table.getRowModel().rows" :key="row.id" class="text-sm">
```

Badge text in column definitions (lines 106, 116): `text-[10px]` → `text-xs`
In the columns array, update the Badge class strings:
```typescript
cell: ({ row }) => h(Badge, { variant: 'outline', class: 'text-xs' }, () => row.getValue('state')),
```
```typescript
...deckList.map(d => h(Badge, { key: d.id, variant: 'outline', class: 'text-xs' }, () => d.name)),
```

"+" button (line 119): `text-[10px]` → `text-xs`
```typescript
class: 'text-xs text-muted-foreground hover:text-foreground',
```

Action buttons (lines 130-131): `text-[10px]` → `text-xs`
```typescript
h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-xs', onClick: () => openEdit(card) }, () => 'Edit'),
h(Button, { variant: 'ghost', size: 'sm', class: 'h-6 text-xs text-muted-foreground hover:text-destructive', onClick: () => { deleteTarget.value = card } }, () => '×'),
```

- [ ] **Step 2: Update DeckDetailView**

Table header row (line 114): `text-[10px]` → `text-xs`
```html
<TableRow class="text-xs uppercase tracking-wider text-muted-foreground hover:bg-transparent">
```

Table body rows (line 123): `text-xs` → `text-sm`
```html
<TableRow v-for="card in deck.cards" :key="card.id" class="text-sm">
```

Source badge (line 126): `text-[10px]` → `text-xs`
```html
<Badge v-if="card.sourceFile" variant="outline" class="text-xs font-mono">{{ card.sourceFile }}</Badge>
```

Remove button (line 135): `text-[10px]` → `text-xs`
```html
class="h-6 text-xs text-destructive hover:text-destructive"
```

- [ ] **Step 3: Update DeckTable**

Table header row (line 14): `text-[10px]` → `text-xs`
```html
<TableRow class="text-xs uppercase tracking-wider text-muted-foreground hover:bg-transparent">
```

Table body rows (line 25): `text-xs` → `text-sm`
```html
class="cursor-pointer text-sm"
```

- [ ] **Step 4: Verify tables**

Check Cards, Deck Detail, and Dashboard (DeckTable) views at mobile and desktop widths.

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/src/views/CardsView.vue fasolt.client/src/views/DeckDetailView.vue fasolt.client/src/components/DeckTable.vue
git commit -m "fix: bump table text sizes across CardsView, DeckDetailView, DeckTable"
```

---

### Task 7: Remaining Views — DecksView, SourcesView, CardDetailView

**Files:**
- Modify: `fasolt.client/src/views/DecksView.vue`
- Modify: `fasolt.client/src/views/SourcesView.vue`
- Modify: `fasolt.client/src/views/CardDetailView.vue`

- [ ] **Step 1: Update DecksView**

Deck description and card count (lines 65-66): `text-[11px]` → `text-xs`
```html
<div v-if="deck.description" class="mt-0.5 text-xs text-muted-foreground">{{ deck.description }}</div>
<div class="mt-0.5 text-xs text-muted-foreground">
  {{ deck.cardCount }} cards
</div>
```

- [ ] **Step 2: Update SourcesView**

Card count (line 36): `text-[11px]` → `text-xs`
```html
<div class="mt-0.5 text-xs text-muted-foreground">{{ source.cardCount }} cards</div>
```

Due badge (line 39): `text-[10px]` → `text-xs`
```html
<Badge v-if="source.dueCount > 0" variant="outline" class="font-mono text-xs text-warning">
```

- [ ] **Step 3: Update CardDetailView**

State badge (line 83): `text-[10px]` → `text-xs`
```html
<Badge variant="outline" class="text-xs">{{ card.state }}</Badge>
```

Card front/back prose (lines 138, 142): `prose-sm` → `prose` (consistent with ReviewCard)
```html
<div class="prose dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="render(card.front)" />
```
```html
<div class="prose dark:prose-invert max-w-none rounded-md border border-border p-3" v-html="render(card.back)" />
```

- [ ] **Step 4: Verify**

Check Decks, Sources, and Card Detail views at 390px viewport.

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/src/views/DecksView.vue fasolt.client/src/views/SourcesView.vue fasolt.client/src/views/CardDetailView.vue
git commit -m "fix: bump text sizes in DecksView, SourcesView, CardDetailView"
```

---

### Task 8: Playwright Smoke Test

**Files:** None (uses existing app)

- [ ] **Step 1: Start the full stack**

Run: `./dev.sh` (if not already running). Make sure the backend has been rebuilt if there were recent backend changes.

- [ ] **Step 2: Test study flow at mobile viewport**

Using Playwright MCP, navigate to the app at 390px width:
1. Log in with dev@fasolt.local / Dev1234!
2. Navigate to dashboard — verify stat cards are readable
3. Start a review session — verify card + rating buttons fit in one viewport without scrolling
4. Rate a card — verify buttons are easily tappable
5. Complete session — verify session complete screen labels are readable

- [ ] **Step 3: Test list views at mobile viewport**

1. Navigate to Cards view — verify table text is readable
2. Navigate to Decks view — verify card counts are readable
3. Navigate to Sources view — verify badges are readable
4. Check bottom nav — verify labels and icons are clear

- [ ] **Step 4: Test at desktop viewport**

1. Resize to 1280px width
2. Verify study mode card centers nicely (not stretched)
3. Verify tables look correct with larger text
4. Verify no visual regressions
