# Epic 12: Agent API

## Overview

Allow AI agents (Claude Code, Cursor, etc.) running in a user's local environment (e.g., an Obsidian vault) to programmatically create cards, decks, and upload files to spaced-md. The main use case: a user points an agent at their notes, the agent reads the content, generates flashcard questions, and pushes them into spaced-md — all without the user touching the web UI.

## US-12.1 — Personal Access Tokens (P2)

As a user, I want to create API tokens so agents can authenticate without my username and password.

**Acceptance criteria:**

- Settings page has a "API Tokens" section
- User can create a named token (e.g., "Obsidian Agent")
- Token is shown once on creation, then stored as a hash (not retrievable later)
- User can list active tokens and revoke any token
- Tokens authenticate via `Authorization: Bearer <token>` header
- All existing API endpoints accept token auth in addition to cookie auth
- Tokens are scoped to the creating user — same data isolation as cookie auth

## US-12.2 — Agent-Friendly Card Creation (P2)

As an agent, I want to create cards in bulk from structured content so I can turn a full note into flashcards in one call.

**Acceptance criteria:**

- `POST /api/cards/bulk` accepts an array of cards:
  ```json
  {
    "fileId": "optional-guid",
    "deckId": "optional-guid",
    "cards": [
      { "front": "What is X?", "back": "X is...", "sourceHeading": "optional" }
    ]
  }
  ```
- If `fileId` is provided, cards are linked to that file with `cardType: "section"` (if `sourceHeading` given) or `"file"`
- If `fileId` is omitted, cards are created as `cardType: "custom"`
- If `deckId` is provided, cards are automatically added to that deck
- Returns the created cards with their IDs
- Validates all cards in the batch — if any fail validation, none are created (atomic)

## US-12.3 — Agent File Upload (P2)

As an agent, I want to upload a markdown file so the content is available in spaced-md before creating cards from it.

**Acceptance criteria:**

- Existing `POST /api/files` endpoint works with token auth (no changes needed if US-12.1 is implemented)
- If a file with the same name already exists, it is updated (upsert behavior)
- Response includes the file ID and parsed headings so the agent can reference them when creating cards

## US-12.4 — MCP Server (P3)

As a user, I want to connect my AI agent to spaced-md via MCP (Model Context Protocol) so the agent can discover and use the API without manual configuration.

**Acceptance criteria:**

- spaced-md exposes an MCP server (can be a separate lightweight process or built into the backend)
- Tools exposed:
  - `search_cards` — search existing cards (to avoid duplicates)
  - `create_cards` — bulk card creation (wraps US-12.2)
  - `list_decks` — list user's decks
  - `create_deck` — create a new deck
  - `upload_file` — upload/update a markdown file
- Agent authenticates via personal access token (US-12.1)
- MCP server config is documented so users can add it to their Claude Code / agent config

## Typical Agent Workflow

```
1. Agent reads a markdown note from the user's vault
2. Agent detects the language and generates ?:: markers / questions
3. User approves the questions
4. Agent uploads the .md file to spaced-md (POST /api/files)
5. Agent creates cards in bulk (POST /api/cards/bulk), linking to the uploaded file
6. Optionally adds cards to a deck
```

## Out of Scope

- OAuth / third-party app authorization (personal tokens are sufficient for single-user agents)
- Rate limiting (can be added later if needed)
- Webhook notifications (push updates to agents)
