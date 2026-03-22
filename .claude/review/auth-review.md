# Authentication & Authorization Review — Findings

## Critical 🔴

### `AUTH-C001` — Database Credentials Hardcoded in appsettings.json (Checked into Git)
- **ID:** `AUTH-C001`
- **File:** `fasolt.Server/appsettings.json:L3`
- **Risk:** The production connection string with database username and password is hardcoded in a file that ships with the codebase. If the same file is used in production (or a deployment copies it), the database credentials are exposed to anyone with repository access. Even if overridden in production, the pattern normalizes credential-in-code.
- **Evidence:**
  ```json
  "DefaultConnection": "Host=localhost;Port=5432;Database=fasolt;Username=spaced;Password=spaced_dev"
  ```
- **Fix:** Move the connection string to `appsettings.Development.json` (already gitignored or dev-only), environment variables, or a secrets manager. Use `builder.Configuration` with environment-specific overrides so `appsettings.json` contains only a placeholder or no connection string at all.
---

### `AUTH-C002` — Email Change Bypasses Verification (Immediate Apply)
- **ID:** `AUTH-C002`
- **File:** `fasolt.Server/Api/Endpoints/AccountEndpoints.cs:L70-L71`
- **Risk:** The email change endpoint generates a change-email token and immediately applies it in the same request, bypassing the intended email verification flow. An attacker who compromises a session can silently change the account email to one they control, then use password reset to take over the account. The user is never asked to confirm ownership of the new email address.
- **Evidence:**
  ```csharp
  var token = await userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
  var result = await userManager.ChangeEmailAsync(user, request.NewEmail, token);
  ```
- **Fix:** Send the change-email token to the new email address as a confirmation link. Only apply the change when the user clicks the link (a separate endpoint that receives the token). This ensures the user actually owns the new email.
---

### `AUTH-C003` — No CSRF Protection on Cookie-Authenticated State-Changing Endpoints
- **ID:** `AUTH-C003`
- **File:** `fasolt.Server/Program.cs` (entire file — no antiforgery middleware)
- **Risk:** The app uses cookie-based authentication with `SameSite=Strict`, which mitigates most CSRF attacks from cross-origin navigations. However, `SameSite=Strict` does not protect against subdomain attacks or same-site attacks (e.g., XSS on a sibling subdomain). There is no antiforgery token validation on any POST/PUT/DELETE endpoint. State-changing operations (card creation, deletion, email change, password change) are vulnerable if the SameSite cookie boundary is breached.
- **Risk Level Justification:** Marked critical because the email-change endpoint (AUTH-C002) combined with no CSRF allows full account takeover if any same-site vector exists.
- **Evidence:** No references to `AntiForgery`, `antiforgery`, `CSRF`, or `X-XSRF` exist anywhere in the codebase.
- **Fix:** Add ASP.NET Core antiforgery middleware, or at minimum validate a custom header (e.g., `X-Requested-With`) on all state-changing cookie-authenticated requests. API token-authenticated requests are not affected (Bearer tokens are inherently CSRF-safe).
---

## High 🟠

### `AUTH-H001` — DevEmailSender Used Unconditionally (No Real Email Sending)
- **ID:** `AUTH-H001`
- **File:** `fasolt.Server/Program.cs:L54`
- **Risk:** `DevEmailSender` is registered unconditionally (not gated behind `IsDevelopment()`). In production, password reset tokens and email confirmation links are only logged, never actually sent. This means: (1) password reset is silently broken in production, and (2) if logs are accessible, reset tokens are exposed.
- **Evidence:**
  ```csharp
  builder.Services.AddTransient<IEmailSender<AppUser>, DevEmailSender>();
  ```
  The `DevEmailSender` implementation at `fasolt.Server/Infrastructure/Services/DevEmailSender.cs:L17` logs the confirmation link plainly:
  ```csharp
  _logger.LogInformation("Password reset link for {Email}: {Link}", email, resetLink);
  ```
- **Fix:** Gate `DevEmailSender` behind `builder.Environment.IsDevelopment()` and register a real email sender for production. Ensure reset tokens are never logged in production.
---

### `AUTH-H002` — No Rate Limiting on Authentication Endpoints
- **ID:** `AUTH-H002`
- **File:** `fasolt.Server/Program.cs` (entire file — no rate limiting middleware)
- **Risk:** The login endpoint (`/api/identity/login`), registration endpoint, password reset, and API token creation have no rate limiting. While ASP.NET Identity has account lockout (5 attempts / 5 minutes), this only locks the target account — it does not prevent credential stuffing across many accounts, nor brute-force of password reset tokens. Registration spam is also unmitigated.
- **Evidence:** No references to `RateLimit`, `UseRateLimiter`, or `AddRateLimiter` in the codebase.
- **Fix:** Add `Microsoft.AspNetCore.RateLimiting` middleware with fixed-window or sliding-window policies on auth endpoints. Consider IP-based limiting for login and registration, and per-user limiting for token creation and password changes.
---

### `AUTH-H003` — Cookie SecurePolicy Is `SameAsRequest` (Not Enforced HTTPS)
- **ID:** `AUTH-H003`
- **File:** `fasolt.Server/Program.cs:L39`
- **Risk:** `CookieSecurePolicy.SameAsRequest` means the auth cookie will be sent over plain HTTP if the request is HTTP. In production, if the app is accessed over HTTP (before an HTTPS redirect, or if TLS termination is misconfigured), the session cookie is transmitted in cleartext and can be intercepted.
- **Evidence:**
  ```csharp
  options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
  ```
- **Fix:** Set `CookieSecurePolicy.Always` for production. Use conditional configuration: `SameAsRequest` for development, `Always` for production. Also consider adding HSTS headers.
---

### `AUTH-H004` — Health Endpoint Has No Auth and Is Not Explicitly Marked AllowAnonymous
- **ID:** `AUTH-H004`
- **File:** `fasolt.Server/Api/Endpoints/HealthEndpoints.cs:L7`
- **Risk:** The health endpoint does not call `RequireAuthorization()` and relies on the absence of a default authorization fallback policy. This is intentional (health checks should be public), but it is implicit — if the default policy were changed to a fallback policy (via `FallbackPolicy`), this endpoint would silently break. More importantly, the OpenAPI endpoint (`/openapi/v1.json`) is also only gated behind `IsDevelopment()` — if the environment check fails in production, the full API schema is exposed.
- **Evidence:**
  ```csharp
  app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));
  ```
- **Fix:** Explicitly mark the health endpoint with `.AllowAnonymous()` to make the intent clear. For the OpenAPI endpoint, verify the environment gate is reliable in your deployment.
---

## Medium 🟡

### `AUTH-M001` — API Token LastUsedAt Update on Every Request (Write Amplification / Timing Side-Channel)
- **ID:** `AUTH-M001`
- **File:** `fasolt.Server/Api/Auth/BearerTokenHandler.cs:L52-L53`
- **Risk:** Every authenticated API token request triggers a database write to update `LastUsedAt`. This creates write amplification under load and a minor timing side-channel: a valid-but-expired token returns faster than an invalid token hash (hash lookup fails before the write). The performance impact is the larger concern for a SaaS product.
- **Evidence:**
  ```csharp
  apiToken.LastUsedAt = DateTimeOffset.UtcNow;
  await db.SaveChangesAsync();
  ```
- **Fix:** Batch or debounce `LastUsedAt` updates (e.g., update at most once per minute using a cache). Alternatively, fire the update asynchronously without awaiting it in the auth pipeline.
---

### `AUTH-M002` — Dev Seed Data Guard Only Checks Environment Flag
- **ID:** `AUTH-M002`
- **File:** `fasolt.Server/Program.cs:L74-L77`
- **Risk:** The dev seed user and known API token are only gated behind `IsDevelopment()`. If the `ASPNETCORE_ENVIRONMENT` variable is misconfigured or absent in production (defaults to `Production`, so this is unlikely but worth noting), the well-known dev token would be seeded. The token value is publicly documented in the repo README and CLAUDE.md.
- **Evidence:**
  ```csharp
  if (app.Environment.IsDevelopment())
  {
      await DevSeedData.SeedAsync(app.Services);
  }
  ```
  The token is publicly documented:
  ```
  sm_dev_token_for_local_testing_only_do_not_use_in_production_0000
  ```
- **Fix:** Add a secondary guard inside `DevSeedData.SeedAsync` that checks the environment independently, or verify the token does not already exist in production DB. Consider logging a warning if the seed runs.
---

### `AUTH-M003` — Identity API Endpoints Exposed Without Explicit Configuration
- **ID:** `AUTH-M003`
- **File:** `fasolt.Server/Program.cs:L91`
- **Risk:** `MapIdentityApi<AppUser>()` maps the full set of ASP.NET Core Identity endpoints including `/api/identity/manage/*` which provides account info, 2FA setup, and other management endpoints. Some of these may not be intended for use and expand the attack surface. The `/api/identity/register` endpoint is open with no CAPTCHA or rate limit.
- **Evidence:**
  ```csharp
  app.MapGroup("/api/identity").MapIdentityApi<AppUser>();
  ```
- **Fix:** Audit which Identity API endpoints are actually used by the frontend. Consider mapping only needed endpoints or adding `.RequireAuthorization()` to the management group. Add rate limiting to `/api/identity/register`.
---

### `AUTH-M004` — 1-Day Sliding Session Expiration May Be Too Long
- **ID:** `AUTH-M004`
- **File:** `fasolt.Server/Program.cs:L40-L41`
- **Risk:** Sessions last up to 1 day and use sliding expiration, meaning an active user's session never expires as long as they interact at least once per day. For a SaaS application, this increases the window for session hijacking. There is no absolute session lifetime.
- **Evidence:**
  ```csharp
  options.ExpireTimeSpan = TimeSpan.FromDays(1);
  options.SlidingExpiration = true;
  ```
- **Fix:** Consider reducing the sliding window or adding an absolute expiration (e.g., 7 days maximum regardless of activity). Provide a "remember me" option with a longer lifetime for explicit user opt-in (the frontend already passes `useSessionCookies` but the backend does not differentiate).
---

## Low 🔵

### `AUTH-L001` — Password Policy Does Not Require Non-Alphanumeric Characters
- **ID:** `AUTH-L001`
- **File:** `fasolt.Server/Program.cs:L22`
- **Risk:** `RequireNonAlphanumeric` is set to `false`. This slightly weakens password complexity. The minimum length of 8 with upper, lower, and digit requirements is reasonable but below NIST 800-63B recommendations of minimum 8 characters with no composition rules (favoring length over complexity).
- **Evidence:**
  ```csharp
  options.Password.RequireNonAlphanumeric = false;
  ```
- **Fix:** Consider increasing minimum length to 12+ and relying on length rather than composition rules (per NIST guidance), or keep current rules as-is. This is low risk with the current 8-character minimum.
---

### `AUTH-L002` — Account Lockout Duration Is Short (5 Minutes)
- **ID:** `AUTH-L002`
- **File:** `fasolt.Server/Program.cs:L27`
- **Risk:** A 5-minute lockout after 5 failed attempts is easily waited out by an attacker. Combined with no rate limiting (AUTH-H002), an attacker can repeatedly cycle through lockout windows.
- **Evidence:**
  ```csharp
  options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
  ```
- **Fix:** Consider progressive lockout (increasing duration on repeated lockouts) or rely on rate limiting (AUTH-H002 fix) as the primary defense. The lockout is a secondary control.
---

### `AUTH-L003` — Token Prefix Leaks Partial Token Value
- **ID:** `AUTH-L003`
- **File:** `fasolt.Server/Api/Endpoints/ApiTokenEndpoints.cs:L42`
- **Risk:** The first 8 characters of the token (e.g., `sm_XXXXX`) are stored as `TokenPrefix` and returned in the token list API. Since all tokens start with `sm_`, only 5 characters of entropy are exposed. This is a very minor information leak (5 hex chars = 20 bits out of 256 bits total).
- **Evidence:**
  ```csharp
  var prefix = rawToken[..8]; // "sm_XXXXX"
  ```
- **Fix:** No immediate action required. The prefix is useful for token identification. The risk is negligible given the 256-bit token entropy.
---

## ✅ What's Done Well
- **Token hashing:** API tokens are stored as SHA-256 hashes, never in plaintext. The raw token is only returned once at creation time. This is correct practice.
- **User isolation on all data endpoints:** Every endpoint (cards, decks, reviews, sources, search, tokens) filters by the authenticated user's ID. No IDOR vulnerabilities were found — all queries include `UserId == user.Id` or equivalent.
- **Soft delete with global query filter:** Deleted cards are filtered out at the EF Core level via `HasQueryFilter`, reducing the risk of accidentally returning deleted data.
- **Token expiration and revocation:** API tokens support both expiration dates and revocation, with proper checks in the auth handler.
- **Cookie configuration:** `HttpOnly = true` and `SameSite = Strict` are both set, which protects against XSS cookie theft and most CSRF vectors.
- **No user enumeration on forgot-password:** The forgot-password endpoint always returns OK regardless of whether the email exists.
- **Proper password verification on email change:** The email change endpoint requires the current password before proceeding.
- **Bearer token handler uses constant-time hash comparison via database lookup:** Token validation queries the hash from the database rather than comparing in application code, which avoids timing attacks on the token value itself.
- **Frontend auth guard:** The Vue router has a `beforeEach` guard that redirects unauthenticated users away from protected routes, and the API client includes `credentials: 'include'` for cookie-based auth.
- **Dev seed data is gated behind IsDevelopment():** The well-known dev token is only seeded in development mode.
