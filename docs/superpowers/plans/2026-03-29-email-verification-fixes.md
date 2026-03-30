# Email Verification Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the per-request email verification middleware with a claims-based authorization policy, fix MCP/OAuth security gaps, and fix minor frontend issues.

**Architecture:** Store `email_confirmed` as a claim in the auth cookie at sign-in. Check it via an ASP.NET Core authorization policy instead of middleware. Re-sign-in users when their email is confirmed to refresh the claim. Apply the policy to all data endpoints and MCP.

**Tech Stack:** ASP.NET Core authorization policies, Identity claims, Vue Router meta

---

### Task 1: Add `SignInWithEmailClaimAsync` helper and use in Register/Login

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`

- [ ] **Step 1: Add the helper method**

Add this private static method to `AccountEndpoints`:

```csharp
private static async Task SignInWithEmailClaimAsync(
    SignInManager<AppUser> signInManager, AppUser user, bool isPersistent)
{
    var claims = new List<Claim>
    {
        new("email_confirmed", user.EmailConfirmed.ToString().ToLower())
    };
    await signInManager.SignInWithClaimsAsync(user, isPersistent, claims);
}
```

- [ ] **Step 2: Use helper in `Register`**

Replace `await signInManager.SignInAsync(user, isPersistent: false);` (line 49) with:

```csharp
await SignInWithEmailClaimAsync(signInManager, user, isPersistent: false);
```

- [ ] **Step 3: Use helper in `Login`**

Replace the `PasswordSignInAsync` block (lines 57-63) with:

```csharp
var result = await signInManager.PasswordSignInAsync(
    request.Email, request.Password, isPersistent: false, lockoutOnFailure: true);

if (result.IsLockedOut)
    return Results.Problem("Account locked. Try again later.", statusCode: 429);
if (!result.Succeeded)
    return Results.Problem("Invalid email or password.", statusCode: 401);

// Re-sign-in with email_confirmed claim in cookie
var user = await signInManager.UserManager.FindByEmailAsync(request.Email);
if (user is not null)
    await SignInWithEmailClaimAsync(signInManager, user, request.RememberMe);

return Results.Ok();
```

Note: `PasswordSignInAsync` already validated credentials, so `FindByEmailAsync` will always find the user here. We re-sign-in to inject the custom claim — `PasswordSignInAsync` doesn't support additional claims.

- [ ] **Step 4: Use helper in `ConfirmEmail`**

Add `SignInManager<AppUser> signInManager` to the `ConfirmEmail` method parameters and re-sign-in after confirmation:

```csharp
private static async Task<IResult> ConfirmEmail(
    ConfirmEmailRequest request,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager)
{
    var user = await userManager.FindByIdAsync(request.UserId);
    if (user is null)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [""] = ["Invalid or expired confirmation link."]
        });

    var result = await userManager.ConfirmEmailAsync(user, request.Token);
    if (!result.Succeeded)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [""] = ["Invalid or expired confirmation link."]
        });

    // Refresh cookie so email_confirmed claim is updated
    await SignInWithEmailClaimAsync(signInManager, user, isPersistent: false);

    return Results.Ok();
}
```

- [ ] **Step 5: Verify it compiles**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Api/Endpoints/AccountEndpoints.cs
git commit -m "feat: add SignInWithEmailClaimAsync and use in Register/Login/ConfirmEmail"
```

---

### Task 2: Use claim-based sign-in in GitHubCallback

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`

- [ ] **Step 1: Replace `SignInAsync` in `GitHubCallback`**

In the `GitHubCallback` method (line 309), replace:

```csharp
await signInManager.SignInAsync(user, isPersistent: true);
```

with:

```csharp
await SignInWithEmailClaimAsync(signInManager, user, isPersistent: true);
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/Endpoints/AccountEndpoints.cs
git commit -m "feat: use claim-based sign-in for GitHub callback"
```

---

### Task 3: Add `EmailVerified` authorization policy and 403 handler

**Files:**
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Add the `EmailVerified` policy**

In `Program.cs`, inside the `AddAuthorization` block (after the `AdminCookieOnly` policy, around line 168), add:

```csharp
options.AddPolicy("EmailVerified", policy =>
    policy.RequireAuthenticatedUser()
          .AddAuthenticationSchemes(
              IdentityConstants.ApplicationScheme,
              OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
          .RequireClaim("email_confirmed", "true"));
```

- [ ] **Step 2: Add 403 handler to cookie config**

In the `ConfigureApplicationCookie` block (around line 120), add a handler for `OnRedirectToAccessDenied` after the existing `OnRedirectToLogin`:

```csharp
options.Events.OnRedirectToAccessDenied = context =>
{
    context.Response.StatusCode = 403;
    context.Response.ContentType = "application/problem+json";
    return context.Response.WriteAsJsonAsync(new
    {
        type = "https://tools.ietf.org/html/rfc9110#section-15.5.4",
        title = "Email not verified",
        status = 403,
        detail = "You must verify your email address before accessing this resource."
    });
};
```

- [ ] **Step 3: Remove middleware registration**

Delete this line from `Program.cs` (line 461):

```csharp
app.UseMiddleware<EmailVerificationMiddleware>();
```

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Program.cs
git commit -m "feat: add EmailVerified authorization policy, remove middleware"
```

---

### Task 4: Apply `EmailVerified` policy to all data endpoints and MCP

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/CardEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/DeckEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/ReviewEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/SearchEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/SourceEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/NotificationEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/SchedulingSettingsEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/SnapshotEndpoints.cs`
- Modify: `fasolt.Server/Program.cs` (MCP endpoint)

- [ ] **Step 1: Update CardEndpoints**

In `CardEndpoints.cs` line 13, change:
```csharp
var group = app.MapGroup("/api/cards").RequireAuthorization().RequireRateLimiting("api");
```
to:
```csharp
var group = app.MapGroup("/api/cards").RequireAuthorization("EmailVerified").RequireRateLimiting("api");
```

- [ ] **Step 2: Update DeckEndpoints**

In `DeckEndpoints.cs` line 13, change:
```csharp
var group = app.MapGroup("/api/decks").RequireAuthorization().RequireRateLimiting("api");
```
to:
```csharp
var group = app.MapGroup("/api/decks").RequireAuthorization("EmailVerified").RequireRateLimiting("api");
```

- [ ] **Step 3: Update ReviewEndpoints**

In `ReviewEndpoints.cs` line 13, change:
```csharp
var group = app.MapGroup("/api/review").RequireAuthorization().RequireRateLimiting("api");
```
to:
```csharp
var group = app.MapGroup("/api/review").RequireAuthorization("EmailVerified").RequireRateLimiting("api");
```

- [ ] **Step 4: Update SearchEndpoints**

In `SearchEndpoints.cs` line 12, change:
```csharp
var group = app.MapGroup("/api/search").RequireAuthorization().RequireRateLimiting("api");
```
to:
```csharp
var group = app.MapGroup("/api/search").RequireAuthorization("EmailVerified").RequireRateLimiting("api");
```

- [ ] **Step 5: Update SourceEndpoints**

In `SourceEndpoints.cs` line 12, change:
```csharp
var group = app.MapGroup("/api/sources").RequireAuthorization().RequireRateLimiting("api");
```
to:
```csharp
var group = app.MapGroup("/api/sources").RequireAuthorization("EmailVerified").RequireRateLimiting("api");
```

- [ ] **Step 6: Update NotificationEndpoints**

In `NotificationEndpoints.cs` line 13, change:
```csharp
var group = app.MapGroup("/api/notifications").RequireAuthorization().RequireRateLimiting("api");
```
to:
```csharp
var group = app.MapGroup("/api/notifications").RequireAuthorization("EmailVerified").RequireRateLimiting("api");
```

- [ ] **Step 7: Update SchedulingSettingsEndpoints**

In `SchedulingSettingsEndpoints.cs` line 13, change:
```csharp
var group = app.MapGroup("/api/settings/scheduling").RequireAuthorization().RequireRateLimiting("api");
```
to:
```csharp
var group = app.MapGroup("/api/settings/scheduling").RequireAuthorization("EmailVerified").RequireRateLimiting("api");
```

- [ ] **Step 8: Update SnapshotEndpoints**

In `SnapshotEndpoints.cs` line 13, change:
```csharp
var group = app.MapGroup("/api").RequireAuthorization().RequireRateLimiting("api");
```
to:
```csharp
var group = app.MapGroup("/api").RequireAuthorization("EmailVerified").RequireRateLimiting("api");
```

- [ ] **Step 9: Update MCP endpoint**

In `Program.cs` line 479, change:
```csharp
app.MapMcp("/mcp").RequireAuthorization().RequireRateLimiting("api");
```
to:
```csharp
app.MapMcp("/mcp").RequireAuthorization("EmailVerified").RequireRateLimiting("api");
```

- [ ] **Step 10: Verify it compiles**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 11: Commit**

```bash
git add fasolt.Server/Api/Endpoints/CardEndpoints.cs fasolt.Server/Api/Endpoints/DeckEndpoints.cs fasolt.Server/Api/Endpoints/ReviewEndpoints.cs fasolt.Server/Api/Endpoints/SearchEndpoints.cs fasolt.Server/Api/Endpoints/SourceEndpoints.cs fasolt.Server/Api/Endpoints/NotificationEndpoints.cs fasolt.Server/Api/Endpoints/SchedulingSettingsEndpoints.cs fasolt.Server/Api/Endpoints/SnapshotEndpoints.cs fasolt.Server/Program.cs
git commit -m "feat: apply EmailVerified policy to all data endpoints and MCP"
```

---

### Task 5: Add EmailConfirmed check to OAuth token refresh

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`

- [ ] **Step 1: Add EmailConfirmed check in token endpoint**

In `OAuthEndpoints.cs`, in the `/oauth/token` handler, after the user-not-found check (around line 208), add:

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

- [ ] **Step 2: Replace DB lookup in OAuth authorize with claim check**

In the `/oauth/authorize` handler (around lines 122-128), replace the DB lookup:

```csharp
// Block unverified users from authorizing OAuth clients
var authUserId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
var appUser = await userManager.FindByIdAsync(authUserId);
if (appUser is not null && !appUser.EmailConfirmed)
{
    return Results.Redirect("/verify-email");
}
```

with a claim-based check:

```csharp
// Block unverified users from authorizing OAuth clients
var emailConfirmed = result.Principal.FindFirstValue("email_confirmed");
if (emailConfirmed != "true")
{
    return Results.Redirect("/verify-email");
}
```

Then remove `UserManager<AppUser> userManager` from the method parameters if it's no longer used elsewhere in the handler. Check the rest of the method first — it is not used elsewhere, so remove it.

- [ ] **Step 3: Verify it compiles**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs
git commit -m "fix: check EmailConfirmed on OAuth token refresh, use claim in authorize"
```

---

### Task 6: Delete EmailVerificationMiddleware

**Files:**
- Delete: `fasolt.Server/Api/Middleware/EmailVerificationMiddleware.cs`

- [ ] **Step 1: Delete the middleware file**

```bash
rm fasolt.Server/Api/Middleware/EmailVerificationMiddleware.cs
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded. No references to `EmailVerificationMiddleware` remain (the `Program.cs` line was already removed in Task 3).

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/Middleware/EmailVerificationMiddleware.cs
git commit -m "refactor: delete EmailVerificationMiddleware (replaced by policy)"
```

---

### Task 7: Fix frontend — confirm-email-change route and timer leak

**Files:**
- Modify: `fasolt.client/src/router/index.ts`
- Modify: `fasolt.client/src/views/EmailVerificationView.vue`

- [ ] **Step 1: Add `skipVerificationCheck` to confirm-email-change route**

In `router/index.ts`, change the `/confirm-email-change` route (lines 46-49) from:

```typescript
{
  path: '/confirm-email-change',
  name: 'confirm-email-change',
  component: () => import('@/views/ConfirmEmailChangeView.vue'),
},
```

to:

```typescript
{
  path: '/confirm-email-change',
  name: 'confirm-email-change',
  component: () => import('@/views/ConfirmEmailChangeView.vue'),
  meta: { skipVerificationCheck: true },
},
```

- [ ] **Step 2: Fix timer leak in EmailVerificationView**

In `EmailVerificationView.vue`, at the top of `handleResend()` (line 23), add a guard to clear any existing timer:

```typescript
async function handleResend() {
  if (timer) {
    clearInterval(timer)
    timer = null
  }
  resending.value = true
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/router/index.ts fasolt.client/src/views/EmailVerificationView.vue
git commit -m "fix: add skipVerificationCheck to confirm-email-change, fix timer leak"
```

---

### Task 8: Run tests and manual verification

- [ ] **Step 1: Run unit/integration tests**

Run: `dotnet test`
Expected: All tests pass

- [ ] **Step 2: Start full stack and test in browser**

Run: `./dev.sh`

Test the following flows with Playwright:
1. Register a new user → should land on `/verify-email`, API calls to `/api/cards` should return 403 with JSON body
2. Click confirmation link → should redirect to `/study`, API calls now succeed
3. Log out and log back in → should go to `/study` (verified user)
4. Unverified user tries to access `/confirm-email-change` → should NOT be redirected to `/verify-email`

- [ ] **Step 3: Commit any fixes if needed**
