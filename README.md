# spaced-md

Spaced repetition for markdown files. Upload your `.md` notes, create flashcards from them (whole file or specific sections), and review with SM-2 scheduling.

## Features

- Upload, view, and delete `.md` files
- Create flashcards from entire files or individual heading sections
- SM-2 spaced repetition with quality-based scheduling
- Organize cards into groups for focused study
- Dashboard with due counts, totals, and study streaks
- Per-user accounts with cookie-based auth

## Tech Stack

| Layer | Tech |
|-------|------|
| Backend | .NET 10, ASP.NET Core Minimal API, EF Core + Npgsql |
| Frontend | Vue 3 + TypeScript + Vite, shadcn-vue, Tailwind CSS 3, Pinia |
| Database | Postgres 17 (docker-compose) |
| Auth | ASP.NET Core Identity (cookies) |

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

## Project Structure

```
spaced-md.Server/
  Domain/           — entities, value objects, interfaces
  Application/      — services, DTOs, use case logic
  Infrastructure/   — EF Core DbContext, repos, migrations
  Api/              — endpoints, middleware, Program.cs
spaced-md.client/   — Vue 3 SPA
```

## License

MIT
