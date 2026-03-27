# Project Maintenance Audit

Audit the current state of the project and update documentation that has drifted. Read every source of truth before changing anything — do not guess.

## 1. Update Package Inventory (`docs/security/packages.md`)

Regenerate the full package inventory by reading the actual manifests:

- `fasolt.Server/fasolt.Server.csproj` — NuGet packages
- `fasolt.Tests/fasolt.Tests.csproj` — NuGet test packages
- `fasolt.client/package.json` — npm dependencies and devDependencies
- `fasolt.ios/` — Swift Package Manager (Package.swift or .xcodeproj resolved packages)
- `docker-compose.yml`, `docker-compose.prod.yml`, `Dockerfile`, `.github/workflows/` — infrastructure image versions

Overwrite `docs/security/packages.md` with the current data. Keep the existing format (tables grouped by subproject). Update the `Generated:` date to today.

## 2. Update CLAUDE.md

Read the actual codebase and compare against each section of `CLAUDE.md`. Fix anything that has drifted. Common drift areas:

### Tech Stack
- Read `fasolt.Server/fasolt.Server.csproj` for .NET/framework versions
- Read `fasolt.client/package.json` for frontend dependency versions
- Read `docker-compose.yml` for Postgres version

### Architecture
- Verify the folder structure under `fasolt.Server/` still matches the documented tree
- Check if any new top-level directories exist in the repo that aren't listed in Repository Structure

### Key API Routes
- Grep for `MapGet`, `MapPost`, `MapPut`, `MapDelete`, `MapGroup` in `fasolt.Server/Api/Endpoints/` and `Program.cs`
- Verify every route group is documented; add missing ones, remove stale ones

### MCP Tools
- Read the MCP tool definitions (grep for `[McpServerTool]` or tool registration)
- Verify the Available MCP Tools list matches; add missing tools, remove stale ones

### Features
- Skim the frontend routes (`fasolt.client/src/router/`) and views to check for new or removed features

### Build & Run / Ports / Environment Variables
- Verify ports, connection string, and env var docs match `appsettings.Development.json`, `docker-compose.yml`, `.env.example`, and `Program.cs`

### Everything Else
- Read through every remaining section of CLAUDE.md and verify it still reflects reality
- Don't add new sections unless something major is undocumented
- Don't remove sections that are still accurate

## Rules

- Only change lines that are actually wrong or missing. Don't rewrite sections that are accurate.
- If you're unsure whether something has changed, read the source file — don't guess.
- Keep CLAUDE.md concise. Match the existing tone and level of detail.
- Commit the changes when done with a message like `docs: update package inventory and CLAUDE.md`.
