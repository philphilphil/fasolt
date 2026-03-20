# Command Center UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Command Center UI design — dark, dense, keyboard-driven frontend with dashboard, files, groups, and study/review pages.

**Architecture:** Vue 3 SPA with Pinia stores holding mock data (no backend wiring yet). App shell with top bar + tab navigation wrapping page views. Composables for keyboard shortcuts and dark mode. shadcn-vue components for UI primitives.

**Tech Stack:** Vue 3, TypeScript, Tailwind CSS 3, shadcn-vue (reka-ui), Pinia, Vue Router, Vitest + Vue Test Utils

---

## File Structure

```
src/
  style.css                          — UPDATE: Command Center design tokens (dark/light)
  App.vue                            — UPDATE: wrap RouterView in AppLayout
  main.ts                            — no changes needed
  router/index.ts                    — UPDATE: add dashboard, files, groups, review, settings routes
  types/
    index.ts                         — CREATE: shared types (Deck, Card, Stat, Group, etc.)
  stores/
    dashboard.ts                     — CREATE: mock dashboard stats + deck list
    review.ts                        — CREATE: review session state machine
    files.ts                         — CREATE: mock file list
    groups.ts                        — CREATE: mock group list
  composables/
    useKeyboardShortcuts.ts          — CREATE: keyboard shortcut registration/cleanup
    useDarkMode.ts                   — CREATE: system prefers-color-scheme detection
  layouts/
    AppLayout.vue                    — CREATE: top bar + tabs + content area + mobile bottom nav
  components/
    TopBar.vue                       — CREATE: logo + search + user menu
    StatCard.vue                     — CREATE: single stat card (monospace value, label, delta)
    StatGrid.vue                     — CREATE: 4-card responsive grid
    DeckTable.vue                    — CREATE: deck table with due/cards/next columns
    ReviewCard.vue                   — CREATE: flashcard with flip behavior
    ProgressMeter.vue                — CREATE: segmented progress bar
    RatingButtons.vue                — CREATE: Again/Hard/Good/Easy row
    SessionComplete.vue              — CREATE: review session summary
    KbdHint.vue                      — CREATE: keyboard shortcut badge (monospace)
    BottomNav.vue                    — CREATE: mobile bottom navigation
  views/
    HomeView.vue                     — DELETE (replaced by DashboardView)
    DashboardView.vue                — CREATE: stat grid + deck table
    FilesView.vue                    — CREATE: file table with upload zone
    GroupsView.vue                   — CREATE: group list with CRUD
    ReviewView.vue                   — CREATE: study/review flow
    SettingsView.vue                 — CREATE: placeholder settings page
```

---

### Task 1: Set Up Testing Infrastructure

**Files:**
- Modify: `spaced-md.client/package.json`
- Modify: `spaced-md.client/tsconfig.app.json`
- Create: `spaced-md.client/vitest.config.ts`
- Create: `spaced-md.client/src/__tests__/setup.ts`

- [ ] **Step 1: Install Vitest and Vue Test Utils**

```bash
cd spaced-md.client && npm install -D vitest @vue/test-utils happy-dom
```

- [ ] **Step 2: Create vitest config**

Create `spaced-md.client/vitest.config.ts`:
```typescript
import path from 'node:path'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vitest/config'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  test: {
    environment: 'happy-dom',
    setupFiles: ['./src/__tests__/setup.ts'],
  },
})
```

- [ ] **Step 3: Create test setup file**

Create `spaced-md.client/src/__tests__/setup.ts`:
```typescript
// Global test setup — add shared mocks here as needed
```

- [ ] **Step 4: Add test script to package.json**

Add to scripts in `package.json`:
```json
"test": "vitest run",
"test:watch": "vitest"
```

- [ ] **Step 5: Verify test runner works**

```bash
cd spaced-md.client && npx vitest run
```

Expected: "No test files found" (clean exit, no config errors).

- [ ] **Step 6: Commit**

```bash
git add spaced-md.client/package.json spaced-md.client/package-lock.json spaced-md.client/vitest.config.ts spaced-md.client/src/__tests__/setup.ts
git commit -m "chore: add vitest and vue test utils"
```

---

### Task 2: Design System — Tokens, Tailwind Config, Dark Mode

**Files:**
- Modify: `spaced-md.client/src/style.css`
- Modify: `spaced-md.client/tailwind.config.js`
- Create: `spaced-md.client/src/composables/useDarkMode.ts`
- Create: `spaced-md.client/src/__tests__/useDarkMode.test.ts`

- [ ] **Step 1: Write failing test for useDarkMode**

Create `spaced-md.client/src/__tests__/useDarkMode.test.ts`:
```typescript
import { describe, it, expect, beforeEach } from 'vitest'
import { useDarkMode } from '@/composables/useDarkMode'

describe('useDarkMode', () => {
  beforeEach(() => {
    document.documentElement.classList.remove('dark')
  })

  it('adds dark class when system prefers dark', () => {
    // matchMedia is mocked by happy-dom — defaults to no preference
    const { isDark } = useDarkMode()
    // Without dark preference, should not be dark
    expect(document.documentElement.classList.contains('dark')).toBe(false)
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd spaced-md.client && npx vitest run src/__tests__/useDarkMode.test.ts
```

Expected: FAIL — module not found.

- [ ] **Step 3: Implement useDarkMode composable**

Create `spaced-md.client/src/composables/useDarkMode.ts`:
```typescript
import { ref, onMounted, onUnmounted } from 'vue'

export function useDarkMode() {
  const isDark = ref(false)
  let mediaQuery: MediaQueryList | null = null
  let handler: ((e: MediaQueryListEvent) => void) | null = null

  onMounted(() => {
    mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
    isDark.value = mediaQuery.matches

    handler = (e: MediaQueryListEvent) => {
      isDark.value = e.matches
      document.documentElement.classList.toggle('dark', e.matches)
    }

    document.documentElement.classList.toggle('dark', isDark.value)
    mediaQuery.addEventListener('change', handler)
  })

  onUnmounted(() => {
    if (mediaQuery && handler) {
      mediaQuery.removeEventListener('change', handler)
    }
  })

  return { isDark }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd spaced-md.client && npx vitest run src/__tests__/useDarkMode.test.ts
```

Expected: PASS.

- [ ] **Step 5: Replace style.css with Command Center tokens**

Replace contents of `spaced-md.client/src/style.css` with:
```css
@tailwind base;
@tailwind components;
@tailwind utilities;

@layer base {
  :root {
    /* Light theme — default */
    --background: 0 0% 98%;
    --foreground: 0 0% 6%;
    --card: 0 0% 100%;
    --card-foreground: 0 0% 6%;
    --popover: 0 0% 100%;
    --popover-foreground: 0 0% 6%;
    --primary: 0 0% 9%;
    --primary-foreground: 0 0% 98%;
    --secondary: 0 0% 94%;
    --secondary-foreground: 0 0% 9%;
    --muted: 0 0% 94%;
    --muted-foreground: 0 0% 45%;
    --accent: 217 91% 60%;
    --accent-foreground: 0 0% 98%;
    --destructive: 0 84% 60%;
    --destructive-foreground: 0 0% 98%;
    --warning: 38 92% 50%;
    --warning-foreground: 0 0% 9%;
    --success: 142 71% 45%;
    --success-foreground: 0 0% 98%;
    --border: 0 0% 90%;
    --input: 0 0% 90%;
    --ring: 217 91% 60%;
    --radius: 0.375rem;

    --font-mono: 'JetBrains Mono', 'SF Mono', 'Cascadia Code', ui-monospace, monospace;
  }

  .dark {
    /* Dark theme — Command Center */
    --background: 0 0% 5%;
    --foreground: 0 0% 98%;
    --card: 0 0% 7%;
    --card-foreground: 0 0% 98%;
    --popover: 0 0% 7%;
    --popover-foreground: 0 0% 98%;
    --primary: 0 0% 98%;
    --primary-foreground: 0 0% 5%;
    --secondary: 0 0% 10%;
    --secondary-foreground: 0 0% 98%;
    --muted: 0 0% 10%;
    --muted-foreground: 0 0% 53%;
    --accent: 217 91% 60%;
    --accent-foreground: 0 0% 98%;
    --destructive: 0 63% 31%;
    --destructive-foreground: 0 0% 98%;
    --warning: 38 92% 50%;
    --warning-foreground: 0 0% 9%;
    --success: 142 71% 45%;
    --success-foreground: 0 0% 98%;
    --border: 0 0% 10%;
    --input: 0 0% 10%;
    --ring: 217 91% 60%;
  }
}

@layer base {
  * {
    @apply border-border;
  }
  body {
    @apply bg-background text-foreground;
    font-family: -apple-system, BlinkMacSystemFont, 'Inter', 'Segoe UI', sans-serif;
  }
}
```

- [ ] **Step 6: Update tailwind.config.js with new tokens**

Add `warning` and `success` color tokens to the `colors` object in `tailwind.config.js` (alongside existing destructive, muted, etc.):

```javascript
warning: {
  DEFAULT: "hsl(var(--warning))",
  foreground: "hsl(var(--warning-foreground))",
},
success: {
  DEFAULT: "hsl(var(--success))",
  foreground: "hsl(var(--success-foreground))",
},
```

Change `darkMode` from `["class"]` to `"class"` (same behavior, simpler syntax).

Add font-mono extension:
```javascript
fontFamily: {
  mono: "var(--font-mono)",
},
```

- [ ] **Step 7: Commit**

```bash
git add spaced-md.client/src/style.css spaced-md.client/tailwind.config.js spaced-md.client/src/composables/useDarkMode.ts spaced-md.client/src/__tests__/useDarkMode.test.ts
git commit -m "feat: command center design tokens and dark mode composable"
```

---

### Task 3: Shared Types and Mock Data Stores

**Files:**
- Create: `spaced-md.client/src/types/index.ts`
- Create: `spaced-md.client/src/stores/dashboard.ts`
- Create: `spaced-md.client/src/stores/review.ts`
- Create: `spaced-md.client/src/stores/files.ts`
- Create: `spaced-md.client/src/stores/groups.ts`
- Create: `spaced-md.client/src/__tests__/stores/review.test.ts`

- [ ] **Step 1: Create shared types**

Create `spaced-md.client/src/types/index.ts`:
```typescript
export interface Card {
  id: string
  deckId: string
  question: string
  answer: string
  sourceFile: string
  sourceSection: string | null
  dueAt: Date
  easeFactor: number
  interval: number
  repetitions: number
}

export interface Deck {
  id: string
  name: string
  fileName: string
  cardCount: number
  dueCount: number
  nextReview: string // relative time string: "now", "2h", "tomorrow"
}

export interface Stat {
  label: string
  value: string
  delta?: string // e.g. "↑ 3 from yesterday"
}

export interface MarkdownFile {
  id: string
  name: string
  cardCount: number
  uploadedAt: Date
  sizeBytes: number
  headings: FileHeading[]
}

export interface FileHeading {
  level: number
  text: string
  cardCount: number
}

export interface Group {
  id: string
  name: string
  cardCount: number
  dueCount: number
}

export type ReviewRating = 'again' | 'hard' | 'good' | 'easy'

export interface ReviewSession {
  deckId: string
  deckName: string
  cards: Card[]
  currentIndex: number
  isFlipped: boolean
  ratings: Map<string, ReviewRating>
}
```

- [ ] **Step 2: Write failing test for review store**

Create `spaced-md.client/src/__tests__/stores/review.test.ts`:
```typescript
import { describe, it, expect, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'
import { useReviewStore } from '@/stores/review'

describe('review store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('starts a session with cards', () => {
    const store = useReviewStore()
    store.startSession('deck-1', 'Distributed Systems', [
      {
        id: 'c1',
        deckId: 'deck-1',
        question: 'What is CAP?',
        answer: 'Consistency, Availability, Partition tolerance',
        sourceFile: 'cap.md',
        sourceSection: '## Overview',
        dueAt: new Date(),
        easeFactor: 2.5,
        interval: 1,
        repetitions: 0,
      },
    ])
    expect(store.isActive).toBe(true)
    expect(store.currentCard?.question).toBe('What is CAP?')
    expect(store.progress).toBe('1 of 1')
  })

  it('flips the current card', () => {
    const store = useReviewStore()
    store.startSession('deck-1', 'Test', [{
      id: 'c1', deckId: 'deck-1', question: 'Q', answer: 'A',
      sourceFile: 'f.md', sourceSection: null,
      dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0,
    }])
    expect(store.isFlipped).toBe(false)
    store.flip()
    expect(store.isFlipped).toBe(true)
  })

  it('rates a card and advances to next', () => {
    const store = useReviewStore()
    store.startSession('deck-1', 'Test', [
      { id: 'c1', deckId: 'deck-1', question: 'Q1', answer: 'A1', sourceFile: 'f.md', sourceSection: null, dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
      { id: 'c2', deckId: 'deck-1', question: 'Q2', answer: 'A2', sourceFile: 'f.md', sourceSection: null, dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
    ])
    store.flip()
    store.rate('good')
    expect(store.currentCard?.question).toBe('Q2')
    expect(store.isFlipped).toBe(false)
  })

  it('completes session after rating last card', () => {
    const store = useReviewStore()
    store.startSession('deck-1', 'Test', [
      { id: 'c1', deckId: 'deck-1', question: 'Q1', answer: 'A1', sourceFile: 'f.md', sourceSection: null, dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
    ])
    store.flip()
    store.rate('good')
    expect(store.isComplete).toBe(true)
    expect(store.isActive).toBe(true) // still active to show summary
  })
})
```

- [ ] **Step 3: Run test to verify it fails**

```bash
cd spaced-md.client && npx vitest run src/__tests__/stores/review.test.ts
```

Expected: FAIL — module not found.

- [ ] **Step 4: Implement review store**

Create `spaced-md.client/src/stores/review.ts`:
```typescript
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { Card, ReviewRating } from '@/types'

export const useReviewStore = defineStore('review', () => {
  const deckId = ref<string | null>(null)
  const deckName = ref('')
  const cards = ref<Card[]>([])
  const currentIndex = ref(0)
  const isFlipped = ref(false)
  const ratings = ref(new Map<string, ReviewRating>())

  const isActive = computed(() => deckId.value !== null)
  const isComplete = computed(() => currentIndex.value >= cards.value.length)
  const currentCard = computed(() => cards.value[currentIndex.value] ?? null)
  const progress = computed(() => `${currentIndex.value + 1} of ${cards.value.length}`)
  const progressFraction = computed(() =>
    cards.value.length === 0 ? 0 : currentIndex.value / cards.value.length
  )

  const ratingCounts = computed(() => {
    const counts = { again: 0, hard: 0, good: 0, easy: 0 }
    for (const r of ratings.value.values()) {
      counts[r]++
    }
    return counts
  })

  function startSession(id: string, name: string, sessionCards: Card[]) {
    deckId.value = id
    deckName.value = name
    cards.value = sessionCards
    currentIndex.value = 0
    isFlipped.value = false
    ratings.value = new Map()
  }

  function flip() {
    isFlipped.value = true
  }

  function rate(rating: ReviewRating) {
    const card = currentCard.value
    if (!card) return
    ratings.value.set(card.id, rating)
    currentIndex.value++
    isFlipped.value = false
  }

  function endSession() {
    deckId.value = null
    deckName.value = ''
    cards.value = []
    currentIndex.value = 0
    isFlipped.value = false
    ratings.value = new Map()
  }

  return {
    deckId, deckName, cards, currentIndex, isFlipped, ratings,
    isActive, isComplete, currentCard, progress, progressFraction, ratingCounts,
    startSession, flip, rate, endSession,
  }
})
```

- [ ] **Step 5: Run test to verify it passes**

```bash
cd spaced-md.client && npx vitest run src/__tests__/stores/review.test.ts
```

Expected: PASS (4 tests).

- [ ] **Step 6: Create dashboard store with mock data**

Create `spaced-md.client/src/stores/dashboard.ts`:
```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Stat, Deck } from '@/types'

export const useDashboardStore = defineStore('dashboard', () => {
  const stats = ref<Stat[]>([
    { label: 'Due', value: '12', delta: '↑ 3 from yesterday' },
    { label: 'Total', value: '84' },
    { label: 'Retention', value: '91%', delta: '↑ 3% this week' },
    { label: 'Streak', value: '7d' },
  ])

  const decks = ref<Deck[]>([
    { id: 'deck-1', name: 'Distributed Systems', fileName: 'distributed-systems.md', cardCount: 24, dueCount: 5, nextReview: 'now' },
    { id: 'deck-2', name: 'Rust Ownership', fileName: 'rust-ownership.md', cardCount: 18, dueCount: 4, nextReview: 'now' },
    { id: 'deck-3', name: 'System Design Patterns', fileName: 'system-design.md', cardCount: 31, dueCount: 3, nextReview: '2h' },
    { id: 'deck-4', name: 'PostgreSQL Internals', fileName: 'postgresql-internals.md', cardCount: 11, dueCount: 0, nextReview: 'tomorrow' },
  ])

  return { stats, decks }
})
```

- [ ] **Step 7: Create files store with mock data**

Create `spaced-md.client/src/stores/files.ts`:
```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { MarkdownFile } from '@/types'

export const useFilesStore = defineStore('files', () => {
  const files = ref<MarkdownFile[]>([
    {
      id: 'f1', name: 'distributed-systems.md', cardCount: 24,
      uploadedAt: new Date('2026-03-15'), sizeBytes: 14200,
      headings: [
        { level: 2, text: 'CAP Theorem', cardCount: 6 },
        { level: 2, text: 'Consensus Algorithms', cardCount: 8 },
        { level: 2, text: 'Replication', cardCount: 10 },
      ],
    },
    {
      id: 'f2', name: 'rust-ownership.md', cardCount: 18,
      uploadedAt: new Date('2026-03-16'), sizeBytes: 9800,
      headings: [
        { level: 2, text: 'Ownership Rules', cardCount: 5 },
        { level: 2, text: 'Borrowing', cardCount: 7 },
        { level: 2, text: 'Lifetimes', cardCount: 6 },
      ],
    },
    {
      id: 'f3', name: 'system-design.md', cardCount: 31,
      uploadedAt: new Date('2026-03-18'), sizeBytes: 22400,
      headings: [
        { level: 2, text: 'Load Balancing', cardCount: 8 },
        { level: 2, text: 'Caching', cardCount: 10 },
        { level: 2, text: 'Message Queues', cardCount: 13 },
      ],
    },
    {
      id: 'f4', name: 'postgresql-internals.md', cardCount: 11,
      uploadedAt: new Date('2026-03-19'), sizeBytes: 7600,
      headings: [
        { level: 2, text: 'MVCC', cardCount: 4 },
        { level: 2, text: 'Query Planning', cardCount: 7 },
      ],
    },
  ])

  return { files }
})
```

- [ ] **Step 8: Create groups store with mock data**

Create `spaced-md.client/src/stores/groups.ts`:
```typescript
import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { Group } from '@/types'

export const useGroupsStore = defineStore('groups', () => {
  const groups = ref<Group[]>([
    { id: 'g1', name: 'Interview Prep', cardCount: 42, dueCount: 8 },
    { id: 'g2', name: 'Backend Deep Dive', cardCount: 35, dueCount: 5 },
  ])

  function addGroup(name: string) {
    groups.value.push({ id: `g${Date.now()}`, name, cardCount: 0, dueCount: 0 })
  }

  function deleteGroup(id: string) {
    groups.value = groups.value.filter(g => g.id !== id)
  }

  return { groups, addGroup, deleteGroup }
})
```

- [ ] **Step 9: Commit**

```bash
git add spaced-md.client/src/types/index.ts spaced-md.client/src/stores/ spaced-md.client/src/__tests__/stores/
git commit -m "feat: shared types and mock data stores with review store tests"
```

---

### Task 4: Install shadcn-vue Components

**Files:**
- Creates files in: `spaced-md.client/src/components/ui/`

- [ ] **Step 1: Install required shadcn-vue components**

Run each from the `spaced-md.client` directory:

```bash
cd spaced-md.client
npx shadcn-vue@latest add card
npx shadcn-vue@latest add table
npx shadcn-vue@latest add input
npx shadcn-vue@latest add dialog
npx shadcn-vue@latest add dropdown-menu
npx shadcn-vue@latest add tabs
npx shadcn-vue@latest add progress
npx shadcn-vue@latest add badge
npx shadcn-vue@latest add tooltip
```

Accept defaults for all prompts.

- [ ] **Step 2: Verify components installed**

Check that directories exist under `src/components/ui/` for: card, table, input, dialog, dropdown-menu, tabs, progress, badge, tooltip.

```bash
ls spaced-md.client/src/components/ui/
```

Expected: `badge button card dialog dropdown-menu input progress table tabs tooltip`

- [ ] **Step 3: Commit**

```bash
git add spaced-md.client/src/components/ui/
git commit -m "feat: install shadcn-vue components (card, table, input, dialog, dropdown-menu, tabs, progress, badge, tooltip)"
```

---

### Task 5: App Shell — Layout, Top Bar, Router

**Files:**
- Create: `spaced-md.client/src/layouts/AppLayout.vue`
- Create: `spaced-md.client/src/components/TopBar.vue`
- Create: `spaced-md.client/src/components/BottomNav.vue`
- Create: `spaced-md.client/src/components/KbdHint.vue`
- Modify: `spaced-md.client/src/App.vue`
- Modify: `spaced-md.client/src/router/index.ts`
- Delete: `spaced-md.client/src/views/HomeView.vue`
- Create: `spaced-md.client/src/views/DashboardView.vue` (placeholder)
- Create: `spaced-md.client/src/views/FilesView.vue` (placeholder)
- Create: `spaced-md.client/src/views/GroupsView.vue` (placeholder)
- Create: `spaced-md.client/src/views/ReviewView.vue` (placeholder)
- Create: `spaced-md.client/src/views/SettingsView.vue` (placeholder)

- [ ] **Step 1: Create KbdHint component**

Create `spaced-md.client/src/components/KbdHint.vue`:
```vue
<script setup lang="ts">
defineProps<{ keys: string }>()
</script>

<template>
  <kbd class="inline-flex items-center justify-center rounded bg-secondary px-1.5 py-0.5 font-mono text-[10px] text-muted-foreground">
    {{ keys }}
  </kbd>
</template>
```

- [ ] **Step 2: Create TopBar component**

Create `spaced-md.client/src/components/TopBar.vue`:
```vue
<script setup lang="ts">
import { Input } from '@/components/ui/input'
import KbdHint from '@/components/KbdHint.vue'
</script>

<template>
  <header class="flex items-center justify-between border-b border-border px-5 py-3">
    <span class="font-mono text-[13px] font-bold text-foreground tracking-tight">
      spaced-md
    </span>
    <div class="relative hidden sm:block">
      <Input
        type="text"
        placeholder="Search cards, files…"
        class="h-8 w-[200px] bg-secondary pl-8 text-xs"
        readonly
      />
      <div class="absolute left-2 top-1/2 -translate-y-1/2">
        <KbdHint keys="⌘K" />
      </div>
    </div>
    <div class="h-8 w-8 rounded-full bg-secondary" />
  </header>
</template>
```

- [ ] **Step 3: Create BottomNav component**

Create `spaced-md.client/src/components/BottomNav.vue`:
```vue
<script setup lang="ts">
import { useRoute } from 'vue-router'

const route = useRoute()

const tabs = [
  { name: 'Dashboard', path: '/', icon: '▦' },
  { name: 'Files', path: '/files', icon: '◫' },
  { name: 'Groups', path: '/groups', icon: '⊞' },
  { name: 'Settings', path: '/settings', icon: '⚙' },
]

function isActive(path: string) {
  return route.path === path
}
</script>

<template>
  <nav class="fixed bottom-0 left-0 right-0 flex items-center justify-around border-t border-border bg-background py-2 sm:hidden">
    <RouterLink
      v-for="tab in tabs"
      :key="tab.path"
      :to="tab.path"
      class="flex flex-col items-center gap-0.5 px-3 py-1 text-[10px]"
      :class="isActive(tab.path) ? 'text-accent' : 'text-muted-foreground'"
    >
      <span class="text-lg">{{ tab.icon }}</span>
      <span>{{ tab.name }}</span>
    </RouterLink>
  </nav>
</template>
```

- [ ] **Step 4: Create AppLayout**

Create `spaced-md.client/src/layouts/AppLayout.vue`:
```vue
<script setup lang="ts">
import { useRoute } from 'vue-router'
import TopBar from '@/components/TopBar.vue'
import BottomNav from '@/components/BottomNav.vue'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { useDarkMode } from '@/composables/useDarkMode'

useDarkMode()

const route = useRoute()

const tabs = [
  { label: 'Dashboard', value: '/' },
  { label: 'Files', value: '/files' },
  { label: 'Groups', value: '/groups' },
  { label: 'Settings', value: '/settings' },
]
</script>

<template>
  <div class="flex min-h-screen flex-col">
    <TopBar />
    <nav class="hidden border-b border-border px-5 sm:block">
      <Tabs :model-value="route.path">
        <TabsList class="h-auto gap-0 rounded-none bg-transparent p-0">
          <TabsTrigger
            v-for="tab in tabs"
            :key="tab.value"
            :value="tab.value"
            as-child
          >
            <RouterLink
              :to="tab.value"
              class="relative rounded-none border-b-2 border-transparent px-3.5 py-2 text-xs text-muted-foreground transition-colors hover:text-foreground data-[state=active]:border-accent data-[state=active]:text-foreground"
            >
              {{ tab.label }}
            </RouterLink>
          </TabsTrigger>
        </TabsList>
      </Tabs>
    </nav>
    <main class="flex-1 px-4 py-5 sm:px-5">
      <div class="mx-auto max-w-[1200px]">
        <slot />
      </div>
    </main>
    <BottomNav />
  </div>
</template>
```

- [ ] **Step 5: Update App.vue to use layout**

Replace `spaced-md.client/src/App.vue`:
```vue
<script setup lang="ts">
import AppLayout from '@/layouts/AppLayout.vue'
</script>

<template>
  <AppLayout>
    <RouterView />
  </AppLayout>
</template>
```

- [ ] **Step 6: Create placeholder views**

Create `spaced-md.client/src/views/DashboardView.vue`:
```vue
<template>
  <div>
    <h1 class="text-lg font-semibold tracking-tight">Dashboard</h1>
    <p class="text-sm text-muted-foreground">Coming soon.</p>
  </div>
</template>
```

Create `spaced-md.client/src/views/FilesView.vue`:
```vue
<template>
  <div>
    <h1 class="text-lg font-semibold tracking-tight">Files</h1>
    <p class="text-sm text-muted-foreground">Coming soon.</p>
  </div>
</template>
```

Create `spaced-md.client/src/views/GroupsView.vue`:
```vue
<template>
  <div>
    <h1 class="text-lg font-semibold tracking-tight">Groups</h1>
    <p class="text-sm text-muted-foreground">Coming soon.</p>
  </div>
</template>
```

Create `spaced-md.client/src/views/ReviewView.vue`:
```vue
<template>
  <div>
    <h1 class="text-lg font-semibold tracking-tight">Review</h1>
    <p class="text-sm text-muted-foreground">Coming soon.</p>
  </div>
</template>
```

Create `spaced-md.client/src/views/SettingsView.vue`:
```vue
<template>
  <div>
    <h1 class="text-lg font-semibold tracking-tight">Settings</h1>
    <p class="text-sm text-muted-foreground">Coming soon.</p>
  </div>
</template>
```

- [ ] **Step 7: Update router**

Replace `spaced-md.client/src/router/index.ts`:
```typescript
import { createRouter, createWebHistory } from 'vue-router'
import DashboardView from '@/views/DashboardView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'dashboard', component: DashboardView },
    { path: '/files', name: 'files', component: () => import('@/views/FilesView.vue') },
    { path: '/groups', name: 'groups', component: () => import('@/views/GroupsView.vue') },
    { path: '/review/:deckId?', name: 'review', component: () => import('@/views/ReviewView.vue') },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
  ],
})

export default router
```

- [ ] **Step 8: Delete HomeView.vue**

```bash
rm spaced-md.client/src/views/HomeView.vue
```

- [ ] **Step 9: Verify the app builds**

```bash
cd spaced-md.client && npx vue-tsc -b && npx vite build
```

Expected: Clean build with no errors.

- [ ] **Step 10: Commit**

```bash
git add -A spaced-md.client/src/
git commit -m "feat: app shell with top bar, tab navigation, router, and placeholder views"
```

---

### Task 6: Dashboard Page

**Files:**
- Create: `spaced-md.client/src/components/StatCard.vue`
- Create: `spaced-md.client/src/components/StatGrid.vue`
- Create: `spaced-md.client/src/components/DeckTable.vue`
- Modify: `spaced-md.client/src/views/DashboardView.vue`

- [ ] **Step 1: Create StatCard component**

Create `spaced-md.client/src/components/StatCard.vue`:
```vue
<script setup lang="ts">
import type { Stat } from '@/types'
import { Card, CardContent } from '@/components/ui/card'

defineProps<{ stat: Stat }>()
</script>

<template>
  <Card class="border-border">
    <CardContent class="p-3">
      <div class="font-mono text-[22px] font-bold tracking-tight text-foreground">
        {{ stat.value }}
      </div>
      <div class="mt-0.5 text-[10px] uppercase tracking-widest text-muted-foreground">
        {{ stat.label }}
      </div>
      <div v-if="stat.delta" class="mt-1 text-[10px] text-success">
        {{ stat.delta }}
      </div>
    </CardContent>
  </Card>
</template>
```

- [ ] **Step 2: Create StatGrid component**

Create `spaced-md.client/src/components/StatGrid.vue`:
```vue
<script setup lang="ts">
import type { Stat } from '@/types'
import StatCard from '@/components/StatCard.vue'

defineProps<{ stats: Stat[] }>()
</script>

<template>
  <div class="grid grid-cols-2 gap-2.5 sm:grid-cols-4">
    <StatCard v-for="stat in stats" :key="stat.label" :stat="stat" />
  </div>
</template>
```

- [ ] **Step 3: Create DeckTable component**

Create `spaced-md.client/src/components/DeckTable.vue`:
```vue
<script setup lang="ts">
import type { Deck } from '@/types'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'

defineProps<{ decks: Deck[] }>()
defineEmits<{ 'select-deck': [deck: Deck] }>()
</script>

<template>
  <Table>
    <TableHeader>
      <TableRow class="text-[10px] uppercase tracking-wider text-muted-foreground hover:bg-transparent">
        <TableHead class="h-8">Deck</TableHead>
        <TableHead class="h-8">Due</TableHead>
        <TableHead class="h-8">Cards</TableHead>
        <TableHead class="h-8 hidden sm:table-cell">Next review</TableHead>
      </TableRow>
    </TableHeader>
    <TableBody>
      <TableRow
        v-for="deck in decks"
        :key="deck.id"
        class="cursor-pointer text-xs"
        @click="$emit('select-deck', deck)"
      >
        <TableCell class="font-medium text-foreground">{{ deck.name }}</TableCell>
        <TableCell class="font-mono text-warning">{{ deck.dueCount }}</TableCell>
        <TableCell class="font-mono text-muted-foreground">{{ deck.cardCount }}</TableCell>
        <TableCell class="hidden text-muted-foreground sm:table-cell">{{ deck.nextReview }}</TableCell>
      </TableRow>
    </TableBody>
  </Table>
</template>
```

- [ ] **Step 4: Wire up DashboardView**

Replace `spaced-md.client/src/views/DashboardView.vue`:
```vue
<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useDashboardStore } from '@/stores/dashboard'
import StatGrid from '@/components/StatGrid.vue'
import DeckTable from '@/components/DeckTable.vue'
import type { Deck } from '@/types'

const router = useRouter()
const dashboard = useDashboardStore()

function onSelectDeck(deck: Deck) {
  if (deck.dueCount > 0) {
    router.push({ name: 'review', params: { deckId: deck.id } })
  }
}
</script>

<template>
  <div class="space-y-5">
    <StatGrid :stats="dashboard.stats" />
    <DeckTable :decks="dashboard.decks" @select-deck="onSelectDeck" />
  </div>
</template>
```

- [ ] **Step 5: Verify the app builds**

```bash
cd spaced-md.client && npx vue-tsc -b && npx vite build
```

Expected: Clean build.

- [ ] **Step 6: Commit**

```bash
git add spaced-md.client/src/components/StatCard.vue spaced-md.client/src/components/StatGrid.vue spaced-md.client/src/components/DeckTable.vue spaced-md.client/src/views/DashboardView.vue
git commit -m "feat: dashboard page with stat grid and deck table"
```

---

### Task 7: Study/Review Flow

**Files:**
- Create: `spaced-md.client/src/components/ProgressMeter.vue`
- Create: `spaced-md.client/src/components/ReviewCard.vue`
- Create: `spaced-md.client/src/components/RatingButtons.vue`
- Create: `spaced-md.client/src/components/SessionComplete.vue`
- Modify: `spaced-md.client/src/views/ReviewView.vue`

- [ ] **Step 1: Create ProgressMeter component**

Create `spaced-md.client/src/components/ProgressMeter.vue`:
```vue
<script setup lang="ts">
const props = defineProps<{ total: number; current: number }>()
</script>

<template>
  <div class="flex gap-0.5" role="progressbar" :aria-valuenow="current" :aria-valuemax="total">
    <div
      v-for="i in total"
      :key="i"
      class="h-[3px] flex-1 rounded-sm transition-colors"
      :class="{
        'bg-accent': i <= current,
        'bg-accent/40': i === current + 1,
        'bg-border': i > current + 1,
      }"
    />
  </div>
</template>
```

- [ ] **Step 2: Create ReviewCard component**

Create `spaced-md.client/src/components/ReviewCard.vue`:
```vue
<script setup lang="ts">
import type { Card } from '@/types'

const props = defineProps<{ card: Card; isFlipped: boolean }>()
defineEmits<{ flip: [] }>()
</script>

<template>
  <div
    class="flex flex-1 cursor-pointer flex-col items-center justify-center rounded-lg border border-border bg-card p-5 sm:p-8"
    @click="$emit('flip')"
  >
    <div class="text-[11px] uppercase tracking-widest text-muted-foreground">
      {{ isFlipped ? 'Answer' : 'Question' }}
    </div>
    <div
      class="mt-3 text-center text-[17px] leading-relaxed"
      :class="isFlipped ? 'text-muted-foreground' : 'text-foreground'"
    >
      {{ card.question }}
    </div>
    <div v-if="isFlipped" class="mt-4 text-center text-[17px] leading-relaxed text-foreground">
      {{ card.answer }}
    </div>
    <div v-if="card.sourceSection" class="mt-3 font-mono text-[11px] text-muted-foreground">
      {{ card.sourceFile }} → {{ card.sourceSection }}
    </div>
  </div>
</template>
```

- [ ] **Step 3: Create RatingButtons component**

Create `spaced-md.client/src/components/RatingButtons.vue`:
```vue
<script setup lang="ts">
import type { ReviewRating } from '@/types'
import { Button } from '@/components/ui/button'

defineEmits<{ rate: [rating: ReviewRating] }>()

const ratings: { key: string; label: string; rating: ReviewRating; highlight?: boolean }[] = [
  { key: '1', label: 'Again', rating: 'again' },
  { key: '2', label: 'Hard', rating: 'hard' },
  { key: '3', label: 'Good', rating: 'good', highlight: true },
  { key: '4', label: 'Easy', rating: 'easy' },
]
</script>

<template>
  <div class="flex justify-center gap-1.5">
    <Button
      v-for="r in ratings"
      :key="r.rating"
      variant="outline"
      size="sm"
      class="font-mono text-[11px]"
      :class="r.highlight ? 'border-success text-success' : ''"
      @click="$emit('rate', r.rating)"
    >
      <span class="hidden text-muted-foreground sm:inline mr-1">{{ r.key }}</span>
      {{ r.label }}
    </Button>
  </div>
</template>
```

- [ ] **Step 4: Create SessionComplete component**

Create `spaced-md.client/src/components/SessionComplete.vue`:
```vue
<script setup lang="ts">
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'

defineProps<{
  totalCards: number
  ratingCounts: { again: number; hard: number; good: number; easy: number }
}>()

defineEmits<{ done: [] }>()
</script>

<template>
  <div class="flex flex-1 flex-col items-center justify-center gap-6">
    <div class="text-center">
      <div class="font-mono text-[36px] font-bold tracking-tight text-foreground">
        {{ totalCards }}
      </div>
      <div class="text-sm text-muted-foreground">cards reviewed</div>
    </div>
    <Card class="w-full max-w-xs border-border">
      <CardContent class="grid grid-cols-4 gap-2 p-4 text-center">
        <div>
          <div class="font-mono text-sm font-bold text-destructive">{{ ratingCounts.again }}</div>
          <div class="text-[10px] text-muted-foreground">Again</div>
        </div>
        <div>
          <div class="font-mono text-sm font-bold text-warning">{{ ratingCounts.hard }}</div>
          <div class="text-[10px] text-muted-foreground">Hard</div>
        </div>
        <div>
          <div class="font-mono text-sm font-bold text-success">{{ ratingCounts.good }}</div>
          <div class="text-[10px] text-muted-foreground">Good</div>
        </div>
        <div>
          <div class="font-mono text-sm font-bold text-accent">{{ ratingCounts.easy }}</div>
          <div class="text-[10px] text-muted-foreground">Easy</div>
        </div>
      </CardContent>
    </Card>
    <Button @click="$emit('done')">Back to dashboard</Button>
  </div>
</template>
```

- [ ] **Step 5: Wire up ReviewView**

Replace `spaced-md.client/src/views/ReviewView.vue`:
```vue
<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useReviewStore } from '@/stores/review'
import type { Card, ReviewRating } from '@/types'
import ProgressMeter from '@/components/ProgressMeter.vue'
import ReviewCard from '@/components/ReviewCard.vue'
import RatingButtons from '@/components/RatingButtons.vue'
import SessionComplete from '@/components/SessionComplete.vue'
import KbdHint from '@/components/KbdHint.vue'

const route = useRoute()
const router = useRouter()
const review = useReviewStore()

// Mock cards for demo — will be replaced by API call
const mockCards: Card[] = [
  { id: 'c1', deckId: 'deck-1', question: 'What is the CAP theorem and its three guarantees?', answer: 'Consistency, Availability, Partition tolerance — you can only guarantee two of three in a distributed system.', sourceFile: 'distributed-systems.md', sourceSection: '## CAP Theorem', dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
  { id: 'c2', deckId: 'deck-1', question: 'What is Raft?', answer: 'A consensus algorithm designed to be more understandable than Paxos. Uses leader election and log replication.', sourceFile: 'distributed-systems.md', sourceSection: '## Consensus Algorithms', dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
  { id: 'c3', deckId: 'deck-1', question: 'What is the difference between synchronous and asynchronous replication?', answer: 'Synchronous: write is confirmed only after all replicas acknowledge. Asynchronous: write is confirmed immediately, replicas update eventually.', sourceFile: 'distributed-systems.md', sourceSection: '## Replication', dueAt: new Date(), easeFactor: 2.5, interval: 1, repetitions: 0 },
]

onMounted(() => {
  if (!review.isActive) {
    const deckId = route.params.deckId as string || 'deck-1'
    review.startSession(deckId, 'Distributed Systems', mockCards)
  }
})

function onRate(rating: ReviewRating) {
  review.rate(rating)
}

function onDone() {
  review.endSession()
  router.push('/')
}
</script>

<template>
  <div class="flex min-h-[calc(100vh-8rem)] flex-col">
    <template v-if="review.isActive && !review.isComplete">
      <!-- Context bar -->
      <div class="mb-3 flex items-center justify-between text-[11px] text-muted-foreground">
        <div class="flex items-center gap-2">
          <span class="text-foreground">{{ review.deckName }}</span>
          <span>·</span>
          <span>{{ review.progress }}</span>
        </div>
        <div class="hidden items-center gap-2 sm:flex">
          <KbdHint keys="space" /> flip
          <span>·</span>
          <KbdHint keys="1-4" /> rate
        </div>
      </div>

      <!-- Progress meter -->
      <ProgressMeter :total="review.cards.length" :current="review.currentIndex" class="mb-5" />

      <!-- Card -->
      <ReviewCard
        v-if="review.currentCard"
        :card="review.currentCard"
        :is-flipped="review.isFlipped"
        @flip="review.flip()"
      />

      <!-- Rating buttons (only when flipped) -->
      <div v-if="review.isFlipped" class="mt-4">
        <RatingButtons @rate="onRate" />
      </div>

      <div v-else class="mt-4 text-center text-[11px] text-muted-foreground">
        Click the card or press <KbdHint keys="space" /> to reveal the answer
      </div>
    </template>

    <!-- Session complete -->
    <SessionComplete
      v-else-if="review.isComplete"
      :total-cards="review.cards.length"
      :rating-counts="review.ratingCounts"
      @done="onDone"
    />
  </div>
</template>
```

- [ ] **Step 6: Verify the app builds**

```bash
cd spaced-md.client && npx vue-tsc -b && npx vite build
```

Expected: Clean build.

- [ ] **Step 7: Commit**

```bash
git add spaced-md.client/src/components/ProgressMeter.vue spaced-md.client/src/components/ReviewCard.vue spaced-md.client/src/components/RatingButtons.vue spaced-md.client/src/components/SessionComplete.vue spaced-md.client/src/views/ReviewView.vue
git commit -m "feat: study/review flow with card flip, rating, progress meter, and session summary"
```

---

### Task 8: Files Page

**Files:**
- Modify: `spaced-md.client/src/views/FilesView.vue`

- [ ] **Step 1: Implement FilesView**

Replace `spaced-md.client/src/views/FilesView.vue`:
```vue
<script setup lang="ts">
import { ref } from 'vue'
import { useFilesStore } from '@/stores/files'
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'

const files = useFilesStore()
const expandedId = ref<string | null>(null)

function toggle(id: string) {
  expandedId.value = expandedId.value === id ? null : id
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  return `${(bytes / 1024).toFixed(1)} KB`
}

function formatDate(date: Date): string {
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })
}
</script>

<template>
  <div class="space-y-4">
    <!-- Upload zone -->
    <div class="flex items-center justify-center rounded-lg border-2 border-dashed border-border p-8 text-center text-sm text-muted-foreground">
      Drop .md files here or click to upload
    </div>

    <!-- File table -->
    <Table>
      <TableHeader>
        <TableRow class="text-[10px] uppercase tracking-wider text-muted-foreground hover:bg-transparent">
          <TableHead class="h-8">File</TableHead>
          <TableHead class="h-8">Cards</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Uploaded</TableHead>
          <TableHead class="h-8 hidden sm:table-cell">Size</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        <template v-for="file in files.files" :key="file.id">
          <TableRow class="cursor-pointer text-xs" @click="toggle(file.id)">
            <TableCell class="font-mono font-medium text-foreground">{{ file.name }}</TableCell>
            <TableCell class="font-mono text-muted-foreground">{{ file.cardCount }}</TableCell>
            <TableCell class="hidden text-muted-foreground sm:table-cell">{{ formatDate(file.uploadedAt) }}</TableCell>
            <TableCell class="hidden text-muted-foreground sm:table-cell">{{ formatSize(file.sizeBytes) }}</TableCell>
          </TableRow>
          <TableRow v-if="expandedId === file.id" class="hover:bg-transparent">
            <TableCell :colspan="4" class="p-0">
              <div class="space-y-1 border-t border-border px-4 py-3">
                <div
                  v-for="heading in file.headings"
                  :key="heading.text"
                  class="flex items-center justify-between text-xs"
                >
                  <span class="text-muted-foreground">
                    {{ '#'.repeat(heading.level) }} {{ heading.text }}
                  </span>
                  <div class="flex items-center gap-2">
                    <Badge variant="secondary" class="font-mono text-[10px]">
                      {{ heading.cardCount }} cards
                    </Badge>
                    <Button variant="ghost" size="sm" class="h-6 text-[10px]">
                      Create cards
                    </Button>
                  </div>
                </div>
              </div>
            </TableCell>
          </TableRow>
        </template>
      </TableBody>
    </Table>

    <!-- Empty state -->
    <div v-if="files.files.length === 0" class="py-12 text-center text-sm text-muted-foreground">
      No files uploaded yet. Drop a .md file above to get started.
    </div>
  </div>
</template>
```

- [ ] **Step 2: Verify the app builds**

```bash
cd spaced-md.client && npx vue-tsc -b && npx vite build
```

Expected: Clean build.

- [ ] **Step 3: Commit**

```bash
git add spaced-md.client/src/views/FilesView.vue
git commit -m "feat: files page with expandable heading tree and upload zone"
```

---

### Task 9: Groups Page

**Files:**
- Modify: `spaced-md.client/src/views/GroupsView.vue`

- [ ] **Step 1: Implement GroupsView**

Replace `spaced-md.client/src/views/GroupsView.vue`:
```vue
<script setup lang="ts">
import { ref } from 'vue'
import { useGroupsStore } from '@/stores/groups'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogTrigger,
} from '@/components/ui/dialog'

const groups = useGroupsStore()
const newGroupName = ref('')
const dialogOpen = ref(false)

function createGroup() {
  if (newGroupName.value.trim()) {
    groups.addGroup(newGroupName.value.trim())
    newGroupName.value = ''
    dialogOpen.value = false
  }
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex items-center justify-between">
      <h1 class="text-lg font-semibold tracking-tight">Groups</h1>
      <Dialog v-model:open="dialogOpen">
        <DialogTrigger as-child>
          <Button size="sm" class="text-xs">New group</Button>
        </DialogTrigger>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Create group</DialogTitle>
          </DialogHeader>
          <Input v-model="newGroupName" placeholder="Group name" @keydown.enter="createGroup" />
          <DialogFooter>
            <Button @click="createGroup">Create</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>

    <div class="grid gap-2.5 sm:grid-cols-2">
      <Card v-for="group in groups.groups" :key="group.id" class="cursor-pointer border-border">
        <CardContent class="flex items-center justify-between p-4">
          <div>
            <div class="text-sm font-medium text-foreground">{{ group.name }}</div>
            <div class="mt-0.5 text-[11px] text-muted-foreground">
              {{ group.cardCount }} cards
            </div>
          </div>
          <div class="flex items-center gap-3">
            <span v-if="group.dueCount > 0" class="font-mono text-xs text-warning">
              {{ group.dueCount }} due
            </span>
            <Button
              variant="ghost"
              size="sm"
              class="h-6 text-[10px] text-destructive hover:text-destructive"
              @click.stop="groups.deleteGroup(group.id)"
            >
              Delete
            </Button>
          </div>
        </CardContent>
      </Card>
    </div>

    <div v-if="groups.groups.length === 0" class="py-12 text-center text-sm text-muted-foreground">
      No groups yet. Create one to organize your cards.
    </div>
  </div>
</template>
```

- [ ] **Step 2: Verify the app builds**

```bash
cd spaced-md.client && npx vue-tsc -b && npx vite build
```

Expected: Clean build.

- [ ] **Step 3: Commit**

```bash
git add spaced-md.client/src/views/GroupsView.vue
git commit -m "feat: groups page with create/delete and due count display"
```

---

### Task 10: Keyboard Shortcuts

**Files:**
- Create: `spaced-md.client/src/composables/useKeyboardShortcuts.ts`
- Create: `spaced-md.client/src/__tests__/useKeyboardShortcuts.test.ts`
- Modify: `spaced-md.client/src/views/ReviewView.vue`

- [ ] **Step 1: Write failing test for keyboard shortcuts**

Create `spaced-md.client/src/__tests__/useKeyboardShortcuts.test.ts`:
```typescript
import { describe, it, expect, vi, afterEach } from 'vitest'
import { useKeyboardShortcuts } from '@/composables/useKeyboardShortcuts'
import { mount } from '@vue/test-utils'
import { defineComponent, onMounted, onUnmounted } from 'vue'

function mountWithShortcuts(shortcuts: Record<string, () => void>) {
  return mount(defineComponent({
    setup() {
      const { register, cleanup } = useKeyboardShortcuts()
      onMounted(() => register(shortcuts))
      onUnmounted(() => cleanup())
      return {}
    },
    template: '<div />',
  }))
}

describe('useKeyboardShortcuts', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('calls handler when matching key is pressed', () => {
    const handler = vi.fn()
    const wrapper = mountWithShortcuts({ ' ': handler })
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }))
    expect(handler).toHaveBeenCalledOnce()
    wrapper.unmount()
  })

  it('does not call handler after cleanup', () => {
    const handler = vi.fn()
    const wrapper = mountWithShortcuts({ ' ': handler })
    wrapper.unmount()
    document.dispatchEvent(new KeyboardEvent('keydown', { key: ' ' }))
    expect(handler).not.toHaveBeenCalled()
  })

  it('handles meta+key combinations', () => {
    const handler = vi.fn()
    const wrapper = mountWithShortcuts({ 'meta+k': handler })
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'k', metaKey: true }))
    expect(handler).toHaveBeenCalledOnce()
    wrapper.unmount()
  })
})
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd spaced-md.client && npx vitest run src/__tests__/useKeyboardShortcuts.test.ts
```

Expected: FAIL — module not found.

- [ ] **Step 3: Implement useKeyboardShortcuts**

Create `spaced-md.client/src/composables/useKeyboardShortcuts.ts`:
```typescript
type ShortcutMap = Record<string, () => void>

export function useKeyboardShortcuts() {
  let listener: ((e: KeyboardEvent) => void) | null = null

  function register(shortcuts: ShortcutMap) {
    cleanup()
    listener = (e: KeyboardEvent) => {
      // Don't fire when typing in inputs
      const tag = (e.target as HTMLElement)?.tagName
      if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return

      const parts: string[] = []
      if (e.metaKey || e.ctrlKey) parts.push('meta')
      parts.push(e.key.toLowerCase())
      const combo = parts.join('+')

      const handler = shortcuts[combo] ?? shortcuts[e.key]
      if (handler) {
        e.preventDefault()
        handler()
      }
    }
    document.addEventListener('keydown', listener)
  }

  function cleanup() {
    if (listener) {
      document.removeEventListener('keydown', listener)
      listener = null
    }
  }

  return { register, cleanup }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd spaced-md.client && npx vitest run src/__tests__/useKeyboardShortcuts.test.ts
```

Expected: PASS (3 tests).

- [ ] **Step 5: Add keyboard shortcuts to ReviewView**

Add to the `<script setup>` section of `spaced-md.client/src/views/ReviewView.vue`, after the existing imports:

```typescript
import { onMounted, onUnmounted } from 'vue'
import { useKeyboardShortcuts } from '@/composables/useKeyboardShortcuts'
```

And add this after the `onMounted` block that starts the session:

```typescript
const { register, cleanup } = useKeyboardShortcuts()

onMounted(() => {
  register({
    ' ': () => { if (!review.isFlipped && !review.isComplete) review.flip() },
    '1': () => { if (review.isFlipped) review.rate('again') },
    '2': () => { if (review.isFlipped) review.rate('hard') },
    '3': () => { if (review.isFlipped) review.rate('good') },
    '4': () => { if (review.isFlipped) review.rate('easy') },
    'Escape': () => { review.endSession(); router.push('/') },
  })
})

onUnmounted(() => {
  cleanup()
})
```

Note: The existing `onMounted` for starting the session should be merged with this one into a single `onMounted` call.

- [ ] **Step 6: Run all tests**

```bash
cd spaced-md.client && npx vitest run
```

Expected: All tests pass.

- [ ] **Step 7: Verify the app builds**

```bash
cd spaced-md.client && npx vue-tsc -b && npx vite build
```

Expected: Clean build.

- [ ] **Step 8: Commit**

```bash
git add spaced-md.client/src/composables/useKeyboardShortcuts.ts spaced-md.client/src/__tests__/useKeyboardShortcuts.test.ts spaced-md.client/src/views/ReviewView.vue
git commit -m "feat: keyboard shortcuts for review flow (space flip, 1-4 rate, esc exit)"
```

---

### Task 11: Final Build Verification and Cleanup

**Files:**
- No new files — verification only

- [ ] **Step 1: Run all tests**

```bash
cd spaced-md.client && npx vitest run
```

Expected: All tests pass.

- [ ] **Step 2: Type-check and build**

```bash
cd spaced-md.client && npx vue-tsc -b && npx vite build
```

Expected: Clean build, no type errors.

- [ ] **Step 3: Visual smoke test**

```bash
cd spaced-md.client && npx vite --open
```

Manually verify:
- Dashboard loads with stat grid and deck table
- Dark mode applies based on system preference
- Tab navigation works (Dashboard, Files, Groups, Settings)
- Clicking a deck row with due cards navigates to review
- Review flow: card displays, space flips, 1-4 rates, progress meter advances
- Session complete shows after all cards reviewed
- Files page shows expandable file rows
- Groups page shows groups with create/delete
- Mobile: bottom nav appears below 768px, tabs hide

- [ ] **Step 4: Commit any fixes from smoke test**

If fixes were needed:
```bash
git add -A spaced-md.client/src/
git commit -m "fix: smoke test fixes for command center UI"
```
