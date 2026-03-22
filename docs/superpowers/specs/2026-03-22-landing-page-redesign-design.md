# Landing Page Redesign

## Goal

Replace the current marketing-heavy landing page with a compact, conversational page that leads with the MCP-first angle and avoids fluff.

## Target Audience

Developers and technical people who use AI coding tools. Lead with the developer angle; don't over-explain MCP.

## Tone

Conversational but minimal. Slightly warmer than a README, but no marketing voice. One short sentence per idea.

## Page Structure

### 1. Nav Bar

- Logo: "fasolt"
- Dark mode toggle
- "Log in" link
- No separate "Sign up" button in nav (hero handles it)

### 2. Hero

- **Headline:** "MCP-first spaced repetition for your markdown notes."
- **Subline:** "API and browser also fully supported. Free forever."
- **Buttons:** "Get started" (primary → register), "Log in" (outline/link)
- No tagline badge, no explanatory paragraph

### 3. Terminal Demo

- Full-width terminal showing the MCP workflow (reading a markdown file, creating flashcards)
- **Static** — all lines visible at once, no typing animation
- Keep macOS window chrome (red/yellow/green dots)
- No replay button

### 4. How It Works

Three columns, one line each:

1. **Write notes** — Use Obsidian, any editor, or plain text files.
2. **Your AI agent creates flashcards** — It reads your notes and pushes cards to fasolt via MCP.
3. **Learn and remember** — Study your cards on the web. Spaced repetition schedules reviews automatically.

No icons, no code snippets.

### 5. Footer CTA + Footer

- Single "Get started" button
- One line: "Free and open source."
- Minimal footer: logo + GitHub link

## What's Removed

- "Built for developers" feature grid
- Repeated CTAs scattered throughout
- Tag badge in hero
- Explanatory paragraphs / marketing copy
- Typing animation in terminal demo

## Technical Notes

- Rewrite `LandingView.vue` in place
- Simplify `TerminalDemo.vue` to static (remove intersection observer, typing animation, replay button)
- Keep existing dark mode toggle, router links, and responsive grid patterns
