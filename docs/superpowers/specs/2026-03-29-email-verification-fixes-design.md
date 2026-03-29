# Email Verification Fixes

Fixes for issues identified during PR #68 review. Replaces the per-request middleware with a claims-based authorization policy, closes security gaps in MCP and OAuth token refresh, and fixes minor frontend issues.

## 1. Replace middleware with claims-based authorization policy

**Delete `EmailVerificationMiddleware` entirely.**

### Custom claim at sign-in

Add a custom claim `email_confirmed` (value `"true"` or `"false"`) to the cookie principal at every sign-in point:

- `Register` — claim is `"false"` (new user, unverified)
- `Login` — read `user.EmailConfirmed` from DB, set claim accordingly
- `GitHubCallback` — always `"true"` (GitHub users are auto-confirmed)
- `ConfirmEmail` endpoint — after `ConfirmEmailAsync` succeeds, re-sign-in the user with `SignInManager` to refresh the cookie with `email_confirmed = "true"`

Use `SignInManager.SignInAsync` with additional claims via `AuthenticationProperties` or by adding claims to the user's `ClaimsPrincipal` before sign-in. The cleanest approach: override the claims principal factory or add the claim directly before calling `SignInAsync`.

Implementation: create a helper method `SignInWithEmailClaimAsync(SignInManager, AppUser, bool isPersistent)` in `AccountEndpoints` that:
1. Creates `additionalClaims` list with `new Claim("email_confirmed", user.EmailConfirmed.ToString().ToLower())`
2. Calls `signInManager.SignInWithClaimsAsync(user, isPersistent, additionalClaims)`

Use this helper in `Register`, `Login`, `GitHubCallback`, and `ConfirmEmail`.

### Authorization policy

Add an `"EmailVerified"` policy in `Program.cs`:

```csharp
options.AddPolicy("EmailVerified", policy =>
    policy.RequireAuthenticatedUser()
          .RequireClaim("email_confirmed", "true"));
```

### Apply policy to protected endpoints

Apply `.RequireAuthorization("EmailVerified")` to endpoint groups/routes that require a verified email:

- Card endpoints (`/api/cards/*`)
- Deck endpoints (`/api/decks/*`)
- Review endpoints (`/api/review/*`)
- Source endpoints (`/api/sources/*`)
- Search endpoints (`/api/search/*`)
- Notification endpoints (`/api/notifications/*`)
- Admin endpoints (`/api/admin/*`)
- MCP endpoint (`/mcp`) — **fixes the MCP bypass**

Endpoints that should NOT require the policy (only require basic auth or be public):
- `/api/account/me` — needs to work for unverified users (frontend reads `emailConfirmed` from here)
- `/api/account/resend-verification` — unverified users call this
- `/api/account/logout` — always allowed
- `/api/account/confirm-email` — public (no auth required)
- `/api/account/confirm-email-change` — requires auth but not verification (fixes blocker #3)
- `/api/account/register`, `/api/account/login` — public
- `/api/health` — public
- OAuth endpoints — handled separately (see section 2)

### Handle 403 with structured response

Configure the `"EmailVerified"` policy's forbid behavior. When the policy denies access, return a JSON `ProblemDetails` response:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Email not verified",
  "status": 403,
  "detail": "You must verify your email address before accessing this resource."
}
```

This is done via `options.Events.OnRedirectToAccessDenied` on the application cookie, or via a custom `IAuthorizationMiddlewareResultHandler`. The simplest approach: handle it in the cookie config alongside the existing `OnRedirectToLogin` handler.

## 2. OAuth token refresh checks EmailConfirmed

In the `/oauth/token` endpoint, after looking up the user (line ~200), add:

```csharp
if (!user.EmailConfirmed)
    return Results.Forbid(
        authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
        properties: new(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Email address not verified.",
        }));
```

This prevents unverified users from refreshing OAuth tokens.

## 3. `/confirm-email-change` frontend route

Add `meta: { skipVerificationCheck: true }` to the `/confirm-email-change` route in `router/index.ts`. The backend endpoint already requires auth but not the `EmailVerified` policy (per section 1).

## 4. Timer leak fix in EmailVerificationView

In `EmailVerificationView.vue`, clear any existing interval before starting a new one in `handleResend`:

```typescript
if (timer) clearInterval(timer)
```

Add this at the top of the resend handler, before setting the new interval.

## Files changed

- `fasolt.Server/Api/Middleware/EmailVerificationMiddleware.cs` — **delete**
- `fasolt.Server/Api/Endpoints/AccountEndpoints.cs` — add `SignInWithEmailClaimAsync` helper, use it in Register/Login/ConfirmEmail
- `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` — add EmailConfirmed check in token endpoint, use helper in authorize endpoint
- `fasolt.Server/Program.cs` — add `EmailVerified` policy, remove middleware registration, add 403 handler to cookie config
- `fasolt.Server/Api/Endpoints/CardEndpoints.cs` — apply `EmailVerified` policy
- `fasolt.Server/Api/Endpoints/DeckEndpoints.cs` — apply `EmailVerified` policy
- `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs` — apply `EmailVerified` policy
- `fasolt.Server/Api/Endpoints/SearchEndpoints.cs` — apply `EmailVerified` policy
- `fasolt.Server/Api/Endpoints/SourceEndpoints.cs` — apply `EmailVerified` policy
- `fasolt.Server/Api/Endpoints/NotificationEndpoints.cs` — apply `EmailVerified` policy
- `fasolt.Server/Api/Endpoints/AdminEndpoints.cs` — apply `EmailVerified` policy
- MCP endpoint registration in `Program.cs` — apply `EmailVerified` policy
- `fasolt.client/src/router/index.ts` — add `skipVerificationCheck` to confirm-email-change route
- `fasolt.client/src/views/EmailVerificationView.vue` — fix timer leak
