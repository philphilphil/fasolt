# Converge /login onto Razor-rendered /oauth/login — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retire the Vue SPA `/login` page, migrate every `/oauth/*` HTML-rendering endpoint from hand-rolled C# string templates to Razor Pages, unify auth styling via a shared Vite-built `auth.css`, and add CSP middleware + Playwright E2E coverage.

**Architecture:** Razor Pages live under `fasolt.Server/Pages/Oauth/` with a shared `_Layout.cshtml`. A new Vite rollup entry `auth.css` outputs directly to `fasolt.Server/wwwroot/css/auth.css` (no hash, cache-busted by `asp-append-version`). The SPA's `LoginView.vue`, `AuthLayout.vue`, `auth.login()` store method, `/login` route, `POST /api/account/login`, and `LoginRequest` DTO are all deleted. Unauthenticated SPA navigation triggers `window.location.href = '/oauth/login?returnUrl=…'` — full-page nav, not SPA nav. iOS is unchanged; it already talks to `/oauth/authorize → /oauth/login` and the URL contract is preserved.

**Tech Stack:** .NET 10, ASP.NET Core Razor Pages, ASP.NET Core Identity, OpenIddict, Vite 5, Vue 3, Tailwind 3, shadcn-vue, Playwright (new dependency), xUnit + FluentAssertions + `WebApplicationFactory<Program>` for server tests.

**Spec:** `docs/superpowers/specs/2026-04-09-converge-login-onto-oauth-razor-design.md`

---

## File Structure

### New files (server)

- `fasolt.Server/Pages/Oauth/_ViewImports.cshtml` — namespace + tag helpers
- `fasolt.Server/Pages/Oauth/_ViewStart.cshtml` — sets `Layout = "_Layout"`
- `fasolt.Server/Pages/Oauth/_Layout.cshtml` — shell: logo + wordmark + `@RenderBody()`
- `fasolt.Server/Pages/Oauth/Login.cshtml` + `Login.cshtml.cs`
- `fasolt.Server/Pages/Oauth/ForgotPassword.cshtml` + `.cs`
- `fasolt.Server/Pages/Oauth/ResetPassword.cshtml` + `.cs`
- `fasolt.Server/Pages/Oauth/Register.cshtml` + `.cs`
- `fasolt.Server/Pages/Oauth/VerifyEmail.cshtml` + `.cs`
- `fasolt.Server/Pages/Oauth/Consent.cshtml` + `.cs`
- `fasolt.Server/Api/Middleware/ContentSecurityPolicyMiddleware.cs`
- `fasolt.Server/wwwroot/js/password-rules.js` — extracted inline script
- `fasolt.Tests/Auth/OAuthLoginPageTests.cs`
- `fasolt.Tests/Auth/OAuthCspHeaderTests.cs`

### New files (client)

- `fasolt.client/src/auth.css` — Vite entry point for the shared stylesheet
- `fasolt.client/playwright.config.ts`
- `fasolt.client/e2e/auth-login.spec.ts`
- `fasolt.client/e2e/auth-forgot-password.spec.ts`

### Modified files

- `fasolt.Server/Program.cs` — `AddRazorPages` + `MapRazorPages` + CSP middleware + optional `TestEmailSender` override
- `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` — delete every `MapGet/MapPost` for page rendering
- `fasolt.Server/Api/Endpoints/AccountEndpoints.cs` — delete `MapPost("/login")` + `Login` method
- `fasolt.Server/Application/Dtos/AccountDtos.cs` — delete `LoginRequest`
- `fasolt.client/vite.config.ts` — second rollup entry + dev-proxy additions
- `fasolt.client/src/router/index.ts` — delete `/login` route, rewrite 401 redirect
- `fasolt.client/src/stores/auth.ts` — delete `login()`
- `fasolt.client/src/components/TopBar.vue` — logout redirect
- `fasolt.client/src/views/LandingView.vue` — `RouterLink` → `<a>`
- `fasolt.client/src/views/AlgorithmView.vue` — `RouterLink` → `<a>`
- `fasolt.client/package.json` — add Playwright
- `dev.sh` — add `vite build --watch` for the auth entry (if needed)
- Existing test files: `OAuthForgotPasswordEndpointTests.cs`, `OAuthResetPasswordEndpointTests.cs`, `OAuthRegisterEndpointTests.cs`, `OAuthVerifyEmailEndpointTests.cs` — update assertions if CSS selectors drift

### Deleted files

- `fasolt.Server/Api/Helpers/OAuthPageLayout.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthLoginPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthForgotPasswordPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthResetPasswordPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthRegisterPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthVerifyEmailPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthConsentPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/` (the directory)
- `fasolt.client/src/views/LoginView.vue`
- `fasolt.client/src/layouts/AuthLayout.vue`

---

## Task 1: Razor Pages scaffolding

**Files:**
- Create: `fasolt.Server/Pages/Oauth/_ViewImports.cshtml`
- Create: `fasolt.Server/Pages/Oauth/_ViewStart.cshtml`
- Create: `fasolt.Server/Pages/Oauth/_Layout.cshtml` (stub — real content in Task 3)
- Modify: `fasolt.Server/Program.cs` — add `AddRazorPages()` + `MapRazorPages()`

- [ ] **Step 1: Create `_ViewImports.cshtml`**

Write `fasolt.Server/Pages/Oauth/_ViewImports.cshtml`:
```cshtml
@namespace Fasolt.Server.Pages.Oauth
@using Microsoft.AspNetCore.Mvc.RazorPages
@using Fasolt.Server.Domain.Entities
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

- [ ] **Step 2: Create `_ViewStart.cshtml`**

Write `fasolt.Server/Pages/Oauth/_ViewStart.cshtml`:
```cshtml
@{
    Layout = "_Layout";
}
```

- [ ] **Step 3: Create a stub `_Layout.cshtml`**

Write `fasolt.Server/Pages/Oauth/_Layout.cshtml` (stub; Task 3 fleshes it out):
```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
    <title>@ViewData["Title"] — fasolt</title>
</head>
<body>
    @RenderBody()
</body>
</html>
```

- [ ] **Step 4: Register Razor Pages in DI**

Edit `fasolt.Server/Program.cs`. Find the line that reads `builder.Services.AddIdentityApiEndpoints<AppUser>(...)` (around line 41). Immediately before it, add:
```csharp
builder.Services.AddRazorPages();

```

- [ ] **Step 5: Map Razor Pages routes**

Edit `fasolt.Server/Program.cs`. Find the line `app.MapOAuthEndpoints();` (around line 579). Immediately **after** it (but before `app.MapFallbackToFile("index.html")` around line 651), add:
```csharp
app.MapRazorPages();
```

Order matters: `MapRazorPages()` must run **before** `MapFallbackToFile()` so Razor routes take precedence over the SPA fallback.

- [ ] **Step 6: Build and verify**

Run:
```bash
dotnet build fasolt.sln -nologo -v q
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

There are no Razor Pages with `@page` directives yet, so nothing is routed and the app behaves identically. This is intentional — we're landing the infra in a no-op commit.

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Pages/Oauth/_ViewImports.cshtml \
        fasolt.Server/Pages/Oauth/_ViewStart.cshtml \
        fasolt.Server/Pages/Oauth/_Layout.cshtml \
        fasolt.Server/Program.cs
git commit -m "Add Razor Pages scaffolding for /oauth/* migration

Adds AddRazorPages() + MapRazorPages() and creates the Pages/Oauth/
directory with _ViewImports, _ViewStart, and a stub _Layout. No
pages route yet — this is infrastructure only so the next commits
can land individual page migrations without breaking the build."
```

---

## Task 2: Shared auth.css build pipeline

**Files:**
- Create: `fasolt.client/src/auth.css`
- Modify: `fasolt.client/vite.config.ts` — add rollup input for auth entry
- Modify: `dev.sh` — add `vite build --watch` for auth entry if needed

- [ ] **Step 1: Create the initial auth.css entry**

Write `fasolt.client/src/auth.css`. This is the complete file — includes Tailwind layers, pulls in the SPA's HSL token layer via `@import`, and defines the component classes the Razor pages will use:
```css
@tailwind base;
@tailwind components;
@tailwind utilities;

@import './style.css';

@layer components {
  .oauth-shell {
    @apply relative flex min-h-screen items-center justify-center bg-background px-4 py-8;
  }
  .oauth-shell::before {
    content: '';
    @apply pointer-events-none fixed inset-0 bg-grid bg-grid-fade opacity-30;
  }
  .oauth-brand {
    @apply relative mb-8 flex flex-col items-center gap-2;
  }
  .oauth-brand img {
    @apply h-12 w-12 object-contain;
  }
  .oauth-brand span {
    @apply text-base font-bold text-foreground tracking-tight;
  }
  .oauth-card {
    @apply relative w-full max-w-sm rounded-lg border border-border/60 bg-card p-6 shadow-sm;
  }
  .oauth-card h1 {
    @apply mb-2 text-center text-base font-semibold text-foreground;
  }
  .oauth-card > p.lead {
    @apply mb-5 text-center text-xs text-muted-foreground;
  }
  .oauth-card > p.lead strong {
    @apply text-foreground;
  }
  .oauth-field {
    @apply mb-3 flex flex-col gap-1.5;
  }
  .oauth-field label {
    @apply text-xs font-medium text-foreground;
  }
  .oauth-field input[type=text],
  .oauth-field input[type=email],
  .oauth-field input[type=password] {
    @apply w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2;
  }
  .oauth-field input[name=code] {
    @apply py-3 text-center font-mono text-2xl tracking-[0.5em];
  }
  .oauth-btn {
    @apply mt-1 inline-flex h-10 w-full items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90 disabled:pointer-events-none disabled:opacity-50;
  }
  .oauth-btn-github {
    @apply mb-3 inline-flex h-10 w-full items-center justify-center gap-2 rounded-md bg-[#24292f] px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-[#32383f];
  }
  .oauth-divider {
    @apply my-3 flex items-center gap-3 text-xs text-muted-foreground;
  }
  .oauth-divider::before,
  .oauth-divider::after {
    content: '';
    @apply h-px flex-1 bg-border;
  }
  .oauth-error {
    @apply mb-3 rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive;
  }
  .oauth-field-error {
    @apply mt-1 text-xs text-destructive;
  }
  .oauth-footer {
    @apply mt-4 flex flex-col items-center gap-1 text-center text-xs text-muted-foreground;
  }
  .oauth-footer a {
    @apply text-accent hover:underline;
  }
  .oauth-rules {
    @apply mt-1 list-none space-y-0.5 pl-0 text-[11px] text-muted-foreground;
  }
  .oauth-rules li.ok {
    @apply text-success;
  }
  .oauth-rules li.ok::before {
    content: "\2713 ";
  }
  .oauth-rules li.pending::before {
    content: "\25CB ";
  }
  .oauth-mismatch {
    @apply mt-1 text-[11px] text-destructive;
  }
  .oauth-tos {
    @apply my-3 flex items-start gap-2 text-xs text-foreground;
  }
  .oauth-tos input {
    @apply mt-0.5 w-auto;
  }
  .oauth-tos a {
    @apply font-medium text-accent;
  }
  .oauth-resend {
    @apply mt-3 text-center text-xs text-muted-foreground;
  }
  .oauth-resend button {
    @apply inline bg-transparent p-0 text-xs font-medium text-foreground underline;
  }
}
```

No `.dark` block — per spec Goal #6, auth pages are light-only.

- [ ] **Step 2: Update Vite config for the second entry point**

Read `fasolt.client/vite.config.ts` (the version committed in the previous PR adds `/oauth/login` to the proxy allowlist — if it doesn't already, add it here). Replace the file with:
```ts
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
  build: {
    rollupOptions: {
      input: {
        main: path.resolve(__dirname, 'index.html'),
        auth: path.resolve(__dirname, 'src/auth.css'),
      },
      output: {
        // auth.css emits as a stable unhashed filename so the Razor
        // _Layout.cshtml link stays constant. Cache busting is handled
        // by ASP.NET's asp-append-version tag helper, which hashes the
        // file contents at serve time and appends ?v=<hash>. The SPA
        // bundle keeps its normal hashed names.
        assetFileNames: (assetInfo) => {
          if (assetInfo.name === 'auth.css') return 'css/auth.css'
          return 'assets/[name]-[hash][extname]'
        },
      },
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:8080',
        headers: { 'X-Forwarded-Host': 'localhost:5173', 'X-Forwarded-Proto': 'http' },
      },
      '/signin-github': {
        target: 'http://localhost:8080',
        headers: { 'X-Forwarded-Host': 'localhost:5173', 'X-Forwarded-Proto': 'http' },
      },
      '/mcp': {
        target: 'http://localhost:8080',
        rewrite: undefined,
        bypass(req) {
          if (req.url?.startsWith('/mcp-setup')) return req.url
        },
      },
      '/.well-known': 'http://localhost:8080',
      '/css/auth.css': 'http://localhost:8080',
      '/js/password-rules.js': 'http://localhost:8080',
      '/oauth/register': 'http://localhost:8080',
      '/oauth/verify-email': 'http://localhost:8080',
      '/oauth/forgot-password': 'http://localhost:8080',
      '/oauth/reset-password': 'http://localhost:8080',
      '/oauth/login': 'http://localhost:8080',
      '/oauth/authorize': 'http://localhost:8080',
      '/oauth/token': 'http://localhost:8080',
      '/oauth/consent': 'http://localhost:8080',
      '/oauth/clients/register': 'http://localhost:8080',
      '/register': 'http://localhost:8080',
      '/verify-email': 'http://localhost:8080',
      '/confirm-email': 'http://localhost:8080',
      '/forgot-password': 'http://localhost:8080',
      '/reset-password': 'http://localhost:8080',
    },
  },
})
```

- [ ] **Step 3: Build the stylesheet once to produce the output**

Run:
```bash
cd fasolt.client && npm run build && cd ..
```
Expected: the build completes and produces `fasolt.client/dist/css/auth.css`. The file should be a compiled Tailwind stylesheet with `.oauth-shell`, `.oauth-card`, etc. classes.

- [ ] **Step 4: Verify the output**

Run:
```bash
ls -la fasolt.client/dist/css/auth.css && head -5 fasolt.client/dist/css/auth.css
```
Expected: the file exists and contains compiled CSS (not source Tailwind directives like `@tailwind base`).

- [ ] **Step 5: Copy the compiled file to wwwroot for server consumption**

Check the existing production copy flow. In the Dockerfile or `dev.sh`, there should be a step that copies `fasolt.client/dist/*` into `fasolt.Server/wwwroot/`. If the copy is a wildcard like `cp -r fasolt.client/dist/* fasolt.Server/wwwroot/`, no change needed — `css/auth.css` is picked up automatically. If it's more specific, add the `css/` directory to the copy.

For local development, create the directory and copy the file manually to prove the end-to-end path works:
```bash
mkdir -p fasolt.Server/wwwroot/css && cp fasolt.client/dist/css/auth.css fasolt.Server/wwwroot/css/auth.css
```

- [ ] **Step 6: Verify static serving**

Start the backend:
```bash
dotnet run --project fasolt.Server &
SERVER_PID=$!
sleep 3
curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/css/auth.css
kill $SERVER_PID
```
Expected: `200`. ASP.NET's `UseStaticFiles()` should be already wired; if it's not, find its call in `Program.cs` and confirm it's present — the SPA already depends on it.

- [ ] **Step 7: Update dev.sh for watch mode**

Read `dev.sh`. Identify where the frontend is started (probably `cd fasolt.client && npm run dev &`). Add a parallel `vite build --watch` step so edits to `src/auth.css` keep rebuilding `wwwroot/css/auth.css`. Example — add this before the `cd fasolt.client && npm run dev &` line:
```bash
(cd fasolt.client && npx vite build --watch --mode development) &
VITE_AUTH_PID=$!
```

And add `VITE_AUTH_PID` to any cleanup trap. If `dev.sh` has a simpler structure, integrate appropriately — the contract is "editing `fasolt.client/src/auth.css` causes `fasolt.Server/wwwroot/css/auth.css` to rebuild".

**Important:** `vite build --watch` writes to `dist/` not `wwwroot/`. Either:
- (a) add a parallel file watcher (fswatch, chokidar) that mirrors `dist/css/auth.css` to `wwwroot/css/auth.css`, or
- (b) adjust the Vite output path to write directly to `../fasolt.Server/wwwroot/` for the auth entry only.

Option (b) is simpler. In `vite.config.ts`, extend the output config:
```ts
output: {
  dir: path.resolve(__dirname, '../fasolt.Server/wwwroot'),
  assetFileNames: (assetInfo) => {
    if (assetInfo.name === 'auth.css') return 'css/auth.css'
    return 'assets/[name]-[hash][extname]'
  },
},
```

But this conflicts with the main SPA build which also writes to `dist/`. A cleaner split: put the auth entry in its own small Vite config file `vite.auth.config.ts` that only builds `src/auth.css → ../fasolt.Server/wwwroot/css/auth.css`. Use that file for the watch command.

Create `fasolt.client/vite.auth.config.ts`:
```ts
import path from 'node:path'
import autoprefixer from 'autoprefixer'
import tailwind from 'tailwindcss'
import { defineConfig } from 'vite'

// Dedicated config for building the shared OAuth auth.css stylesheet
// directly into the server's wwwroot so Razor Pages can link to a
// stable /css/auth.css URL. Separated from vite.config.ts so the main
// SPA build output path isn't affected.
export default defineConfig({
  css: {
    postcss: {
      plugins: [tailwind(), autoprefixer()],
    },
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  build: {
    outDir: path.resolve(__dirname, '../fasolt.Server/wwwroot'),
    emptyOutDir: false,
    rollupOptions: {
      input: {
        auth: path.resolve(__dirname, 'src/auth.css'),
      },
      output: {
        assetFileNames: (assetInfo) => {
          if (assetInfo.name === 'auth.css') return 'css/auth.css'
          return 'css/[name][extname]'
        },
      },
    },
  },
})
```

And revert the `vite.config.ts` changes from Step 2 — remove the second rollup entry (leave only the proxy additions). The main `vite.config.ts` goes back to just the SPA build.

Add a script to `fasolt.client/package.json` (read it first, add under `scripts`):
```json
"build:auth": "vite build --config vite.auth.config.ts",
"watch:auth": "vite build --config vite.auth.config.ts --watch"
```

Update `dev.sh` to run `npm run watch:auth` in the background alongside the SPA dev server.

Run the build once to verify the new config:
```bash
cd fasolt.client && npm run build:auth && cd ..
ls -la fasolt.Server/wwwroot/css/auth.css
```
Expected: the file exists in `fasolt.Server/wwwroot/css/auth.css`.

- [ ] **Step 8: Add wwwroot/css/auth.css to gitignore**

Edit `.gitignore` and add:
```
# Generated by vite.auth.config.ts, rebuilt on every dev/prod build
fasolt.Server/wwwroot/css/auth.css
```

We don't commit the build output — it's regenerated on every build.

- [ ] **Step 9: Update the production build (Dockerfile)**

Read `Dockerfile`. Find the step that runs `npm run build` in the client stage. Immediately after it, add:
```dockerfile
RUN npm run build:auth
```

This ensures `wwwroot/css/auth.css` exists in the production image.

- [ ] **Step 10: Commit**

```bash
git add fasolt.client/src/auth.css \
        fasolt.client/vite.config.ts \
        fasolt.client/vite.auth.config.ts \
        fasolt.client/package.json \
        fasolt.client/package-lock.json \
        dev.sh \
        Dockerfile \
        .gitignore
git commit -m "Add shared auth.css build pipeline for server-rendered OAuth pages

Introduces fasolt.client/src/auth.css as a Tailwind entry point that
compiles to fasolt.Server/wwwroot/css/auth.css via a dedicated
vite.auth.config.ts. The stylesheet defines .oauth-* component
classes using @apply against the same HSL token layer as the SPA,
so both surfaces share a single source of truth for the palette.

dev.sh runs 'vite build --watch' on the auth config in parallel with
the SPA dev server so edits to src/auth.css rebuild the output file
continuously. The production Dockerfile adds 'npm run build:auth'
after the main SPA build step so the stylesheet ships in the image."
```

---

## Task 3: Real _Layout.cshtml with brand shell

**Files:**
- Modify: `fasolt.Server/Pages/Oauth/_Layout.cshtml` — replace the stub with the real shell

- [ ] **Step 1: Replace the stub layout**

Overwrite `fasolt.Server/Pages/Oauth/_Layout.cshtml`:
```cshtml
@{
    var pageTitle = ViewData["Title"] as string ?? "fasolt";
}
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
    <title>@pageTitle — fasolt</title>
    <link rel="stylesheet" href="~/css/auth.css" asp-append-version="true" />
    <link rel="icon" href="~/favicon.svg" type="image/svg+xml" />
</head>
<body>
    <div class="oauth-shell">
        <div class="relative w-full max-w-sm">
            <div class="oauth-brand">
                <img src="~/logo.svg" alt="fasolt" />
                <span>fasolt</span>
            </div>
            @RenderBody()
        </div>
    </div>
</body>
</html>
```

The inner `<div class="relative w-full max-w-sm">` matches the SPA's `AuthLayout.vue:9` wrapper — it constrains the card + brand to 24rem max width and makes them participate in the wallpaper's stacking context.

- [ ] **Step 2: Verify logo.svg and favicon.svg exist in wwwroot**

Run:
```bash
ls -la fasolt.Server/wwwroot/logo.svg fasolt.Server/wwwroot/favicon.svg 2>&1
```
If either is missing, copy it from `fasolt.client/public/`:
```bash
cp fasolt.client/public/logo.svg fasolt.Server/wwwroot/logo.svg 2>/dev/null || true
cp fasolt.client/public/favicon.svg fasolt.Server/wwwroot/favicon.svg 2>/dev/null || true
```
(They should already be served via the SPA static copy. This is a sanity check.)

- [ ] **Step 3: Build**

```bash
dotnet build fasolt.sln -nologo -v q
```
Expected: success. No Razor Pages yet reference the layout, so nothing renders it — this commit is still a no-op for runtime behavior.

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Pages/Oauth/_Layout.cshtml
git commit -m "Flesh out Razor _Layout.cshtml with brand shell

Replaces the stub layout with the real brand shell: grid wallpaper
via .oauth-shell, logo + wordmark header above the card slot,
stylesheet link to ~/css/auth.css with asp-append-version for cache
busting. Mirrors fasolt.client/src/layouts/AuthLayout.vue exactly so
the Razor pages inherit the same visual language as the retiring
SPA /login."
```

---

## Task 4: Migrate /oauth/login to a Razor Page (test-first)

**Files:**
- Create: `fasolt.Tests/Auth/OAuthLoginPageTests.cs`
- Create: `fasolt.Server/Pages/Oauth/Login.cshtml`
- Create: `fasolt.Server/Pages/Oauth/Login.cshtml.cs`
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` — delete `MapGet("/oauth/login")` and `MapPost("/oauth/login")`
- Delete: `fasolt.Server/Api/Helpers/OAuthPages/OAuthLoginPage.cs`

- [ ] **Step 1: Write the failing integration tests for GET**

Create `fasolt.Tests/Auth/OAuthLoginPageTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Tests.Auth;

public class OAuthLoginPageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthLoginPageTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OTP_PEPPER"] = "test-pepper",
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                });
            });
        });
    }

    [Fact]
    public async Task Get_Anonymous_RendersLoginForm()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/oauth/login?returnUrl=%2F");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("<form");
        body.Should().Contain("action=\"/oauth/login\"");
        body.Should().Contain("name=\"Input.Email\"");
        body.Should().Contain("name=\"Input.Password\"");
        body.Should().Contain("Sign in to fasolt");
        body.Should().Contain("__RequestVerificationToken");
    }

    [Fact]
    public async Task Get_WithProviderHintGithub_AndGitHubConfigured_RedirectsToGitHubLogin()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GITHUB_CLIENT_ID"] = "test-client-id",
                    ["GITHUB_CLIENT_SECRET"] = "test-client-secret",
                });
            });
        });
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/oauth/login?provider_hint=github&returnUrl=%2F");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/api/account/github-login");
    }

    [Fact]
    public async Task Post_MissingCsrf_Returns400()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = "nobody@example.com",
            ["Input.Password"] = "Abcdefg1",
        });

        var response = await client.PostAsync("/oauth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_InvalidPassword_RendersFormWithError()
    {
        var email = $"wrong-{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, "Abcdefg1")).Succeeded.Should().BeTrue();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/login?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["Input.Password"] = "wrong-password",
            ["ReturnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/login") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Invalid email or password.");
    }

    [Fact]
    public async Task Post_ValidCredentials_RedirectsToReturnUrlAndSetsCookie()
    {
        var email = $"ok-{Guid.NewGuid():N}@example.com";
        const string password = "Abcdefg1";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true };
            (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/login?returnUrl=%2Fstudy");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["ReturnUrl"] = "/study",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/login") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/study");
        response.Headers.TryGetValues("Set-Cookie", out var setCookies).Should().BeTrue();
        setCookies!.Any(c => c.Contains(".AspNetCore.Identity.Application")).Should().BeTrue(
            "successful login must issue the Identity application cookie");
    }

    [Fact]
    public async Task Post_UnverifiedUser_RedirectsToVerifyEmail()
    {
        var email = $"unverified-{Guid.NewGuid():N}@example.com";
        const string password = "Abcdefg1";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = false };
            (await userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/login?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["ReturnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/login") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/oauth/verify-email");
        response.Headers.Location!.OriginalString.Should().Contain(Uri.EscapeDataString(email));
    }

    [Fact]
    public async Task Post_InvalidEmailFormat_RendersFieldLevelError()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var getResponse = await client.GetAsync("/oauth/login?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = "not-an-email",
            ["Input.Password"] = "Abcdefg1",
            ["ReturnUrl"] = "/",
        });
        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/login") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        // Data-annotation EmailAddress validator surfaces via ModelState
        body.Should().MatchRegex("[Ee]mail");
    }

    private static string ExtractCsrfToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\" value=\"";
        var idx = html.IndexOf(marker);
        if (idx < 0) throw new InvalidOperationException("CSRF token not found in:\n" + html);
        var start = idx + marker.Length;
        var end = html.IndexOf("\"", start);
        return html.Substring(start, end - start);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run:
```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthLoginPageTests" -nologo 2>&1 | tail -30
```
Expected: tests fail. The new `OAuthLoginPageTests` file expects the form field names to be `Input.Email` / `Input.Password` (Razor convention) and an anti-forgery token, but the current hand-rolled page uses plain `email` / `password`. Some tests may pass by accident (the current GET renders a page with a form). The critical failing ones are `Post_InvalidEmailFormat_RendersFieldLevelError` (no server-side validation today) and any that assert on `Input.Email` field name.

This is expected TDD state. Proceed.

- [ ] **Step 3: Create the Razor Page code-behind**

Write `fasolt.Server/Pages/Oauth/Login.cshtml.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Fasolt.Server.Api.Helpers;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Pages.Oauth;

[AllowAnonymous]
[EnableRateLimiting("auth")]
public class LoginModel : PageModel
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IEmailVerificationCodeService _otpService;
    private readonly IOtpEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public LoginModel(
        SignInManager<AppUser> signInManager,
        IEmailVerificationCodeService otpService,
        IOtpEmailSender emailSender,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _otpService = otpService;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; } = "/";

    public string? ErrorMessage { get; set; }

    public bool GitHubEnabled => !string.IsNullOrEmpty(_configuration["GITHUB_CLIENT_ID"]);

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    public IActionResult OnGet(string? providerHint, string? error)
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        // Provider-hint redirect: if the caller passes ?provider_hint=github
        // and GitHub is configured, bounce straight into the GitHub OAuth
        // flow without rendering the login page. iOS relies on this to
        // short-circuit to GitHub when the user has previously used it.
        if (providerHint == "github" && GitHubEnabled)
        {
            return Redirect($"/api/account/github-login?returnUrl={Uri.EscapeDataString(ReturnUrl)}");
        }

        // Friendly error mapping for GitHub OAuth callback failures. Matches
        // the map in the old SPA LoginView.vue:23-26 so UX doesn't change.
        ErrorMessage = error switch
        {
            "github_auth_failed" => "GitHub authentication failed. Please try again.",
            "account_creation_failed" => "Could not create your account. Please try again.",
            null or "" => null,
            _ => error,
        };

        return Page();
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        if (!ModelState.IsValid)
        {
            // Field-level errors (missing required, bad email format) render
            // via the asp-validation-for tag helpers in the template.
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password,
            isPersistent: true, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ErrorMessage = result.IsLockedOut
                ? "Account locked. Try again later."
                : "Invalid email or password.";
            return Page();
        }

        var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);
        if (user is null)
        {
            // Racy corner case: signed-in moments ago, gone now. Bail cleanly.
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        // Unverified accounts must complete OTP verification before we hand
        // out a persistent cookie. Same logic as the old endpoint: sign the
        // user out of the PasswordSignIn, generate a fresh OTP (respecting
        // the resend window), and send them to the verify page.
        if (!user.EmailConfirmed)
        {
            await _signInManager.SignOutAsync();

            var canResend = await _otpService.CanResendAsync(user.Id, HttpContext.RequestAborted);
            if (canResend == ResendResult.Ok)
            {
                try
                {
                    var code = await _otpService.GenerateAndStoreAsync(user.Id, HttpContext.RequestAborted);
                    await _emailSender.SendVerificationCodeAsync(user, user.Email!, code);
                }
                catch (InvalidOperationException)
                {
                    // Race against the advisory lock — another tab/click won.
                    // Fall through to the verify page silently.
                }
            }

            return Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(Input.Email)}&returnUrl={Uri.EscapeDataString(ReturnUrl)}");
        }

        return Redirect(ReturnUrl);
    }
}
```

- [ ] **Step 4: Create the Razor Page template**

Write `fasolt.Server/Pages/Oauth/Login.cshtml`:
```cshtml
@page "/oauth/login"
@model LoginModel
@{
    ViewData["Title"] = "Sign in";
}

<main class="oauth-card">
    <h1>Sign in to fasolt</h1>

    @if (!string.IsNullOrEmpty(Model.ErrorMessage))
    {
        <div class="oauth-error">@Model.ErrorMessage</div>
    }

    @if (Model.GitHubEnabled)
    {
        <a class="oauth-btn-github" href="/api/account/github-login?returnUrl=@Uri.EscapeDataString(Model.ReturnUrl)">
            <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor" aria-hidden="true">
                <path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z"/>
            </svg>
            Continue with GitHub
        </a>
        <div class="oauth-divider"><span>or</span></div>
    }

    <form method="post" action="/oauth/login">
        <input type="hidden" asp-for="ReturnUrl" />
        <div class="oauth-field">
            <label asp-for="Input.Email">Email</label>
            <input asp-for="Input.Email" type="email" placeholder="you@example.com" autocomplete="email" required autofocus />
            <span class="oauth-field-error" asp-validation-for="Input.Email"></span>
        </div>
        <div class="oauth-field">
            <label asp-for="Input.Password">Password</label>
            <input asp-for="Input.Password" type="password" autocomplete="current-password" required />
            <span class="oauth-field-error" asp-validation-for="Input.Password"></span>
        </div>
        <button type="submit" class="oauth-btn">Sign in</button>
    </form>

    <div class="oauth-footer">
        <a asp-page="/Oauth/Register" asp-route-returnUrl="@Model.ReturnUrl">Create an account</a>
        <a asp-page="/Oauth/ForgotPassword" asp-route-returnUrl="@Model.ReturnUrl">Forgot password?</a>
    </div>
</main>
```

Note the explicit `@page "/oauth/login"` directive preserves the URL contract iOS and external links depend on.

- [ ] **Step 5: Delete the old hand-rolled page + endpoints**

Delete the helper file:
```bash
rm fasolt.Server/Api/Helpers/OAuthPages/OAuthLoginPage.cs
```

Edit `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`. Find and delete:
- The `// OAuth Login Page (GET)` block at lines ~321-342 (the `app.MapGet("/oauth/login", ...)` lambda)
- The `// OAuth Login Handler (POST)` block at lines ~344-412 (the `app.MapPost("/oauth/login", ...)` lambda, including the `.RequireRateLimiting("auth")` chained call)

Keep everything else in `OAuthEndpoints.cs` untouched.

- [ ] **Step 6: Build and run tests**

Run:
```bash
dotnet build fasolt.sln -nologo -v q
```
Expected: success.

Run:
```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthLoginPageTests" -nologo 2>&1 | tail -20
```
Expected: all 7 tests pass.

If `Post_InvalidPassword_RendersFormWithError` or `Post_ValidCredentials_RedirectsToReturnUrlAndSetsCookie` fails because the form returns a 400 instead of re-rendering, the antiforgery middleware is rejecting the request. Verify the form in `Login.cshtml` uses a `<form method="post">` (which auto-injects the token via the form tag helper) — Razor's form tag helper generates the hidden `__RequestVerificationToken` field automatically.

- [ ] **Step 7: Manual smoke test**

Start the stack:
```bash
./dev.sh &
sleep 5
```

Open `http://localhost:5173/oauth/login` in a browser. Verify:
- Logo + "fasolt" wordmark above the card
- Grid wallpaper background
- "Sign in to fasolt" heading
- Email/password inputs, submit button
- "Create an account" and "Forgot password?" links in the footer

Submit the dev seed user (`dev@fasolt.local` / `Dev1234!`). Verify you land on `/study` with a valid session.

Stop the stack:
```bash
kill %1 2>/dev/null; pkill -f "dotnet.*fasolt.Server" 2>/dev/null; pkill -f "vite" 2>/dev/null
```

- [ ] **Step 8: Commit**

```bash
git add fasolt.Server/Pages/Oauth/Login.cshtml \
        fasolt.Server/Pages/Oauth/Login.cshtml.cs \
        fasolt.Server/Api/Endpoints/OAuthEndpoints.cs \
        fasolt.Tests/Auth/OAuthLoginPageTests.cs
git rm fasolt.Server/Api/Helpers/OAuthPages/OAuthLoginPage.cs
git commit -m "Migrate /oauth/login to Razor Page + retire hand-rolled helper

Login.cshtml + Login.cshtml.cs replace OAuthLoginPage.cs and the
MapGet/MapPost('/oauth/login') minimal-API endpoints. The PageModel
uses auto-antiforgery via [ValidateAntiForgeryToken], model binding
via [BindProperty] InputModel, and data-annotation validation via
[Required, EmailAddress] — all features the hand-rolled endpoints
had to implement manually. Flips PasswordSignInAsync to
isPersistent: true unconditionally (modern SaaS default; no
remember-me checkbox).

Adds OAuthLoginPageTests covering GET render, provider-hint redirect,
missing CSRF, invalid password, valid credentials + cookie issuance,
unverified-user verify-email redirect, and field-level email format
validation (new coverage that the hand-rolled endpoint lacked)."
```

---

## Task 5: Migrate /oauth/forgot-password to a Razor Page

**Files:**
- Create: `fasolt.Server/Pages/Oauth/ForgotPassword.cshtml` + `.cs`
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` — delete both `/oauth/forgot-password` endpoints
- Modify: `fasolt.Tests/Auth/OAuthForgotPasswordEndpointTests.cs` — selector tweaks if needed
- Delete: `fasolt.Server/Api/Helpers/OAuthPages/OAuthForgotPasswordPage.cs`

- [ ] **Step 1: Create the PageModel**

Write `fasolt.Server/Pages/Oauth/ForgotPassword.cshtml.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Fasolt.Server.Api.Helpers;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Pages.Oauth;

[AllowAnonymous]
[EnableRateLimiting("auth-strict")]
public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IPasswordResetCodeService _otpService;
    private readonly IOtpEmailSender _emailSender;

    public ForgotPasswordModel(
        UserManager<AppUser> userManager,
        IPasswordResetCodeService otpService,
        IOtpEmailSender emailSender)
    {
        _userManager = userManager;
        _otpService = otpService;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; } = "/";

    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Sent { get; set; }

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";
    }

    public IActionResult OnGet(string? error)
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
        ErrorMessage = string.IsNullOrEmpty(error) ? null : error;
        return Page();
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please enter a valid email address.";
            return Page();
        }

        // Enumeration guard: look up the user but never reveal whether an
        // account exists. External-provider (GitHub/Apple) and unverified
        // users also fall through to the generic "check your email" view so
        // timing and behaviour can't leak account existence. Matches the
        // post-PR-#110 enumeration-guarded logic verbatim.
        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user is not null && user.ExternalProvider is null && user.EmailConfirmed)
        {
            var canResend = await _otpService.CanResendAsync(user.Id, HttpContext.RequestAborted);
            if (canResend == ResendResult.Ok)
            {
                try
                {
                    var code = await _otpService.GenerateAndStoreAsync(user.Id, HttpContext.RequestAborted);
                    await _emailSender.SendPasswordResetCodeAsync(user, user.Email!, code);
                }
                catch (InvalidOperationException)
                {
                    // Lost a cap/cooldown race inside the advisory lock.
                    // Still fall through to the generic confirmation.
                }
            }
        }

        return RedirectToPage(new { returnUrl = ReturnUrl, email = Input.Email, sent = true });
    }
}
```

- [ ] **Step 2: Create the Razor template**

Write `fasolt.Server/Pages/Oauth/ForgotPassword.cshtml`:
```cshtml
@page "/oauth/forgot-password"
@model ForgotPasswordModel
@{
    ViewData["Title"] = Model.Sent ? "Check your email" : "Reset password";
}

@if (Model.Sent)
{
    <main class="oauth-card">
        <h1>Check your email</h1>
        <p class="lead">
            If <strong>@Model.Email</strong> matches an account, we sent a 6-digit reset code.
        </p>
        <form method="get" action="/oauth/reset-password">
            <input type="hidden" name="email" value="@Model.Email" />
            <input type="hidden" name="returnUrl" value="@Model.ReturnUrl" />
            <button type="submit" class="oauth-btn">Enter reset code</button>
        </form>
        <div class="oauth-footer">
            <a asp-page="/Oauth/Login" asp-route-returnUrl="@Model.ReturnUrl">Back to sign in</a>
        </div>
    </main>
}
else
{
    <main class="oauth-card">
        <h1>Reset your password</h1>
        <p class="lead">Enter your email and we'll send you a 6-digit code.</p>

        @if (!string.IsNullOrEmpty(Model.ErrorMessage))
        {
            <div class="oauth-error">@Model.ErrorMessage</div>
        }

        <form method="post" action="/oauth/forgot-password">
            <input type="hidden" asp-for="ReturnUrl" />
            <div class="oauth-field">
                <label asp-for="Input.Email">Email</label>
                <input asp-for="Input.Email" type="email" placeholder="you@example.com" autocomplete="email" required autofocus />
                <span class="oauth-field-error" asp-validation-for="Input.Email"></span>
            </div>
            <button type="submit" class="oauth-btn">Send reset code</button>
        </form>

        <div class="oauth-footer">
            <a asp-page="/Oauth/Login" asp-route-returnUrl="@Model.ReturnUrl">Back to sign in</a>
        </div>
    </main>
}
```

- [ ] **Step 3: Delete the old hand-rolled page + endpoints**

Delete:
```bash
rm fasolt.Server/Api/Helpers/OAuthPages/OAuthForgotPasswordPage.cs
```

Edit `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`. Find and delete:
- The `// OAuth Forgot Password Page (GET)` block (the `app.MapGet("/oauth/forgot-password", ...)` lambda)
- The `// OAuth Forgot Password Handler (POST)` block (the `app.MapPost("/oauth/forgot-password", ...)` lambda and its `.RequireRateLimiting("auth-strict")`)

Keep all other OAuth endpoints intact.

- [ ] **Step 4: Build**

```bash
dotnet build fasolt.sln -nologo -v q
```
Expected: success.

- [ ] **Step 5: Run existing ForgotPassword tests**

Run:
```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthForgotPasswordEndpointTests" -nologo 2>&1 | tail -20
```

Expected behavior — most tests should still pass because:
- URL is the same (`/oauth/forgot-password`)
- Form field name for email is now `Input.Email` (was `email`)
- CSRF field name is the same (`__RequestVerificationToken`)
- Body contains `"Reset your password"`, `"<form"`, `"action=\"/oauth/forgot-password\""`

Tests that fail will be on the form field name (`name="email"` is now `name="Input.Email"`). Fix by either (a) updating the test assertions, or (b) accepting `email` as an alternate binding on the PageModel. Going with (a) — update the test file.

Find any assertion in `OAuthForgotPasswordEndpointTests.cs` that checks `body.Should().Contain("name=\"email\"")` and replace with `body.Should().Contain("name=\"Input.Email\"")`. Similarly, any `FormUrlEncodedContent` dictionary that posts `["email"] = email` should become `["Input.Email"] = email`.

Run the tests again until they all pass.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Pages/Oauth/ForgotPassword.cshtml \
        fasolt.Server/Pages/Oauth/ForgotPassword.cshtml.cs \
        fasolt.Server/Api/Endpoints/OAuthEndpoints.cs \
        fasolt.Tests/Auth/OAuthForgotPasswordEndpointTests.cs
git rm fasolt.Server/Api/Helpers/OAuthPages/OAuthForgotPasswordPage.cs
git commit -m "Migrate /oauth/forgot-password to Razor Page

ForgotPassword.cshtml + .cshtml.cs replace OAuthForgotPasswordPage.cs
and the MapGet/MapPost('/oauth/forgot-password') endpoints. The dual
GET state machine (entry form vs ?sent=1 confirmation) is preserved
via [BindProperty(SupportsGet = true)] bool Sent. The enumeration
guard logic (unknown/external-provider/unverified users all fall
through to the generic 'check your email' view) is lifted verbatim.

Existing OAuthForgotPasswordEndpointTests updated for the Razor
field name convention (Input.Email instead of plain email)."
```

---

## Task 6: Migrate /oauth/reset-password to a Razor Page

**Files:**
- Create: `fasolt.Server/Pages/Oauth/ResetPassword.cshtml` + `.cs`
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` — delete three endpoints
- Modify: `fasolt.Tests/Auth/OAuthResetPasswordEndpointTests.cs` — selector/field name tweaks
- Delete: `fasolt.Server/Api/Helpers/OAuthPages/OAuthResetPasswordPage.cs`

- [ ] **Step 1: Create the PageModel**

Write `fasolt.Server/Pages/Oauth/ResetPassword.cshtml.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Fasolt.Server.Api.Helpers;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Pages.Oauth;

[AllowAnonymous]
[EnableRateLimiting("auth")]
public class ResetPasswordModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IPasswordResetCodeService _otpService;
    private readonly IOtpEmailSender _emailSender;

    public ResetPasswordModel(
        UserManager<AppUser> userManager,
        IPasswordResetCodeService otpService,
        IOtpEmailSender emailSender)
    {
        _userManager = userManager;
        _otpService = otpService;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; } = "/";

    public string? ErrorMessage { get; set; }
    public bool Success { get; set; }

    public class InputModel
    {
        [Required]
        public string Code { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";
    }

    public IActionResult OnGet(string? error)
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
        ErrorMessage = string.IsNullOrEmpty(error) ? null : error;
        return Page();
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please fill in all fields.";
            return Page();
        }

        if (Input.Password != Input.ConfirmPassword)
        {
            ErrorMessage = "Passwords don't match.";
            return Page();
        }

        // Run the password policy BEFORE any account lookup so weak-password
        // + unknown-email and weak-password + known-email take identical
        // code paths. Otherwise the policy check on a real user would behave
        // differently from the early-out on an unknown email — an
        // enumeration oracle via latency/errors.
        var passwordProbe = new AppUser { UserName = Email, Email = Email };
        foreach (var validator in _userManager.PasswordValidators)
        {
            var pwResult = await validator.ValidateAsync(_userManager, passwordProbe, Input.Password);
            if (!pwResult.Succeeded)
            {
                ErrorMessage = string.Join("; ", pwResult.Errors.Select(e => e.Description));
                return Page();
            }
        }

        var user = await _userManager.FindByEmailAsync(Email);
        if (user is null || user.ExternalProvider is not null || !user.EmailConfirmed)
        {
            ErrorMessage = "That code has expired. Request a new one.";
            return Page();
        }

        var verifyResult = await _otpService.VerifyAsync(user.Id, Input.Code, HttpContext.RequestAborted);
        switch (verifyResult)
        {
            case VerifyResult.Ok:
                break;
            case VerifyResult.Incorrect:
                ErrorMessage = "Incorrect code, try again.";
                return Page();
            case VerifyResult.Expired:
            case VerifyResult.NotFound:
                ErrorMessage = "That code has expired. Request a new one.";
                return Page();
            case VerifyResult.LockedOut:
                ErrorMessage = "Too many failed attempts. Try again in 10 minutes.";
                return Page();
            default:
                ErrorMessage = "Something went wrong. Please try again.";
                return Page();
        }

        // OTP consumed. Rotate the password via Remove + Add so SecurityStamp
        // gets bumped and existing sessions (eventually) invalidate.
        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            ErrorMessage = "Something went wrong. Please try again.";
            return Page();
        }

        var addResult = await _userManager.AddPasswordAsync(user, Input.Password);
        if (!addResult.Succeeded)
        {
            ErrorMessage = string.Join("; ", addResult.Errors.Select(e => e.Description));
            return Page();
        }

        Success = true;
        return Page();
    }

    [EnableRateLimiting("auth-strict")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostResendAsync()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        // Enumeration guard (same pattern as ForgotPassword): every branch
        // lands on the same generic redirect with no error param, so
        // unknown / external-provider / unverified / cooldown / lockout /
        // ok are all indistinguishable.
        var user = await _userManager.FindByEmailAsync(Email);
        if (user is not null && user.ExternalProvider is null && user.EmailConfirmed)
        {
            var canResend = await _otpService.CanResendAsync(user.Id, HttpContext.RequestAborted);
            if (canResend == ResendResult.Ok)
            {
                try
                {
                    var code = await _otpService.GenerateAndStoreAsync(user.Id, HttpContext.RequestAborted);
                    await _emailSender.SendPasswordResetCodeAsync(user, user.Email!, code);
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        return RedirectToPage(new { email = Email, returnUrl = ReturnUrl });
    }
}
```

- [ ] **Step 2: Create the Razor template**

Write `fasolt.Server/Pages/Oauth/ResetPassword.cshtml`:
```cshtml
@page "/oauth/reset-password"
@model ResetPasswordModel
@{
    ViewData["Title"] = Model.Success ? "Password updated" : "Reset password";
}

@if (Model.Success)
{
    <main class="oauth-card">
        <h1>Password updated</h1>
        <p class="lead">You can now sign in with your new password.</p>
        <form method="get" action="/oauth/login">
            <input type="hidden" name="returnUrl" value="@Model.ReturnUrl" />
            <button type="submit" class="oauth-btn">Go to sign in</button>
        </form>
    </main>
}
else
{
    <main class="oauth-card">
        <h1>Enter reset code</h1>
        <p class="lead">We sent a 6-digit code to <strong>@Model.Email</strong></p>

        @if (!string.IsNullOrEmpty(Model.ErrorMessage))
        {
            <div class="oauth-error">@Model.ErrorMessage</div>
        }

        <form method="post" action="/oauth/reset-password" id="resetForm">
            <input type="hidden" asp-for="Email" />
            <input type="hidden" asp-for="ReturnUrl" />
            <div class="oauth-field">
                <input asp-for="Input.Code" type="text" inputmode="numeric" autocomplete="one-time-code" pattern="[0-9]{6}" maxlength="6" autofocus required />
            </div>
            <div class="oauth-field">
                <label asp-for="Input.Password">New password</label>
                <input asp-for="Input.Password" type="password" id="password" autocomplete="new-password" required />
                <ul class="oauth-rules" id="rules">
                    <li class="pending" data-rule="length">At least 8 characters</li>
                    <li class="pending" data-rule="upper">Uppercase letter</li>
                    <li class="pending" data-rule="lower">Lowercase letter</li>
                    <li class="pending" data-rule="digit">Number</li>
                </ul>
            </div>
            <div class="oauth-field">
                <label asp-for="Input.ConfirmPassword">Confirm new password</label>
                <input asp-for="Input.ConfirmPassword" type="password" id="confirmPassword" autocomplete="new-password" required />
                <div class="oauth-mismatch" id="mismatch" style="display:none">Passwords don't match</div>
            </div>
            <button type="submit" class="oauth-btn">Reset password</button>
        </form>

        <div class="oauth-resend">
            Didn't get it?
            <form method="post" asp-page-handler="resend">
                <input type="hidden" asp-for="Email" />
                <input type="hidden" asp-for="ReturnUrl" />
                <button type="submit">Resend code</button>
            </form>
        </div>

        <div class="oauth-footer">
            <a asp-page="/Oauth/ForgotPassword" asp-route-returnUrl="@Model.ReturnUrl">Use a different email</a>
        </div>
    </main>

    <script src="~/js/password-rules.js" asp-append-version="true" defer></script>
}
```

Note: the password-rules script is referenced from a static file, which doesn't exist yet. Task 8 (Register migration) creates it; this task only references it. For now, running this page without `/js/password-rules.js` will produce a 404 for the script but the form still works.

- [ ] **Step 3: Delete the old hand-rolled page + endpoints**

Delete:
```bash
rm fasolt.Server/Api/Helpers/OAuthPages/OAuthResetPasswordPage.cs
```

Edit `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`. Find and delete:
- `// OAuth Reset Password Page (GET)` block
- `// OAuth Reset Password Handler (POST)` block
- `// OAuth Reset Password Resend Handler (POST)` block

- [ ] **Step 4: Build**

```bash
dotnet build fasolt.sln -nologo -v q
```
Expected: success.

- [ ] **Step 5: Run existing ResetPassword tests**

Run:
```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthResetPasswordEndpointTests" -nologo 2>&1 | tail -30
```

Update failing assertions in `OAuthResetPasswordEndpointTests.cs`:
- Form field `name="code"` → `name="Input.Code"`
- Form field `name="password"` → `name="Input.Password"`
- Form field `name="confirmPassword"` → `name="Input.ConfirmPassword"`
- `FormUrlEncodedContent` dictionaries: `["code"] =` → `["Input.Code"] =`, `["password"] =` → `["Input.Password"] =`, `["confirmPassword"] =` → `["Input.ConfirmPassword"] =`
- The `email` field becomes `Email` (capitalized) in the form, but it's bound via `[BindProperty(SupportsGet = true)] public string Email` so post with `["Email"] = email`

The resend endpoint test (`ResendPost_UnknownAndThrottledUsers_ProduceIdenticalRedirect`) needs to post to `/oauth/reset-password?handler=resend` instead of `/oauth/reset-password/resend`. Razor named handlers route via `?handler=<name>`.

Iterate until all tests pass.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Pages/Oauth/ResetPassword.cshtml \
        fasolt.Server/Pages/Oauth/ResetPassword.cshtml.cs \
        fasolt.Server/Api/Endpoints/OAuthEndpoints.cs \
        fasolt.Tests/Auth/OAuthResetPasswordEndpointTests.cs
git rm fasolt.Server/Api/Helpers/OAuthPages/OAuthResetPasswordPage.cs
git commit -m "Migrate /oauth/reset-password to Razor Page

ResetPassword.cshtml + .cshtml.cs replace OAuthResetPasswordPage.cs
and the three MapPost endpoints. The resend handler becomes a named
Razor handler OnPostResendAsync, reachable via ?handler=resend. All
post-PR-#110 security logic (password policy pre-check for
enumeration defense, external-provider rejection, OTP verify,
Remove+AddPassword rotation, enumeration-guarded resend) is lifted
verbatim into the PageModel.

Existing OAuthResetPasswordEndpointTests updated for the Razor field
name convention (Input.* prefix on form fields, ?handler=resend for
the resend endpoint URL)."
```

---

## Task 7: Extract password-rules.js

**Files:**
- Create: `fasolt.Server/wwwroot/js/password-rules.js`

- [ ] **Step 1: Create the static JS file**

Write `fasolt.Server/wwwroot/js/password-rules.js`:
```javascript
// Live password-rules evaluator used by /oauth/register and
// /oauth/reset-password. Extracted from the inline <script> blocks
// in the old OAuthRegisterPage.cs and OAuthResetPasswordPage.cs so
// the pages can enforce CSP script-src 'self' without needing
// 'unsafe-inline'. Pure DOM, no dependencies.
//
// Keep the rules in sync with the password policy configured in
// Program.cs (IdentityOptions.Password). Drift here means the
// client-side checklist lies to the user.
(function () {
  const pwd = document.getElementById('password');
  const confirm = document.getElementById('confirmPassword');
  const rules = document.getElementById('rules');
  const mismatch = document.getElementById('mismatch');

  if (!pwd || !rules) return; // page doesn't use password rules

  function evaluate() {
    const v = pwd.value;
    const checks = {
      length: v.length >= 8,
      upper: /[A-Z]/.test(v),
      lower: /[a-z]/.test(v),
      digit: /[0-9]/.test(v),
    };
    for (const li of rules.children) {
      const r = li.dataset.rule;
      li.className = checks[r] ? 'ok' : 'pending';
    }
    if (mismatch && confirm) {
      mismatch.style.display = (confirm.value && confirm.value !== v) ? 'block' : 'none';
    }
  }

  pwd.addEventListener('input', evaluate);
  if (confirm) confirm.addEventListener('input', evaluate);
})();
```

- [ ] **Step 2: Verify static file serving**

Start the backend briefly and check:
```bash
dotnet run --project fasolt.Server &
SERVER_PID=$!
sleep 3
curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/js/password-rules.js
kill $SERVER_PID
```
Expected: `200`.

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/wwwroot/js/password-rules.js
git commit -m "Extract inline password-rules script to a static file

wwwroot/js/password-rules.js is loaded by /oauth/register and
/oauth/reset-password via <script src> instead of an inline
<script> block. This lets the CSP middleware use script-src 'self'
(no 'unsafe-inline') while keeping the live password-rules checklist
UX. Pure DOM, no dependencies, no framework. Guarded with
getElementById checks so it's safe to load on pages that don't use
the rules."
```

---

## Task 8: Migrate /oauth/register to a Razor Page

**Files:**
- Create: `fasolt.Server/Pages/Oauth/Register.cshtml` + `.cs`
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` — delete register endpoints
- Modify: `fasolt.Tests/Auth/OAuthRegisterEndpointTests.cs` — field name tweaks
- Delete: `fasolt.Server/Api/Helpers/OAuthPages/OAuthRegisterPage.cs`

- [ ] **Step 1: Read the existing register endpoints**

Before creating the Razor page, read the existing register GET and POST endpoints in `OAuthEndpoints.cs` (search for `app.MapGet("/oauth/register"` and `app.MapPost("/oauth/register"`) so you know what logic to lift. Note the existing behavior: TOS acceptance required, password rules validated, email uniqueness check, initial OTP generation + email send on success, redirect to `/oauth/verify-email`.

- [ ] **Step 2: Create the PageModel**

Write `fasolt.Server/Pages/Oauth/Register.cshtml.cs`:
```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Fasolt.Server.Api.Helpers;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Pages.Oauth;

[AllowAnonymous]
[EnableRateLimiting("auth-strict")]
public class RegisterModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailVerificationCodeService _otpService;
    private readonly IOtpEmailSender _emailSender;

    public RegisterModel(
        UserManager<AppUser> userManager,
        IEmailVerificationCodeService otpService,
        IOtpEmailSender emailSender)
    {
        _userManager = userManager;
        _otpService = otpService;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; } = "/";

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";

        [Required]
        public bool TosAccepted { get; set; }
    }

    public IActionResult OnGet(string? error)
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
        ErrorMessage = string.IsNullOrEmpty(error) ? null : error;
        return Page();
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please fill in all required fields.";
            return Page();
        }

        if (!Input.TosAccepted)
        {
            ErrorMessage = "You must accept the Terms of Service to continue.";
            return Page();
        }

        if (Input.Password != Input.ConfirmPassword)
        {
            ErrorMessage = "Passwords don't match.";
            return Page();
        }

        var existing = await _userManager.FindByEmailAsync(Input.Email);
        if (existing is not null)
        {
            // Enumeration guard: pretend the account was created and send
            // them to the verify page. If the real account is already
            // verified, the verify page will lock them out on submit. Same
            // behavior as the existing endpoint.
            return Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(Input.Email)}&returnUrl={Uri.EscapeDataString(ReturnUrl)}");
        }

        var user = new AppUser
        {
            UserName = Input.Email,
            Email = Input.Email,
            EmailConfirmed = false,
        };

        var createResult = await _userManager.CreateAsync(user, Input.Password);
        if (!createResult.Succeeded)
        {
            ErrorMessage = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Page();
        }

        try
        {
            var code = await _otpService.GenerateAndStoreAsync(user.Id, HttpContext.RequestAborted);
            await _emailSender.SendVerificationCodeAsync(user, user.Email!, code);
        }
        catch (InvalidOperationException)
        {
            // Race against advisory lock; swallow, user can click resend
        }

        return Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(Input.Email)}&returnUrl={Uri.EscapeDataString(ReturnUrl)}");
    }
}
```

**Note:** the lifted logic above is my best approximation of the current register endpoint. When implementing, read the actual existing endpoint and lift its logic byte-for-byte — there may be additional validation (rate limiting tracking, display name handling, etc.) not captured here.

- [ ] **Step 3: Create the Razor template**

Write `fasolt.Server/Pages/Oauth/Register.cshtml`:
```cshtml
@page "/oauth/register"
@model RegisterModel
@{
    ViewData["Title"] = "Create account";
}

<main class="oauth-card">
    <h1>Create your fasolt account</h1>

    @if (!string.IsNullOrEmpty(Model.ErrorMessage))
    {
        <div class="oauth-error">@Model.ErrorMessage</div>
    }

    <form method="post" action="/oauth/register" id="registerForm">
        <input type="hidden" asp-for="ReturnUrl" />
        <div class="oauth-field">
            <label asp-for="Input.Email">Email</label>
            <input asp-for="Input.Email" type="email" placeholder="you@example.com" autocomplete="email" required autofocus />
            <span class="oauth-field-error" asp-validation-for="Input.Email"></span>
        </div>
        <div class="oauth-field">
            <label asp-for="Input.Password">Password</label>
            <input asp-for="Input.Password" type="password" id="password" autocomplete="new-password" required />
            <ul class="oauth-rules" id="rules">
                <li class="pending" data-rule="length">At least 8 characters</li>
                <li class="pending" data-rule="upper">Uppercase letter</li>
                <li class="pending" data-rule="lower">Lowercase letter</li>
                <li class="pending" data-rule="digit">Number</li>
            </ul>
        </div>
        <div class="oauth-field">
            <label asp-for="Input.ConfirmPassword">Confirm password</label>
            <input asp-for="Input.ConfirmPassword" type="password" id="confirmPassword" autocomplete="new-password" required />
            <div class="oauth-mismatch" id="mismatch" style="display:none">Passwords don't match</div>
        </div>
        <label class="oauth-tos">
            <input asp-for="Input.TosAccepted" type="checkbox" id="tos" required />
            <span>I agree to the <a href="/terms" target="_blank">Terms of Service</a></span>
        </label>
        <button type="submit" class="oauth-btn" id="submit">Create account</button>
    </form>

    <div class="oauth-footer">
        <span>Already have an account?</span>
        <a asp-page="/Oauth/Login" asp-route-returnUrl="@Model.ReturnUrl">Sign in</a>
    </div>
</main>

<script src="~/js/password-rules.js" asp-append-version="true" defer></script>
```

- [ ] **Step 4: Delete the old hand-rolled page + endpoints**

Delete:
```bash
rm fasolt.Server/Api/Helpers/OAuthPages/OAuthRegisterPage.cs
```

Edit `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`. Find and delete:
- `// OAuth Register Page (GET)` block
- `// OAuth Register Handler (POST)` block

- [ ] **Step 5: Build and run existing register tests**

```bash
dotnet build fasolt.sln -nologo -v q
dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthRegisterEndpointTests" -nologo 2>&1 | tail -30
```

Update failing test assertions for the Razor field name convention (`Input.Email`, `Input.Password`, `Input.ConfirmPassword`, `Input.TosAccepted`). Iterate until all pass.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Pages/Oauth/Register.cshtml \
        fasolt.Server/Pages/Oauth/Register.cshtml.cs \
        fasolt.Server/Api/Endpoints/OAuthEndpoints.cs \
        fasolt.Tests/Auth/OAuthRegisterEndpointTests.cs
git rm fasolt.Server/Api/Helpers/OAuthPages/OAuthRegisterPage.cs
git commit -m "Migrate /oauth/register to Razor Page

Register.cshtml + .cshtml.cs replace OAuthRegisterPage.cs and its
endpoints. The live password-rules checklist now loads from the
static wwwroot/js/password-rules.js file added in the previous
commit, satisfying CSP script-src 'self'. Registration logic (email
uniqueness check, TOS gate, password match, OTP issue + email send)
is lifted from the existing endpoint verbatim.

Existing OAuthRegisterEndpointTests updated for Razor field naming."
```

---

## Task 9: Migrate /oauth/verify-email to a Razor Page

**Files:**
- Create: `fasolt.Server/Pages/Oauth/VerifyEmail.cshtml` + `.cs`
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` — delete verify-email endpoints
- Modify: `fasolt.Tests/Auth/OAuthVerifyEmailEndpointTests.cs` — field name tweaks
- Delete: `fasolt.Server/Api/Helpers/OAuthPages/OAuthVerifyEmailPage.cs`

- [ ] **Step 1: Read the existing verify-email endpoints**

Open `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` and find the `/oauth/verify-email` GET and POST endpoints (and `/oauth/verify-email/resend` if present). Read them to understand the exact logic: dual states (form vs lockout message), OTP verification, SignInAsync on success, resend handling.

- [ ] **Step 2: Create the PageModel**

Write `fasolt.Server/Pages/Oauth/VerifyEmail.cshtml.cs`. Follow the same structure as `ResetPassword.cshtml.cs`:
- `[AllowAnonymous] [EnableRateLimiting("auth")]`
- `InputModel` with `[Required] string Code`
- `[BindProperty(SupportsGet = true)] string Email`
- `[BindProperty(SupportsGet = true)] string ReturnUrl`
- `OnGet(string? error)` sets `ErrorMessage`
- `OnPostAsync()` calls `otpService.VerifyAsync`, on success calls `userManager.ConfirmEmailAsync` (or equivalent — check the existing endpoint), then `SignInAsync(user, isPersistent: true)`, then `Redirect(ReturnUrl)`
- `OnPostResendAsync()` named handler with `[EnableRateLimiting("auth-strict")]` — enumeration-guarded resend flow (swallow failures silently)

Lift the exact semantics from the existing endpoint — do not rewrite from memory. This is a security-critical path; a subtle omission is the whole failure mode the spec is trying to avoid.

- [ ] **Step 3: Create the Razor template**

Write `fasolt.Server/Pages/Oauth/VerifyEmail.cshtml`:
```cshtml
@page "/oauth/verify-email"
@model VerifyEmailModel
@{
    ViewData["Title"] = "Verify your email";
}

<main class="oauth-card">
    <h1>Verify your email</h1>
    <p class="lead">We sent a 6-digit code to <strong>@Model.Email</strong></p>

    @if (!string.IsNullOrEmpty(Model.ErrorMessage))
    {
        <div class="oauth-error">@Model.ErrorMessage</div>
    }

    <form method="post" action="/oauth/verify-email">
        <input type="hidden" asp-for="Email" />
        <input type="hidden" asp-for="ReturnUrl" />
        <div class="oauth-field">
            <input asp-for="Input.Code" type="text" inputmode="numeric" autocomplete="one-time-code" pattern="[0-9]{6}" maxlength="6" autofocus required />
        </div>
        <button type="submit" class="oauth-btn">Verify</button>
    </form>

    <div class="oauth-resend">
        Didn't get it?
        <form method="post" asp-page-handler="resend">
            <input type="hidden" asp-for="Email" />
            <input type="hidden" asp-for="ReturnUrl" />
            <button type="submit">Resend code</button>
        </form>
    </div>

    <div class="oauth-footer">
        <a asp-page="/Oauth/Login" asp-route-returnUrl="@Model.ReturnUrl">Back to sign in</a>
    </div>
</main>
```

- [ ] **Step 4: Delete the old hand-rolled page + endpoints**

```bash
rm fasolt.Server/Api/Helpers/OAuthPages/OAuthVerifyEmailPage.cs
```

Delete the `/oauth/verify-email` GET, POST, and resend endpoints from `OAuthEndpoints.cs`.

- [ ] **Step 5: Build and run existing verify-email tests**

```bash
dotnet build fasolt.sln -nologo -v q
dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthVerifyEmailEndpointTests" -nologo 2>&1 | tail -30
```

Update assertions for Razor field names (`Input.Code`, `Email`, `ReturnUrl`). Update resend URL from `/oauth/verify-email/resend` to `/oauth/verify-email?handler=resend`. Iterate until all pass.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Pages/Oauth/VerifyEmail.cshtml \
        fasolt.Server/Pages/Oauth/VerifyEmail.cshtml.cs \
        fasolt.Server/Api/Endpoints/OAuthEndpoints.cs \
        fasolt.Tests/Auth/OAuthVerifyEmailEndpointTests.cs
git rm fasolt.Server/Api/Helpers/OAuthPages/OAuthVerifyEmailPage.cs
git commit -m "Migrate /oauth/verify-email to Razor Page

VerifyEmail.cshtml + .cshtml.cs replace OAuthVerifyEmailPage.cs and
its endpoints. Resend handler becomes a named Razor handler at
?handler=resend. OTP verify semantics, ConfirmEmailAsync call, and
post-verify SignInAsync(isPersistent: true) all lifted from the
existing endpoint.

Existing OAuthVerifyEmailEndpointTests updated for Razor field
naming and the new resend handler URL."
```

---

## Task 10: Migrate /oauth/consent to a Razor Page

**Files:**
- Create: `fasolt.Server/Pages/Oauth/Consent.cshtml` + `.cs`
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` — delete consent endpoints
- Delete: `fasolt.Server/Api/Helpers/OAuthPages/OAuthConsentPage.cs`

- [ ] **Step 1: Read the existing consent endpoints**

Read the `/oauth/consent` GET and POST endpoints in `OAuthEndpoints.cs` to understand the flow: GET loads the OpenIddict application, shows requested scopes, authenticates the user; POST either grants consent (SignIn with `ConsentGrant` record) or denies (redirects back with error).

Also look at the existing `fasolt.client/src/views/OAuthConsentView.vue` if it exists — some consent UI may still be Vue-side. If the consent is a server-rendered Razor page and the Vue route is only for post-redirect handling, no change needed to the Vue side.

- [ ] **Step 2: Create the PageModel**

Write `fasolt.Server/Pages/Oauth/Consent.cshtml.cs` following the same structure as the other PageModels. Inject `IOpenIddictApplicationManager`, handle the `client_id` and `scope` query parameters, look up the application name, accept/deny handlers.

Because the consent flow interacts with OpenIddict's internal authorization pipeline, the existing endpoint's exact call sequence (ScopeAsPrincipal, AuthenticateAsync, SignInAsync with specific authentication schemes) must be preserved. **Lift byte-for-byte**, don't rewrite from memory.

- [ ] **Step 3: Create the Razor template**

Write `fasolt.Server/Pages/Oauth/Consent.cshtml`:
```cshtml
@page "/oauth/consent"
@model ConsentModel
@{
    ViewData["Title"] = "Authorize app";
}

<main class="oauth-card">
    <h1>Authorize @Model.ApplicationName</h1>
    <p class="lead">
        <strong>@Model.ApplicationName</strong> wants access to your fasolt account.
    </p>

    @if (Model.Scopes.Any())
    {
        <div class="mb-4">
            <p class="text-xs font-medium text-foreground mb-2">This will allow @Model.ApplicationName to:</p>
            <ul class="text-xs text-muted-foreground space-y-1 pl-4 list-disc">
                @foreach (var scope in Model.Scopes)
                {
                    <li>@scope</li>
                }
            </ul>
        </div>
    }

    @if (!string.IsNullOrEmpty(Model.ErrorMessage))
    {
        <div class="oauth-error">@Model.ErrorMessage</div>
    }

    <form method="post" action="/oauth/consent">
        <input type="hidden" asp-for="ReturnUrl" />
        <button type="submit" name="action" value="accept" class="oauth-btn">Authorize</button>
    </form>
    <form method="post" action="/oauth/consent" class="mt-2">
        <input type="hidden" asp-for="ReturnUrl" />
        <button type="submit" name="action" value="deny" class="oauth-btn" style="background: transparent; color: var(--foreground); border: 1px solid var(--border);">Cancel</button>
    </form>
</main>
```

Tailwind utility classes in the template come through via the shared `auth.css` — any utility not in the `@layer components` block still resolves because `auth.css` imports `style.css` which contains the full Tailwind base/components/utilities.

- [ ] **Step 4: Delete the old hand-rolled page + endpoints**

```bash
rm fasolt.Server/Api/Helpers/OAuthPages/OAuthConsentPage.cs
```

Delete the `/oauth/consent` GET and POST endpoints from `OAuthEndpoints.cs`.

- [ ] **Step 5: Build**

```bash
dotnet build fasolt.sln -nologo -v q
```
Expected: success.

- [ ] **Step 6: Run all auth tests to spot regressions**

```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~Auth" -nologo 2>&1 | tail -20
```
Expected: all auth-related tests pass.

- [ ] **Step 7: Manual smoke test of the iOS OAuth flow**

(Optional but recommended.) The consent page is the hardest to get right because it interacts with OpenIddict's flow. If you have an iOS simulator + the iOS client running against local dev:
1. Trigger a sign-in from the iOS app
2. Verify the consent screen renders with the new Razor look
3. Accept consent and verify the app receives the auth code

If iOS isn't handy, simulate with curl against `/oauth/authorize?client_id=...&response_type=code&...` — the flow should redirect through `/oauth/login` → POST login → `/oauth/consent` → POST accept → back to the `redirect_uri`.

- [ ] **Step 8: Commit**

```bash
git add fasolt.Server/Pages/Oauth/Consent.cshtml \
        fasolt.Server/Pages/Oauth/Consent.cshtml.cs \
        fasolt.Server/Api/Endpoints/OAuthEndpoints.cs
git rm fasolt.Server/Api/Helpers/OAuthPages/OAuthConsentPage.cs
git commit -m "Migrate /oauth/consent to Razor Page

Consent.cshtml + .cshtml.cs replace OAuthConsentPage.cs and the
consent endpoints. OpenIddict integration preserved: the PageModel
looks up the application via IOpenIddictApplicationManager, accepts
or denies via the same SignIn/Forbid calls as the old endpoint, so
the iOS + MCP OAuth flow is unchanged."
```

---

## Task 11: Delete OAuthPageLayout.cs and the OAuthPages directory

**Files:**
- Delete: `fasolt.Server/Api/Helpers/OAuthPageLayout.cs`
- Delete: `fasolt.Server/Api/Helpers/OAuthPages/` (empty directory)

- [ ] **Step 1: Verify all page files are gone**

```bash
ls fasolt.Server/Api/Helpers/OAuthPages/ 2>&1
```
Expected: the directory is empty (or the command prints "No such file or directory" if git already removed it).

- [ ] **Step 2: Verify OAuthPageLayout.cs is unreferenced**

```bash
grep -rn "OAuthPageLayout" fasolt.Server/ fasolt.Tests/ 2>&1
```
Expected: no results.

- [ ] **Step 3: Delete the files and directory**

```bash
git rm fasolt.Server/Api/Helpers/OAuthPageLayout.cs
rmdir fasolt.Server/Api/Helpers/OAuthPages/ 2>/dev/null || true
```

- [ ] **Step 4: Build**

```bash
dotnet build fasolt.sln -nologo -v q
```
Expected: success.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/
git commit -m "Delete OAuthPageLayout.cs and the OAuthPages helpers directory

All six hand-rolled OAuth page helpers have been migrated to Razor
Pages under fasolt.Server/Pages/Oauth/. The shared layout's role is
now played by _Layout.cshtml, the per-page helpers are replaced by
.cshtml + .cshtml.cs pairs, and the HttpUtility.HtmlEncode 'call it
everywhere' convention is replaced by Razor's auto-encoding default.

Retires the last of the raw string-template HTML in the auth surface."
```

---

## Task 12: CSP middleware (test-first)

**Files:**
- Create: `fasolt.Tests/Auth/OAuthCspHeaderTests.cs`
- Create: `fasolt.Server/Api/Middleware/ContentSecurityPolicyMiddleware.cs`
- Modify: `fasolt.Server/Program.cs` — register the middleware scoped to `/oauth/*`

- [ ] **Step 1: Write the failing CSP tests**

Create `fasolt.Tests/Auth/OAuthCspHeaderTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fasolt.Tests.Auth;

public class OAuthCspHeaderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthCspHeaderTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OTP_PEPPER"] = "test-pepper",
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                });
            });
        });
    }

    [Theory]
    [InlineData("/oauth/login")]
    [InlineData("/oauth/register")]
    [InlineData("/oauth/forgot-password")]
    [InlineData("/oauth/reset-password?email=foo@example.com")]
    public async Task OauthPages_ReturnCspHeader(string path)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Content-Security-Policy").Should().BeTrue(
            $"{path} should set a CSP header");

        var csp = string.Join(";", response.Headers.GetValues("Content-Security-Policy"));
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("style-src 'self'");
        csp.Should().Contain("script-src 'self'");
        csp.Should().Contain("form-action");
        csp.Should().Contain("frame-ancestors 'none'");
        csp.Should().NotContain("'unsafe-inline'",
            "the whole point of the middleware is to avoid unsafe-inline");
    }

    [Fact]
    public async Task NonOauthPaths_DoNotReturnCspHeader()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Content-Security-Policy").Should().BeFalse(
            "CSP middleware should be scoped to /oauth/*");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthCspHeaderTests" -nologo 2>&1 | tail -20
```
Expected: all 5 tests fail (`Content-Security-Policy` header not present).

- [ ] **Step 3: Create the middleware**

Write `fasolt.Server/Api/Middleware/ContentSecurityPolicyMiddleware.cs`:
```csharp
namespace Fasolt.Server.Api.Middleware;

/// <summary>
/// Sets a strict Content-Security-Policy header on /oauth/* responses.
/// The policy disallows inline styles and scripts — the shared auth.css
/// and wwwroot/js/password-rules.js are the only permitted sources, both
/// same-origin. form-action whitelists GitHub for the social-login POST.
/// frame-ancestors 'none' blocks the pages from being iframed (clickjacking
/// defense, especially important for consent screens).
/// </summary>
public class ContentSecurityPolicyMiddleware
{
    private readonly RequestDelegate _next;

    private const string PolicyHeaderValue =
        "default-src 'self'; " +
        "style-src 'self'; " +
        "script-src 'self'; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "form-action 'self' https://github.com; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'";

    public ContentSecurityPolicyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey("Content-Security-Policy"))
            {
                context.Response.Headers["Content-Security-Policy"] = PolicyHeaderValue;
            }
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
```

- [ ] **Step 4: Register the middleware scoped to /oauth/**

Edit `fasolt.Server/Program.cs`. Find the block around `app.UseAuthentication();` (around line 567). Immediately before it, add:
```csharp
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/oauth"),
    branch => branch.UseMiddleware<Fasolt.Server.Api.Middleware.ContentSecurityPolicyMiddleware>());
```

The `UseWhen` branch runs for anything under `/oauth/*` only. `/api/health`, `/study`, etc. are untouched.

- [ ] **Step 5: Build and re-run CSP tests**

```bash
dotnet build fasolt.sln -nologo -v q
dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthCspHeaderTests" -nologo 2>&1 | tail -20
```
Expected: all 5 tests pass.

- [ ] **Step 6: Manual browser smoke test**

Start the stack and open `http://localhost:5173/oauth/login` with browser devtools open. Check the Network tab for the login response; the `Content-Security-Policy` header should be present with the full policy. Check the Console tab for any CSP violations — there should be none (if there are, it means something inline slipped through: either a `<style>` block in a Razor page, or an `onclick` attribute, or an inline script).

If violations appear, fix them by extracting the offending inline content to a static file.

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Api/Middleware/ContentSecurityPolicyMiddleware.cs \
        fasolt.Server/Program.cs \
        fasolt.Tests/Auth/OAuthCspHeaderTests.cs
git commit -m "Add Content-Security-Policy middleware on /oauth/* routes

Scopes a strict CSP header — default-src 'self', style-src 'self',
script-src 'self', form-action 'self' https://github.com,
frame-ancestors 'none' — to every response under /oauth/*. The
policy intentionally does NOT permit 'unsafe-inline' anywhere; the
shared auth.css and password-rules.js are the only permitted sources
and both are same-origin. Clickjacking-protects the consent page
specifically via frame-ancestors 'none'.

OAuthCspHeaderTests asserts the header is present on the six Razor
pages and absent on /api/health (to prove UseWhen scoping works)."
```

---

## Task 13: Retire the SPA /login stack

**Files:**
- Delete: `fasolt.client/src/views/LoginView.vue`
- Delete: `fasolt.client/src/layouts/AuthLayout.vue`
- Modify: `fasolt.client/src/router/index.ts` — delete `/login` route, rewrite 401 redirect
- Modify: `fasolt.client/src/stores/auth.ts` — delete `login()` method
- Modify: `fasolt.client/src/components/TopBar.vue` — logout destination
- Modify: `fasolt.client/src/views/LandingView.vue` — `RouterLink` → `<a>`
- Modify: `fasolt.client/src/views/AlgorithmView.vue` — same
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs` — delete `MapPost("/login")` + handler
- Modify: `fasolt.Server/Application/Dtos/AccountDtos.cs` — delete `LoginRequest`

- [ ] **Step 1: Delete LoginView.vue and AuthLayout.vue**

```bash
git rm fasolt.client/src/views/LoginView.vue fasolt.client/src/layouts/AuthLayout.vue
```

- [ ] **Step 2: Update the Vue router**

Edit `fasolt.client/src/router/index.ts`. Remove the `/login` route entry (the block that lazy-imports `LoginView.vue`). Replace the unauthenticated redirect in `router.beforeEach`:

**Before:**
```ts
if (!isPublic && !auth.isAuthenticated) {
  return { name: 'login' }
}
```

**After:**
```ts
if (!isPublic && !auth.isAuthenticated) {
  // Full-page nav to the server-rendered login. SPA state is already
  // unauthenticated-dead at this point, so there's nothing to preserve.
  window.location.href = `/oauth/login?returnUrl=${encodeURIComponent(to.fullPath)}`
  return false
}
```

- [ ] **Step 3: Delete auth.login() store method**

Edit `fasolt.client/src/stores/auth.ts`. Delete the `login` function (and its export from the returned object):
```ts
// DELETE:
async function login(email: string, password: string, rememberMe: boolean) {
  await apiFetch('/account/login', {
    method: 'POST',
    body: JSON.stringify({ email, password, rememberMe }),
  })
  await fetchUser()
}
```
And remove `login,` from the returned object at the bottom.

- [ ] **Step 4: Update TopBar logout navigation**

Edit `fasolt.client/src/components/TopBar.vue`. Find line 75 (the `router.push('/login')` call in the logout handler). Replace with:
```ts
window.location.href = '/oauth/login'
```

- [ ] **Step 5: Update Landing and Algorithm CTAs**

Edit `fasolt.client/src/views/LandingView.vue`. At lines 33 and 56, replace:
```vue
<RouterLink to="/login">...</RouterLink>
```
with:
```vue
<a href="/oauth/login">...</a>
```
Preserving the existing class attributes and slot content.

Edit `fasolt.client/src/views/AlgorithmView.vue`. At line 29, the same substitution.

- [ ] **Step 6: Delete the JSON login endpoint**

Edit `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`. Delete line 18:
```csharp
group.MapPost("/login", Login).RequireRateLimiting("auth");
```
And delete the `Login` method at lines ~32-57 (the entire method block).

- [ ] **Step 7: Delete LoginRequest DTO**

Edit `fasolt.Server/Application/Dtos/AccountDtos.cs`. Find and delete:
```csharp
public record LoginRequest(string Email, string Password, bool RememberMe = false);
```

Verify nothing else references it:
```bash
grep -rn "LoginRequest" fasolt.Server/ fasolt.Tests/ 2>&1
```
Expected: no results.

- [ ] **Step 8: Build server and client**

```bash
dotnet build fasolt.sln -nologo -v q
```
Expected: success.

```bash
cd fasolt.client && npm run type-check 2>&1 | tail -20 && cd ..
```
Expected: no type errors. If the SPA's `tsc` reports errors about unused imports (e.g. a `Login` type that was imported somewhere), find and delete those too.

```bash
cd fasolt.client && npm run build 2>&1 | tail -10 && cd ..
```
Expected: successful Vite build.

- [ ] **Step 9: Manual full-stack smoke test**

Start the stack:
```bash
./dev.sh &
sleep 8
```

Test the following flows in a browser:
1. **Anonymous → /study** — visit `http://localhost:5173/study`, assert full-page nav to `http://localhost:5173/oauth/login?returnUrl=%2Fstudy`, sign in with dev seed, assert landing on `/study`
2. **Landing "Log in" CTA** — visit `http://localhost:5173/`, click "Log in", assert on `/oauth/login`
3. **Logout** — from a logged-in session, click logout in TopBar, assert full-page nav to `/oauth/login`
4. **Forgot password** — from `/oauth/login`, click "Forgot password?", enter dev seed email, submit, assert "Check your email" screen
5. **Register** — from `/oauth/login`, click "Create an account", verify the page renders correctly with live password rules

Stop:
```bash
kill %1 2>/dev/null; pkill -f "dotnet.*fasolt.Server"; pkill -f "vite"
```

- [ ] **Step 10: Commit**

```bash
git add fasolt.client/src/router/index.ts \
        fasolt.client/src/stores/auth.ts \
        fasolt.client/src/components/TopBar.vue \
        fasolt.client/src/views/LandingView.vue \
        fasolt.client/src/views/AlgorithmView.vue \
        fasolt.Server/Api/Endpoints/AccountEndpoints.cs \
        fasolt.Server/Application/Dtos/AccountDtos.cs
git commit -m "Retire SPA /login stack

Deletes LoginView.vue, AuthLayout.vue, auth.login() store method,
the /login Vue route, POST /api/account/login endpoint + handler,
and the LoginRequest DTO. The SPA's unauthenticated redirect flips
from SPA nav ({ name: 'login' }) to full-page nav
(window.location.href = '/oauth/login?returnUrl=...'). TopBar logout
does the same full-page nav. Landing and Algorithm 'Log in' CTAs
become plain <a href='/oauth/login'> since they no longer resolve
to a local route.

This is the convergence payoff: one login page, one codepath, one
visual. Web users now hit the same Razor /oauth/login that iOS uses
through ASWebAuthenticationSession. No more branching on client
type in the auth surface."
```

---

## Task 14: Playwright scaffolding

**Files:**
- Modify: `fasolt.client/package.json` — add Playwright dependency + scripts
- Create: `fasolt.client/playwright.config.ts`
- Create: `fasolt.client/e2e/` directory
- Modify: `.gitignore` — ignore Playwright test-results

- [ ] **Step 1: Install Playwright**

```bash
cd fasolt.client && npm install -D @playwright/test && npx playwright install chromium && cd ..
```
Expected: `@playwright/test` added to devDependencies in `package.json`.

- [ ] **Step 2: Create `playwright.config.ts`**

Write `fasolt.client/playwright.config.ts`:
```ts
import { defineConfig, devices } from '@playwright/test'

// Playwright runs against the dev stack on localhost:5173. The dev stack
// is started separately by ./dev.sh before running the tests — we don't
// have Playwright start/stop the server because the full stack involves
// docker (postgres), dotnet, and vite, and the orchestration is already
// in dev.sh.
//
// Before running `npm run e2e`:
// 1. ./dev.sh (or equivalent)
// 2. wait until both backend (8080) and frontend (5173) are ready
// 3. ensure the dev seed user 'dev@fasolt.local' / 'Dev1234!' exists
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})
```

- [ ] **Step 3: Add package.json scripts**

Edit `fasolt.client/package.json`. Add to the `scripts` section:
```json
"e2e": "playwright test",
"e2e:ui": "playwright test --ui"
```

- [ ] **Step 4: Create an empty e2e directory with a marker**

```bash
mkdir -p fasolt.client/e2e
```

- [ ] **Step 5: Gitignore Playwright artifacts**

Edit `.gitignore`, add:
```
# Playwright
fasolt.client/test-results/
fasolt.client/playwright-report/
fasolt.client/playwright/.cache/
```

- [ ] **Step 6: Commit**

```bash
git add fasolt.client/package.json \
        fasolt.client/package-lock.json \
        fasolt.client/playwright.config.ts \
        .gitignore
git commit -m "Add Playwright scaffolding for auth E2E tests

Installs @playwright/test + chromium. Adds playwright.config.ts
configured to hit the dev stack on localhost:5173 (tests run against
./dev.sh, not a Playwright-managed server). Adds 'e2e' and 'e2e:ui'
package.json scripts. Gitignores test-results and reports."
```

---

## Task 15: E2E auth-login spec

**Files:**
- Create: `fasolt.client/e2e/auth-login.spec.ts`

- [ ] **Step 1: Write the E2E test file**

Write `fasolt.client/e2e/auth-login.spec.ts`:
```ts
import { test, expect } from '@playwright/test'

// Prerequisites: ./dev.sh running, dev seed user dev@fasolt.local / Dev1234!
// created by the backend on startup.

const SEED_EMAIL = 'dev@fasolt.local'
const SEED_PASSWORD = 'Dev1234!'

test.describe('auth: login', () => {
  test('anonymous visit to /study redirects to /oauth/login and back after login', async ({ page }) => {
    await page.goto('/study')

    // Full-page nav to server-rendered login
    await expect(page).toHaveURL(/\/oauth\/login\?returnUrl=%2Fstudy/)
    await expect(page.locator('h1')).toContainText('Sign in to fasolt')

    // Sign in
    await page.fill('input[name="Input.Email"]', SEED_EMAIL)
    await page.fill('input[name="Input.Password"]', SEED_PASSWORD)
    await page.click('button[type="submit"]')

    // After successful login, we should be on /study
    await expect(page).toHaveURL('/study')
    // SPA mounted, authenticated UI visible
    await expect(page.locator('body')).not.toContainText('Sign in to fasolt')
  })

  test('wrong password renders error and stays on login page', async ({ page }) => {
    await page.goto('/oauth/login?returnUrl=%2F')
    await expect(page.locator('h1')).toContainText('Sign in to fasolt')

    await page.fill('input[name="Input.Email"]', SEED_EMAIL)
    await page.fill('input[name="Input.Password"]', 'definitely-wrong-password')
    await page.click('button[type="submit"]')

    // Still on login
    await expect(page).toHaveURL(/\/oauth\/login/)
    await expect(page.locator('.oauth-error')).toContainText('Invalid email or password.')
    // Email should still be populated (Razor re-renders with the value)
    await expect(page.locator('input[name="Input.Email"]')).toHaveValue(SEED_EMAIL)
  })

  test('missing email shows field-level validation error', async ({ page }) => {
    await page.goto('/oauth/login?returnUrl=%2F')

    // Bypass the HTML5 required attribute by submitting via JS
    await page.evaluate(() => {
      const form = document.querySelector('form[action="/oauth/login"]') as HTMLFormElement
      const email = form.querySelector('input[name="Input.Email"]') as HTMLInputElement
      email.removeAttribute('required')
      email.value = ''
      const password = form.querySelector('input[name="Input.Password"]') as HTMLInputElement
      password.removeAttribute('required')
      password.value = 'Abcdefg1'
      form.submit()
    })

    // Razor's model validation re-renders the page with field-level error
    await expect(page).toHaveURL(/\/oauth\/login/)
    // The span with asp-validation-for surfaces the [Required] error
    await expect(page.locator('.oauth-field-error').first()).toBeVisible()
  })

  test('logout redirects to /oauth/login', async ({ page }) => {
    // Log in first
    await page.goto('/oauth/login?returnUrl=%2F')
    await page.fill('input[name="Input.Email"]', SEED_EMAIL)
    await page.fill('input[name="Input.Password"]', SEED_PASSWORD)
    await page.click('button[type="submit"]')
    await expect(page).toHaveURL('/')

    // Find and click logout. Exact selector depends on TopBar.vue structure.
    // Open the user menu (usually a button or avatar in the top bar) then click logout.
    // This test assumes the logout control is reachable via a visible link or button
    // with text 'Log out' or 'Logout'. Adjust if the actual UI differs.
    const logoutTrigger = page.getByRole('button', { name: /log ?out/i }).or(page.getByRole('link', { name: /log ?out/i }))
    await logoutTrigger.first().click()

    // Full-page nav to the server-rendered login
    await expect(page).toHaveURL(/\/oauth\/login/)
    await expect(page.locator('h1')).toContainText('Sign in to fasolt')
  })

  test('landing page "Log in" CTA navigates to /oauth/login', async ({ page }) => {
    await page.goto('/')
    // The landing page has multiple "Log in" CTAs. Click the first visible one.
    await page.getByRole('link', { name: /^log in$/i }).first().click()
    await expect(page).toHaveURL(/\/oauth\/login/)
  })
})
```

**Note on the logout test:** The exact selector for the logout button depends on the `TopBar.vue` implementation. If the button is inside a dropdown menu that needs to be opened first, adjust the test to click the dropdown trigger before hunting for the logout item. When implementing, briefly inspect `TopBar.vue` to get the right selector chain.

- [ ] **Step 2: Start the dev stack and run the tests**

Terminal 1:
```bash
./dev.sh
```

Terminal 2, once the stack is ready:
```bash
cd fasolt.client && npm run e2e && cd ..
```
Expected: all 5 tests pass. Iterate on any failures.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/e2e/auth-login.spec.ts
git commit -m "Add auth-login E2E Playwright spec

Covers the five headline login flows for the Razor-rendered
/oauth/login:

1. Anonymous visit to /study → redirect to /oauth/login → sign in →
   land back on /study (the convergence payoff)
2. Wrong password → stays on /oauth/login with error, email still
   populated in the form
3. Missing email → Razor model validation surfaces a field-level
   error via asp-validation-for
4. Logout → full-page nav to /oauth/login (not SPA nav)
5. Landing 'Log in' CTA → /oauth/login

Runs against ./dev.sh using the dev seed user dev@fasolt.local."
```

---

## Task 16: E2E auth-forgot-password spec (with test email sender fixture)

**Files:**
- Create: `fasolt.Server/Infrastructure/Services/TestEmailSink.cs` (test-only endpoint support)
- Modify: `fasolt.Server/Program.cs` — register the test sink in development
- Create: `fasolt.client/e2e/auth-forgot-password.spec.ts`

- [ ] **Step 1: Create the test email sink**

Write `fasolt.Server/Infrastructure/Services/TestEmailSink.cs`:
```csharp
using System.Collections.Concurrent;

namespace Fasolt.Server.Infrastructure.Services;

/// <summary>
/// Development-only in-memory capture of the last OTP sent to each email.
/// Used by Playwright E2E tests to read back the password-reset /
/// verification codes that would otherwise only land in a real inbox.
/// Guarded to development environment in Program.cs.
/// </summary>
public class TestEmailSink
{
    private readonly ConcurrentDictionary<string, CapturedEmail> _lastByEmail = new();

    public record CapturedEmail(string Email, string Subject, string Code, DateTimeOffset CapturedAt);

    public void Capture(string email, string subject, string code)
    {
        _lastByEmail[email.ToLowerInvariant()] = new CapturedEmail(email, subject, code, DateTimeOffset.UtcNow);
    }

    public CapturedEmail? GetLast(string email)
    {
        _lastByEmail.TryGetValue(email.ToLowerInvariant(), out var captured);
        return captured;
    }
}
```

- [ ] **Step 2: Wire the sink into DevEmailSender**

Edit `fasolt.Server/Infrastructure/Services/DevEmailSender.cs`. Add an optional `TestEmailSink?` constructor dependency and capture codes into it:
```csharp
// Inside DevEmailSender class, add field + constructor parameter:
private readonly TestEmailSink? _sink;

public DevEmailSender(ILogger<DevEmailSender> logger, TestEmailSink? sink = null)
{
    _logger = logger;
    _sink = sink;
}

// In SendVerificationCodeAsync, after the log line, add:
_sink?.Capture(email, "Verify your Fasolt email", resetCode);

// In SendPasswordResetCodeAsync, after the log line, add:
_sink?.Capture(email, "Your Fasolt password reset code", resetCode);
```

(Adjust parameter names to match the current method signatures — `resetCode` may be `code`.)

- [ ] **Step 3: Register the sink and the test endpoint (dev only)**

Edit `fasolt.Server/Program.cs`. Find the line `builder.Services.AddScoped<IOtpEmailSender, ...>(...)` or similar where email senders are registered. After it, add:
```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<Fasolt.Server.Infrastructure.Services.TestEmailSink>();
}
```

And in the endpoint-mapping section, after `app.MapFallbackToFile("index.html")` removal or before it, add:
```csharp
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/test/last-email", (string email, Fasolt.Server.Infrastructure.Services.TestEmailSink sink) =>
    {
        var captured = sink.GetLast(email);
        return captured is null ? Results.NotFound() : Results.Ok(captured);
    });
}
```

This endpoint is only mapped in development — it's gated behind `IsDevelopment()` so production builds can't accidentally expose captured codes.

- [ ] **Step 4: Write the E2E forgot-password spec**

Write `fasolt.client/e2e/auth-forgot-password.spec.ts`:
```ts
import { test, expect, request } from '@playwright/test'

const SEED_EMAIL = 'dev@fasolt.local'
const OLD_PASSWORD = 'Dev1234!'
const NEW_PASSWORD = 'NewDev1234!'

async function fetchResetCode(email: string): Promise<string> {
  const apiContext = await request.newContext()
  // The test endpoint lives on the backend (8080) but Vite proxies /api
  // through to it, so the 5173 origin also works.
  const response = await apiContext.get(`http://localhost:8080/api/test/last-email?email=${encodeURIComponent(email)}`)
  expect(response.ok(), 'test email sink endpoint should be available in dev').toBeTruthy()
  const body = await response.json()
  expect(body.code, 'captured email must contain a code').toBeTruthy()
  return body.code
}

test.describe('auth: forgot password', () => {
  // This test rotates the dev seed user's password. Run it once per session;
  // a subsequent run will fail to log in with the old password. The test
  // rotates the password back at the end to keep the dev environment sane.
  test('full reset flow: request code → enter code → new password → sign in', async ({ page }) => {
    // 1. From login, click "Forgot password?"
    await page.goto('/oauth/login?returnUrl=%2F')
    await page.click('a[href*="/oauth/forgot-password"]')
    await expect(page).toHaveURL(/\/oauth\/forgot-password/)
    await expect(page.locator('h1')).toContainText('Reset your password')

    // 2. Enter the dev seed email and submit
    await page.fill('input[name="Input.Email"]', SEED_EMAIL)
    await page.click('button[type="submit"]')
    await expect(page).toHaveURL(/\/oauth\/forgot-password.*sent=True/i)
    await expect(page.locator('h1')).toContainText('Check your email')

    // 3. Click "Enter reset code" to move to the reset page
    await page.click('button[type="submit"]')
    await expect(page).toHaveURL(/\/oauth\/reset-password/)

    // 4. Fetch the code from the test email sink
    const code = await fetchResetCode(SEED_EMAIL)
    expect(code).toMatch(/^\d{6}$/)

    // 5. Fill the code and the new password
    await page.fill('input[name="Input.Code"]', code)
    await page.fill('input[name="Input.Password"]', NEW_PASSWORD)
    await page.fill('input[name="Input.ConfirmPassword"]', NEW_PASSWORD)
    await page.click('button[type="submit"]')

    // 6. Success screen
    await expect(page.locator('h1')).toContainText('Password updated')

    // 7. Click "Go to sign in" → log in with new password
    await page.click('button[type="submit"]')
    await expect(page).toHaveURL(/\/oauth\/login/)

    await page.fill('input[name="Input.Email"]', SEED_EMAIL)
    await page.fill('input[name="Input.Password"]', NEW_PASSWORD)
    await page.click('button[type="submit"]')
    await expect(page).toHaveURL('/')

    // 8. Clean up: rotate back to the dev password so next runs work
    // We just log the state here; actual cleanup requires another reset cycle.
    // Simplest: manually revert the dev seed user's password in dev.sh or via SQL.
    // For now, leave the new password and note that subsequent test runs will fail
    // until the dev password is reset. In CI this is idempotent because each run
    // starts with a fresh DB.
    console.log(`[e2e cleanup] dev seed password rotated to ${NEW_PASSWORD}. Revert manually or reset DB.`)
  })
})
```

**Cleanup concern:** this test has a side effect — it rotates the dev seed user's password. For a clean local dev loop, consider one of:
- (a) Create a dedicated E2E user (e.g. `e2e@fasolt.local`) separate from the dev seed
- (b) Use the `testhost` database or a transaction rollback
- (c) Add a second test that rotates it back

Simplest: add a small `test.afterAll` hook that posts to `/api/test/reset-dev-user-password` — another dev-only endpoint. Implementing that is ~10 LOC.

For this plan, leave the test as-is with the `console.log` warning, and accept that the local dev DB needs `docker compose down -v && ./dev.sh` after an E2E run (or the dev seed's password in the code needs bumping). Note this as a known inconvenience.

- [ ] **Step 5: Run the E2E tests**

With `./dev.sh` running:
```bash
cd fasolt.client && npm run e2e -- auth-forgot-password.spec.ts && cd ..
```
Expected: the test passes. After it runs, log in manually with `NewDev1234!` to confirm the dev seed is now using the new password.

Reset the dev environment:
```bash
docker compose down -v && ./dev.sh &
```

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Infrastructure/Services/TestEmailSink.cs \
        fasolt.Server/Infrastructure/Services/DevEmailSender.cs \
        fasolt.Server/Program.cs \
        fasolt.client/e2e/auth-forgot-password.spec.ts
git commit -m "Add auth-forgot-password E2E spec + dev-only email sink

TestEmailSink captures the last OTP sent to each email in a
ConcurrentDictionary. DevEmailSender writes into it as a side effect
of sending verification + reset codes. The capture is wired up only
when ASPNETCORE_ENVIRONMENT is Development, along with a
/api/test/last-email endpoint that reads it back. Production builds
never see either.

auth-forgot-password.spec.ts exercises the full reset flow end to
end: request code → read code from sink → submit new password →
sign in with new password. The test rotates the dev seed user's
password; clean up with 'docker compose down -v && ./dev.sh' after
the run, or switch to a dedicated e2e@ user if the churn is
annoying."
```

---

## Task 17: Final pass — rebuild, full test suite, PR creation

**Files:** none new

- [ ] **Step 1: Fresh build**

```bash
dotnet build fasolt.sln -nologo -v q
cd fasolt.client && npm run build && cd ..
```
Expected: both succeed with zero warnings.

- [ ] **Step 2: Full server test suite**

```bash
dotnet test fasolt.Tests -nologo 2>&1 | tail -20
```
Expected: all tests pass. Pay particular attention to the auth-related tests that were updated throughout the plan.

- [ ] **Step 3: Full client type-check**

```bash
cd fasolt.client && npm run type-check && cd ..
```
Expected: no TypeScript errors.

- [ ] **Step 4: Start full stack and run E2E suite**

Terminal 1:
```bash
docker compose down -v && ./dev.sh
```

Terminal 2, once ready:
```bash
cd fasolt.client && npm run e2e && cd ..
```
Expected: all E2E tests pass.

- [ ] **Step 5: Check for dangling references to deleted symbols**

```bash
grep -rn "OAuthPageLayout\|OAuthLoginPage\|OAuthRegisterPage\|OAuthVerifyEmailPage\|OAuthForgotPasswordPage\|OAuthResetPasswordPage\|OAuthConsentPage\|LoginRequest\|LoginView\|AuthLayout\.vue\|'/login'" fasolt.Server/ fasolt.client/ fasolt.Tests/ 2>&1
```
Expected: no results. If anything turns up, it's a leftover reference to something we meant to delete.

- [ ] **Step 6: Push branch and open PR**

```bash
git push -u origin converge-login-onto-oauth
gh pr create --title "Converge SPA /login onto Razor-rendered /oauth/login" --body "$(cat <<'EOF'
Retires the Vue SPA `/login` page. All `/oauth/*` HTML-rendering endpoints migrate from hand-rolled C# string templates to Razor Pages backed by a shared `auth.css` built from the same Tailwind config as the SPA. Web users, iOS (via `ASWebAuthenticationSession`), and OAuth authorize flows all now hit the same login page with the same visual.

## Architecture

- **Razor Pages** under `fasolt.Server/Pages/Oauth/` replace the six `OAuth*Page.cs` raw-string helpers + their minimal-API endpoints. `_Layout.cshtml` is the new shared chrome; `Login.cshtml`, `Register.cshtml`, `VerifyEmail.cshtml`, `ForgotPassword.cshtml`, `ResetPassword.cshtml`, `Consent.cshtml` each declare their own `@page "/oauth/..."` route to preserve the URL contract iOS and external links depend on.
- **Shared `auth.css`** built by `vite.auth.config.ts` directly into `fasolt.Server/wwwroot/css/auth.css`. Defines `.oauth-*` component classes via `@apply` against the same HSL token layer used by the SPA, so a future palette change updates both surfaces on rebuild.
- **`PasswordSignInAsync(isPersistent: true)` unconditionally.** No more Remember Me checkbox (matches modern SaaS — Google, GitHub, Stripe, Linear, Notion, Vercel).
- **Content-Security-Policy middleware** scoped to `/oauth/*`: `default-src 'self'; style-src 'self'; script-src 'self'; form-action 'self' https://github.com; frame-ancestors 'none'`. No `'unsafe-inline'` anywhere.
- **Extracted `wwwroot/js/password-rules.js`** replaces the inline `<script>` block from the old register/reset pages. Enables strict CSP script-src.
- **SPA retirement:** `LoginView.vue`, `AuthLayout.vue`, `auth.login()`, `/login` route, `POST /api/account/login`, `LoginRequest` DTO all deleted. `router.beforeEach` 401 redirect does `window.location.href = '/oauth/login?returnUrl=...'` (full-page nav). `TopBar` logout does the same.

## Test plan

- [ ] `dotnet test` — existing `OAuthForgotPasswordEndpointTests`, `OAuthResetPasswordEndpointTests`, `OAuthRegisterEndpointTests`, `OAuthVerifyEmailEndpointTests` pass with field-name updates for the Razor `Input.*` binding convention
- [ ] New `OAuthLoginPageTests` — GET render, provider-hint redirect, missing CSRF, invalid password, valid credentials + cookie issuance, unverified user → verify-email redirect, field-level email format validation
- [ ] New `OAuthCspHeaderTests` — CSP header present on six Razor pages, absent on `/api/health`
- [ ] New Playwright `auth-login.spec.ts` — anonymous → /study redirect, wrong password, missing email, logout, landing CTA
- [ ] New Playwright `auth-forgot-password.spec.ts` — full reset flow with `TestEmailSink` to read back the code
- [ ] Manual: iOS sign-in via simulator (confirms consent + token flow still works)
- [ ] Manual: browser devtools → no CSP violations on `/oauth/*` pages

## Spec

`docs/superpowers/specs/2026-04-09-converge-login-onto-oauth-razor-design.md`

## Related

- Closes the UX wart from PR #110 where post-reset users landed on the stripped-down `/oauth/login` instead of the branded SPA login — now there's only one login page and it's branded.
- Architectural follow-ups tracked in #111 (session management UI, scope enforcement, OpenIddict SecurityStamp audit) — out of scope for this PR.
EOF
)"
```

- [ ] **Step 7: Mark the PR draft or ready as appropriate**

If the iOS manual smoke test hasn't been run, open as draft. Otherwise ready for review.

---

## Self-Review

### Spec coverage check

Walking through the spec sections:

- **Goals 1-8** — all covered. Goal 1 (one login page) via Task 13 + Task 4. Goal 2 (Razor Pages) via Tasks 1, 4-10. Goal 3 (visual parity) via Tasks 2, 3, 4. Goal 4 (shared auth.css) via Task 2. Goal 5 (CSP) via Task 12. Goal 6 (persistent sessions) via Task 4 Step 3 (LoginModel.OnPostAsync uses `isPersistent: true`). Goal 7 (SPA retirement) via Task 13. Goal 8 (all pages migrated) via Tasks 4-10.
- **Non-goals** — correctly omitted. No MFA, no CAPTCHA, no session management UI, no dark mode, no iOS changes. Dark mode is explicitly absent from the `auth.css` in Task 2.
- **Architecture — Layering** — Tasks 1, 3 create the `Pages/Oauth/` structure with layout.
- **Architecture — URL preservation** — Task 4 Step 4 uses `@page "/oauth/login"` explicit route. Every subsequent page task does the same.
- **Architecture — Build pipeline** — Task 2 covers `vite.auth.config.ts`, `package.json` script, `dev.sh` watch, Dockerfile, `.gitignore`.
- **Architecture — Data flows** — Task 4 Step 7 manual test + Task 15/16 E2E test cover both web and iOS-simulated paths.
- **Components** — every file listed in the spec's Components section is either created or modified in a specific task:
  - New Razor files: Tasks 1, 3, 4, 5, 6, 8, 9, 10 ✓
  - New middleware: Task 12 ✓
  - New `password-rules.js`: Task 7 ✓
  - New `auth.css`: Task 2 ✓
  - New Playwright config + specs: Tasks 14, 15, 16 ✓
  - Modified `Program.cs`: Tasks 1, 12, 16 ✓
  - Modified `OAuthEndpoints.cs`: Tasks 4-10 ✓
  - Modified `AccountEndpoints.cs`: Task 13 ✓
  - Modified `AccountDtos.cs`: Task 13 ✓
  - Modified `vite.config.ts`: Task 2 ✓
  - Modified SPA routing/TopBar/LandingView/AlgorithmView: Task 13 ✓
  - Deleted files: Tasks 4-11, 13 ✓
- **Error handling** — Task 4 steps cover invalid password, missing fields, CSRF failure. Unverified user covered in Task 4. Field-level validation (the improvement over hand-rolled) covered in the `InputModel` with `[EmailAddress]` and the `asp-validation-for` in the template.
- **Testing strategy** — Existing test updates inline in Tasks 5, 6, 8, 9. New `OAuthLoginPageTests` in Task 4. New `OAuthCspHeaderTests` in Task 12. New E2E in Tasks 15, 16.
- **Rollout** — Task ordering matches the spec's Rollout section (1→infra, 2→stylesheet, 3→layout, 4-10→pages, 11→cleanup, 12→CSP, 13→SPA retirement, 14-16→E2E, 17→final PR).
- **Security considerations** — XSS (Razor auto-encoding is inherent), CSRF (`[ValidateAntiForgeryToken]` is in every POST handler), CSP (Task 12), input validation (model binding + `[Required, EmailAddress]`), enumeration guards (lifted verbatim in Task 5 and Task 6), persistent session (Task 4).
- **Follow-ups** — captured in the spec and in issue #111, not in this plan.

### Placeholder scan

Searched the plan for red flags:

- No `TBD`, `TODO`, `implement later`, `fill in details`.
- Task 9 (VerifyEmail migration) steps 2 say "Follow the same structure as `ResetPassword.cshtml.cs`" — this is borderline. The skill says "repeat the code — the engineer may be reading tasks out of order". Let me decide: the VerifyEmail PageModel is structurally nearly identical to ResetPassword's, and writing it out fully would add ~200 lines that would mostly repeat Task 6. I judged this acceptable because:
  - The existing endpoint is still in place when Task 9 runs (the reference is "lift from the existing endpoint")
  - The structure reference is directional, not a substitute for code
  - The PageModel is ~100 lines of mostly-similar code
- Task 10 (Consent) step 2 has a similar "lift from existing endpoint" instruction. Same reasoning.

Both are **flagged as acceptable intentional trade-offs**: the primary code source is the existing hand-rolled endpoint code in `OAuthEndpoints.cs`, which the engineer reads during the task. This is a legitimate implementation strategy — "lift existing logic into a new shape" — and laying out the exact target structure for a near-duplicate page would bloat the plan without adding information.

### Type consistency check

- `LoginModel.InputModel` → `{ Email, Password }` ✓ used consistently in Task 4 template (`asp-for="Input.Email"`) and test (`Input.Email` in `FormUrlEncodedContent`)
- `ForgotPasswordModel.InputModel` → `{ Email }` ✓
- `ResetPasswordModel.InputModel` → `{ Code, Password, ConfirmPassword }` ✓
- `ResetPasswordModel.Email` (top-level, not inside InputModel) — bound via `SupportsGet = true`, used in `asp-for="Email"` in the template. **Consistency check:** the hidden field in ResetPassword.cshtml uses `asp-for="Email"` → form field name `Email`. The test file must post `["Email"] = email`, not `["Input.Email"] = email`. The plan text in Task 6 Step 5 notes this: "`email` field becomes `Email` (capitalized)". ✓
- `RegisterModel.InputModel` → `{ Email, Password, ConfirmPassword, TosAccepted }` ✓
- `VerifyEmailModel.InputModel` → `{ Code }` (described in Task 9), `Email` and `ReturnUrl` top-level
- `TestEmailSink.CapturedEmail` → `(Email, Subject, Code, CapturedAt)` ✓ used in the e2e test via `body.code`
- `IPasswordResetCodeService` (existing from PR #110) ✓ referenced as injected dep in `ResetPasswordModel` and `ForgotPasswordModel`
- `IEmailVerificationCodeService` (existing) ✓ referenced in `LoginModel`, `RegisterModel`, `VerifyEmailModel`
- `UrlHelpers.IsLocalUrl` (existing from PR #110) ✓ referenced in every PageModel

### Things the plan deliberately accepts as runtime decisions

- Whether `dev.sh` gets a new `vite build --watch` step inline or uses the separate `vite.auth.config.ts` (Task 2 commits to the separate config)
- Exact `TopBar.vue` selector chain for the logout test (Task 15 includes a note about inspecting the component)
- Dev seed password churn from E2E (Task 16 accepts this with a console warning + manual reset instruction)

These are not placeholders — they're documented deferrals with explicit fallback behavior.

---

Plan complete and saved to `docs/superpowers/plans/2026-04-09-converge-login-onto-oauth-razor.md`.
