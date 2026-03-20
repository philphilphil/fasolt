# Epic 3: Flashcard Creation

## US-3.1 — Create Card from Entire File (P0)

As a user, I want to create a flashcard from an entire markdown file so I can review the full content.

**Acceptance criteria:**

- "Create card" action from file view
- Card front: file title (first H1 or filename)
- Card back: rendered markdown content of the file
- Card linked to source file
- Warn if file content exceeds 2000 characters — suggest creating section-based cards instead

## US-3.2 — Create Card from Heading Section (P0)

As a user, I want to create a flashcard from a specific heading section so I can study granular topics.

**Acceptance criteria:**

- Select a heading from the file's TOC
- Card front: heading text
- Card back: rendered markdown content under that heading (down to the next same-level or higher heading)
- Card linked to source file and heading

## US-3.3 — Edit Cards (P1)

As a user, I want to edit a card's front and back content so I can refine what I'm studying.

**Acceptance criteria:**

- Edit both front (question) and back (answer)
- Markdown editor with live preview
- Preserve link to source file
- Save and cancel buttons
- Edit doesn't reset review schedule

## US-3.4 — Delete Cards (P1)

As a user, I want to delete cards I no longer want to study.

**Acceptance criteria:**

- Delete from card list or during review
- Confirmation prompt
- Card removed from all groups
- Review history preserved for stats (soft delete)

## US-3.5 — Custom Cards (P1)

As a user, I want to create cards from scratch (not linked to a file) for things I want to remember that aren't in my notes yet.

**Acceptance criteria:**

- Markdown editor for front and back
- No source file required
- Same review behavior as file-based cards

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

## US-3.8 — Card Preview (P1)

As a user, I want to preview how a card will look before saving it so I know the content is right.

**Acceptance criteria:**

- Show rendered front and back side by side or with flip animation
- Preview available during create and edit flows

## US-3.9 — Quick Card from Selection (P2)

As a user, I want to highlight text in the file preview and create a card from the selection so I'm not limited to heading boundaries.

**Acceptance criteria:**

- Select/highlight arbitrary text in markdown preview
- "Create card from selection" action appears
- Selected text becomes card back; user enters card front
- Card linked to source file
