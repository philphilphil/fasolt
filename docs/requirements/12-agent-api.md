# Epic 12: Agent Integration (MCP)

## Overview

Allow AI agents (Claude Code, Cursor, etc.) to interact with spaced-md through MCP (Model Context Protocol). The primary use case: a user runs an agent in their Obsidian vault, the agent reads their notes, generates flashcard questions, and pushes them into spaced-md — without the user touching the web UI.

## Architecture

```
AI Agent (Claude Code, Cursor, etc.)
    ↕ stdio
spaced-md MCP server (lightweight .NET tool, runs locally on user's machine)
    ↕ HTTPS
spaced-md backend (hosted remotely or locally)
```

The MCP server is a .NET project (`spaced-md.Mcp/`) in the same solution, published as a dotnet tool (`dotnet tool install -g spaced-md-mcp`). It uses the official `ModelContextProtocol` C# SDK with stdio transport. It translates MCP tool calls into authenticated API requests. It has no state — just a bridge. Users configure it in their agent's MCP settings with a server URL and personal access token.

## US-12.1 — Personal Access Tokens (P2)

As a user, I want to create API tokens so the MCP server can authenticate on my behalf.

**Acceptance criteria:**

- Settings page has an "API Tokens" section
- User can create a named token (e.g., "Obsidian Agent")
- Token is shown once on creation, then stored as a hash (not retrievable later)
- User can list active tokens with creation date and last-used date
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
- Returns the created cards with their IDs
- Validates all cards in the batch — if any fail validation, none are created (atomic)

## US-12.3 — File Upload Upsert (P2)

As an MCP server, I need to upload or update a markdown file so cards can be linked to it.

**Acceptance criteria:**

- Existing `POST /api/files` works with token auth (no changes if US-12.1 is done)
- If a file with the same name already exists for this user, it is updated (upsert)
- Response includes the file ID and parsed headings so the MCP server can pass them back to the agent for card creation

## US-12.4 — MCP Server (.NET) (P2)

As a user, I want to install and configure an MCP server so my AI agent can talk to spaced-md.

**Acceptance criteria:**

- Separate .NET project in the solution: `spaced-md.Mcp/`
- Uses the official `ModelContextProtocol` NuGet package with stdio transport
- Shares DTOs with the backend project (via shared project or package reference)
- Published as a dotnet tool: `dotnet tool install -g spaced-md-mcp`
- Configured with two environment variables: `SPACED_MD_URL` and `SPACED_MD_TOKEN`
- Tools are auto-discovered via `[McpServerToolType]` and `[McpServerTool]` attributes
- `HttpClient` is injected via DI, pre-configured with the API URL and Bearer token
- Logging goes to stderr (required for stdio transport)
- User adds to their agent config (e.g., Claude Code `settings.json`):
  ```json
  {
    "mcpServers": {
      "spaced-md": {
        "command": "spaced-md-mcp",
        "env": {
          "SPACED_MD_URL": "https://spaced-md.example.com",
          "SPACED_MD_TOKEN": "sm_..."
        }
      }
    }
  }
  ```
- Alternative for dev/local use (no global install):
  ```json
  {
    "mcpServers": {
      "spaced-md": {
        "command": "dotnet",
        "args": ["run", "--project", "/path/to/spaced-md.Mcp"],
        "env": {
          "SPACED_MD_URL": "http://localhost:5000",
          "SPACED_MD_TOKEN": "sm_..."
        }
      }
    }
  }
  ```

**MCP Tools exposed:**

| Tool | Description | Wraps |
|------|-------------|-------|
| `search_cards` | Search existing cards by query (avoid duplicates) | `GET /api/search?q=` |
| `list_cards` | List all cards, optionally filtered by file or deck | `GET /api/cards` |
| `create_cards` | Create one or more cards, optionally linked to a file and deck | `POST /api/cards/bulk` |
| `list_decks` | List all decks with card counts | `GET /api/decks` |
| `create_deck` | Create a new deck | `POST /api/decks` |
| `upload_file` | Upload or update a markdown file (upsert) | `POST /api/files` |
| `list_files` | List all uploaded files | `GET /api/files` |

Each tool includes clear descriptions and parameter schemas so the agent can discover and use them without documentation.

## Typical Workflow

```
1. User opens Obsidian vault in Claude Code
2. User: "Create flashcards from my kubernetes-notes.md"
3. Agent reads the local .md file
4. Agent calls search_cards to check what already exists
5. Agent generates questions in the file's language, proposes them to user
6. User approves (possibly with edits)
7. Agent calls upload_file to push the .md into spaced-md
8. Agent calls create_cards with the questions, linking to the uploaded file
9. Optionally calls create_deck or adds to existing deck
10. Cards are immediately available for study in the web UI
```

## Out of Scope

- OAuth / third-party app authorization (personal tokens are sufficient)
- Rate limiting (can be added later)
- Webhook notifications / real-time sync
- Two-way sync with Obsidian (agent pushes to spaced-md, not the other way)
