# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Spaced repetition app for markdown files. Users upload `.md` files, create flashcards from entire files or specific heading sections, and study them. ASP.NET Core 9 backend + Vue 3 frontend.

## Repository Structure

```
spaced-md.sln                  # Visual Studio solution
spaced-md.Server/              # ASP.NET Core 9 backend (Minimal API)
spaced-md.Server.Tests/        # xUnit tests
spaced-md.client/              # Vue 3 frontend (TypeScript)
```

## Build & Run Commands

### Backend
```bash
dotnet run --project spaced-md.Server          # Run server (http://localhost:5041)
dotnet test                                     # Run xUnit tests
dotnet ef migrations add "Name" --output-dir Infrastructure/Database/Migrations  # New migration
dotnet ef database update                       # Apply migrations
```

### Frontend (from spaced-md.client/)
```bash
npm run dev          # Vite dev server (https://localhost:52325, proxies /api to backend)
npm run build        # Type-check + production build
npm run type-check   # vue-tsc only
npm run lint         # ESLint with auto-fix
```

Run both together: start the ASP.NET server first, then `npm run dev`.

### Kiota API Client Generation
The TypeScript API client in `spaced-md.client/src/api/` is **auto-generated** — do not edit manually. It regenerates automatically on server build via a Kiota MSBuild target. Install Kiota globally: `dotnet tool install --global Microsoft.OpenApi.Kiota`

## Backend Architecture

- **Minimal API with endpoint pattern**: all endpoints implement `IEndpoint` and are auto-discovered via `ServiceExtension.cs` (`AddEndpoints`/`MapEndpoints`)
- **Endpoints organized by feature** in `Endpoints/` (Cards, Files, Identity, User)
- **EF Core 9 + SQLite** (`spaced-md.db`), connection string in `appsettings.Development.json`
- **ASP.NET Core Identity** with cookie auth
- **Domain models**: `MarkdownFile`, `Card` (with `UsageType` EntireFile|PartialFile), `Group`, `GroupCard` — all inherit `AuditableEntity`
- **Services**: `MarkdownService` parses headings and extracts sections from markdown
- **Dev seed user**: `local@spaced-md.com` / `sp4cEeEd!`
- **OpenAPI docs**: Scalar UI at `/scalar/v1` in development

## Frontend Architecture

- **Vue 3** Composition API (`<script setup>`) + **TypeScript 5.8** + **Vite 6**
- **State**: Pinia (`authStore.ts`)
- **UI**: PrimeVue 4 (Aura theme, orange primary) + TailwindCSS 4
- **Routing**: Vue Router with auth guard; unauthenticated routes: `/`, `/auth/login`
- **API client**: Kiota-generated, provided via `app.provide('api', client)` and consumed with `inject<SpacedMdApiClient>('api')`
- **Path alias**: `@/*` maps to `./src/*`
- **Dark mode**: CSS selector `.app-dark`
- **Markdown rendering**: `marked` library; heading/section parsing in `mdService.ts`
