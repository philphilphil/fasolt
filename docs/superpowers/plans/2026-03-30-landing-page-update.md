# Landing Page Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Update the landing page to showcase the iOS app, add a features grid, and refresh copy to reflect the current product state (#70).

**Architecture:** Single-page update to `LandingView.vue` — modify hero copy, update "How it works" step 3, add a features grid section, replace the web study screenshots with iOS screenshots. Copy iOS images to `public/`.

**Tech Stack:** Vue 3, Tailwind CSS 3, lucide-vue-next icons

---

### Task 1: Copy iOS screenshots to public directory

**Files:**
- Copy: `docs/media/ios_screenshot_dashboard.png` → `fasolt.client/public/ios_screenshot_dashboard.png`
- Copy: `docs/media/ios_screenshot_front.png` → `fasolt.client/public/ios_screenshot_front.png`
- Copy: `docs/media/ios_screenshot_back.png` → `fasolt.client/public/ios_screenshot_back.png`
- Copy: `docs/media/ios_screenshot_sessionComplete.png` → `fasolt.client/public/ios_screenshot_sessionComplete.png`

- [ ] **Step 1: Copy the 4 iOS screenshots**

```bash
cp docs/media/ios_screenshot_dashboard.png fasolt.client/public/
cp docs/media/ios_screenshot_front.png fasolt.client/public/
cp docs/media/ios_screenshot_back.png fasolt.client/public/
cp docs/media/ios_screenshot_sessionComplete.png fasolt.client/public/
```

- [ ] **Step 2: Verify files exist**

```bash
ls -la fasolt.client/public/ios_screenshot_*.png
```

Expected: 4 files listed.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/public/ios_screenshot_*.png
git commit -m "chore: copy iOS screenshots to public for landing page"
```

---

### Task 2: Update hero copy

**Files:**
- Modify: `fasolt.client/src/views/LandingView.vue:42-50`

- [ ] **Step 1: Update the hero headline and subheading**

In `LandingView.vue`, replace the hero content (lines 42-50):

```html
<!-- OLD -->
<p class="mb-4 text-xs uppercase tracking-[0.2em] text-accent">spaced repetition</p>
<h1 class="text-2xl sm:text-4xl font-bold tracking-tight leading-tight mb-5">
  <span class="text-accent text-glow">MCP-first</span> spaced repetition<br class="hidden sm:block" />
  for your markdown notes.
</h1>
<p class="text-sm text-muted-foreground mb-8 max-w-md leading-relaxed">
  Your AI agent reads your notes and creates flashcards.
  You review them here. API and browser also fully supported. Free.
</p>
```

```html
<!-- NEW -->
<p class="mb-4 text-xs uppercase tracking-[0.2em] text-accent">spaced repetition</p>
<h1 class="text-2xl sm:text-4xl font-bold tracking-tight leading-tight mb-5">
  <span class="text-accent text-glow">MCP-first</span> spaced repetition<br class="hidden sm:block" />
  for your notes.
</h1>
<p class="text-sm text-muted-foreground mb-8 max-w-md leading-relaxed">
  Your AI agent reads your notes and creates flashcards.
  Study them on the web or the iOS app. Free.
</p>
```

- [ ] **Step 2: Verify the dev server renders correctly**

Run: `cd fasolt.client && npm run dev` (if not already running)

Open `http://localhost:5173` and check:
- Headline says "for your notes." (no "markdown")
- Subheading mentions iOS app

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/LandingView.vue
git commit -m "feat: update landing page hero copy — remove markdown, mention iOS app"
```

---

### Task 3: Update "How it works" step 3

**Files:**
- Modify: `fasolt.client/src/views/LandingView.vue:88-94`

- [ ] **Step 1: Update step 3 description**

Replace the step 3 block (lines 88-94):

```html
<!-- OLD -->
<div>
  <span class="text-xs text-accent/60 mb-2 block">03</span>
  <h3 class="text-sm font-semibold mb-2">Learn and remember</h3>
  <p class="text-xs text-muted-foreground leading-relaxed">
    Study your cards on the web. Spaced repetition schedules reviews at optimal intervals.
  </p>
</div>
```

```html
<!-- NEW -->
<div>
  <span class="text-xs text-accent/60 mb-2 block">03</span>
  <h3 class="text-sm font-semibold mb-2">Learn and remember</h3>
  <p class="text-xs text-muted-foreground leading-relaxed">
    Study your cards on the web or the iOS app. Spaced repetition schedules reviews at optimal intervals.
  </p>
</div>
```

- [ ] **Step 2: Verify on dev server**

Check "How it works" section — step 3 should mention "web or the iOS app".

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/LandingView.vue
git commit -m "feat: update how-it-works step 3 to mention iOS app"
```

---

### Task 4: Add features grid section

**Files:**
- Modify: `fasolt.client/src/views/LandingView.vue` (insert after "How it works" section, before study preview)
- Modify: `fasolt.client/src/views/LandingView.vue:1-8` (add lucide icon imports)

- [ ] **Step 1: Add icon imports**

In the `<script setup>` block, update the lucide import:

```typescript
// OLD
import { Moon, Sun } from 'lucide-vue-next'

// NEW
import { Moon, Sun, Bot, Layers, FileText, Brain, BarChart3, Image, Search, Server } from 'lucide-vue-next'
```

- [ ] **Step 2: Add features grid section**

Insert this section after the closing `</section>` of "How it works" (after line 97) and before the study preview section:

```html
<!-- Features -->
<section class="mx-auto max-w-5xl px-6 py-16">
  <p class="text-xs uppercase tracking-[0.2em] text-accent mb-8">Features</p>
  <div class="grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
    <div class="rounded border border-border/60 bg-card/50 p-4">
      <Bot :size="16" class="text-accent mb-2" />
      <h3 class="text-sm font-semibold mb-1">MCP tools</h3>
      <p class="text-xs text-muted-foreground leading-relaxed">AI agents create and manage cards via MCP.</p>
    </div>
    <div class="rounded border border-border/60 bg-card/50 p-4">
      <Layers :size="16" class="text-accent mb-2" />
      <h3 class="text-sm font-semibold mb-1">Decks</h3>
      <p class="text-xs text-muted-foreground leading-relaxed">Organize cards into focused study groups.</p>
    </div>
    <div class="rounded border border-border/60 bg-card/50 p-4">
      <FileText :size="16" class="text-accent mb-2" />
      <h3 class="text-sm font-semibold mb-1">Source tracking</h3>
      <p class="text-xs text-muted-foreground leading-relaxed">Cards retain provenance — file and heading.</p>
    </div>
    <div class="rounded border border-border/60 bg-card/50 p-4">
      <Brain :size="16" class="text-accent mb-2" />
      <h3 class="text-sm font-semibold mb-1">FSRS scheduling</h3>
      <p class="text-xs text-muted-foreground leading-relaxed">Optimal review intervals backed by research.</p>
    </div>
    <div class="rounded border border-border/60 bg-card/50 p-4">
      <BarChart3 :size="16" class="text-accent mb-2" />
      <h3 class="text-sm font-semibold mb-1">Dashboard</h3>
      <p class="text-xs text-muted-foreground leading-relaxed">Stats, due counts, and study streaks.</p>
    </div>
    <div class="rounded border border-border/60 bg-card/50 p-4">
      <Image :size="16" class="text-accent mb-2" />
      <h3 class="text-sm font-semibold mb-1">SVG support</h3>
      <p class="text-xs text-muted-foreground leading-relaxed">Diagrams and visualizations on cards.</p>
    </div>
    <div class="rounded border border-border/60 bg-card/50 p-4">
      <Search :size="16" class="text-accent mb-2" />
      <h3 class="text-sm font-semibold mb-1">Search</h3>
      <p class="text-xs text-muted-foreground leading-relaxed">Full-text search across all your cards.</p>
    </div>
    <div class="rounded border border-border/60 bg-card/50 p-4">
      <Server :size="16" class="text-accent mb-2" />
      <h3 class="text-sm font-semibold mb-1">Self-hostable</h3>
      <p class="text-xs text-muted-foreground leading-relaxed">Run your own instance with Docker.</p>
    </div>
  </div>
</section>
```

- [ ] **Step 3: Verify on dev server**

Check that the features grid appears between "How it works" and the study preview. Should show 4 columns on desktop, 2 on tablet, 1 on mobile. Each card has an icon, title, and description.

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/src/views/LandingView.vue
git commit -m "feat: add features grid section to landing page"
```

---

### Task 5: Replace web study screenshots with iOS showcase

**Files:**
- Modify: `fasolt.client/src/views/LandingView.vue` (replace study preview section, lines 99-127)

- [ ] **Step 1: Replace the study preview section**

Replace the entire study preview section:

```html
<!-- OLD: Study preview -->
<section class="mx-auto max-w-5xl px-6 py-16">
  <div class="flex flex-col items-center gap-6">
    <p class="text-xs uppercase tracking-[0.2em] text-muted-foreground">Study on any device</p>
    <div class="flex items-center gap-4">
      <div class="rounded border border-border/60 bg-card p-2 shadow-lg">
        <img
          src="/study-question.png"
          alt="Flashcard question side"
          class="rounded w-[180px] sm:w-[200px]"
        />
      </div>
      <div class="rounded border border-border/60 bg-card p-2 shadow-lg">
        <img
          src="/study-answer.png"
          alt="Flashcard answer side with rating buttons"
          class="rounded w-[180px] sm:w-[200px]"
        />
      </div>
      <div class="hidden sm:block rounded border border-border/60 bg-card p-2 shadow-lg">
        <img
          src="/study-complete.png"
          alt="Study session complete with statistics"
          class="rounded w-[200px]"
        />
      </div>
    </div>
  </div>
</section>
```

```html
<!-- NEW: iOS showcase -->
<section class="mx-auto max-w-5xl px-6 py-16">
  <div class="flex flex-col items-center gap-6">
    <p class="text-xs uppercase tracking-[0.2em] text-accent">Study anywhere</p>
    <p class="text-xs text-muted-foreground">Native iOS app — coming soon</p>
    <div class="grid grid-cols-2 gap-4 sm:grid-cols-4">
      <img
        src="/ios_screenshot_dashboard.png"
        alt="iOS app dashboard"
        class="rounded-lg w-full"
      />
      <img
        src="/ios_screenshot_front.png"
        alt="iOS app flashcard front"
        class="rounded-lg w-full"
      />
      <img
        src="/ios_screenshot_back.png"
        alt="iOS app flashcard back"
        class="rounded-lg w-full"
      />
      <img
        src="/ios_screenshot_sessionComplete.png"
        alt="iOS app study session complete"
        class="rounded-lg w-full"
      />
    </div>
  </div>
</section>
```

- [ ] **Step 2: Verify on dev server**

Check that:
- 4 iOS screenshots display in a row on desktop (4 columns)
- On mobile, they show as a 2x2 grid
- "Study anywhere" heading appears with accent color
- "Coming soon" label appears below
- Old web screenshots are gone

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/LandingView.vue
git commit -m "feat: replace web study screenshots with iOS app showcase"
```

---

### Task 6: Run Playwright browser tests

**Files:** None (verification only)

- [ ] **Step 1: Start the full stack if not running**

```bash
./dev.sh
```

- [ ] **Step 2: Test the landing page in the browser using Playwright**

Use Playwright MCP to:
1. Navigate to `http://localhost:5173`
2. Verify the hero headline contains "for your notes." (not "markdown notes")
3. Verify the subheading mentions "iOS app"
4. Scroll down and verify the features grid is visible with 8 feature cards
5. Scroll down and verify iOS screenshots are visible
6. Verify "Coming soon" text is present
7. Check dark mode toggle works
8. Check "Get started" link navigates to `/register`

- [ ] **Step 3: Create the PR**

```bash
git checkout -b feat/landing-page-update-70
git push -u origin feat/landing-page-update-70
gh pr create --title "Update landing page: iOS app showcase and features grid (#70)" --body "..."
```
