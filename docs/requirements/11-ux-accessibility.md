# Epic 11: UX & Accessibility

## US-11.1 — Keyboard Shortcuts (P1)

As a user, I want keyboard shortcuts for common actions so I can study efficiently without reaching for the mouse.

**Acceptance criteria:**

- Space: flip card
- 1/2/3/4: rate Again/Hard/Good/Easy
- N: next card (after rating)
- Esc: exit study session
- Shortcuts shown in a help overlay (? key)

## US-11.2 — Dark Mode (P1)

As a user, I want a dark mode so I can study comfortably at night without eye strain.

**Acceptance criteria:**

- Toggle between light and dark themes
- Respect system preference by default
- Preference persisted per user
- All markdown rendering adapts to theme (code blocks, blockquotes, etc.)

## US-11.3 — Card Detail View (P1)

As a user, I want to view a card's full details — content, source file, review history, and scheduling info — so I can understand how well I know it.

**Acceptance criteria:**

- Show card front and back (rendered markdown)
- Link to source file (if applicable)
- Review history: list of past reviews with date, rating, and interval
- Current scheduling: ease factor, interval, next due date, state (new/learning/mature)
- Actions: edit, delete, suspend, assign to group
