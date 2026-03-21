# Epic 5: Decks & Organization

_Replaces the original "Groups & Organization" epic. "Deck" replaces "Group" as the primary organizational concept._

## US-5.1 — Create Decks (P1)

As a user, I want to create decks so I can organize my cards by topic (e.g., "Backend Interviews", "Cooking", "Guitar Chords").

**Acceptance criteria:**

- Name and optional description
- Deck list view
- Empty state encourages adding cards

## US-5.2 — Assign Cards to Decks (P1)

As a user, I want to add cards to one or more decks so I can organize my study.

**Acceptance criteria:**

- Assign during card creation or from card detail
- A card can belong to multiple decks
- "Add all cards from this file" action on a deck
- Remove card from deck without deleting the card

## US-5.3 — View Deck Contents (P1)

As a user, I want to see all cards in a deck so I can review what I'm studying for a topic.

**Acceptance criteria:**

- Card list with front text preview, next review date, state
- Sort by next review, creation date, or alphabetical
- Show due count badge

## US-5.4 — Edit and Delete Decks (P1)

As a user, I want to rename or delete decks.

**Acceptance criteria:**

- Rename updates everywhere
- Delete deck does not delete cards (just unlinks them)
- Confirmation before delete

## US-5.5 — Study by Deck (P1)

As a user, I want to study cards from a specific deck so I can focus on one topic at a time.

**Acceptance criteria:**

- Start study session from deck view or dashboard deck table
- Only due cards from that deck shown
- Same SM-2 scheduling applies
- Session summary scoped to deck

_Corresponds to the deferred US-4.4 from Epic 4._

## US-5.6 — Dashboard Deck Table (P1)

As a user, I want to see my decks on the dashboard with due counts so I know what to study.

**Acceptance criteria:**

- Table showing deck name, due count, total cards, next review time
- Click deck row to start studying that deck
- Replaces the current mock deck table with real data

## US-5.7 — Tags on Cards (P2)

As a user, I want to tag cards with keywords so I can filter and find them easily.

**Acceptance criteria:**

- Add multiple tags to a card
- Autocomplete from existing tags
- Filter card list by tag
- Tag management (rename, delete unused)
