# Error Tracking (Bugsink) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Bugsink error tracking to the .NET backend, Vue 3 frontend, and iOS app using Sentry-compatible SDKs.

**Architecture:** All three platforms use Sentry SDKs pointed at a Bugsink DSN. If the DSN is not configured, the SDK is a no-op (self-hoster safe). No PII is sent — `SendDefaultPii = false` everywhere.

**Tech Stack:** `Sentry.AspNetCore` (NuGet), `@sentry/vue` (npm), `sentry-cocoa` (SPM)

---

### Task 1: Add Sentry SDK to .NET backend

**Files:**
- Modify: `fasolt.Server/fasolt.Server.csproj`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Add the NuGet package**

Run:
```bash
cd fasolt.Server && dotnet add package Sentry.AspNetCore
```

- [ ] **Step 2: Add UseSentry to Program.cs**

Add after `builder.Configuration.AddEnvironmentVariables();` (line 21), before the DbContext registration:

```csharp
var bugsinkDsn = builder.Configuration["Bugsink:Dsn"];
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

This must be guarded with the `if` check so self-hosters without Bugsink configured get a clean no-op. The `using Sentry;` import is not needed — `UseSentry` is an extension method on `IWebHostBuilder` from the `Sentry.AspNetCore` namespace which is auto-imported via `<ImplicitUsings>enable</ImplicitUsings>`.

- [ ] **Step 3: Verify it builds**

Run:
```bash
cd fasolt.Server && dotnet build
```

Expected: Build succeeded.

- [ ] **Step 4: Verify no-op when DSN is empty**

Start the backend without a DSN configured and confirm it starts normally with no errors:

```bash
dotnet run --project fasolt.Server
```

Expected: App starts, no Sentry-related errors in output.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/fasolt.Server.csproj fasolt.Server/Program.cs
git commit -m "feat: add Sentry SDK to backend for Bugsink error tracking"
```

---

### Task 2: Add Sentry SDK to Vue frontend

**Files:**
- Modify: `fasolt.client/package.json` (via npm install)
- Modify: `fasolt.client/src/main.ts`

- [ ] **Step 1: Install the npm package**

Run:
```bash
cd fasolt.client && npm install @sentry/vue
```

- [ ] **Step 2: Update main.ts to initialize Sentry**

Replace the entire contents of `fasolt.client/src/main.ts` with:

```typescript
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import * as Sentry from '@sentry/vue'
import App from './App.vue'
import router from './router'
import './style.css'

const app = createApp(App)

const bugsinkDsn = import.meta.env.VITE_BUGSINK_DSN
if (bugsinkDsn) {
  Sentry.init({
    app,
    dsn: bugsinkDsn,
    environment: import.meta.env.MODE,
    integrations: [
      Sentry.browserTracingIntegration({ router }),
    ],
  })
}

app.use(createPinia())
app.use(router)
app.mount('#app')
```

Key points:
- The `if (bugsinkDsn)` guard ensures self-hosters without the env var get a clean app with no Sentry overhead.
- `Sentry.init({ app })` hooks into Vue's `app.config.errorHandler` automatically — this resolves issue #76.
- `browserTracingIntegration({ router })` gives route-level context on errors (which page the user was on).
- `sendDefaultPii` defaults to `false` so no PII is captured.

- [ ] **Step 3: Verify it builds**

Run:
```bash
cd fasolt.client && npm run build
```

Expected: Build succeeds with no errors.

- [ ] **Step 4: Verify no-op when DSN is absent**

Run the dev server without `VITE_BUGSINK_DSN` set:

```bash
cd fasolt.client && npm run dev
```

Expected: App loads normally in browser, no errors in console.

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/package.json fasolt.client/package-lock.json fasolt.client/src/main.ts
git commit -m "feat: add Sentry SDK to Vue frontend for Bugsink error tracking

Resolves #76 — Sentry.init hooks into app.config.errorHandler to catch
unhandled Vue errors in lifecycle hooks, watchers, and render functions."
```

---

### Task 3: Add Sentry SDK to iOS app

**Files:**
- Modify: `fasolt.ios/Fasolt.xcodeproj/project.pbxproj` (via Xcode SPM)
- Modify: `fasolt.ios/Fasolt/FasoltApp.swift`

- [ ] **Step 1: Add sentry-cocoa SPM dependency**

In Xcode:
1. Open `fasolt.ios/Fasolt.xcodeproj`
2. File → Add Package Dependencies
3. Enter URL: `https://github.com/getsentry/sentry-cocoa`
4. Set version rule to "Up to Next Major" from latest stable
5. Add the `Sentry` library to the Fasolt target

- [ ] **Step 2: Initialize Sentry in FasoltApp.swift**

Add the import at the top of the file, after the existing imports:

```swift
import Sentry
```

Add an `init()` method to the `FasoltApp` struct, before the `body` property:

```swift
init() {
    if let dsn = Bundle.main.object(forInfoDictionaryKey: "SENTRY_DSN") as? String, !dsn.isEmpty {
        SentrySDK.start { options in
            options.dsn = dsn
            options.environment = "production"
            options.sendDefaultPii = false
        }
    }
}
```

This reads the DSN from `Info.plist` so it can be configured per build without code changes. If the key is missing or empty, Sentry is not started.

- [ ] **Step 3: Add SENTRY_DSN to Info.plist**

Add to `fasolt.ios/Fasolt/Info.plist` (or create if using Xcode-generated plist):

```xml
<key>SENTRY_DSN</key>
<string>$(SENTRY_DSN)</string>
```

This allows setting the value via an Xcode build setting or xcconfig file. Leave it empty for development.

- [ ] **Step 4: Build and verify**

Build the iOS app in Xcode (Cmd+B). Verify:
- No build errors
- App launches in simulator without crashes
- No Sentry-related console output when DSN is empty

- [ ] **Step 5: Commit**

```bash
git add fasolt.ios/
git commit -m "feat: add Sentry SDK to iOS app for Bugsink error tracking"
```

---

### Task 4: Add configuration to .env.example

**Files:**
- Modify: `.env.example`

- [ ] **Step 1: Add Bugsink env vars**

Add the following lines at the end of `.env.example`:

```
# Error tracking (Bugsink) — optional, leave empty to disable
Bugsink__Dsn=
VITE_BUGSINK_DSN=
```

- [ ] **Step 2: Commit**

```bash
git add .env.example
git commit -m "docs: add Bugsink DSN to .env.example"
```

---

### Task 5: End-to-end verification

- [ ] **Step 1: Configure a test DSN**

Sign up at bugsink.com, create a project, and get a DSN. Add to `.env`:

```
Bugsink__Dsn=https://your-key@app.bugsink.com/123
VITE_BUGSINK_DSN=https://your-key@app.bugsink.com/123
```

- [ ] **Step 2: Start the full stack**

```bash
./dev.sh
```

- [ ] **Step 3: Trigger a backend error**

Use the browser or curl to hit an endpoint that will produce an error (e.g., an invalid API call). Check the Bugsink dashboard to confirm the error appears.

- [ ] **Step 4: Trigger a frontend error**

Open the browser console and run:

```javascript
throw new Error('Test Bugsink frontend error')
```

Check the Bugsink dashboard to confirm the error appears with Vue context (route, component).

- [ ] **Step 5: Verify self-hoster mode**

Remove both DSN values from `.env`, restart the stack, and confirm everything works normally with no errors about missing Sentry/Bugsink config.

- [ ] **Step 6: Run existing tests**

```bash
dotnet test
cd fasolt.client && npm run test
```

Expected: All existing tests pass — the SDK addition should not affect tests (DSN is not set in test environments).
