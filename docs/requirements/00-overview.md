# User Stories — Spaced-MD

Spaced repetition app for markdown files. Organized by epic, prioritized by tier:

- **P0 (MVP)** — core loop: account, upload md, create cards, study with SM-2
- **P1 (Essential)** — groups, dashboard, card editing, markdown preview, password reset, responsive design
- **P2 (Growth)** — card types, progress insights, sharing, search, tags, bulk upload
- **P3 (Polish)** — Obsidian vault import, export, mobile/PWA

## Implementation Order

### Done

| # | Epic | Scope | Status |
|---|------|-------|--------|
| 1 | [User Accounts](done/01-user-accounts.md) | Registration, login, password reset, profile | Done |
| 2 | [Markdown File Management](done/02-file-management.md) | Upload, view, delete, browse by headings, bulk upload (P0-P2) | Done |
| 3 | [Flashcard Creation](done/03-flashcard-creation.md) | Create from files/sections, edit, delete, custom cards, preview (P0-P1) | Done |
| — | [File Update](done/03c_md_file_update.md) | Re-upload files with card impact preview | Done |

### Up Next — MVP Completion

| Order | Epic | Why this order | Depends on |
|-------|------|----------------|------------|
| **Next** | [04 — Spaced Repetition Study](04-spaced-repetition.md) | **Completes the core loop.** Without SM-2 scheduling, cards exist but can't be studied. This is the last P0 blocker. | Epic 3 (cards) |
| Then | [06 — Dashboard & Insights](06-dashboard-insights.md) (P0-P1 only) | Wire the dashboard with real stats (cards due, total, streak). Currently mock data. Motivates daily use. | Epic 4 (review data) |
| Then | [05 — Groups & Organization](05-groups-organization.md) (P0-P1 only) | Organize cards into study groups. Useful once you have enough cards. | Epic 3 (cards) |

### After MVP — Essential Features

| Order | Epic | Why | Depends on |
|-------|------|-----|------------|
| 4 | [11 — UX & Accessibility](11-ux-accessibility.md) (P1 only) | Keyboard shortcuts, dark mode polish, card detail view. Quality of life. | Epics 1-4 |
| 5 | [03b — Advanced Flashcard Features](03b-flashcard-advanced.md) | Cloze deletion, reversed cards, quick card from selection (P2) | Epic 3 |
| 6 | [07 — Search](07-search.md) | Full-text search across cards and files. Essential at scale. | Epics 2-3 |

### Later — Growth & Polish

| Order | Epic | Notes |
|-------|------|-------|
| 7 | [10 — Mobile & PWA](10-mobile-pwa.md) | Responsive design is mostly done; this adds installable PWA |
| 8 | [08 — Sharing & Collaboration](08-sharing.md) | Share decks, browse community cards |
| 9 | [09 — Export & Portability](09-export-portability.md) | Markdown/Anki export |
| 10 | [12 — Agent Integration (MCP)](12-agent-api.md) | MCP server, personal access tokens, bulk card creation |

### Rationale

The order follows one principle: **build the core study loop first, then make it better.**

1. Epic 4 (SM-2) is the highest priority because it's the last missing piece of the core loop: upload → create cards → study → repeat. Everything else is enhancement.
2. Dashboard comes right after because it provides the "why should I come back tomorrow" motivation (cards due, streak, retention).
3. Groups follow because organizing cards becomes important once you're actively studying.
4. Everything else is polish, growth, or nice-to-have.

## Epics (full list)

1. [User Accounts](done/01-user-accounts.md) — registration, login, password reset, profile
2. [Markdown File Management](done/02-file-management.md) — upload, view, delete, browse, import
3. [Flashcard Creation](done/03-flashcard-creation.md) — create from files/sections, edit, card types
4. [Spaced Repetition Study](04-spaced-repetition.md) — SM-2 scheduling, study sessions, ratings
5. [Groups & Organization](05-groups-organization.md) — groups, tags, card organization
6. [Dashboard & Insights](06-dashboard-insights.md) — stats, streaks, heatmaps, forecasts
7. [Search](07-search.md) — full-text search across cards and files
8. [Sharing & Collaboration](08-sharing.md) — share decks, browse, import
9. [Export & Portability](09-export-portability.md) — markdown/Anki export, stats export
10. [Mobile & PWA](10-mobile-pwa.md) — responsive design, installable PWA
11. [UX & Accessibility](11-ux-accessibility.md) — keyboard shortcuts, dark mode, card details
12. [Agent Integration (MCP)](12-agent-api.md) — MCP server, personal access tokens, bulk creation

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
