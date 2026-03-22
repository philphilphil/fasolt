# fasolt

MCP-first spaced repetition for markdown notes. Your AI reads your notes — you learn and remember.

Connect fasolt to Claude or any MCP-compatible agent. Tell it which notes to create cards from. Study when they're due.

`100% Vibecoded to the best of my knowledge and belief.`

## How It Works

1. **Take notes in Obsidian** — write in your normal workflow, optionally add `?::` markers for precise card boundaries
2. **Your AI creates flashcards** — ask Claude (or any MCP agent) to read a file and push cards to fasolt
3. **Learn and remember** — open fasolt when cards are due, SM-2 schedules reviews at increasing intervals

## Features

- MCP server for AI agents (Claude Code, Claude Desktop, Cursor, etc.)
- REST API for programmatic card creation
- SM-2 spaced repetition with quality-based scheduling
- Source tracking — cards retain provenance (file, heading) as metadata
- Organize cards into decks for focused study
- Dashboard with due counts, totals, and study streaks
- Per-user accounts with cookie-based auth + API tokens
- Self-hostable via Docker

## Tech Stack

| Layer | Tech |
|-------|------|
| Backend | .NET 10, ASP.NET Core Minimal API, EF Core + Npgsql |
| Frontend | Vue 3 + TypeScript + Vite, shadcn-vue, Tailwind CSS 3, Pinia |
| Database | Postgres 17 (docker-compose) |
| Auth | ASP.NET Core Identity (cookies + Bearer tokens) |
| MCP Server | Built into the backend, streamable HTTP transport |

## Quick Start

Prerequisites: Docker, .NET 10 SDK, Node.js

```bash
./dev.sh  # starts Postgres, backend, and frontend
```

Or run individually:

```bash
docker compose up -d                       # Postgres on :5432
dotnet run --project fasolt.Server      # API on :8080
cd fasolt.client && npm run dev         # UI on :5173
```

The frontend proxies `/api` requests to the backend.

## MCP Server Setup

The MCP server is built into the backend and exposed at `/mcp` via streamable HTTP transport.

1. Start the full stack: `./dev.sh`
2. Add to Claude Code:
   ```bash
   claude mcp add fasolt --transport http http://localhost:8080/mcp
   ```
   You'll be prompted to log in via OAuth when your agent first connects.

### MCP Tools

| Tool | Description |
|------|-------------|
| `CreateCards` | Create cards with optional source file/heading metadata and deck assignment |
| `SearchCards` | Search cards and decks by query text |
| `ListCards` | List cards with optional source file/deck filter and pagination |
| `ListSources` | List source files with card and due counts |
| `ListDecks` | List all decks with card counts |
| `CreateDeck` | Create a new deck |

### Typical Workflow

```
User: "Create flashcards from my kubernetes-notes.md"
Agent: reads local file → searches for duplicates → generates questions →
       creates cards via API → done
```

## Project Structure

```
fasolt.Server/
  Domain/           — entities, value objects, interfaces
  Application/      — services, DTOs, use case logic
  Infrastructure/   — EF Core DbContext, repos, migrations
  Api/              — endpoints, middleware, Program.cs
fasolt.client/   — Vue 3 SPA
```

## License

MIT
