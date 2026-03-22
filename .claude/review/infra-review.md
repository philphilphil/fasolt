# Configuration & Infrastructure Review — Findings

## Critical 🔴

No findings.

---

## High 🟠

### `INFRA-H001` — Database credentials hardcoded in appsettings.json (checked into source control)
- **ID:** `INFRA-H001`
- **File:** `fasolt.Server/appsettings.json:L3`
- **Risk:** The base `appsettings.json` is committed to the repository and contains database credentials. If this file is used as-is in production (or serves as the template), the password `spaced_dev` ships in the repo. Even if production overrides via environment variables, the pattern encourages credential leakage.
- **Evidence:**
  ```json
  "DefaultConnection": "Host=localhost;Port=5432;Database=fasolt;Username=spaced;Password=spaced_dev"
  ```
- **Fix:** Move the connection string to `appsettings.Development.json` (already gitignored or dev-only) and set the base `appsettings.json` value to a placeholder or remove it entirely. In production, inject via `ConnectionStrings__DefaultConnection` environment variable or a secrets manager.

---

### `INFRA-H002` — No CORS policy configured
- **ID:** `INFRA-H002`
- **File:** `fasolt.Server/Program.cs`
- **Risk:** No `AddCors` / `UseCors` calls exist. In production, if the API and SPA are on different origins (e.g., separate domain or port), browsers will block requests. More importantly, the absence of an explicit CORS policy means the default allows no cross-origin requests, which is safe — but when production deployment requires CORS, it will likely be added permissively in a rush. A deliberate restrictive policy should be defined now.
- **Evidence:** No CORS-related code found anywhere in the server project.
- **Fix:** Add an explicit CORS policy restricted to the expected frontend origin(s):
  ```csharp
  builder.Services.AddCors(options =>
      options.AddDefaultPolicy(policy =>
          policy.WithOrigins("https://your-production-domain.com")
                .AllowCredentials()
                .AllowAnyHeader()
                .AllowAnyMethod()));
  app.UseCors();
  ```

---

### `INFRA-H003` — No security headers (CSP, X-Frame-Options, HSTS, etc.)
- **ID:** `INFRA-H003`
- **File:** `fasolt.Server/Program.cs`
- **Risk:** No Content-Security-Policy, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, or Strict-Transport-Security headers are set. This leaves the application vulnerable to clickjacking, MIME-type sniffing attacks, and downgrades from HTTPS.
- **Evidence:** Grep for `SecurityHeaders`, `Content-Security-Policy`, `X-Frame-Options`, `UseHsts`, `UseHttpsRedirection` returned no results in the server project.
- **Fix:** Add security headers middleware. At minimum:
  ```csharp
  app.UseHsts(); // outside IsDevelopment check
  app.UseHttpsRedirection();
  app.Use(async (context, next) => {
      context.Response.Headers["X-Content-Type-Options"] = "nosniff";
      context.Response.Headers["X-Frame-Options"] = "DENY";
      context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
      await next();
  });
  ```

---

### `INFRA-H004` — DevEmailSender registered unconditionally in all environments
- **ID:** `INFRA-H004`
- **File:** `fasolt.Server/Program.cs:L54`
- **Risk:** `DevEmailSender` is registered for all environments, not just Development. In production, password reset links and confirmation links will be silently logged instead of being emailed. This means anyone who triggers a password reset can find the reset token in server logs, bypassing email verification entirely.
- **Evidence:**
  ```csharp
  builder.Services.AddTransient<IEmailSender<AppUser>, DevEmailSender>();
  ```
  This line is outside any `IsDevelopment()` guard.
- **Fix:** Wrap in an environment check and register a real email sender for production:
  ```csharp
  if (builder.Environment.IsDevelopment())
      builder.Services.AddTransient<IEmailSender<AppUser>, DevEmailSender>();
  else
      builder.Services.AddTransient<IEmailSender<AppUser>, SmtpEmailSender>();
  ```

---

## Medium 🟡

### `INFRA-M001` — Cookie SecurePolicy set to SameAsRequest instead of Always
- **ID:** `INFRA-M001`
- **File:** `fasolt.Server/Program.cs:L39`
- **Risk:** `CookieSecurePolicy.SameAsRequest` means that if the app is accessed over HTTP (e.g., behind a misconfigured reverse proxy, or during a downgrade attack), authentication cookies will be sent over plaintext, enabling session hijacking.
- **Evidence:**
  ```csharp
  options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
  ```
- **Fix:** Set to `CookieSecurePolicy.Always` for production. Use a conditional check if HTTP is needed for local development:
  ```csharp
  options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
      ? CookieSecurePolicy.SameAsRequest
      : CookieSecurePolicy.Always;
  ```

---

### `INFRA-M002` — No rate limiting on authentication endpoints
- **ID:** `INFRA-M002`
- **File:** `fasolt.Server/Program.cs`
- **Risk:** No rate limiting middleware is configured. The login and registration endpoints are exposed without throttling, making them susceptible to brute-force and credential-stuffing attacks. Account lockout (5 attempts / 5 minutes, L26-27) helps but does not prevent distributed attacks across many accounts.
- **Evidence:** Grep for `RateLimit`, `rate.limit`, `Throttl` returned no results in the server project.
- **Fix:** Add ASP.NET Core rate limiting middleware:
  ```csharp
  builder.Services.AddRateLimiter(options => {
      options.AddFixedWindowLimiter("auth", opt => {
          opt.PermitLimit = 10;
          opt.Window = TimeSpan.FromMinutes(1);
      });
  });
  app.UseRateLimiter();
  ```

---

### `INFRA-M003` — Postgres port exposed on all interfaces in docker-compose
- **ID:** `INFRA-M003`
- **File:** `docker-compose.yml:L10`
- **Risk:** Port mapping `"5432:5432"` binds to `0.0.0.0`, exposing the database to the network. On a developer machine with weak DB credentials (`spaced_dev`), any machine on the same network can connect directly to Postgres.
- **Evidence:**
  ```yaml
  ports:
    - "5432:5432"
  ```
- **Fix:** Bind to localhost only:
  ```yaml
  ports:
    - "127.0.0.1:5432:5432"
  ```

---

### `INFRA-M004` — No Docker health check for Postgres container
- **ID:** `INFRA-M004`
- **File:** `docker-compose.yml`
- **Risk:** Without a health check, `dev.sh` starts the backend immediately after `docker compose up -d`, but Postgres may not be ready to accept connections yet, causing intermittent startup failures.
- **Evidence:** No `healthcheck` directive in docker-compose.yml.
- **Fix:** Add a health check:
  ```yaml
  healthcheck:
    test: ["CMD-SHELL", "pg_isready -U spaced -d fasolt"]
    interval: 5s
    timeout: 3s
    retries: 5
  ```

---

### `INFRA-M005` — `AllowedHosts` set to wildcard
- **ID:** `INFRA-M005`
- **File:** `fasolt.Server/appsettings.json:L11`
- **Risk:** `"AllowedHosts": "*"` disables host header validation, which can facilitate host header injection attacks (cache poisoning, password reset link manipulation).
- **Evidence:**
  ```json
  "AllowedHosts": "*"
  ```
- **Fix:** Set to the expected production hostname(s) in production config:
  ```json
  "AllowedHosts": "fasolt.example.com;localhost"
  ```

---

## Low 🔵

### `INFRA-L001` — No HTTPS profile used by default in dev
- **ID:** `INFRA-L001`
- **File:** `fasolt.Server/Properties/launchSettings.json:L4-L12`
- **Risk:** The default launch profile is `http` (no TLS). While acceptable for local development, it means dev testing never exercises the HTTPS code path, and cookie/header behavior differences may go unnoticed until production.
- **Evidence:**
  ```json
  "http": {
    "applicationUrl": "http://localhost:8080",
  }
  ```
- **Fix:** Consider making `https` the default profile, or at least regularly testing with it.

---

### `INFRA-L002` — No CI/CD pipeline defined
- **ID:** `INFRA-L002`
- **File:** N/A (no `.github/workflows/` directory)
- **Risk:** No automated build, test, or security scanning pipeline exists. Vulnerabilities in dependencies or code will not be caught automatically. No Dockerfile exists either, suggesting no containerized production deployment strategy yet.
- **Evidence:** No CI/CD configuration files found in the project (only in `node_modules/` from third-party packages).
- **Fix:** Add a GitHub Actions workflow with at minimum: build, test, `dotnet audit`, and `npm audit` steps.

---

### `INFRA-L003` — Password reset tokens and confirmation links logged at Information level
- **ID:** `INFRA-L003`
- **File:** `fasolt.Server/Infrastructure/Services/DevEmailSender.cs:L17,L23,L29`
- **Risk:** Security-sensitive tokens (confirmation links, password reset links/codes) are logged at `Information` level. If logs are aggregated to a centralized service, these tokens become accessible to anyone with log access.
- **Evidence:**
  ```csharp
  _logger.LogInformation("Confirmation link for {Email}: {Link}", email, confirmationLink);
  _logger.LogInformation("Password reset link for {Email}: {Link}", email, resetLink);
  _logger.LogInformation("Password reset code for {Email}: {Code}", email, resetCode);
  ```
- **Fix:** This is acceptable for dev-only use, but is reinforced by INFRA-H004 — ensure this sender is never used in production.

---

## ✅ What's Done Well

- **Dev seed data is gated behind `IsDevelopment()`** (Program.cs L74-77) — the hardcoded dev token and user are only created in development mode.
- **API tokens are stored as SHA-256 hashes**, not plaintext (BearerTokenHandler.cs L71-74). Token lookup is done by hash comparison.
- **Cookie configuration is solid for an early-stage project**: `HttpOnly = true`, `SameSite = Strict`, 1-day expiry with sliding expiration.
- **Identity lockout policy** is configured (5 attempts, 5-minute lockout window) — a good baseline against brute-force on individual accounts.
- **Password policy** requires 8+ characters with mixed case and digits (Program.cs L18-22).
- **`.gitignore` covers `.env` files** — `.env` and `.env.local` are excluded from version control.
- **OpenAPI endpoint is only exposed in Development** (Program.cs L69-72).
- **Error response middleware** returns generic error messages without leaking stack traces or internal details.
- **401 responses return JSON instead of redirecting to a login page** — appropriate for an API-first application.
