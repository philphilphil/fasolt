# Epic 3b: Advanced Flashcard Features (P2)

Split from Epic 3 — these are deferred P2 features.

## US-3.6 — Cloze Deletion Cards (P2)

As a user, I want to create cloze deletion cards so I can test myself on specific facts within a passage.

**Acceptance criteria:**

- Select text within markdown content and mark it as a cloze (e.g., `{{c1::hidden text}}`)
- Card shows passage with blank, reveal shows filled text
- Multiple cloze deletions per card supported
- Each cloze generates a separate review item

## US-3.7 — Reversed Cards (P2)

As a user, I want to create reversed cards so I can study in both directions (question→answer and answer→question).

**Acceptance criteria:**

- Toggle "create reversed card" when creating a card
- Generates two cards: original and flipped
- Both cards schedule independently
- Deleting one optionally deletes the other

## US-3.9 — Quick Card from Selection (P2)

As a user, I want to highlight text in the file preview and create a card from the selection so I'm not limited to heading boundaries.

**Acceptance criteria:**

- Select/highlight arbitrary text in markdown preview
- "Create card from selection" action appears
- Selected text becomes card back; user enters card front
- Card linked to source file
