# Base Tech Stack Design

## Overview

Scaffold the base tech stack for the spaced-md project — a spaced repetition app for markdown files.

## Tech Stack

- **Backend**: .NET 10, Minimal API, EF Core + Postgres, ASP.NET Core Identity (cookie auth)
- **Frontend**: Vue 3 + TypeScript + Vite, shadcn-vue + Tailwind CSS, Pinia, Vue Router
- **Database**: Postgres via docker-compose
- **API client**: Manual fetch wrappers (no codegen)

## Backend Structure

Single project, folder-based Clean Architecture:

```
spaced-md.Server/
  Domain/           — entities, value objects, interfaces
  Application/      — services, DTOs, use case logic
  Infrastructure/   — EF Core DbContext, repositories, Postgres config
  Api/              — endpoints, middleware, Program.cs
```

- Minimal API with endpoint classes grouped by feature
- EF Core with Npgsql provider
- ASP.NET Core Identity with cookie-based auth
- OpenAPI via Swashbuckle or built-in .NET 10 support

## Frontend Structure

```
spaced-md.client/
  src/
    components/     — shadcn-vue components (copied in, owned by project)
    views/          — page-level components
    api/            — manual fetch wrappers + TypeScript types
    stores/         — Pinia state management
    lib/            — utilities (e.g., cn() helper)
```

- Vue 3 Composition API + `<script setup>` + TypeScript
- Vite for dev/build
- shadcn-vue + Tailwind CSS v4
- Pinia for state management
- Vue Router for routing

## Infrastructure

- `docker-compose.yml` — Postgres 17 container with volume
- `dev.sh` — runs both backend and frontend concurrently
- `spaced-md.sln` — .NET solution file

## Scope (what gets built now)

Skeleton only:

- Project files and config
- Health check endpoint (`GET /api/health`)
- Empty Vue app with shadcn-vue wired up, router, and Pinia installed
- Docker-compose for Postgres
- EF Core DbContext configured (no entities yet)
- Dev script to run everything
