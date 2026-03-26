# Package Inventory

Generated: 2026-03-26

All packages used across the fasolt project, grouped by subproject.

---

## fasolt.Server (NuGet)

| Package | Version | Type | Notes |
|---------|---------|------|-------|
| dotenv.net | 4.0.1 | Runtime | |
| FSRS.Core | 1.0.7 | Runtime | |
| Microsoft.AspNetCore.DataProtection.EntityFrameworkCore | 10.0.5 | Runtime | |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.5 | Runtime | |
| Microsoft.AspNetCore.OpenApi | 10.0.5 | Runtime | |
| Microsoft.EntityFrameworkCore.Design | 10.0.5 | Build | |
| Microsoft.IdentityModel.JsonWebTokens | 8.17.0 | Runtime | |
| ModelContextProtocol.AspNetCore | 1.0.0 | Runtime | |
| Nanoid | 3.1.0 | Runtime | |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.1 | Runtime | |
| OpenIddict.AspNetCore | 7.0.0 | Runtime | |
| OpenIddict.EntityFrameworkCore | 7.0.0 | Runtime | |

## fasolt.Tests (NuGet)

| Package | Version | Type | Notes |
|---------|---------|------|-------|
| coverlet.collector | 6.0.4 | Test | |
| FluentAssertions | 8.9.0 | Test | |
| FSRS.Core | 1.0.7 | Test | |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.5 | Test | |
| Microsoft.Extensions.TimeProvider.Testing | 10.4.0 | Test | |
| Microsoft.NET.Test.Sdk | 17.14.1 | Test | |
| xunit | 2.9.3 | Test | |
| xunit.runner.visualstudio | 3.1.4 | Test | |

## fasolt.client (npm)

### Dependencies

| Package | Version | Type | Notes |
|---------|---------|------|-------|
| @tanstack/vue-table | ^8.21.3 | dependency | |
| @vueuse/core | ^14.2.1 | dependency | |
| class-variance-authority | ^0.7.1 | dependency | |
| clsx | ^2.1.1 | dependency | |
| dompurify | ^3.3.3 | dependency | |
| lucide-vue-next | ^0.577.0 | dependency | |
| markdown-it | ^14.1.1 | dependency | |
| pinia | ^3.0.4 | dependency | |
| reka-ui | ^2.9.2 | dependency | |
| tailwind-merge | ^3.5.0 | dependency | |
| tailwindcss-animate | ^1.0.7 | dependency | |
| vue | ^3.5.30 | dependency | |
| vue-router | ^4.6.4 | dependency | |

### Dev Dependencies

| Package | Version | Type | Notes |
|---------|---------|------|-------|
| @tailwindcss/typography | ^0.5.19 | devDependency | |
| @types/markdown-it | ^14.1.2 | devDependency | |
| @types/node | ^24.12.0 | devDependency | |
| @vitejs/plugin-vue | ^6.0.5 | devDependency | |
| @vue/test-utils | ^2.4.6 | devDependency | |
| @vue/tsconfig | ^0.9.0 | devDependency | |
| autoprefixer | ^10.4.27 | devDependency | |
| happy-dom | ^20.8.4 | devDependency | |
| tailwindcss | ^3.4.19 | devDependency | |
| typescript | ~5.9.3 | devDependency | |
| vite | ^8.0.1 | devDependency | |
| vitest | ^4.1.0 | devDependency | |
| vue-tsc | ^3.2.5 | devDependency | |

## fasolt.ios (Swift)

| Package | Version | Type | Notes |
|---------|---------|------|-------|
| *(no external packages)* | | | |

## Infrastructure / Root

| Tool / Image | Version | Source File | Notes |
|--------------|---------|-------------|-------|
| postgres | 17.5 | docker-compose.yml | |
| postgres | 17.5 | docker-compose.prod.yml | |
| node | 22-alpine | Dockerfile | |
| mcr.microsoft.com/dotnet/sdk | 10.0 | Dockerfile | |
| mcr.microsoft.com/dotnet/aspnet | 10.0 | Dockerfile | |
| postgres | 17 | .github/workflows/test.yml | |
| dotnet | 10.0.x | .github/workflows/test.yml | |
| actions/checkout | v4 | .github/workflows/test.yml | |
| actions/setup-dotnet | v4 | .github/workflows/test.yml | |
