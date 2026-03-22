# Data Exposure & Cryptography Review — Findings

## Critical 🔴

No findings.

---

## High 🟠

### `DATA-H001` — Database Credentials Committed to Version Control
- **ID:** `DATA-H001`
- **File:** `fasolt.Server/appsettings.json:L3`
- **Risk:** The Postgres connection string with username and password is checked into git. If the repo is ever made public, or accessed by an unauthorized party, database credentials are immediately exposed. In production, this file may be deployed with these dev defaults if environment-specific overrides are missing, granting access to the database.
- **Evidence:**
  ```json
  "DefaultConnection": "Host=localhost;Port=5432;Database=fasolt;Username=spaced;Password=spaced_dev"
  ```
- **Fix:** Remove the connection string from `appsettings.json`. Use environment variables or a secrets manager (e.g., `dotnet user-secrets`, Azure Key Vault) for all environments. Add `appsettings.json` with credentials to `.gitignore` or use a template file (`appsettings.json.example`) with placeholder values.

---

### `DATA-H002` — DevEmailSender Logs Password Reset Tokens and Links
- **ID:** `DATA-H002`
- **File:** `fasolt.Server/Infrastructure/Services/DevEmailSender.cs:L23, L29`
- **Risk:** Password reset links (containing security tokens) and reset codes are logged at `Information` level. In production, if this sender is accidentally left registered (or logs are shipped to a centralized system), an attacker with log access can hijack any user's account by replaying the reset token.
- **Evidence:**
  ```csharp
  _logger.LogInformation("Password reset link for {Email}: {Link}", email, resetLink);
  _logger.LogInformation("Password reset code for {Email}: {Code}", email, resetCode);
  ```
- **Fix:** Ensure `DevEmailSender` is never registered in production — gate its registration behind `IsDevelopment()`. Currently the registration at `Program.cs:L54` (`builder.Services.AddTransient<IEmailSender<AppUser>, DevEmailSender>()`) is unconditional and will be used in all environments. Wrap it in an `if (builder.Environment.IsDevelopment())` block and provide a real email sender for production.

---

### `DATA-H003` — DevEmailSender Registered Unconditionally (Not Dev-Only)
- **ID:** `DATA-H003`
- **File:** `fasolt.Server/Program.cs:L54`
- **Risk:** `DevEmailSender` is registered for all environments, not just Development. In production, password reset emails will not be sent (users cannot reset passwords), and security tokens will be logged instead. This is both a functionality bug and a security issue.
- **Evidence:**
  ```csharp
  builder.Services.AddTransient<IEmailSender<AppUser>, DevEmailSender>();
  ```
- **Fix:** Wrap in `if (builder.Environment.IsDevelopment())` and register a real SMTP/transactional email sender for production.

---

## Medium 🟡

### `DATA-M001` — Cookie SecurePolicy Set to `SameAsRequest` Instead of `Always`
- **ID:** `DATA-M001`
- **File:** `fasolt.Server/Program.cs:L39`
- **Risk:** With `CookieSecurePolicy.SameAsRequest`, if the app is accessed over HTTP (e.g., behind a misconfigured reverse proxy, or during local testing that leaks to a network), the authentication cookie will be sent without the `Secure` flag, making it interceptable via network sniffing (MITM).
- **Evidence:**
  ```csharp
  options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
  ```
- **Fix:** Use `CookieSecurePolicy.Always` for production. Consider making this configurable per environment — `SameAsRequest` is fine for local dev, but production should enforce `Always`.

---

### `DATA-M002` — Email Enumeration via ChangeEmail Endpoint
- **ID:** `DATA-M002`
- **File:** `fasolt.Server/Api/Endpoints/AccountEndpoints.cs:L64-L69`
- **Risk:** The `ChangeEmail` endpoint reveals whether an email is already registered by returning a specific validation error `"This email is already in use."`. An attacker can probe this endpoint (it requires auth, but any logged-in user can enumerate other users' emails).
- **Evidence:**
  ```csharp
  var existingUser = await userManager.FindByEmailAsync(request.NewEmail);
  if (existingUser is not null && existingUser.Id != user.Id)
      return Results.ValidationProblem(new Dictionary<string, string[]>
      {
          ["newEmail"] = ["This email is already in use."]
      });
  ```
- **Fix:** Return a generic error message (e.g., "Unable to change email. Please try a different address.") or silently accept the request and send a verification email instead of revealing registration status.

---

### `DATA-M003` — No HTTPS Redirection or HSTS Configured
- **ID:** `DATA-M003`
- **File:** `fasolt.Server/Program.cs`
- **Risk:** The application does not call `UseHttpsRedirection()` or `UseHsts()`. If deployed without a reverse proxy enforcing HTTPS, users may access the app over plain HTTP, exposing authentication cookies, API tokens, and all request/response data to network interception.
- **Evidence:** Neither `app.UseHttpsRedirection()` nor `app.UseHsts()` appear anywhere in the codebase.
- **Fix:** Add `app.UseHttpsRedirection()` and `app.UseHsts()` to `Program.cs` for production environments. If the app runs behind a TLS-terminating proxy, ensure the proxy enforces HTTPS and consider adding HSTS headers via the proxy config.

---

### `DATA-M004` — API Token Lookup Susceptible to Timing Attacks
- **ID:** `DATA-M004`
- **File:** `fasolt.Server/Api/Auth/BearerTokenHandler.cs:L36-L43`
- **Risk:** Token authentication hashes the incoming token with SHA-256 and does a database string comparison. While hashing mitigates most timing attacks, the database `==` comparison on the hash string may still leak information through query timing differences (e.g., early-exit string comparison in the DB engine). This is a low-practical-impact issue but a defense-in-depth concern.
- **Evidence:**
  ```csharp
  var hash = ComputeHash(token);
  var apiToken = await db.ApiTokens
      .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null);
  ```
- **Fix:** This is largely mitigated by hashing first. For additional defense-in-depth, consider using a token prefix index to narrow down candidates, then use `CryptographicOperations.FixedTimeEquals` for the final hash comparison in application code.

---

## Low 🔵

### `DATA-L001` — Password Policy Does Not Require Special Characters
- **ID:** `DATA-L001`
- **File:** `fasolt.Server/Program.cs:L22`
- **Risk:** `RequireNonAlphanumeric` is set to `false`, allowing passwords without special characters. This marginally reduces the keyspace for brute-force attacks, though the 8-character minimum with mixed case and digits provides reasonable baseline security.
- **Evidence:**
  ```csharp
  options.Password.RequireNonAlphanumeric = false;
  ```
- **Fix:** Consider setting `RequireNonAlphanumeric = true` or increasing the minimum length to 10+ characters to compensate. Modern guidance (NIST SP 800-63B) actually favors longer passwords over complexity rules, so increasing `RequiredLength` to 12 may be a better alternative.

---

### `DATA-L002` — Dev Seed Credentials Hardcoded in Source
- **ID:** `DATA-L002`
- **File:** `fasolt.Server/Infrastructure/Data/DevSeedData.cs:L11-L13`
- **Risk:** Dev credentials (email, password, API token) are hardcoded as constants. The seed is gated behind `IsDevelopment()` in `Program.cs:L74-L77`, so they won't be created in production. However, if the environment variable is misconfigured, these known credentials provide immediate access.
- **Evidence:**
  ```csharp
  public const string DevToken = "sm_dev_token_for_local_testing_only_do_not_use_in_production_0000";
  public const string DevEmail = "dev@fasolt.local";
  public const string DevPassword = "Dev1234!";
  ```
- **Fix:** The `IsDevelopment()` guard is appropriate. As additional protection, add a startup check that explicitly fails if these credentials exist in a production database, or log a warning if `ASPNETCORE_ENVIRONMENT` is not `Development` but the seed user is detected.

---

### `DATA-L003` — Password Reset Token Passed in URL Query Parameters
- **ID:** `DATA-L003`
- **File:** `fasolt.Server/Api/Endpoints/AccountEndpoints.cs:L101`
- **Risk:** The password reset link includes the email and reset token as URL query parameters. URLs can be logged by web servers, proxies, browsers (history), and referrer headers. While this is a common pattern, it creates multiple points where the token could be leaked.
- **Evidence:**
  ```csharp
  var resetLink = $"/reset-password?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(token)}";
  ```
- **Fix:** This is a standard pattern used by most frameworks. The 1-hour token lifespan (`Program.cs:L51`) limits the exposure window. For added security, consider sending only a short code via email and having the user enter it on the reset page, or use a POST-based token submission flow.

---

### `DATA-L004` — DevEmailSender Logs Email Confirmation Links
- **ID:** `DATA-L004`
- **File:** `fasolt.Server/Infrastructure/Services/DevEmailSender.cs:L17`
- **Risk:** Email confirmation links (containing tokens) are logged. Same class of issue as DATA-H002 but lower severity since confirmation tokens are less sensitive than password reset tokens. Covered by the fix for DATA-H003 (making DevEmailSender dev-only).
- **Evidence:**
  ```csharp
  _logger.LogInformation("Confirmation link for {Email}: {Link}", email, confirmationLink);
  ```
- **Fix:** Addressed by ensuring `DevEmailSender` is only registered in Development (see DATA-H003).

---

## ✅ What's Done Well

- **API tokens are properly hashed** -- SHA-256 hashing is applied before storage (`BearerTokenHandler.ComputeHash`), and only the hash is persisted in the database. Raw tokens are never stored. The token is shown to the user exactly once at creation time.
- **Token prefix for identification** -- Only the first 8 characters are stored as `TokenPrefix` for display/identification, avoiding exposure of the full token.
- **Cookie security flags** -- `HttpOnly = true` and `SameSite = Strict` are properly set on authentication cookies, preventing XSS-based cookie theft and CSRF attacks.
- **No sensitive data in frontend storage** -- The frontend uses cookie-based auth with `credentials: 'include'` and does not store tokens, passwords, or PII in `localStorage` or `sessionStorage`. Only the dark mode preference is stored locally.
- **Email enumeration prevention on forgot-password** -- The `ForgotPassword` endpoint always returns `200 OK` regardless of whether the email exists, preventing account enumeration.
- **User data isolation** -- All data queries consistently filter by `UserId`, preventing cross-user data access. This pattern is applied uniformly across cards, decks, tokens, and search endpoints.
- **Token expiration and revocation** -- API tokens support both expiration dates and explicit revocation, with both checks performed during authentication.
- **Account lockout** -- Configured with 5 failed attempts and 5-minute lockout, providing brute-force protection.
- **Parameterized SQL queries** -- Raw SQL in `SearchEndpoints.cs` uses parameterized queries (`{0}`, `{1}`) via `SqlQueryRaw`, preventing SQL injection.
- **Generic error messages in middleware** -- `ErrorResponseMiddleware` returns generic error messages for 401/403/404 without leaking internal details.
- **Cryptographically secure token generation** -- API tokens use `RandomNumberGenerator.GetBytes(32)` for generating 256 bits of entropy.
