# User Stories — Spaced-MD

Spaced repetition app for markdown files. Organized by epic, prioritized by tier:

- **P0 (MVP)** — core loop: account, upload md, create cards, study with SM-2
- **P1 (Essential)** — groups, dashboard, card editing, markdown preview, password reset, responsive design
- **P2 (Growth)** — card types, progress insights, sharing, search, tags, bulk upload
- **P3 (Polish)** — Obsidian vault import, export, mobile/PWA

## Epics

1. [User Accounts](01-user-accounts.md) — registration, login, password reset, profile
2. [Markdown File Management](02-file-management.md) — upload, view, delete, browse, import
3. [Flashcard Creation](03-flashcard-creation.md) — create from files/sections, edit, card types
4. [Spaced Repetition Study](04-spaced-repetition.md) — SM-2 scheduling, study sessions, ratings
5. [Groups & Organization](05-groups-organization.md) — groups, tags, card organization
6. [Dashboard & Insights](06-dashboard-insights.md) — stats, streaks, heatmaps, forecasts
7. [Search](07-search.md) — full-text search across cards and files
8. [Sharing & Collaboration](08-sharing.md) — share decks, browse, import
9. [Export & Portability](09-export-portability.md) — markdown/Anki export, stats export
10. [Mobile & PWA](10-mobile-pwa.md) — responsive design, installable PWA
11. [UX & Accessibility](11-ux-accessibility.md) — keyboard shortcuts, dark mode, card details

## Card States

Cards progress through three states:

- **New** — created but never studied. Immediately available for study.
- **Learning** — studied fewer than 3 times, or ease factor below 2.0. Short intervals.
- **Mature** — studied 3+ times with ease factor at or above 2.0. Longer intervals.

## Rating Scale

The UI uses 4 buttons mapped to SM-2 quality scores:

| Button | SM-2 quality | Effect |
|--------|-------------|--------|
| Again  | 0           | Reset interval, card stays in session |
| Hard   | 2           | Slightly shorter interval, ease drops |
| Good   | 4           | Normal interval progression |
| Easy   | 5           | Longer interval, ease rises |
