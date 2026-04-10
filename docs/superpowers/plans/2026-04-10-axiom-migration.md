# Switch Error Tracking from Bugsink to Axiom

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace Bugsink/Sentry error tracking with Axiom on backend (.NET/Serilog) and frontend (Vue/@axiomhq/js). Resolves #117.

**Architecture:** Backend switches from `Sentry.AspNetCore` to Serilog with an HTTP sink that posts structured JSON to Axiom's ingest API. Frontend switches from `@sentry/vue` to `@axiomhq/js` + `@axiomhq/logging` with a Vue error handler bridge. Both are guarded by env var presence so self-hosters without Axiom get a clean no-op.

**Tech Stack:** `Serilog`, `Serilog.AspNetCore`, `Serilog.Sinks.Http`, `Serilog.Formatting.Compact` (backend); `@axiomhq/js`, `@axiomhq/logging` (frontend)

---

## Task 1: Remove Sentry/Bugsink from backend

**Files:**
- Modify: `fasolt.Server/fasolt.Server.csproj` — remove `Sentry.AspNetCore` package
- Modify: `fasolt.Server/Program.cs:24-33` — remove Bugsink/Sentry block

- [ ] **Step 1: Remove Sentry.AspNetCore NuGet package**

```bash
cd fasolt.Server && dotnet remove package Sentry.AspNetCore
```

- [ ] **Step 2: Remove Bugsink/Sentry setup from Program.cs**

Delete these lines (24-33) from `fasolt.Server/Program.cs`:

```csharp
var bugsinkDsn = builder.Configuration["BUGSINK_DSN"];
if (!string.IsNullOrEmpty(bugsinkDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = bugsinkDsn;
        o.Environment = builder.Environment.EnvironmentName;
        o.SendDefaultPii = false;
    });
}
```

- [ ] **Step 3: Update the comment on line 212 that references Bugsink/Sentry**

In `fasolt.Server/Program.cs`, find the comment:

```csharp
    // links, and password reset tokens would all end up in Bugsink/Sentry and
```

Replace with:

```csharp
    // links, and password reset tokens would all end up in Axiom and
```

- [ ] **Step 4: Verify build**

```bash
cd fasolt.Server && dotnet build
```

Expected: Build succeeds with no Sentry-related errors.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/fasolt.Server.csproj fasolt.Server/Program.cs fasolt.Server/packages.lock.json
git commit -m "refactor: remove Sentry/Bugsink SDK from backend"
```

---

## Task 2: Add Serilog + Axiom to backend

**Files:**
- Modify: `fasolt.Server/fasolt.Server.csproj` — add Serilog packages
- Create: `fasolt.Server/Infrastructure/Logging/AxiomHttpClient.cs` — custom `IHttpClient` that adds Axiom Bearer auth
- Modify: `fasolt.Server/Program.cs` — configure Serilog with Axiom HTTP sink

- [ ] **Step 1: Add Serilog NuGet packages**

```bash
cd fasolt.Server && dotnet add package Serilog.AspNetCore && dotnet add package Serilog.Sinks.Http && dotnet add package Serilog.Formatting.Compact
```

`Serilog.AspNetCore` pulls in `Serilog`, `Serilog.Sinks.Console`, and the ASP.NET Core integration. `Serilog.Sinks.Http` provides the batched HTTP sink. `Serilog.Formatting.Compact` gives us `RenderedCompactJsonFormatter` which produces JSON Axiom can ingest.

- [ ] **Step 2: Create AxiomHttpClient**

Create `fasolt.Server/Infrastructure/Logging/AxiomHttpClient.cs`:

```csharp
using Microsoft.Extensions.Configuration;
using Serilog.Sinks.Http;

namespace Fasolt.Server.Infrastructure.Logging;

public sealed class AxiomHttpClient : IHttpClient
{
    private readonly HttpClient _httpClient = new();

    public AxiomHttpClient(string apiToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);
    }

    public void Configure(IConfiguration configuration) { }

    public async Task<HttpResponseMessage> PostAsync(string requestUri, Stream contentStream,
        CancellationToken cancellationToken = default)
    {
        using var content = new StreamContent(contentStream);
        content.Headers.Add("Content-Type", "application/json");
        return await _httpClient.PostAsync(requestUri, content, cancellationToken);
    }

    public void Dispose() => _httpClient.Dispose();
}
```

- [ ] **Step 3: Configure Serilog in Program.cs**

At the top of `fasolt.Server/Program.cs`, after the existing `using` statements, add:

```csharp
using Serilog;
using Serilog.Formatting.Compact;
using Fasolt.Server.Infrastructure.Logging;
```

Replace the removed Bugsink block (after `builder.Configuration.AddEnvironmentVariables();`) with:

```csharp
var axiomToken = builder.Configuration["AXIOM_TOKEN"];
var axiomDataset = builder.Configuration["AXIOM_DATASET"];

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console();

if (!string.IsNullOrEmpty(axiomToken) && !string.IsNullOrEmpty(axiomDataset))
{
    loggerConfig.WriteTo.Http(
        requestUri: $"https://api.axiom.co/v1/datasets/{axiomDataset}/ingest",
        queueLimitBytes: null,
        textFormatter: new CompactJsonFormatter(),
        httpClient: new AxiomHttpClient(axiomToken));
}

builder.Host.UseSerilog(loggerConfig.CreateLogger());
```

This keeps console logging always on and conditionally adds Axiom. The `MinimumLevel.Warning()` means only Warning/Error/Fatal go to Axiom — no noisy info/debug spam. `ILogger.LogError`/`LogCritical` calls throughout the codebase (APNs, notifications, etc.) flow to Axiom automatically.

- [ ] **Step 4: Verify build**

```bash
cd fasolt.Server && dotnet build
```

Expected: Build succeeds.

- [ ] **Step 5: Run the app without Axiom vars to verify clean no-op**

```bash
cd fasolt.Server && dotnet run
```

Expected: App starts normally, console logs appear, no errors about Axiom.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/fasolt.Server.csproj fasolt.Server/Program.cs fasolt.Server/Infrastructure/Logging/AxiomHttpClient.cs fasolt.Server/packages.lock.json
git commit -m "feat: add Serilog with Axiom HTTP sink for error tracking

Replaces Sentry.AspNetCore. Serilog ships Warning+ logs to Axiom's
ingest API when AXIOM_TOKEN and AXIOM_DATASET are configured.
Self-hosters without credentials get console-only logging."
```

---

## Task 3: Remove Sentry from frontend

**Files:**
- Modify: `fasolt.client/package.json` — remove `@sentry/vue`
- Modify: `fasolt.client/src/main.ts` — remove Sentry init

- [ ] **Step 1: Uninstall @sentry/vue**

```bash
cd fasolt.client && npm uninstall @sentry/vue
```

- [ ] **Step 2: Remove Sentry init from main.ts**

Edit `fasolt.client/src/main.ts` — remove the Sentry import and init block. The file should become:

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

- [ ] **Step 3: Verify frontend builds**

```bash
cd fasolt.client && npm run build
```

Expected: Build succeeds with no Sentry-related imports.

- [ ] **Step 4: Commit**

```bash
git add fasolt.client/package.json fasolt.client/package-lock.json fasolt.client/src/main.ts
git commit -m "refactor: remove @sentry/vue from frontend"
```

---

## Task 4: Add Axiom to frontend

**Files:**
- Modify: `fasolt.client/package.json` — add `@axiomhq/js` and `@axiomhq/logging`
- Create: `fasolt.client/src/lib/axiom.ts` — Axiom logger setup
- Modify: `fasolt.client/src/main.ts` — init Axiom logger + Vue error handler

- [ ] **Step 1: Install Axiom packages**

```bash
cd fasolt.client && npm install @axiomhq/js @axiomhq/logging
```

- [ ] **Step 2: Create axiom.ts logger module**

Create `fasolt.client/src/lib/axiom.ts`:

```typescript
import { Axiom } from '@axiomhq/js'
import { Logger, AxiomJSTransport, ConsoleTransport } from '@axiomhq/logging'

const token = import.meta.env.VITE_AXIOM_TOKEN
const dataset = import.meta.env.VITE_AXIOM_DATASET

let logger: Logger | null = null

if (token && dataset) {
  const axiom = new Axiom({ token })
  logger = new Logger({
    transports: [
      new AxiomJSTransport({ axiom, dataset }),
      new ConsoleTransport(),
    ],
  })
}

export { logger }
```

- [ ] **Step 3: Wire into main.ts**

Edit `fasolt.client/src/main.ts`:

```typescript
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { logger } from './lib/axiom'
import './style.css'

const app = createApp(App)

if (logger) {
  app.config.errorHandler = (err, instance, info) => {
    logger.error('Vue error', {
      error: err instanceof Error ? { message: err.message, stack: err.stack } : String(err),
      info,
      component: instance?.$options?.name ?? 'unknown',
      url: window.location.href,
    })
  }

  window.addEventListener('unhandledrejection', (event) => {
    logger.error('Unhandled promise rejection', {
      reason: event.reason instanceof Error
        ? { message: event.reason.message, stack: event.reason.stack }
        : String(event.reason),
      url: window.location.href,
    })
  })
}

app.use(createPinia())
app.use(router)
app.mount('#app')
```

This captures both Vue component errors (via `app.config.errorHandler`) and unhandled promise rejections. The Axiom logger batches and flushes automatically.

- [ ] **Step 4: Verify frontend builds**

```bash
cd fasolt.client && npm run build
```

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/package.json fasolt.client/package-lock.json fasolt.client/src/lib/axiom.ts fasolt.client/src/main.ts
git commit -m "feat: add Axiom error tracking to Vue frontend

Uses @axiomhq/js + @axiomhq/logging. Captures Vue component errors
and unhandled promise rejections. No-op when VITE_AXIOM_TOKEN is unset."
```

---

## Task 5: Update config files

**Files:**
- Modify: `.env.example` — replace Bugsink vars with Axiom vars
- Modify: `.env` — replace Bugsink vars with Axiom vars
- Modify: `docker-compose.prod.yml` — replace `VITE_BUGSINK_DSN` build arg
- Modify: `Dockerfile` — replace `VITE_BUGSINK_DSN` ARG/ENV

- [ ] **Step 1: Update .env.example**

Replace the Bugsink section:

```
# Error tracking (Bugsink) — optional, leave empty to disable
BUGSINK_DSN=
VITE_BUGSINK_DSN=
```

With:

```
# Error tracking (Axiom) — optional, leave empty to disable
AXIOM_TOKEN=
AXIOM_DATASET=
VITE_AXIOM_TOKEN=
VITE_AXIOM_DATASET=
```

- [ ] **Step 2: Update .env**

Replace the Bugsink DSN lines:

```
BUGSINK_DSN=https://23b85c375a0b4dd080cea9bdf87243ed@alberich.bugsink.com/1
VITE_BUGSINK_DSN=https://23b85c375a0b4dd080cea9bdf87243ed@alberich.bugsink.com/1
```

With (empty for now — user fills in their Axiom credentials):

```
AXIOM_TOKEN=
AXIOM_DATASET=
VITE_AXIOM_TOKEN=
VITE_AXIOM_DATASET=
```

- [ ] **Step 3: Update docker-compose.prod.yml**

Replace the build arg:

```yaml
    build:
      context: .
      args:
        VITE_BUGSINK_DSN: ${VITE_BUGSINK_DSN:-}
```

With:

```yaml
    build:
      context: .
      args:
        VITE_AXIOM_TOKEN: ${VITE_AXIOM_TOKEN:-}
        VITE_AXIOM_DATASET: ${VITE_AXIOM_DATASET:-}
```

- [ ] **Step 4: Update Dockerfile**

Replace the ARG/ENV lines (15-16):

```dockerfile
ARG VITE_BUGSINK_DSN=""
ENV VITE_BUGSINK_DSN=$VITE_BUGSINK_DSN
```

With:

```dockerfile
ARG VITE_AXIOM_TOKEN=""
ARG VITE_AXIOM_DATASET=""
ENV VITE_AXIOM_TOKEN=$VITE_AXIOM_TOKEN
ENV VITE_AXIOM_DATASET=$VITE_AXIOM_DATASET
```

- [ ] **Step 5: Verify build still works**

```bash
cd fasolt.Server && dotnet build && cd ../fasolt.client && npm run build
```

Expected: Both build successfully.

- [ ] **Step 6: Commit**

```bash
git add .env.example docker-compose.prod.yml Dockerfile
git commit -m "chore: replace Bugsink env vars with Axiom config

Updates .env.example, docker-compose.prod.yml, and Dockerfile.
Backend uses AXIOM_TOKEN + AXIOM_DATASET.
Frontend uses VITE_AXIOM_TOKEN + VITE_AXIOM_DATASET."
```

---

## Task 6: Clean up old docs and specs

**Files:**
- Modify: `docs/superpowers/specs/2026-03-31-error-tracking-design.md` — update title/references or delete
- Modify: `docs/superpowers/plans/2026-03-31-error-tracking.md` — update title/references or delete

- [ ] **Step 1: Delete old Bugsink plan and spec**

These are historical implementation docs for the old Bugsink integration and are now misleading:

```bash
rm docs/superpowers/specs/2026-03-31-error-tracking-design.md
rm docs/superpowers/plans/2026-03-31-error-tracking.md
```

- [ ] **Step 2: Commit**

```bash
git add -A docs/superpowers/specs/2026-03-31-error-tracking-design.md docs/superpowers/plans/2026-03-31-error-tracking.md
git commit -m "chore: remove obsolete Bugsink error tracking docs"
```

---

## Task 7: Run tests and verify

- [ ] **Step 1: Run backend tests**

```bash
dotnet test
```

Expected: All tests pass. If any tests reference Sentry/Bugsink, they need updating (none found in the codebase, so this should be clean).

- [ ] **Step 2: Run frontend tests**

```bash
cd fasolt.client && npm test
```

Expected: All tests pass.

- [ ] **Step 3: Start full stack and verify**

```bash
./dev.sh
```

Verify: App starts, no errors in console about missing Sentry or Axiom config. Both frontend and backend work normally.

- [ ] **Step 4: Close issue**

```bash
gh issue close 117 --comment "Switched to Axiom. Backend uses Serilog + HTTP sink, frontend uses @axiomhq/js. Both are no-op when env vars are unset."
```
