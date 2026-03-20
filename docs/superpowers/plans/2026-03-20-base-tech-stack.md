# Base Tech Stack Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scaffold the full project skeleton — .NET 10 backend with folder-based Clean Architecture, Vue 3 frontend with shadcn-vue, Postgres via Docker, and a dev script to run everything.

**Architecture:** Single .NET project with Domain/Application/Infrastructure/Api folders. Vue 3 SPA served separately via Vite dev server, proxied to the backend API. EF Core with Npgsql for Postgres. ASP.NET Core Identity for auth with cookie-based sessions.

**Tech Stack:** .NET 10, EF Core + Npgsql, ASP.NET Core Identity, Vue 3, TypeScript, Vite, shadcn-vue, Tailwind CSS 3, Pinia, Vue Router

---

## File Structure

### Backend — `spaced-md.Server/`

```
spaced-md.Server/
  Program.cs                              — app startup, DI, middleware, endpoint mapping
  appsettings.json                        — config (connection string, etc.)
  appsettings.Development.json            — dev overrides
  spaced-md.Server.csproj                 — project file with packages
  Domain/
    (empty for now — entities go here later)
  Application/
    (empty for now — services/DTOs go here later)
  Infrastructure/
    Data/
      AppDbContext.cs                     — EF Core DbContext with Identity
  Api/
    Endpoints/
      HealthEndpoints.cs                 — GET /api/health
```

### Frontend — `spaced-md.client/`

```
spaced-md.client/
  index.html
  package.json
  tsconfig.json
  tsconfig.app.json
  tsconfig.node.json
  vite.config.ts
  tailwind.config.js
  postcss.config.js
  env.d.ts
  src/
    main.ts                              — app entry, router + pinia setup
    App.vue                              — root component with <RouterView>
    style.css                            — tailwind imports
    lib/
      utils.ts                           — cn() utility for shadcn-vue
    api/
      client.ts                          — base fetch wrapper
    stores/
      (empty for now)
    views/
      HomeView.vue                       — placeholder home page
    components/
      ui/
        button/
          Button.vue                     — shadcn-vue button (added via CLI)
          index.ts
    router/
      index.ts                           — Vue Router setup
  components.json                        — shadcn-vue config
```

### Root

```
spaced-md.sln                            — .NET solution
global.json                              — .NET SDK version pin
docker-compose.yml                       — Postgres container
dev.sh                                   — runs backend + frontend
.gitignore                               — combined .NET + Node ignores
```

---

## Task 1: Docker Compose + Postgres

**Files:**
- Create: `docker-compose.yml`

- [ ] **Step 1: Create docker-compose.yml**

```yaml
services:
  db:
    image: postgres:17
    restart: unless-stopped
    environment:
      POSTGRES_USER: spaced
      POSTGRES_PASSWORD: spaced_dev
      POSTGRES_DB: spacedmd
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data

volumes:
  pgdata:
```

- [ ] **Step 2: Verify Postgres starts**

Run: `docker compose up -d && docker compose ps`
Expected: `db` container is running, port 5432 mapped.

- [ ] **Step 3: Commit**

```bash
git add docker-compose.yml
git commit -m "feat: add docker-compose for Postgres"
```

---

## Task 2: .NET Solution + Server Project Skeleton

**Files:**
- Create: `global.json`
- Create: `spaced-md.sln`
- Create: `spaced-md.Server/spaced-md.Server.csproj`
- Create: `spaced-md.Server/Program.cs`
- Create: `spaced-md.Server/appsettings.json`
- Create: `spaced-md.Server/appsettings.Development.json`

- [ ] **Step 1: Check .NET SDK version**

Run: `dotnet --version`
Expected: 10.x.x

- [ ] **Step 2: Create global.json**

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMinor"
  }
}
```

- [ ] **Step 3: Create solution and project**

```bash
dotnet new sln -n spaced-md
dotnet new web -n spaced-md.Server -o spaced-md.Server
dotnet sln add spaced-md.Server/spaced-md.Server.csproj
```

- [ ] **Step 4: Add NuGet packages**

```bash
cd spaced-md.Server
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Design
cd ..
```

- [ ] **Step 5: Create folder structure**

```bash
mkdir -p spaced-md.Server/Domain
mkdir -p spaced-md.Server/Application
mkdir -p spaced-md.Server/Infrastructure/Data
mkdir -p spaced-md.Server/Api/Endpoints
```

- [ ] **Step 6: Create AppDbContext**

Create `spaced-md.Server/Infrastructure/Data/AppDbContext.cs`:

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SpacedMd.Server.Infrastructure.Data;

public class AppDbContext : IdentityDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
```

- [ ] **Step 7: Create HealthEndpoints**

Create `spaced-md.Server/Api/Endpoints/HealthEndpoints.cs`:

```csharp
namespace SpacedMd.Server.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));
    }
}
```

- [ ] **Step 8: Write Program.cs**

Replace `spaced-md.Server/Program.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SpacedMd.Server.Api.Endpoints;
using SpacedMd.Server.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentityApiEndpoints<IdentityUser>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthorization();

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthEndpoints();
app.MapGroup("/api/identity").MapIdentityApi<IdentityUser>();

app.Run();
```

- [ ] **Step 9: Configure appsettings.json**

Replace `spaced-md.Server/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=spacedmd;Username=spaced;Password=spaced_dev"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

Replace `spaced-md.Server/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 10: Verify build**

Run: `dotnet build`
Expected: Build succeeded with 0 errors.

- [ ] **Step 11: Create initial EF migration**

```bash
cd spaced-md.Server
dotnet ef migrations add InitialIdentity --output-dir Infrastructure/Data/Migrations
cd ..
```

- [ ] **Step 12: Verify migration applies (requires Postgres running)**

```bash
cd spaced-md.Server
dotnet ef database update
cd ..
```

- [ ] **Step 13: Verify health endpoint**

Run: `cd spaced-md.Server && dotnet run &` then `curl http://localhost:5000/api/health`
Expected: `{"status":"healthy"}`
Stop the server after verifying.

- [ ] **Step 14: Commit**

```bash
git add global.json spaced-md.sln spaced-md.Server/
git commit -m "feat: scaffold .NET 10 backend with EF Core, Identity, and health endpoint"
```

---

## Task 3: Vue 3 Frontend Scaffold

**Files:**
- Create: `spaced-md.client/` (entire frontend project)

- [ ] **Step 1: Create Vue project with Vite**

```bash
npm create vite@latest spaced-md.client -- --template vue-ts
```

- [ ] **Step 2: Install dependencies**

```bash
cd spaced-md.client
npm install
npm install vue-router@4 pinia
npm install -D tailwindcss@3 autoprefixer
cd ..
```

- [ ] **Step 3: Initialize Tailwind**

```bash
cd spaced-md.client
npx tailwindcss init
cd ..
```

- [ ] **Step 4: Create postcss.config.js**

Create `spaced-md.client/postcss.config.js`:

```js
export default {
  plugins: {
    tailwindcss: {},
    autoprefixer: {},
  },
}
```

- [ ] **Step 5: Configure tailwind.config.js**

Replace `spaced-md.client/tailwind.config.js`:

```js
/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,js,vue}'],
  theme: {
    extend: {},
  },
  plugins: [],
}
```

- [ ] **Step 6: Update vite.config.ts**

Replace `spaced-md.client/vite.config.ts`:

```typescript
import path from 'node:path'
import vue from '@vitejs/plugin-vue'
import autoprefixer from 'autoprefixer'
import tailwind from 'tailwindcss'
import { defineConfig } from 'vite'

export default defineConfig({
  css: {
    postcss: {
      plugins: [tailwind(), autoprefixer()],
    },
  },
  plugins: [vue()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5000',
    },
  },
})
```

- [ ] **Step 7: Update tsconfig.app.json for path aliases**

Add to `compilerOptions` in `spaced-md.client/tsconfig.app.json`:

```json
{
  "compilerOptions": {
    "baseUrl": ".",
    "paths": {
      "@/*": ["./src/*"]
    }
  }
}
```

- [ ] **Step 8: Replace src/style.css with Tailwind imports**

Replace `spaced-md.client/src/style.css`:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

- [ ] **Step 9: Initialize shadcn-vue**

```bash
cd spaced-md.client
npx shadcn-vue@latest init
```

When prompted:
- Style: Default
- Base color: Neutral
- CSS variables: Yes

This will install required deps (class-variance-authority, clsx, tailwind-merge, reka-ui, etc.) and create `components.json` and `src/lib/utils.ts`.

- [ ] **Step 10: Add a Button component to verify shadcn-vue works**

```bash
cd spaced-md.client
npx shadcn-vue@latest add button
cd ..
```

- [ ] **Step 11: Create router**

Create `spaced-md.client/src/router/index.ts`:

```typescript
import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '@/views/HomeView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'home',
      component: HomeView,
    },
  ],
})

export default router
```

- [ ] **Step 12: Create HomeView**

Create `spaced-md.client/src/views/HomeView.vue`:

```vue
<script setup lang="ts">
import { Button } from '@/components/ui/button'
</script>

<template>
  <div class="flex min-h-screen items-center justify-center">
    <div class="text-center space-y-4">
      <h1 class="text-4xl font-bold">spaced-md</h1>
      <p class="text-muted-foreground">Spaced repetition for your markdown notes.</p>
      <Button>Get Started</Button>
    </div>
  </div>
</template>
```

- [ ] **Step 13: Create API client base**

Create `spaced-md.client/src/api/client.ts`:

```typescript
const BASE_URL = '/api'

export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
    ...options,
  })

  if (!response.ok) {
    throw new Error(`API error: ${response.status}`)
  }

  return response.json()
}
```

- [ ] **Step 14: Update main.ts**

Replace `spaced-md.client/src/main.ts`:

```typescript
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import './style.css'

const app = createApp(App)
app.use(createPinia())
app.use(router)
app.mount('#app')
```

- [ ] **Step 15: Update App.vue**

Replace `spaced-md.client/src/App.vue`:

```vue
<template>
  <RouterView />
</template>
```

- [ ] **Step 16: Delete default Vite boilerplate**

Remove `src/components/HelloWorld.vue` and any other default Vite template files (keep `src/vite-env.d.ts`).

- [ ] **Step 17: Verify frontend builds and runs**

```bash
cd spaced-md.client
npm run build
npm run dev
```

Expected: Dev server starts, page shows "spaced-md" heading with a styled button.

- [ ] **Step 18: Commit**

```bash
git add spaced-md.client/
git commit -m "feat: scaffold Vue 3 frontend with shadcn-vue, Tailwind, Pinia, and Vue Router"
```

---

## Task 4: Dev Script + .gitignore

**Files:**
- Create: `dev.sh`
- Create: `.gitignore`

- [ ] **Step 1: Create dev.sh**

```bash
#!/bin/bash
set -e

# Start Postgres if not running
docker compose up -d

# Run backend and frontend concurrently
dotnet run --project spaced-md.Server &
BACKEND_PID=$!

cd spaced-md.client && npm run dev &
FRONTEND_PID=$!

trap "kill $BACKEND_PID $FRONTEND_PID 2>/dev/null; exit" INT TERM
wait
```

- [ ] **Step 2: Make dev.sh executable**

```bash
chmod +x dev.sh
```

- [ ] **Step 3: Create .gitignore**

```gitignore
# .NET
bin/
obj/
*.user
*.suo
*.db
*.db-journal

# Node
node_modules/
dist/

# IDE
.vs/
.vscode/
.idea/
*.swp

# OS
.DS_Store
Thumbs.db

# Environment
.env
.env.local
```

- [ ] **Step 4: Verify full stack runs**

Run: `./dev.sh`
Expected: Postgres, backend (port 5000), and frontend (port 5173) all start. Frontend proxies `/api` to backend.

- [ ] **Step 5: Commit**

```bash
git add dev.sh .gitignore
git commit -m "feat: add dev script and gitignore"
```

---

## Task 5: Update CLAUDE.md

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update CLAUDE.md with build/run commands and architecture details**

Add sections for:
- Repository structure
- Build & run commands (`./dev.sh`, `dotnet build`, `npm run dev`)
- Backend architecture (folder-based Clean Architecture, endpoint pattern)
- Frontend architecture (Vue 3 Composition API, shadcn-vue, Pinia)
- Connection string and default ports

- [ ] **Step 2: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: update CLAUDE.md with project structure and commands"
```
