# CLAUDE.md

## Project Overview

Spaced repetition app for markdown files. Turn your notes into flashcards and retain what you learn.

**This is a SaaS product** — hosted for users to register and use. Self-hosting is also supported but the primary deployment is a centralized hosted service. Keep this in mind for all decisions: multi-user isolation, security, scalability, and API design.

### Core Concept

Users upload `.md` files and create flashcards from them — either from the entire file or from a specific heading section. Cards are reviewed using spaced repetition (SM-2 algorithm), which schedules reviews at increasing intervals based on how well you recall each card.

### Features

- **Markdown file management** — upload, view, and delete `.md` files
- **Flashcard creation** — create cards from an entire file or a specific heading/section within a file
- **Spaced repetition study** — review due cards with quality-based scheduling (SM-2)
- **Decks** — organize cards into decks for focused study sessions
- **Dashboard** — overview of stats like cards due, total cards, study streaks
- **User accounts** — registration, login, per-user data isolation

## Tech Stack

- **Backend**: .NET 10, ASP.NET Core Minimal API, EF Core + Npgsql
- **Database**: Postgres 17 (via docker-compose)
- **Frontend**: Vue 3 + TypeScript + Vite
- **UI**: shadcn-vue + Tailwind CSS 3
- **State**: Pinia
- **Routing**: Vue Router
- **Auth**: ASP.NET Core Identity (cookie-based)
- **API Docs**: OpenAPI (built-in .NET 10)

## Architecture

Folder-based Clean Architecture (single .NET project):

```
spaced-md.Server/
  Domain/           — entities, value objects, interfaces
  Application/      — services, DTOs, use case logic
  Infrastructure/   — EF Core DbContext, repositories, migrations
  Api/              — endpoints, middleware, Program.cs
```

Endpoints use the static extension method pattern (e.g., `MapHealthEndpoints()`).

## Repository Structure

```
docker-compose.yml          — Postgres container
dev.sh                      — runs everything (docker + backend + frontend)
spaced-md.sln               — .NET solution
global.json                 — .NET SDK version pin
spaced-md.Server/           — backend
spaced-md.Mcp/              — MCP server for AI agents
spaced-md.client/           — frontend (Vue 3 SPA)
```

## Build & Run

```bash
# Full stack (requires Docker)
./dev.sh

# Backend only
dotnet run --project spaced-md.Server

# Frontend only
cd spaced-md.client && npm run dev

# Database
docker compose up -d        # start Postgres
docker compose down          # stop Postgres
```

## Ports

- **Backend**: http://localhost:5000
- **Frontend**: http://localhost:5173 (proxies /api to backend)
- **Postgres**: localhost:5432

## Connection String

`Host=localhost;Port=5432;Database=spacedmd;Username=spaced;Password=spaced_dev`

## Testing

- **UI Tests**: Use Playwright (via MCP) for end-to-end UI testing
- **IMPORTANT**: Always run Playwright browser tests after implementing a feature. API-level curl tests are not sufficient — the UI must be tested in the browser to catch rendering issues, dialog flows, and navigation problems.
- Test the full user flow: navigate to the feature, interact with it (create, edit, delete), verify the UI updates correctly.
- Start the full stack before testing (`./dev.sh` or manually start backend + frontend).
- If the backend was rebuilt, restart it before testing — stale processes will return 404s on new endpoints.

## Requirements

Feature requirements live in `docs/requirements/`. Each file is a self-contained spec for one feature area. `00-overview.md` contains the full overview and a map of all requirement files. To implement a feature, read the corresponding `XX-feature-name.md` file. After implementing a requirement, move it to `docs/requirements/done/`.

## Generating Flashcard Markers in Markdown Files

When asked to generate questions/flashcards for a `.md` file (typically an Obsidian vault note):

**What this does:** Add `?:: Question text` marker lines directly into the markdown file. These markers are what spaced-md uses to extract flashcards when the file is uploaded. The content below the marker (until the next heading or marker) becomes the card's answer.

**Process:**

1. **Read the file** and detect its language (English, German, etc.). All generated questions must be written in that same language.
2. **Scope**: If the user specifies sections or headings, only add markers to those areas. If nothing is specified, cover the entire file.
3. **Quantity**: 1-3 markers per section. Prefer fewer high-quality questions over many shallow ones. Focus on concepts worth memorizing — definitions, key distinctions, cause/effect, "why" questions. Skip trivial or obvious facts.
4. **Do NOT edit the file immediately.** First, propose the questions to the user:
   ```
   Section: [heading]
   1. ?:: What is X?
   2. ?:: Why does Y happen?
   ```
   Then ask: "Want me to add these markers to the file? You can adjust, remove, or add any."
5. **Only after user approval**, edit the file to insert the `?::` marker lines. Place each marker on its own line before the paragraph that answers the question. Do not alter any existing content — only add marker lines.

**Marker format example:**
```markdown
## CAP Theorem

?:: What is the CAP theorem?
The CAP theorem states you can only have two of three: Consistency, Availability, Partition tolerance.
```

## Dev Seed User

In development, a seed user and API token are auto-created on startup:

- **Email:** `dev@spaced-md.local` / **Password:** `Dev1234!`
- **API Token:** `sm_dev_token_for_local_testing_only_do_not_use_in_production_0000`

## MCP Server

The `spaced-md.Mcp/` project is a stdio MCP server that bridges AI agents to the API. To run locally:

```bash
SPACED_MD_URL=http://localhost:5000 \
SPACED_MD_TOKEN=sm_dev_token_for_local_testing_only_do_not_use_in_production_0000 \
  dotnet run --project spaced-md.Mcp
```

## Key API Routes

- `GET /api/health` — health check
- `/api/identity/*` — ASP.NET Core Identity endpoints (register, login, etc.)
- `/api/tokens` — API token management (create, list, revoke)
- `POST /api/cards/bulk` — bulk card creation with duplicate detection
- `POST /api/files` — file upload with upsert support
