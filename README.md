# spaced-md

Spaced repetition for markdown files. Upload your `.md` notes, create flashcards from them (whole file or specific sections), and review with SM-2 scheduling.

## Features

- Upload, view, and delete `.md` files
- Create flashcards from entire files or individual heading sections
- SM-2 spaced repetition with quality-based scheduling
- Organize cards into groups for focused study
- Dashboard with due counts, totals, and study streaks
- Per-user accounts with cookie-based auth
- API tokens for agent/tool integration
- MCP server for AI agents (Claude Code, Cursor, etc.)

## Tech Stack

| Layer | Tech |
|-------|------|
| Backend | .NET 10, ASP.NET Core Minimal API, EF Core + Npgsql |
| Frontend | Vue 3 + TypeScript + Vite, shadcn-vue, Tailwind CSS 3, Pinia |
| Database | Postgres 17 (docker-compose) |
| Auth | ASP.NET Core Identity (cookies + Bearer tokens) |
| MCP Server | .NET 10 console app, ModelContextProtocol SDK, stdio transport |

## Quick Start

Prerequisites: Docker, .NET 10 SDK, Node.js

```bash
./dev.sh  # starts Postgres, backend, and frontend
```

Or run individually:

```bash
docker compose up -d                       # Postgres on :5432
dotnet run --project spaced-md.Server      # API on :5000
cd spaced-md.client && npm run dev         # UI on :5173
```

The frontend proxies `/api` requests to the backend.

## MCP Server (AI Agent Integration)

The MCP server lets AI agents (Claude Code, Cursor, etc.) create flashcards from your notes without using the web UI.

### Setup

1. Start the full stack: `./dev.sh`
2. A dev user is auto-seeded with a pre-generated API token:
   - **Email:** `dev@spaced-md.local` / **Password:** `Dev1234!`
   - **Token:** `sm_dev_token_for_local_testing_only_do_not_use_in_production_0000`
3. Add to Claude Code:
   ```bash
   claude mcp add spaced-md \
     -e SPACED_MD_URL=http://localhost:5000 \
     -e SPACED_MD_TOKEN=sm_dev_token_for_local_testing_only_do_not_use_in_production_0000 \
     -- dotnet run --project /absolute/path/to/spaced-md.Mcp
   ```

You can also create your own tokens via **Settings → API Tokens** in the web UI.

### Available Tools

| Tool | Description |
|------|-------------|
| `SearchCards` | Search cards, decks, and files by query |
| `ListCards` | List cards with optional file/deck filter and pagination |
| `CreateCards` | Bulk create cards, optionally linked to a file and deck |
| `ListDecks` | List all decks with card counts |
| `CreateDeck` | Create a new deck |
| `UploadFile` | Upload or update a markdown file (upsert) |
| `ListFiles` | List uploaded files with pagination |
| `GetFile` | Get a file's content and headings |

### Typical Workflow

```
User: "Create flashcards from my kubernetes-notes.md"
Agent: reads local file → searches for existing cards → generates questions →
       uploads file → creates cards → done
```

## Project Structure

```
spaced-md.Server/
  Domain/           — entities, value objects, interfaces
  Application/      — services, DTOs, use case logic
  Infrastructure/   — EF Core DbContext, repos, migrations
  Api/              — endpoints, middleware, Program.cs
spaced-md.Mcp/      — MCP server (stdio, dotnet tool)
spaced-md.client/   — Vue 3 SPA
```

## License

MIT
