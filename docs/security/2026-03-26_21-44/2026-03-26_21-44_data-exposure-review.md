# Data Exposure & Cryptography Review — Findings

## Critical 🔴

No findings.

---

## High 🟠

### `DATA-H001` — DevEmailSender logs sensitive security tokens at Information level
- **ID:** `DATA-H001`
- **File:** `fasolt.Server/Infrastructure/Services/DevEmailSender.cs:L17-L29`
- **Risk:** Password reset links, password reset codes, and email confirmation links are logged at `LogInformation` level. These tokens allow account takeover if logs are accessible to an attacker (log aggregation services, log files on disk, stdout in container environments). While this class is only registered in Development, there is no runtime guard preventing accidental registration in production, and log output in dev Docker containers can persist.
- **Evidence:**
```csharp
_logger.LogInformation("Confirmation link for {Email}: {Link}", email, confirmationLink);
_logger.LogInformation("Password reset link for {Email}: {Link}", email, resetLink);
_logger.LogInformation("Password reset code for {Email}: {Code}", email, resetCode);
```
- **Fix:** Log that a token was generated (for debugging), but redact the actual token value. Use `LogDebug` instead of `LogInformation` to reduce accidental exposure:
```csharp
_logger.LogDebug("Confirmation link generated for {Email}", email);
_logger.LogDebug("Password reset link generated for {Email}", email);
_logger.LogDebug("Password reset code generated for {Email}", email);
```

---

### `DATA-H002` — Admin API exposes ASP.NET Identity internal user IDs
- **ID:** `DATA-H002`
- **File:** `fasolt.Server/Application/Services/AdminService.cs:L25` and `fasolt.Server/Api/Endpoints/AdminEndpoints.cs:L19-L22`
- **Risk:** The admin `/api/admin/users` endpoint returns the internal ASP.NET Identity `Id` (a GUID string) for each user. The lock/unlock and push endpoints consume this same internal ID directly from the URL path (`/api/admin/users/{id}/lock`). Exposing internal database identifiers — even behind an admin gate — increases the attack surface if an admin session is compromised. The rest of the application consistently uses NanoId-based public IDs for cards and decks, but users lack this abstraction.
- **Evidence:**
```csharp
// AdminService.cs
.Select(u => new AdminUserDto(
    u.Id,  // ASP.NET Identity internal GUID
    u.Email!,
    ...
))
```
```csharp
// AdminEndpoints.cs — lock/unlock use internal ID directly
group.MapPost("/users/{id}/lock", LockUser);
group.MapPost("/users/{id}/unlock", UnlockUser);
```
- **Fix:** This is admin-only and behind cookie auth + role check, so the risk is bounded. Consider adding a public ID to `AppUser` (like cards/decks) or accept the trade-off since admin scope is narrow. At minimum, ensure admin endpoints are not enumerable by rate-limiting the list endpoint.

---

### `DATA-H003` — PII (user emails) stored in application logs database
- **ID:** `DATA-H003`
- **File:** `fasolt.Server/Infrastructure/Services/NotificationBackgroundService.cs:L83,L149,L167` and `fasolt.Server/Api/Endpoints/AdminEndpoints.cs:L142`
- **Risk:** User email addresses are written into `AppLog.Message` fields in the database (e.g., `"Sent to user@example.com: 5 cards due"`, `"Admin push to user@example.com: ..."`). These logs are queryable via the admin API at `/api/admin/logs`. Storing PII in logs creates GDPR/compliance risk — log data is often retained longer than user data and may not be included in account deletion flows.
- **Evidence:**
```csharp
// NotificationBackgroundService.cs
Message = $"Sent to {userEmail}: {totalDue} cards due",
Message = $"Invalid token for {userEmail}, removed",
Message = $"Error for {entry.UserEmail}",

// AdminEndpoints.cs
Message = tokenValid
    ? $"Admin push to {user.Email}: {body}"
    : $"Invalid token for {user.Email}, removed",
```
- **Fix:** Use the user ID instead of email in log messages, or use a truncated/hashed identifier:
```csharp
Message = $"Sent to user {userId}: {totalDue} cards due",
```
Ensure the account deletion flow also purges logs containing the user's ID.

---

## Medium 🟡

### `DATA-M001` — OpenIddict signing/encryption certificates loaded without password
- **ID:** `DATA-M001`
- **File:** `fasolt.Server/Program.cs:L66-L67`
- **Risk:** Production OpenIddict certificates are loaded from PFX files with `null` password. Password-less PFX files mean any file read vulnerability (path traversal, backup exposure) immediately yields the signing key, which can be used to forge OAuth tokens.
- **Evidence:**
```csharp
options.AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12FromFile(encryptionCertPath, null))
       .AddSigningCertificate(X509CertificateLoader.LoadPkcs12FromFile(signingCertPath, null));
```
- **Fix:** Protect PFX files with a password stored in an environment variable:
```csharp
var certPassword = builder.Configuration["OpenIddict:CertificatePassword"];
options.AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12FromFile(encryptionCertPath, certPassword))
       .AddSigningCertificate(X509CertificateLoader.LoadPkcs12FromFile(signingCertPath, certPassword));
```

---

### `DATA-M002` — Dev connection string with password in checked-in config file
- **ID:** `DATA-M002`
- **File:** `fasolt.Server/appsettings.Development.json:L3`
- **Risk:** The development connection string with username/password (`fasolt`/`fasolt_dev`) is checked into source control. While this is standard for dev environments and the password is not a production secret, it sets a pattern where connection strings live in config files rather than environment variables. If someone copies this pattern to production, credentials would be exposed.
- **Evidence:**
```json
"ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=fasolt;Username=fasolt;Password=fasolt_dev"
}
```
- **Fix:** Acceptable for development. Production uses environment variables via `docker-compose.prod.yml`, which is correct. No immediate action needed, but add a comment in the file noting this is dev-only.

---

### `DATA-M003` — APNs error log may leak KeyId and TeamId
- **ID:** `DATA-M003`
- **File:** `fasolt.Server/Infrastructure/Services/ApnsService.cs:L125-L126`
- **Risk:** On APNs failure, the `KeyId`, `TeamId`, and `BundleId` are logged along with the response body. While not full secrets, these identifiers (especially `KeyId`) are semi-sensitive Apple credentials that could aid targeted attacks on the APNs configuration.
- **Evidence:**
```csharp
_logger.LogError("APNs FAILED {StatusCode}: {Body} | KeyId={KeyId} TeamId={TeamId} Topic={Topic}",
    (int)response.StatusCode, responseBody, _settings.KeyId, _settings.TeamId, _settings.BundleId);
```
- **Fix:** Log the status code and response body for debugging, but omit or truncate the credential identifiers:
```csharp
_logger.LogError("APNs FAILED {StatusCode}: {Body}", (int)response.StatusCode, responseBody);
```

---

### `DATA-M004` — Exception messages stored in database log detail field
- **ID:** `DATA-M004`
- **File:** `fasolt.Server/Infrastructure/Services/NotificationBackgroundService.cs:L85,L112`
- **Risk:** Raw `ex.Message` values are stored in the `AppLog.Detail` column, which is accessible via the admin logs API. Exception messages can contain internal details such as connection strings, SQL queries, file paths, or stack frames depending on the exception type.
- **Evidence:**
```csharp
Detail = ex.Message,  // lines 85 and 112
```
- **Fix:** Sanitize or truncate exception messages before storing. For the admin-visible log, use a generic message and log the full exception separately via `ILogger`:
```csharp
Detail = "See application logs for details",
```

---

## Low 🔵

### `DATA-L001` — NanoId entropy is adequate but on the lower end
- **ID:** `DATA-L001`
- **File:** `fasolt.Server/Infrastructure/NanoId.cs:L7-L8`
- **Risk:** The NanoId configuration uses a 62-character alphabet with 12-character length, yielding ~71 bits of entropy. This is sufficient for public-facing resource IDs but is on the lower end for security-critical identifiers. The IDs are used as public identifiers for cards/decks, not for authentication, so this is acceptable.
- **Evidence:**
```csharp
private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
private const int Size = 12;
// 62^12 = ~3.2 * 10^21, log2(62^12) ≈ 71.4 bits
```
- **Fix:** No immediate action needed. If IDs ever serve a security function (e.g., share links without auth), consider increasing to 16 characters (~95 bits).

---

### `DATA-L002` — OAuth login form lacks CSRF (anti-forgery) token
- **ID:** `DATA-L002`
- **File:** `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs:L298-L313`
- **Risk:** The server-rendered OAuth login form (`POST /oauth/login`) and consent form (`POST /oauth/consent`) do not include anti-forgery tokens. A CSRF attack could potentially auto-submit the login form with attacker credentials (login CSRF) causing the user's session to be associated with the attacker's account. The impact is limited because the OAuth flow has PKCE protection downstream.
- **Evidence:**
```csharp
// No antiforgery middleware or tokens in the forms
app.MapPost("/oauth/login", [AllowAnonymous] async (...) => { ... });
```
No calls to `AddAntiforgery()` or `[ValidateAntiForgeryToken]` anywhere in the codebase.
- **Fix:** Add ASP.NET Core antiforgery services and include tokens in the server-rendered forms:
```csharp
builder.Services.AddAntiforgery();
// In the form HTML: include a hidden antiforgery token field
```

---

### `DATA-L003` — Frontend stores no sensitive data in localStorage (good)
- **ID:** `DATA-L003`
- **File:** `fasolt.client/src/composables/useDarkMode.ts:L12,L27`
- **Risk:** None. The only `localStorage` usage is for the dark mode theme preference. Auth is handled via HttpOnly cookies. This is correct.
- **Evidence:**
```typescript
const stored = localStorage.getItem(STORAGE_KEY) // theme preference only
localStorage.setItem(STORAGE_KEY, newTheme)
```
- **Fix:** No action needed.

---

### `DATA-L004` — iOS keychain usage is correct and well-implemented
- **ID:** `DATA-L004`
- **File:** `fasolt.ios/Fasolt/Utilities/KeychainHelper.swift:L21`
- **Risk:** None. The iOS app correctly uses `kSecAttrAccessibleWhenUnlockedThisDeviceOnly` for keychain storage, which prevents access when the device is locked and does not sync to iCloud Keychain. Tokens are properly stored in keychain rather than UserDefaults.
- **Evidence:**
```swift
addQuery[kSecAttrAccessible as String] = kSecAttrAccessibleWhenUnlockedThisDeviceOnly
```
- **Fix:** No action needed.

---

## What's Done Well

- **Public ID pattern**: Cards and decks use NanoId-based public IDs instead of exposing internal GUIDs in API responses. This is a solid data exposure prevention pattern.
- **HttpOnly cookies**: Authentication cookies are configured with `HttpOnly = true`, `SameSite = Strict`, and environment-aware `SecurePolicy`. No auth tokens in localStorage.
- **Generic error messages**: The `ErrorResponseMiddleware` returns generic messages ("Authentication required", "Resource not found") without leaking internal details. The production exception handler returns a generic 500 error.
- **No secrets in source**: `.gitignore` properly excludes `.env`, certificate files (`.pem`, `.key`, `.p8`, `.p12`, `.pfx`), and database directories. The `.env.example` contains only placeholder values.
- **Production DB password externalized**: `docker-compose.prod.yml` uses `${DB_PASSWORD}` from environment variables rather than hardcoded credentials.
- **Data Protection keys persisted to DB**: `PersistKeysToDbContext` ensures Data Protection keys survive container restarts and are shared across instances.
- **APNs device token truncation**: Device tokens are truncated to first 8 characters in log messages (`deviceToken[..8]`), avoiding full token exposure.
- **iOS keychain best practices**: Tokens stored with `kSecAttrAccessibleWhenUnlockedThisDeviceOnly`, proper cleanup on auth failure, and `deleteAll()` on sign-out.
- **PKCE enforcement**: OAuth flow requires Proof Key for Code Exchange (`RequireProofKeyForCodeExchange()`), protecting against authorization code interception.
- **Forgot password non-enumerable**: The forgot password endpoint returns 200 OK regardless of whether the email exists, preventing user enumeration.
- **DTOs separate from entities**: API responses use dedicated DTO records that only include intended fields, preventing accidental exposure of internal entity properties.
