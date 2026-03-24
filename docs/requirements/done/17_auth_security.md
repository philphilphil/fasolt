# Auth Security Hardening

Critical and important security issues found during iOS app auth integration. Must be fixed before production deployment.

## Critical

### SEC-1 — Open Redirect in OAuth Login (P0)

The `POST /oauth/login` handler uses the `returnUrl` form field directly in `Results.Redirect()` with no validation. An attacker can craft a login form that redirects users to a malicious site after successful authentication.

**Fix:** Validate that `returnUrl` is a relative/local path before redirecting. Reject absolute URLs and `//` prefixes.

### SEC-2 — Rate Limiting on OAuth Endpoints (P0)

The following sensitive endpoints have no rate limiting:

- `POST /oauth/register` — anonymous, can be spammed to fill the DB with garbage client registrations
- `POST /oauth/login` — brute-forceable across accounts (lockout only protects per-user)
- `POST /oauth/token` — no limit on token issuance/refresh
- `POST /api/account/forgot-password` — can be used to spam emails
- `POST /api/account/reset-password` — no limit on reset token guessing

**Fix:** Apply `.RequireRateLimiting("auth")` to OAuth endpoints. Consider a stricter limit for `/oauth/register` (e.g., 5/hour/IP).

## Important

### SEC-3 — No Consent Screen + Open Client Registration

`POST /oauth/register` is fully anonymous and accepts any redirect URI. Combined with auto-approve on `/oauth/authorize` (no consent screen), an attacker who registers a malicious client can silently obtain tokens for any signed-in user.

**Fix:** Either add a consent screen showing client name and requested scopes, or restrict client registration (e.g., require admin approval, or whitelist redirect URI patterns like `fasolt://` and `http://localhost`).

### SEC-4 — Claim Destination Oversharing

All claims (including the internal user GUID via `ClaimTypes.NameIdentifier`) are sent to both access tokens and identity tokens. Internal IDs should only be in access tokens (server-side use), not identity tokens (client-visible).

**Fix:** Apply selective destination logic per claim type.

### SEC-5 — No Redirect URI Pattern Validation

Client registration accepts any absolute URI as a redirect URI — including `http://` URLs and arbitrary domains. Should restrict to custom schemes (`fasolt://`) and localhost for native apps.

### SEC-6 — Forwarded Headers Trust Any Proxy

`KnownProxies` and `KnownIPNetworks` are cleared, trusting `X-Forwarded-*` headers from any source. Document this assumption (app must be behind a trusted reverse proxy) or configure known proxy IPs for production.

### SEC-7 — Production Certificate Enforcement

If OpenIddict certificate paths are not configured, the app silently falls back to development certificates. Add a startup check that throws in non-development environments.
