# GitHub Social Login Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add "Sign in with GitHub" as an alternative authentication method across web login, web register, and server-rendered OAuth login pages.

**Architecture:** ASP.NET Core external authentication with `AspNet.Security.OAuth.GitHub`. GitHub OAuth challenge/callback flow handled in `AccountEndpoints.cs`. New `ExternalProvider`/`ExternalProviderId` fields on `AppUser` distinguish GitHub accounts from password accounts. Frontend adds GitHub button via `<a>` link (full-page redirect, not SPA fetch).

**Tech Stack:** AspNet.Security.OAuth.GitHub, ASP.NET Core Identity, EF Core migration, Vue 3 + shadcn-vue

---

### File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `fasolt.Server/fasolt.Server.csproj` | Add GitHub OAuth NuGet package |
| Modify | `fasolt.Server/Domain/Entities/AppUser.cs` | Add `ExternalProvider` and `ExternalProviderId` fields |
| Modify | `fasolt.Server/Infrastructure/Data/AppDbContext.cs` | Configure new AppUser columns |
| Create | `fasolt.Server/Infrastructure/Data/Migrations/<timestamp>_AddExternalProvider.cs` | EF migration for new columns |
| Modify | `fasolt.Server/Program.cs` | Register GitHub auth scheme (conditional on config) |
| Modify | `fasolt.Server/Api/Endpoints/AccountEndpoints.cs` | Add GitHub login/callback endpoints |
| Modify | `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` | Add GitHub button to server-rendered login page |
| Modify | `fasolt.Server/Application/Dtos/AccountDtos.cs` | Add `externalProvider` to `UserInfoResponse` |
| Modify | `.env.example` | Add `GitHub__ClientId` and `GitHub__ClientSecret` |
| Modify | `fasolt.client/src/views/LoginView.vue` | Add GitHub sign-in button + error handling |
| Modify | `fasolt.client/src/views/RegisterView.vue` | Add GitHub sign-in button |
| Modify | `fasolt.client/src/stores/auth.ts` | Add `externalProvider` to User interface |
| Create | `fasolt.Tests/GitHubAuthTests.cs` | Integration tests for GitHub login flow |

---

### Task 1: Add NuGet package and AppUser fields

**Files:**
- Modify: `fasolt.Server/fasolt.Server.csproj`
- Modify: `fasolt.Server/Domain/Entities/AppUser.cs`
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs`

- [ ] **Step 1: Add the GitHub OAuth NuGet package**

```bash
cd fasolt.Server && dotnet add package AspNet.Security.OAuth.GitHub
```

- [ ] **Step 2: Add external provider fields to AppUser**

In `fasolt.Server/Domain/Entities/AppUser.cs`, add two properties:

```csharp
using Microsoft.AspNetCore.Identity;

namespace Fasolt.Server.Domain.Entities;

public class AppUser : IdentityUser
{
    public int NotificationIntervalHours { get; set; } = 8;
    public DateTimeOffset? LastNotifiedAt { get; set; }
    public double? DesiredRetention { get; set; }
    public int? MaximumInterval { get; set; }
    public string? ExternalProvider { get; set; }
    public string? ExternalProviderId { get; set; }
}
```

- [ ] **Step 3: Configure the new columns in AppDbContext**

In `fasolt.Server/Infrastructure/Data/AppDbContext.cs`, inside the `builder.Entity<AppUser>(entity => { ... })` block, add:

```csharp
entity.Property(e => e.ExternalProvider).HasMaxLength(50);
entity.Property(e => e.ExternalProviderId).HasMaxLength(255);
```

- [ ] **Step 4: Create EF migration**

```bash
cd fasolt.Server && dotnet ef migrations add AddExternalProvider
```

- [ ] **Step 5: Verify migration applies**

```bash
cd fasolt.Server && dotnet ef database update
```

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat: add ExternalProvider fields to AppUser and GitHub OAuth package"
```

---

### Task 2: Register GitHub auth scheme in Program.cs

**Files:**
- Modify: `fasolt.Server/Program.cs`
- Modify: `.env.example`

- [ ] **Step 1: Add GitHub config vars to .env.example**

Append to `.env.example`:

```
GitHub__ClientId=
GitHub__ClientSecret=
```

- [ ] **Step 2: Register the GitHub authentication scheme**

In `fasolt.Server/Program.cs`, after the `AddIdentityApiEndpoints` block (after line 43, before the OpenIddict block), add conditional GitHub registration:

```csharp
var gitHubClientId = builder.Configuration["GitHub:ClientId"];
var gitHubClientSecret = builder.Configuration["GitHub:ClientSecret"];

if (!string.IsNullOrEmpty(gitHubClientId) && !string.IsNullOrEmpty(gitHubClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGitHub(options =>
        {
            options.ClientId = gitHubClientId;
            options.ClientSecret = gitHubClientSecret;
            options.CallbackPath = "/signin-github";
            options.Scope.Add("user:email");
        });
}
```

Note: `CallbackPath` is the path ASP.NET Core's middleware intercepts internally — it's NOT the endpoint we write. The middleware handles the GitHub redirect back to this path, exchanges the code, and populates the external login info. We then read that info in our own callback endpoint.

- [ ] **Step 3: Verify the app still builds**

```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: register GitHub OAuth scheme conditionally on config"
```

---

### Task 3: Add GitHub login/callback endpoints

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`
- Modify: `fasolt.Server/Application/Dtos/AccountDtos.cs`

- [ ] **Step 1: Add the GitHub login challenge endpoint**

In `AccountEndpoints.cs`, add two new endpoint mappings inside `MapAccountEndpoints`:

```csharp
group.MapGet("/github-login", GitHubLogin).RequireRateLimiting("auth");
group.MapGet("/github-callback", GitHubCallback).RequireRateLimiting("auth");
```

- [ ] **Step 2: Implement the GitHubLogin handler**

This initiates the GitHub OAuth challenge. It stores the `returnUrl` in authentication properties so we know where to redirect after callback.

```csharp
private static IResult GitHubLogin(HttpContext context, IConfiguration configuration)
{
    // Only available when GitHub auth is configured
    if (string.IsNullOrEmpty(configuration["GitHub:ClientId"]))
        return Results.NotFound();

    var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
    // Validate returnUrl is local to prevent open redirect
    if (!IsLocalUrl(returnUrl))
        returnUrl = "/";

    var properties = new AuthenticationProperties
    {
        RedirectUri = $"/api/account/github-callback?returnUrl={Uri.EscapeDataString(returnUrl)}",
    };

    return Results.Challenge(properties, ["GitHub"]);
}

private static bool IsLocalUrl(string url) =>
    !string.IsNullOrEmpty(url) &&
    url.StartsWith('/') &&
    !url.StartsWith("//") &&
    !url.StartsWith("/\\");
```

Add `using Microsoft.AspNetCore.Authentication;` to the top of the file.

- [ ] **Step 3: Implement the GitHubCallback handler**

This runs after ASP.NET Core's GitHub middleware has exchanged the code and populated the external identity.

```csharp
private static async Task<IResult> GitHubCallback(
    HttpContext context,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager)
{
    var result = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
    if (result?.Principal is null)
        return Results.Redirect("/login?error=github_auth_failed");

    var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
    if (!IsLocalUrl(returnUrl))
        returnUrl = "/";

    var email = result.Principal.FindFirstValue(ClaimTypes.Email);
    var gitHubId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

    if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(gitHubId))
    {
        await context.SignOutAsync(IdentityConstants.ExternalScheme);
        return Results.Redirect("/login?error=github_no_email");
    }

    // Look up by GitHub provider ID first
    var user = await userManager.Users
        .FirstOrDefaultAsync(u => u.ExternalProvider == "GitHub" && u.ExternalProviderId == gitHubId);

    if (user is null)
    {
        // Check if email is already taken by a password-based account
        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser is not null)
        {
            await context.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Redirect($"/login?error=email_exists");
        }

        // Create new account
        user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            ExternalProvider = "GitHub",
            ExternalProviderId = gitHubId,
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            await context.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Redirect("/login?error=account_creation_failed");
        }
    }

    // Sign in with the application cookie
    await signInManager.SignInAsync(user, isPersistent: false);
    await context.SignOutAsync(IdentityConstants.ExternalScheme);

    return Results.Redirect(returnUrl);
}
```

Add `using Microsoft.EntityFrameworkCore;` to the top of the file (for `FirstOrDefaultAsync`).

- [ ] **Step 4: Update UserInfoResponse to include externalProvider**

In `fasolt.Server/Application/Dtos/AccountDtos.cs`, update the record:

```csharp
public record UserInfoResponse(string Email, bool IsAdmin, string? ExternalProvider = null);
```

- [ ] **Step 5: Update GetMe to pass ExternalProvider**

In `AccountEndpoints.cs`, update the `GetMe` method's return:

```csharp
return Results.Ok(new UserInfoResponse(user.Email!, isAdmin, user.ExternalProvider));
```

- [ ] **Step 6: Verify the app builds**

```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat: add GitHub login challenge and callback endpoints"
```

---

### Task 4: Add GitHub button to server-rendered OAuth login page

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`

- [ ] **Step 1: Add GitHub button and "or" divider to the OAuth login HTML**

In `OAuthEndpoints.cs`, in the `MapGet("/oauth/login", ...)` handler, the HTML template needs two additions:

1. A `gitHubEnabled` check based on configuration
2. A GitHub button section in the HTML

Update the handler to accept `IConfiguration` and conditionally render the button. Replace the existing `/oauth/login` GET handler:

In the handler parameters, add `IConfiguration configuration` alongside the existing params.

After the `var returnUrlEncoded = ...` line, add:

```csharp
var gitHubEnabled = !string.IsNullOrEmpty(configuration["GitHub:ClientId"]);
var gitHubHtml = gitHubEnabled ? $$"""
    <div class="or-divider"><span>or</span></div>
    <a href="/api/account/github-login?returnUrl={{returnUrlEncoded}}" class="btn-github">
        <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z"/></svg>
        Sign in with GitHub
    </a>
""" : "";
```

Add these CSS rules inside the existing `<style>` block:

```css
.or-divider { display: flex; align-items: center; gap: 12px; margin: 16px 0; color: #a1a1aa; font-size: 0.75rem; }
.or-divider::before, .or-divider::after { content: ''; flex: 1; height: 1px; background: #e5e7eb; }
.btn-github { display: flex; align-items: center; justify-content: center; gap: 8px; width: 100%; padding: 10px; background: #24292f; color: white; border: none; border-radius: 8px; cursor: pointer; font-size: 0.875rem; font-weight: 500; text-decoration: none; transition: background 0.15s; }
.btn-github:hover { background: #32383f; }
.btn-github:active { background: #1b1f23; }
```

Insert `{{gitHubHtml}}` in the HTML template after the closing `</form>` tag and before the `<p class="footer">` tag.

- [ ] **Step 2: Verify the app builds**

```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: add GitHub button to server-rendered OAuth login page"
```

---

### Task 5: Add GitHub button to Vue login page

**Files:**
- Modify: `fasolt.client/src/views/LoginView.vue`

- [ ] **Step 1: Add error handling for GitHub redirect errors**

The GitHub callback redirects to `/login?error=email_exists` (or similar) on failure. The login page needs to read and display these.

Update the `<script setup>` section — add `useRoute` and read the error query param:

```typescript
import { ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
```

Add after the existing ref declarations:

```typescript
const route = useRoute()

const gitHubErrors: Record<string, string> = {
  email_exists: 'An account with this email already exists. Please sign in with your password.',
  github_auth_failed: 'GitHub authentication failed. Please try again.',
  github_no_email: 'Could not retrieve your email from GitHub. Please ensure your GitHub email is public or verified.',
  account_creation_failed: 'Could not create your account. Please try again.',
}

onMounted(() => {
  const errorCode = route.query.error as string | undefined
  if (errorCode && gitHubErrors[errorCode]) {
    error.value = gitHubErrors[errorCode]
  }
})
```

- [ ] **Step 2: Add GitHub button and divider to the template**

After the closing `</form>` tag and before the closing `</CardContent>`, add:

```html
<div class="flex items-center gap-3 my-4">
  <div class="h-px flex-1 bg-border" />
  <span class="text-xs text-muted-foreground">or</span>
  <div class="h-px flex-1 bg-border" />
</div>
<a
  href="/api/account/github-login"
  class="flex w-full items-center justify-center gap-2 rounded-md bg-[#24292f] px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-[#32383f]"
>
  <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z"/></svg>
  Sign in with GitHub
</a>
```

- [ ] **Step 3: Verify frontend compiles**

```bash
cd fasolt.client && npx vue-tsc --noEmit
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: add GitHub sign-in button to login page"
```

---

### Task 6: Add GitHub button to Vue register page

**Files:**
- Modify: `fasolt.client/src/views/RegisterView.vue`

- [ ] **Step 1: Add GitHub button and divider to the register template**

After the closing `</form>` tag and before the closing `</CardContent>`, add the same button markup:

```html
<div class="flex items-center gap-3 my-4">
  <div class="h-px flex-1 bg-border" />
  <span class="text-xs text-muted-foreground">or</span>
  <div class="h-px flex-1 bg-border" />
</div>
<a
  href="/api/account/github-login"
  class="flex w-full items-center justify-center gap-2 rounded-md bg-[#24292f] px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-[#32383f]"
>
  <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor"><path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z"/></svg>
  Sign in with GitHub
</a>
```

- [ ] **Step 2: Verify frontend compiles**

```bash
cd fasolt.client && npx vue-tsc --noEmit
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: add GitHub sign-in button to register page"
```

---

### Task 7: Update auth store with externalProvider

**Files:**
- Modify: `fasolt.client/src/stores/auth.ts`

- [ ] **Step 1: Add externalProvider to User interface and expose it**

Update the `User` interface:

```typescript
interface User {
  email: string
  isAdmin: boolean
  externalProvider: string | null
}
```

Add a computed to the store:

```typescript
const isExternalAccount = computed(() => user.value?.externalProvider !== null)
```

Add `isExternalAccount` to the returned object.

- [ ] **Step 2: Verify frontend compiles**

```bash
cd fasolt.client && npx vue-tsc --noEmit
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat: add externalProvider to auth store"
```

---

### Task 8: Hide password-related settings for GitHub accounts

**Files:**
- Modify: `fasolt.client/src/views/SettingsView.vue`

- [ ] **Step 1: Read the settings view**

Read the current `SettingsView.vue` to understand the layout before modifying.

```bash
cat fasolt.client/src/views/SettingsView.vue
```

- [ ] **Step 2: Conditionally hide password sections**

In `SettingsView.vue`, import `isExternalAccount` from the auth store and wrap the "Change password" and "Change email" sections with `v-if="!auth.isExternalAccount"`. GitHub users can't change password (they don't have one) or change email (it comes from GitHub).

The exact edit depends on the current template structure — wrap the relevant sections.

- [ ] **Step 3: Verify frontend compiles**

```bash
cd fasolt.client && npx vue-tsc --noEmit
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat: hide password/email settings for GitHub-linked accounts"
```

---

### Task 9: Integration tests

**Files:**
- Create: `fasolt.Tests/GitHubAuthTests.cs`

- [ ] **Step 1: Read the existing test structure**

```bash
ls fasolt.Tests/ && cat fasolt.Tests/fasolt.Tests.csproj
```

Understand the test setup, WebApplicationFactory usage, and patterns before writing tests.

- [ ] **Step 2: Write integration tests**

Create `fasolt.Tests/GitHubAuthTests.cs` with tests for the key behaviors:

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Tests;

public class GitHubAuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GitHubAuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebApplicationFactory();
    }

    [Fact]
    public async Task GitHubLogin_WhenNotConfigured_Returns404()
    {
        // GitHub:ClientId is not set in test env
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/account/github-login");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GitHubCallback_WithoutExternalAuth_RedirectsToLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/api/account/github-callback");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/login", response.Headers.Location?.PathAndQuery);
    }

    [Fact]
    public async Task UserInfoResponse_IncludesExternalProvider()
    {
        // Create a GitHub user directly in the DB, log in via cookie, check /account/me
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var user = new AppUser
        {
            UserName = "ghuser@test.com",
            Email = "ghuser@test.com",
            EmailConfirmed = true,
            ExternalProvider = "GitHub",
            ExternalProviderId = "12345",
        };
        await userManager.CreateAsync(user, "TestPass1!");

        // Sign in as this user and check /account/me returns externalProvider
        var client = _factory.CreateAuthenticatedClient(user.Email, "TestPass1!");
        var response = await client.GetAsync("/api/account/me");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"externalProvider\":\"GitHub\"", json);
    }
}
```

Note: The exact test helper methods (`WithWebApplicationFactory`, `CreateAuthenticatedClient`) should follow the patterns established in the existing test project. Read the existing tests first and adapt accordingly.

- [ ] **Step 3: Run tests**

```bash
dotnet test fasolt.Tests/
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "test: add GitHub auth integration tests"
```

---

### Task 10: End-to-end Playwright testing

**Files:** None (browser-based testing)

- [ ] **Step 1: Start the full stack**

Ensure `./dev.sh` is running (or start backend + frontend manually).

- [ ] **Step 2: Add GitHub credentials to .env for local testing**

Create a GitHub OAuth App at https://github.com/settings/developers with:
- Homepage URL: `http://localhost:8080`
- Callback URL: `http://localhost:8080/signin-github`

Add to `.env`:
```
GitHub__ClientId=<your-client-id>
GitHub__ClientSecret=<your-client-secret>
```

Restart the backend.

- [ ] **Step 3: Test via Playwright**

Use Playwright MCP to verify:

1. Navigate to `/login` — verify "Sign in with GitHub" button is visible
2. Navigate to `/register` — verify "Sign in with GitHub" button is visible
3. Click the GitHub button on login — verify it redirects to `github.com/login/oauth/authorize`
4. Test the error state: navigate to `/login?error=email_exists` — verify the error message "An account with this email already exists" is displayed

- [ ] **Step 4: Test OAuth login page**

1. Navigate to `/oauth/login` — verify the GitHub button appears in the server-rendered page
2. Verify the button links to `/api/account/github-login?returnUrl=...`

- [ ] **Step 5: Test settings page hides password for GitHub users**

This requires a GitHub-linked account in the DB. If the dev seed user was created via GitHub login in step 3, navigate to `/settings` and verify password change and email change sections are hidden.

- [ ] **Step 6: Commit any test fixtures if created**

```bash
git add -A && git commit -m "test: verify GitHub login UI via Playwright"
```
