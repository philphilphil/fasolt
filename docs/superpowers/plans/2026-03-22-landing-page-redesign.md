# Landing Page Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current marketing-heavy landing page with a compact, conversational page that leads with the MCP-first angle.

**Architecture:** Two files to modify in-place: `LandingView.vue` (rewrite) and `TerminalDemo.vue` (simplify to static). No new files needed.

**Tech Stack:** Vue 3, shadcn-vue, Tailwind CSS 3, lucide-vue-next

**Spec:** `docs/superpowers/specs/2026-03-22-landing-page-redesign-design.md`

---

### Task 1: Simplify TerminalDemo to static

**Files:**
- Modify: `spaced-md.client/src/components/TerminalDemo.vue`

- [ ] **Step 1: Replace the script section with static data**

Remove all animation logic (typing, intersection observer, replay, timeouts). Replace with a simple static array rendered immediately.

```vue
<script setup lang="ts">
interface TerminalLine {
  text: string
  type: 'prompt' | 'output' | 'success' | 'dim' | 'blank'
}

const lines: TerminalLine[] = [
  { type: 'prompt', text: 'Read my distributed-systems.md and create flashcards' },
  { type: 'blank', text: '' },
  { type: 'dim', text: 'Reading distributed-systems.md...' },
  { type: 'dim', text: 'Found 4 sections: CAP Theorem, Consensus, Replication, Partitioning' },
  { type: 'blank', text: '' },
  { type: 'dim', text: 'Creating 8 flashcards...' },
  { type: 'success', text: '✓ What are the three guarantees of the CAP theorem?' },
  { type: 'success', text: "✓ Why can't a distributed system provide all three CAP properties?" },
  { type: 'success', text: '✓ What is the difference between strong and eventual consistency?' },
  { type: 'success', text: '✓ How does the Raft consensus algorithm handle leader election?' },
  { type: 'success', text: '✓ What is quorum, and why does it matter for replication?' },
  { type: 'success', text: '✓ What is the difference between synchronous and async replication?' },
  { type: 'success', text: '✓ What causes a network partition in a distributed system?' },
  { type: 'success', text: '✓ How does partition tolerance differ from fault tolerance?' },
  { type: 'blank', text: '' },
  { type: 'output', text: '8 cards created in "Distributed Systems" deck' },
]
</script>
```

- [ ] **Step 2: Replace the template with static rendering**

Remove all `lineStates`/`displayed` refs, replay button, and cursor animation. Render all lines directly.

```vue
<template>
  <div
    class="rounded-lg overflow-hidden border border-zinc-700/60 shadow-2xl"
    style="background: #0d1117; font-family: var(--font-mono)"
  >
    <!-- Window chrome -->
    <div class="flex items-center gap-1.5 px-4 py-3 border-b border-zinc-700/60" style="background: #161b22">
      <span class="h-3 w-3 rounded-full" style="background: #ff5f57"></span>
      <span class="h-3 w-3 rounded-full" style="background: #febc2e"></span>
      <span class="h-3 w-3 rounded-full" style="background: #28c840"></span>
    </div>

    <!-- Terminal body -->
    <div class="px-5 py-4 text-[13px] leading-relaxed" style="color: #e6edf3">
      <template v-for="(line, i) in lines" :key="i">
        <div v-if="line.type === 'blank'" class="h-3"></div>
        <div v-else-if="line.type === 'prompt'" class="flex gap-2 mb-1">
          <span style="color: #58a6ff">›</span>
          <span style="color: #e6edf3">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'success'" class="flex gap-2">
          <span style="color: #3fb950">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'dim'" class="flex gap-2">
          <span style="color: #8b949e">{{ line.text }}</span>
        </div>
        <div v-else-if="line.type === 'output'" class="flex gap-2">
          <span style="color: #e6edf3">{{ line.text }}</span>
        </div>
      </template>

      <!-- Static cursor -->
      <div class="flex gap-2 mt-1">
        <span style="color: #58a6ff">›</span>
        <span
          class="inline-block w-[2px] h-[1em] align-text-bottom animate-pulse"
          style="background: #e6edf3"
        ></span>
      </div>
    </div>
  </div>
</template>
```

- [ ] **Step 3: Verify the component renders**

Run: `cd spaced-md.client && npx vue-tsc --noEmit`
Expected: No type errors.

- [ ] **Step 4: Commit**

```bash
git add spaced-md.client/src/components/TerminalDemo.vue
git commit -m "refactor: simplify TerminalDemo to static rendering"
```

---

### Task 2: Rewrite LandingView

**Files:**
- Modify: `spaced-md.client/src/views/LandingView.vue`

- [ ] **Step 1: Rewrite the full component**

Replace the entire file with the new compact layout. Remove unused icon imports (BookOpen, Cpu, Repeat2, ArrowRight). Keep: RouterLink, Button, Sun, Moon, useDarkMode, TerminalDemo.

```vue
<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Button } from '@/components/ui/button'
import { Moon, Sun } from 'lucide-vue-next'
import { useDarkMode } from '@/composables/useDarkMode'
import TerminalDemo from '@/components/TerminalDemo.vue'

const { isDark, toggle } = useDarkMode()
</script>

<template>
  <div class="min-h-screen bg-background text-foreground">
    <!-- Nav -->
    <nav class="sticky top-0 z-50 border-b border-border bg-background/80 backdrop-blur-sm">
      <div class="mx-auto flex max-w-5xl items-center justify-between px-6 py-4">
        <span class="text-lg font-semibold tracking-tight" style="font-family: var(--font-mono)">
          spaced-md
        </span>
        <div class="flex items-center gap-2">
          <button
            class="rounded-md p-2 text-muted-foreground hover:text-foreground transition-colors"
            @click="toggle"
          >
            <Sun v-if="isDark" :size="18" />
            <Moon v-else :size="18" />
          </button>
          <RouterLink to="/login">
            <Button variant="ghost" size="sm">Log in</Button>
          </RouterLink>
        </div>
      </div>
    </nav>

    <!-- Hero -->
    <section class="mx-auto max-w-5xl px-6 pt-16 pb-10 sm:pt-24 sm:pb-14">
      <div class="max-w-2xl">
        <h1 class="text-3xl sm:text-4xl font-bold tracking-tight leading-tight mb-4">
          MCP-first spaced repetition for your markdown notes.
        </h1>
        <p class="text-base text-muted-foreground mb-8">
          API and browser also fully supported. Free forever.
        </p>
        <div class="flex flex-wrap gap-3">
          <RouterLink to="/register">
            <Button>Get started</Button>
          </RouterLink>
          <RouterLink to="/login">
            <Button variant="outline">Log in</Button>
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- Terminal demo -->
    <section class="mx-auto max-w-5xl px-6 pb-16">
      <div class="max-w-2xl">
        <TerminalDemo />
      </div>
    </section>

    <!-- How it works -->
    <section class="border-y border-border bg-muted/30">
      <div class="mx-auto max-w-5xl px-6 py-14">
        <h2 class="text-lg font-semibold mb-8">How it works</h2>
        <div class="grid gap-8 sm:grid-cols-3">
          <div>
            <h3 class="text-sm font-semibold mb-1">Write notes</h3>
            <p class="text-sm text-muted-foreground">
              Use Obsidian, any editor, or plain text files.
            </p>
          </div>
          <div>
            <h3 class="text-sm font-semibold mb-1">Your AI agent creates flashcards</h3>
            <p class="text-sm text-muted-foreground">
              It reads your notes and pushes cards to spaced-md via MCP.
            </p>
          </div>
          <div>
            <h3 class="text-sm font-semibold mb-1">Learn and remember</h3>
            <p class="text-sm text-muted-foreground">
              Study your cards on the web. Spaced repetition schedules reviews automatically.
            </p>
          </div>
        </div>
      </div>
    </section>

    <!-- CTA -->
    <section class="mx-auto max-w-5xl px-6 py-14 text-center">
      <p class="text-sm text-muted-foreground mb-4">Free and open source.</p>
      <RouterLink to="/register">
        <Button>Get started</Button>
      </RouterLink>
    </section>

    <!-- Footer -->
    <footer class="border-t border-border">
      <div class="mx-auto max-w-5xl px-6 py-6 flex items-center justify-between">
        <span class="text-sm text-muted-foreground font-mono">spaced-md</span>
        <a
          href="https://github.com"
          class="text-sm text-muted-foreground hover:text-foreground transition-colors"
        >
          GitHub
        </a>
      </div>
    </footer>
  </div>
</template>
```

- [ ] **Step 2: Verify type checking passes**

Run: `cd spaced-md.client && npx vue-tsc --noEmit`
Expected: No type errors.

- [ ] **Step 3: Commit**

```bash
git add spaced-md.client/src/views/LandingView.vue
git commit -m "feat: redesign landing page — compact, MCP-first, no marketing fluff"
```

---

### Task 3: Visual verification with Playwright

**Files:** None (testing only)

- [ ] **Step 1: Start the full stack**

Ensure `./dev.sh` is running (backend + frontend + Postgres).

- [ ] **Step 2: Navigate to the landing page and take a screenshot**

Use Playwright to open `http://localhost:5173/` and take a screenshot. Verify:
- Nav shows logo, dark mode toggle, and "Log in" (no "Sign up" button in nav)
- Hero shows the new headline, subline, and two buttons
- Terminal demo renders statically with all lines visible
- "How it works" shows three columns with the correct text
- Footer CTA shows "Free and open source." with a single button
- Footer shows logo and GitHub link

- [ ] **Step 3: Test dark mode toggle**

Click the dark mode toggle and verify the page switches themes correctly.

- [ ] **Step 4: Test navigation**

Click "Get started" and verify it navigates to `/register`. Go back. Click "Log in" and verify it navigates to `/login`.
