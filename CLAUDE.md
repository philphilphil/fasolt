# CLAUDE.md

## Project Overview

Spaced repetition app for markdown files. Turn your notes into flashcards and retain what you learn.

### Core Concept

Users upload `.md` files and create flashcards from them — either from the entire file or from a specific heading section. Cards are reviewed using spaced repetition (SM-2 algorithm), which schedules reviews at increasing intervals based on how well you recall each card.

### Features

- **Markdown file management** — upload, view, and delete `.md` files
- **Flashcard creation** — create cards from an entire file or a specific heading/section within a file
- **Spaced repetition study** — review due cards with quality-based scheduling (SM-2)
- **Groups** — organize cards into groups for focused study sessions
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

## Requirements

Feature requirements live in `docs/requirements/`. Each file is a self-contained spec for one feature area. `00-overview.md` contains the full overview and a map of all requirement files. To implement a feature, read the corresponding `XX-feature-name.md` file.

## Key API Routes

- `GET /api/health` — health check
- `/api/identity/*` — ASP.NET Core Identity endpoints (register, login, etc.)
