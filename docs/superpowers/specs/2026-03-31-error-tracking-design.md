# Error Tracking with Bugsink

**Date**: 2026-03-31
**Extends**: GitHub issue #76 (global Vue error handler)
**Status**: Approved

## Problem

Errors in production are invisible. Backend exceptions go to ephemeral container stdout. Frontend errors are swallowed silently or shown as generic messages with no reporting. There's no way to know what's failing without SSH'ing into the server and reading `docker logs`.

## Decision

Use **Bugsink hosted** (bugsink.com) as the error tracking service. Bugsink is Sentry-SDK compatible, so all three platforms use battle-tested Sentry SDKs pointed at a Bugsink DSN.

**Why Bugsink**: Lightweight, EU-based (Netherlands), Sentry-SDK compatible, free tier covers 15k events/month. No self-hosting overhead.

## Scope

Error tracking only. No performance monitoring, no session replay, no distributed tracing. Just: "see errors, get alerted."

## Backend (.NET)

Add `Sentry.AspNetCore` NuGet package. One-line setup in `Program.cs`:

```csharp
builder.WebHost.UseSentry(o =>
{
    o.Dsn = config["Bugsink:Dsn"];
    o.Environment = builder.Environment.EnvironmentName;
    o.SendDefaultPii = false;
});
```

This automatically captures:
- Unhandled exceptions (via middleware)
- `ILogger.LogError` / `LogCritical` calls (existing calls in ApnsService, NotificationBackgroundService, PlunkEmailSender flow to Bugsink with no code changes)
- Request context (URL, method) attached to each error — but no IP, cookies, or user identity

No changes to existing error handling patterns. The global exception handler middleware in `Program.cs` stays as-is.

If the DSN is empty or missing, the SDK is a no-op — no errors are sent, no crash. This is critical for self-hosters who may not configure error tracking. Same pattern as GitHub social login: unconfigured = disabled, no errors.

## Frontend (Vue 3)

Add `@sentry/vue` npm package. Initialize in `main.ts` before mounting:

```typescript
import * as Sentry from '@sentry/vue'

Sentry.init({
  app,
  dsn: import.meta.env.VITE_BUGSINK_DSN,
  environment: import.meta.env.MODE,
  integrations: [
    Sentry.browserTracingIntegration({ router }),
  ],
})
```

This resolves issue #76 — `Sentry.init({ app })` hooks into `app.config.errorHandler`, catching unhandled errors in lifecycle hooks, watchers, and render functions.

Existing per-component try/catch blocks stay as-is. They handle expected errors with user-facing messages. Only unhandled/unexpected errors bubble up to Bugsink.

`sendDefaultPii` is NOT enabled (defaults to false).

Source maps are not uploaded (Bugsink doesn't support them yet). Frontend errors will have minified stack traces but will still be grouped and visible.

## iOS App

Add `sentry-cocoa` via SPM. Initialize in the app entry point:

```swift
import Sentry

SentrySDK.start { options in
    options.dsn = "bugsink-dsn-here"
    options.environment = "production"
    options.sendDefaultPii = false
}
```

Captures crashes and unhandled exceptions automatically.

## Configuration

All three platforms need one value: the Bugsink DSN.

| Platform | Config mechanism | Variable |
|----------|-----------------|----------|
| Backend | `.env` (dev), env var in docker-compose.prod.yml (prod) | `Bugsink__Dsn` |
| Frontend | `.env` (Vite exposes `VITE_`-prefixed vars) | `VITE_BUGSINK_DSN` |
| iOS | Hardcoded or config plist | — |

Add `Bugsink__Dsn` and `VITE_BUGSINK_DSN` to `.env.example` with empty values and a comment explaining they're optional.

All SDKs gracefully no-op when the DSN is absent — self-hosters don't need to configure this. Same pattern as GitHub OAuth: unconfigured means disabled.

## GDPR / Privacy

Error tracking is covered under **legitimate interest** (GDPR Art. 6(1)(f)) — required to maintain and secure the service. No consent needed.

Privacy constraints:
- `SendDefaultPii = false` on all platforms (no IP addresses, cookies, or user identity sent)
- No user IDs or emails attached to error events — errors are anonymous
- Bugsink is EU-based (Netherlands) — no cross-border transfer concerns
- Mention error tracking in the privacy policy: "We use error tracking to monitor and fix technical issues. This may include technical data such as browser type, device info, and error stack traces."

## What we're NOT doing

- No performance monitoring or tracing
- No session replay
- No source maps upload (Bugsink limitation)
- No custom error boundary UI (existing per-component handling is sufficient)
- No changes to existing try/catch patterns or ILogger usage
- No user identity attached to error events
