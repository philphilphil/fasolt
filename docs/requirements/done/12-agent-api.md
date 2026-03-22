# Epic 12: Agent Integration (MCP)

## Overview

Allow AI agents (Claude Code, Cursor, etc.) to interact with fasolt through MCP (Model Context Protocol). The primary use case: a user runs an agent in their Obsidian vault, the agent reads their notes, generates flashcard questions, and pushes them into fasolt — without the user touching the web UI.

## Architecture

```
AI Agent (Claude Code, Cursor, etc.)
    ↕ stdio
fasolt MCP server (lightweight .NET tool, runs locally on user's machine)
    ↕ HTTPS
fasolt backend (hosted remotely or locally)
```

The MCP server is a .NET project (`fasolt.Mcp/`) in the same solution, published as a dotnet tool (`dotnet tool install -g fasolt-mcp`). It uses the official `ModelContextProtocol` C# SDK with stdio transport. It translates MCP tool calls into authenticated API requests. It has no state — just a bridge. Users configure it in their agent's MCP settings with a server URL and personal access token.

## US-12.1 — Personal Access Tokens (P2)

As a user, I want to create API tokens so the MCP server can authenticate on my behalf.

**Acceptance criteria:**

- Settings page has an "API Tokens" section
- User can create a named token (e.g., "Obsidian Agent")
- Token format: `sm_` prefix + 32 cryptographically random bytes, base62-encoded (total ~46 chars)
- Token is shown once on creation, then stored as a SHA-256 hash (not retrievable later)
- User can optionally set an expiration date on creation (default: no expiry)
- User can list active tokens with creation date, last-used date, and expiration date (if set)
- Expired tokens are rejected at auth time (not garbage-collected — still visible in list as "expired")
- User can revoke any token
- Tokens authenticate via `Authorization: Bearer <token>` header
- All existing API endpoints accept token auth in addition to cookie auth
- Tokens are scoped to the creating user — same data isolation as cookie auth

## US-12.2 — Bulk Card Creation Endpoint (P2)

As an MCP server, I need a bulk endpoint to create multiple cards in one request.

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
- Maximum 100 cards per request (returns 400 if exceeded)
- Duplicate detection: if a card with the same `front` text already exists for the same `fileId` (or same user if no `fileId`), it is skipped and returned in a `skipped` array with the reason
- Returns the created cards with their IDs, plus any skipped cards
- Validates all cards in the batch — if any fail validation, none are created (atomic)

## US-12.3 — File Upload Upsert (P2)

As an MCP server, I need to upload or update a markdown file so cards can be linked to it.

**Acceptance criteria:**

- Existing `POST /api/files` works with token auth (no changes if US-12.1 is done)
- If a file with the same name already exists for this user, it is updated (upsert)
- On upsert, existing cards linked to the file are preserved — their `sourceHeading` references are not invalidated
- Response includes the file ID, parsed headings, and an `orphanedCards` array listing cards whose `sourceHeading` no longer exists in the updated file (so the agent can inform the user)
- Response includes `isUpdate: true/false` so the caller knows whether a new file was created or an existing one was updated

## US-12.4 — MCP Server (.NET) (P2)

As a user, I want to install and configure an MCP server so my AI agent can talk to fasolt.

**Acceptance criteria:**

- Separate .NET project in the solution: `fasolt.Mcp/`
- Uses the official `ModelContextProtocol` NuGet package with stdio transport
- Shares DTOs with the backend project (via shared project or package reference)
- Published as a dotnet tool: `dotnet tool install -g fasolt-mcp`
- Configured with two environment variables: `FASOLT_URL` and `FASOLT_TOKEN`
- Tools are auto-discovered via `[McpServerToolType]` and `[McpServerTool]` attributes
- `HttpClient` is injected via DI, pre-configured with the API URL and Bearer token
- Logging goes to stderr (required for stdio transport)
- HTTP requests use a 30-second timeout; on timeout or connection failure, the tool returns a clear error message (e.g., "fasolt backend unreachable at {url}") — no retries (let the agent decide whether to retry)
- `FASOLT_URL` defaults to the hosted SaaS URL if not set (e.g., `https://fasolt.app`)
- `FASOLT_TOKEN` is required (no anonymous access)
- User adds to their agent config (e.g., Claude Code `settings.json`):
  ```json
  {
    "mcpServers": {
      "fasolt": {
        "command": "fasolt-mcp",
        "env": {
          "FASOLT_TOKEN": "sm_..."
        }
      }
    }
  }
  ```
- Self-hosters override the URL:
  ```json
  {
    "mcpServers": {
      "fasolt": {
        "command": "fasolt-mcp",
        "env": {
          "FASOLT_URL": "https://my-instance.example.com",
          "FASOLT_TOKEN": "sm_..."
        }
      }
    }
  }
  ```
- Dev/local use (no global install):
  ```json
  {
    "mcpServers": {
      "fasolt": {
        "command": "dotnet",
        "args": ["run", "--project", "/path/to/fasolt.Mcp"],
        "env": {
          "FASOLT_URL": "http://localhost:5000",
          "FASOLT_TOKEN": "sm_..."
        }
      }
    }
  }
  ```

**MCP Tools exposed:**

| Tool | Description | Wraps |
|------|-------------|-------|
| `search_cards` | Search existing cards by query (avoid duplicates) | `GET /api/search?q=` |
| `list_cards` | List all cards, optionally filtered by file or deck (paginated) | `GET /api/cards` |
| `create_cards` | Create one or more cards, optionally linked to a file and deck | `POST /api/cards/bulk` |
| `list_decks` | List all decks with card counts | `GET /api/decks` |
| `create_deck` | Create a new deck | `POST /api/decks` |
| `upload_file` | Upload or update a markdown file (upsert) | `POST /api/files` |
| `list_files` | List all uploaded files | `GET /api/files` |
| `get_file` | Get a file's content and headings by ID | `GET /api/files/{id}` |

Each tool includes clear descriptions and parameter schemas so the agent can discover and use them without documentation.

**API changes required for MCP tools:**

- `GET /api/cards` must support an optional `deckId` query parameter to filter cards by deck (currently only supports `fileId`)
- `GET /api/cards` and `GET /api/files` must support cursor-based pagination: `?limit=50&after={id}` (default limit 50, max 200). Response includes `hasMore` and `nextCursor` fields. MCP tools expose these as optional parameters.

**Error response contract:**

All API errors return a consistent JSON shape so the MCP server can translate them into meaningful tool errors:
```json
{
  "error": "validation_error",
  "message": "Human-readable description",
  "details": [ { "field": "cards[0].front", "message": "Front is required" } ]
}
```
HTTP status codes: 400 (validation), 401 (no/invalid token), 403 (token expired or wrong scope), 404 (resource not found), 422 (semantic error, e.g., file too large).

## Typical Workflow

```
1. User opens Obsidian vault in Claude Code
2. User: "Create flashcards from my kubernetes-notes.md"
3. Agent reads the local .md file
4. Agent calls search_cards to check what already exists
5. Agent generates questions in the file's language, proposes them to user
6. User approves (possibly with edits)
7. Agent calls upload_file to push the .md into fasolt
8. Agent calls create_cards with the questions, linking to the uploaded file
9. Optionally calls create_deck or adds to existing deck
10. Cards are immediately available for study in the web UI
```

## Out of Scope

- OAuth / third-party app authorization (personal tokens are sufficient)
- Rate limiting (can be added later)
- Webhook notifications / real-time sync
- Two-way sync with Obsidian (agent pushes to fasolt, not the other way)
- Delete operations via MCP (agents can create and read, not delete — avoids accidental bulk deletion by a misbehaving agent; users delete via the web UI)
- MCP server retry logic (the agent orchestrates retries, not the bridge)
