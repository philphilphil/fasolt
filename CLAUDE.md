# CLAUDE.md

## Repository

GitHub: `philphilphil/fasolt`

## Project Overview

MCP-first spaced repetition for markdown notes. Your AI reads your notes — you learn and remember.

**This is a SaaS product** — hosted for users to register and use. Self-hosting is also supported but the primary deployment is a centralized hosted service. Keep this in mind for all decisions: multi-user isolation, security, scalability, and API design.

### Core Concept

The app is **MCP-first and API-first**. The user's local vault (e.g., Obsidian) is the source of truth for markdown files — no files are stored on the server. The user asks their AI agent (e.g., Claude Code) to process local `.md` files — the agent extracts flashcards and pushes them to the server via the MCP server or REST API. Cards retain provenance metadata (source file, heading). The web app is a study frontend: review due cards, browse decks, and track progress.

Cards are reviewed using spaced repetition (FSRS algorithm), which schedules reviews at increasing intervals based on how well you recall each card.

### Features

- **MCP/API card ingestion** — user triggers their AI agent to read local markdown files and create cards via the API; no file upload to the server
- **Flashcard creation** — create cards with front/back text, linked to a source file and heading via API or MCP tools
- **Source tracking** — cards retain provenance (source file, heading) as metadata; browse cards by source
- **Spaced repetition study** — review due cards with FSRS scheduling
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
- **Auth**: ASP.NET Core Identity (cookie-based) + OpenIddict (OAuth 2.0 for MCP clients)
- **API Docs**: OpenAPI (built-in .NET 10)

## Architecture

Folder-based Clean Architecture (single .NET project):

```
fasolt.Server/
  Domain/           — entities, value objects, interfaces
  Application/      — services, DTOs, use case logic
  Infrastructure/   — EF Core DbContext, repositories, migrations
  Api/              — endpoints, middleware, Program.cs
```

Endpoints use the static extension method pattern (e.g., `MapHealthEndpoints()`).

## Repository Structure

```
docker-compose.yml          — Postgres container (dev)
docker-compose.prod.yml     — production compose
Dockerfile                  — production container build
dev.sh                      — runs everything (docker + backend + frontend)
fasolt.sln                  — .NET solution
fasolt.Server/              — backend (includes remote MCP server)
fasolt.client/              — frontend (Vue 3 SPA)
fasolt.Tests/               — .NET unit/integration tests
fasolt.ios/                 — iOS app (Swift/Xcode)
```

## Build & Run

```bash
# Full stack (requires Docker)
./dev.sh

# Backend only
dotnet run --project fasolt.Server

# Frontend only
cd fasolt.client && npm run dev

# Database
docker compose up -d        # start Postgres
docker compose down          # stop Postgres
```

## Ports

- **Backend**: http://localhost:8080
- **Frontend**: http://localhost:5173 (proxies /api to backend)
- **Postgres**: localhost:5432

## Connection String

`Host=localhost;Port=5432;Database=fasolt;Username=fasolt;Password=fasolt_dev`

## Testing

- **Unit/Integration Tests**: `dotnet test` runs the `fasolt.Tests/` project (service-level tests, FSRS scheduling, etc.)
- **UI Tests**: Use Playwright (via MCP) for end-to-end UI testing
- **IMPORTANT**: Always run Playwright browser tests after implementing a feature. API-level curl tests are not sufficient — the UI must be tested in the browser to catch rendering issues, dialog flows, and navigation problems.
- Test the full user flow: navigate to the feature, interact with it (create, edit, delete), verify the UI updates correctly.
- Start the full stack before testing (`./dev.sh` or manually start backend + frontend).
- If the backend was rebuilt, restart it before testing — stale processes will return 404s on new endpoints.

## Pull Requests

After completing any significant change (multi-step features, refactors, etc.), always create a PR on a feature branch. Don't leave work on main without a PR. Include a summary, test plan, and link to the relevant issue.

## Requirements

Feature requirements are tracked as GitHub issues. Use `gh issue list` to see open work and `gh issue view <number>` to read the full spec before implementing. Close issues when done.

## Environment Variables

Local secrets are stored in `.env` (gitignored). The backend loads it automatically via `dotenv.net` on startup. Copy `.env.example` to `.env` and fill in values.

### iOS Push Notifications (APNs)

To enable push notifications in development, add your Apple APNs credentials to `.env`:

```
Apns__KeyId=YOUR_KEY_ID
Apns__TeamId=YOUR_TEAM_ID
Apns__BundleId=com.fasolt.app
Apns__KeyBase64=<base64-encoded .p8 key>
```

Generate `KeyBase64` from your `.p8` file: `base64 < AuthKey_XXXX.p8 | tr -d '\n'`

The background notification service only starts when APNs credentials are configured. Without them, everything else works normally.

## Dev Seed User

In development, a seed user is auto-created on startup:

- **Email:** `dev@fasolt.local` / **Password:** `Dev1234!`

## MCP Server

The server exposes a remote MCP endpoint at `/mcp` (streamable HTTP transport). AI agents connect directly — no separate local MCP process needed.

### Available MCP Tools

- `CreateCards` — create one or more flashcards, optionally linked to a source file and/or deck
- `SearchCards` — search existing cards by query text (use before creating to detect duplicates)
- `ListCards` — list cards, optionally filtered by source file or deck; supports pagination
- `UpdateCards` — bulk update cards' text or source metadata by ID or natural key (sourceFile + front); preserves SRS history
- `DeleteCards` — delete cards by IDs or by source file
- `AddSvgToCard` — add an SVG image to a card's front or back
- `ListSources` — list all source files that cards were created from, with card and due counts
- `ListDecks` — list all decks with card counts and due counts
- `CreateDeck` — create a new deck for organizing flashcards
- `UpdateDeck` — update a deck's name or description
- `DeleteDeck` — delete a deck, optionally deleting all its cards too
- `AssignCardsToDeck` — assign cards to a deck, remove from a deck, or move between decks
- `SetDeckActive` — activate or deactivate a deck for study
- `GetOverview` — get account overview: total cards, due cards, cards by state, deck and source counts

## Agent Teams

Always use agent teams (`TeamCreate`) instead of plain subagents when parallelizing work. This requires `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1` to be set. Teams provide better coordination, shared context, and visibility into parallel work.

## Key API Routes

- `GET /api/health` — health check
- `/api/identity/*` — ASP.NET Core Identity endpoints (register, login, etc.)
- `/api/account/*` — account management (delete account, etc.)
- `/api/cards/*` — card CRUD, bulk creation with duplicate detection
- `/api/decks/*` — deck CRUD and management
- `/api/review/*` — spaced repetition review sessions
- `/api/search/*` — full-text search across cards
- `/api/sources/*` — source file listing with card counts
- `/api/notifications/*` — push notification device tokens and settings
- `/api/oauth/*` — OAuth consent flow (OpenIddict)
- `/api/admin/*` — admin-only endpoints

## Production Infrastructure

Production runs behind **Cloudflare -> Traefik -> app container**. TLS is terminated at Cloudflare/Traefik, not at the .NET app. `TrustAllProxies=true` in `appsettings.Production.json` is intentional — the app is not directly reachable from the internet.

## Research & Documentation

Use **Context7 MCP** (`resolve-library-id` → `query-docs`) to look up current documentation, API references, and best practices for any library or framework before implementing. Don't rely on training data for library APIs — fetch the docs. This applies to all dependencies: .NET packages, npm packages, Swift frameworks, etc.

## GitHub Operations

Use the **GitHub CLI** (`gh`) for GitHub-related tasks (issues, PRs, workflow runs, repo metadata) instead of GitHub MCP tools.
