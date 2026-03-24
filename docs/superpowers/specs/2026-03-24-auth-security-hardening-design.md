# Auth Security Hardening — Design Spec

Fixes for SEC-1 through SEC-7 from `docs/requirements/17_auth_security.md`.

## SEC-1 — Open Redirect in OAuth Login

**Problem:** `POST /oauth/login` redirects to unvalidated `returnUrl`.

**Fix:** Add a `IsLocalUrl` helper that rejects:
- Absolute URLs (`http://`, `https://`)
- Protocol-relative URLs (`//`)
- Anything that doesn't start with `/`

Apply before `Results.Redirect(returnUrl)`. If invalid, redirect to `/`.

```csharp
// In OAuthEndpoints.cs
static bool IsLocalUrl(string url) =>
    !string.IsNullOrEmpty(url) &&
    url.StartsWith('/') &&
    !url.StartsWith("//");
```

Also apply to the hidden form field in `GET /oauth/login` — validate `returnUrl` before embedding it in the HTML.

## SEC-2 — Rate Limiting on OAuth Endpoints

**Problem:** OAuth endpoints have no rate limiting.

**Fix:** Two rate limit policies:

1. **`"auth"` (existing)** — 10 req/min/IP. Apply to:
   - `POST /oauth/login`
   - `POST /oauth/token`
   - `POST /api/account/forgot-password`
   - `POST /api/account/reset-password`

2. **`"auth-strict"` (new)** — 5 req/hour/IP. Apply to:
   - `POST /oauth/register`

Implementation: Add the new limiter in `Program.cs`. Apply `.RequireRateLimiting()` to each endpoint.

For the account endpoints, since they use a `MapGroup`, we need to apply rate limiting to individual endpoints rather than the group (the group includes authenticated endpoints that don't need limiting).

```csharp
options.AddFixedWindowLimiter("auth-strict", opt =>
{
    opt.PermitLimit = 5;
    opt.Window = TimeSpan.FromHours(1);
    opt.QueueLimit = 0;
});
```

**Partitioning by IP:** Both limiters use the default fixed-window which is global. Change to partition by IP using `AddPolicy` with `RateLimitPartition.GetFixedWindowLimiter` keyed on the remote IP.

```csharp
options.AddPolicy("auth", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

options.AddPolicy("auth-strict", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        }));
```

## SEC-3 — Consent Screen

**Problem:** `/oauth/authorize` auto-approves all requests. Combined with open client registration, an attacker can silently obtain tokens.

**Fix:** Add a consent screen as a Vue SPA page.

### Flow

1. `GET /oauth/authorize` — if user is authenticated but hasn't consented:
   - Return a redirect to `/oauth/consent?client_id=X&scope=Y&state=Z` (the Vue SPA page)
2. Vue consent page calls `GET /api/oauth/consent-info?client_id=X` to get client display name and requested scopes
3. User clicks Approve or Deny
4. Consent page calls `POST /api/oauth/consent` with `{ clientId, approved, originalQuery }`
5. Backend validates and either issues tokens (if approved) or returns an error to the client (if denied)

### Backend changes

**New endpoint: `GET /api/oauth/consent-info`**
- Requires authentication (cookie)
- Takes `client_id` query param
- Returns `{ clientName, scopes }` from the OpenIddict application store

**New endpoint: `POST /api/oauth/consent`**
- Requires authentication (cookie)
- Body: `{ clientId: string, approved: bool, returnPath: string }`
- `returnPath` is the original `/oauth/authorize?...` query string
- If approved: redirects to `/oauth/authorize?...&consent=granted`
- If denied: returns OAuth error redirect to the client's redirect_uri with `error=access_denied`

**Modified: `GET /oauth/authorize`**
- After authenticating the user, check for `consent=granted` query param
- If not present: redirect to Vue consent page
- If present: issue tokens as before (existing behavior)

### Frontend changes

**New route:** `/oauth/consent` (public, no auth guard — user is cookie-authenticated)

**New view:** `OAuthConsentView.vue`
- Reads `client_id`, `scope`, `state` from query params
- Calls `GET /api/oauth/consent-info?client_id=X`
- Shows: app name, requested scopes, Approve/Deny buttons
- On Approve: `POST /api/oauth/consent` with `approved: true`, follows redirect
- On Deny: `POST /api/oauth/consent` with `approved: false`, follows redirect

**Design:** Centered card layout consistent with the server-rendered login page. Shows the fasolt logo, client name, scopes list, and two buttons.

## SEC-4 — Claim Destination Oversharing

**Problem:** All claims (including internal `NameIdentifier` GUID) go to both access and identity tokens.

**Fix:** Selective destination logic:

```csharp
identity.SetDestinations(static claim => claim.Type switch
{
    // Internal user ID — access token only (server-side use)
    ClaimTypes.NameIdentifier => [Destinations.AccessToken],
    // Subject and name — both tokens
    Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
    Claims.Name => [Destinations.AccessToken, Destinations.IdentityToken],
    // Default — access token only
    _ => [Destinations.AccessToken],
});
```

Apply in both the authorize and token endpoints.

## SEC-5 — Redirect URI Pattern Validation

**Problem:** Client registration accepts any absolute URI as redirect URI.

**Fix:** Validate redirect URIs against configurable allowed patterns. Default patterns:
- `fasolt://` — iOS app custom scheme
- `http://localhost` — local dev/MCP clients (any port)
- `http://127.0.0.1` — local dev (any port)

Configuration via `appsettings.json`:
```json
{
  "OAuth": {
    "AllowedRedirectPatterns": ["fasolt://", "http://localhost", "http://127.0.0.1"]
  }
}
```

If not configured (self-hosters who don't set this), fall back to the defaults above. Self-hosters can extend the list.

Validation logic in `/oauth/register`:
```csharp
static bool IsAllowedRedirectUri(string uri, string[] allowedPatterns) =>
    allowedPatterns.Any(pattern => uri.StartsWith(pattern, StringComparison.OrdinalIgnoreCase));
```

Reject URIs that don't match any pattern with a clear error message.

## SEC-6 — Forwarded Headers Trust Any Proxy

**Problem:** `KnownProxies` and `KnownIPNetworks` are cleared, trusting all sources.

**Fix:** This is intentional for Docker/Cloudflare deployments. Changes:

1. Add a configuration option `ReverseProxy:TrustAllProxies` (default: `true` in Development, `false` otherwise)
2. When `TrustAllProxies` is `false`, read `ReverseProxy:KnownNetworks` from config (e.g., `["10.0.0.0/8", "172.16.0.0/12"]`)
3. Log a startup warning in non-development environments if `TrustAllProxies` is `true`

```csharp
var trustAll = builder.Configuration.GetValue("ReverseProxy:TrustAllProxies",
    builder.Environment.IsDevelopment());

if (trustAll)
{
    forwardedHeadersOptions.KnownProxies.Clear();
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    if (!builder.Environment.IsDevelopment())
        Log.Warning("Trusting all proxy headers — configure ReverseProxy:KnownNetworks for production");
}
else
{
    var networks = builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>();
    // Parse and add each CIDR network
}
```

## SEC-7 — Production Certificate Enforcement

**Problem:** Missing cert paths silently fall back to dev certs in any environment.

**Fix:** After the existing if/else block that loads certs, add a check:

```csharp
if (encryptionCertPath is null || signingCertPath is null)
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "OpenIddict:EncryptionCertificatePath and OpenIddict:SigningCertificatePath " +
            "must be configured in non-development environments.");

    options.AddDevelopmentEncryptionCertificate()
           .AddDevelopmentSigningCertificate();
}
```

This makes dev certs only available in Development. Any other environment throws on startup if certs aren't configured.

## Files Changed

| File | Changes |
|------|---------|
| `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` | SEC-1 (returnUrl validation), SEC-3 (consent redirect + new API endpoints), SEC-4 (claim destinations), SEC-5 (redirect URI validation) |
| `fasolt.Server/Program.cs` | SEC-2 (rate limiting policies + application), SEC-6 (forwarded headers config), SEC-7 (cert enforcement) |
| `fasolt.Server/Api/Endpoints/AccountEndpoints.cs` | SEC-2 (rate limiting on forgot/reset password) |
| `fasolt.client/src/views/OAuthConsentView.vue` | SEC-3 (new consent page) |
| `fasolt.client/src/router/index.ts` | SEC-3 (consent route) |

## Testing

Each fix should be verified via Playwright browser tests:
- SEC-1: Submit login with `returnUrl=https://evil.com` — should redirect to `/` not evil.com
- SEC-2: Send > 10 requests/min to `/oauth/login` — should get 429
- SEC-3: Full OAuth flow — should show consent screen with client name, approve/deny both work
- SEC-4: Decode an identity token — should not contain `NameIdentifier`
- SEC-5: Register client with `https://evil.com/callback` — should be rejected
- SEC-6: Startup warning logged when trust-all in production
- SEC-7: Start in Staging without certs — should throw
