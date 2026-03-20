# Epic 9: Export & Portability

## US-9.1 — Export Cards as Markdown (P3)

As a user, I want to export my cards back to markdown so I always have a portable copy.

**Acceptance criteria:**

- Export single card, group, or all cards
- Output as `.md` file with front/back sections
- Download as file

## US-9.2 — Export to Anki Format (P3)

As a user, I want to export my cards to Anki-compatible format so I can use them in other tools.

**Acceptance criteria:**

- Export as tab-separated `.txt` compatible with Anki import
- Preserve card front, back, and tags
- Group maps to Anki deck name

_Note: `.apkg` generation is complex and out of scope. Tab-separated text covers the primary use case._

## US-9.3 — Export Stats (P3)

As a user, I want to export my study history so I can analyze it externally.

**Acceptance criteria:**

- CSV export of review history (date, card, rating, interval)
- Download from settings or dashboard

## US-9.4 — Import from Anki (P3)

As a user, I want to import my existing Anki decks so I can consolidate my study in one place.

**Acceptance criteria:**

- Import tab-separated `.txt` files (Anki export format)
- Map fields to card front/back
- Optionally assign to a group on import
