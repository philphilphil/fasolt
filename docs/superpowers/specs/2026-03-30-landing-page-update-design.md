# Landing Page Update — Design Spec

**Issue:** #70 — Update landing page: highlight iOS app and current features
**Date:** 2026-03-30

## Overview

Update the existing landing page to reflect the current state of the product. Add iOS app showcase, refresh the feature list, and update copy. Keep the existing page structure — this is an evolutionary update, not a redesign.

## Changes by Section

### 1. Hero Section

**Update copy only.**

- Headline: "MCP-first spaced repetition for your notes" (remove "markdown")
- Subheading: "Your AI agent reads your notes and creates flashcards. Study them on the web or the iOS app. Free."
- CTA buttons: unchanged ("Get started" + "Log in")

### 2. Terminal Demo

**No changes.** Keep the existing `TerminalDemo.vue` component.

### 3. How It Works

**Update step 3 copy.**

1. Write notes — unchanged
2. Connect your AI agent — unchanged
3. Learn and remember — update description to mention web and iOS: "Study with FSRS spaced repetition on the web or the iOS app"

### 4. Features Grid (NEW)

**Add a new section between "How It Works" and the iOS showcase.**

Compact grid showcasing current capabilities. Each item has an icon/label and brief description:

- **MCP tools** — AI agents create and manage cards via MCP
- **Decks** — organize cards into focused study groups
- **Source tracking** — cards retain provenance (file, heading)
- **FSRS scheduling** — optimal review intervals backed by research
- **Dashboard** — stats, due counts, study streaks
- **SVG support** — diagrams and visualizations on cards
- **Search** — full-text search across all cards
- **Self-hostable** — run your own instance with Docker

Style: consistent with the existing page design (monospace, dark/light mode, grid layout).

### 5. iOS Showcase (REPLACES web study screenshots)

**Remove the old web study screenshot section.** Replace with iOS app showcase.

- Display all 4 iOS screenshots as plain images in a row:
  - `docs/media/ios_screenshot_dashboard.png`
  - `docs/media/ios_screenshot_front.png`
  - `docs/media/ios_screenshot_back.png`
  - `docs/media/ios_screenshot_sessionComplete.png`
- Copy these images into `fasolt.client/public/` for serving
- Add a "Coming soon" label
- Headline: something like "Study anywhere" or "Take your cards with you"
- Responsive: on mobile, show 2x2 grid or horizontal scroll

### 6. Final CTA

**No changes.**

### 7. Footer

**No changes.**

## Assets

- Copy 4 iOS screenshots from `docs/media/` to `fasolt.client/public/`:
  - `ios_screenshot_dashboard.png`
  - `ios_screenshot_front.png`
  - `ios_screenshot_back.png`
  - `ios_screenshot_sessionComplete.png`
- Remove old web study screenshots from landing page references (files can stay in public/ in case used elsewhere)

## Scope

- Single file change: `fasolt.client/src/views/LandingView.vue`
- Asset copies to `fasolt.client/public/`
- No backend changes
- No routing changes
- No new components (unless the features grid warrants extraction)
