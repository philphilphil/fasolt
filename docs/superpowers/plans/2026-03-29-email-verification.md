# Email Verification & Registration Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Require email verification for new accounts while allowing immediate login to a verification gate page. Fix the broken password reset link.

**Architecture:** Add an `EmailVerificationMiddleware` that intercepts API requests from authenticated-but-unverified users, returning 403 for all endpoints except a small allowlist. Frontend router guard redirects unverified users to a gate page. Registration triggers a confirmation email with an absolute URL.

**Tech Stack:** ASP.NET Core Identity, Plunk email (existing), Vue 3 + Vue Router, shadcn-vue components

**Spec:** `docs/superpowers/specs/2026-03-29-email-verification-registration-design.md`

---

### Task 1: Add `App:BaseUrl` configuration and fix reset link

**Files:**
- Modify: `fasolt.Server/appsettings.json`
- Modify: `fasolt.Server/appsettings.Development.json`
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs:94-108`

- [ ] **Step 1: Add `App:BaseUrl` to appsettings.json**

```json
"App": {
  "BaseUrl": "https://fasolt.app"
}
```

- [ ] **Step 2: Add `App:BaseUrl` to appsettings.Development.json**

```json
"App": {
  "BaseUrl": "http://localhost:5173"
}
```

- [ ] **Step 3: Fix `ForgotPassword` to use absolute URL**

In `AccountEndpoints.cs`, change the `ForgotPassword` method to inject `IConfiguration` and build an absolute reset link:

```csharp
private static async Task<IResult> ForgotPassword(
    ForgotPasswordRequest request,
    UserManager<AppUser> userManager,
    IEmailSender<AppUser> emailSender,
    IConfiguration configuration)
{
    var user = await userManager.FindByEmailAsync(request.Email);
    if (user is not null && user.EmailConfirmed)
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var baseUrl = configuration["App:BaseUrl"]!;
        var resetLink = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(token)}";
        await emailSender.SendPasswordResetLinkAsync(user, request.Email, resetLink);
    }
    // Always return OK to prevent email enumeration
    return Results.Ok();
}
```

Note: Added `user.EmailConfirmed` check — only send reset emails to verified addresses.

- [ ] **Step 4: Fix `ChangeEmail` to use absolute URL**

In `AccountEndpoints.cs`, update the `ChangeEmail` method to also use `App:BaseUrl`:

```csharp
private static async Task<IResult> ChangeEmail(
    ChangeEmailRequest request,
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager,
    IEmailSender<AppUser> emailSender,
    IConfiguration configuration)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["currentPassword"] = ["Current password is incorrect."]
        });

    var token = await userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
    var baseUrl = configuration["App:BaseUrl"]!;
    var confirmLink = $"{baseUrl}/settings?action=confirm-email&token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(request.NewEmail)}";
    await emailSender.SendConfirmationLinkAsync(user, request.NewEmail, confirmLink);

    return Results.Ok(new { message = "Verification email sent to the new address." });
}
```

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/appsettings.json fasolt.Server/appsettings.Development.json fasolt.Server/Api/Endpoints/AccountEndpoints.cs
git commit -m "fix: use absolute URLs for email links via App:BaseUrl config"
```

---

### Task 2: Increase token lifetime to 24 hours

**Files:**
- Modify: `fasolt.Server/Program.cs:116-119`
- Modify: `fasolt.Server/Infrastructure/Services/PlunkEmailSender.cs:39`

- [ ] **Step 1: Update token lifespan in Program.cs**

Change line 118 from `FromHours(1)` to `FromHours(24)`:

```csharp
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(24);
});
```

- [ ] **Step 2: Update email template text in PlunkEmailSender.cs**

Change the reset email body from "1 hour" to "24 hours" on line 39:

```csharp
<p>This link expires in 24 hours. If you didn't request this, you can safely ignore this email.</p>
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Program.cs fasolt.Server/Infrastructure/Services/PlunkEmailSender.cs
git commit -m "chore: increase token lifetime to 24 hours"
```

---

### Task 3: Add `emailConfirmed` to `/account/me` response

**Files:**
- Modify: `fasolt.Server/Application/Dtos/AccountDtos.cs:3`
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs:29-37`

- [ ] **Step 1: Update `UserInfoResponse` DTO**

In `AccountDtos.cs`, add `EmailConfirmed` to the record:

```csharp
public record UserInfoResponse(string Email, bool IsAdmin, bool EmailConfirmed);
```

- [ ] **Step 2: Update `GetMe` endpoint to include EmailConfirmed**

In `AccountEndpoints.cs`, update the `GetMe` method:

```csharp
private static async Task<IResult> GetMe(
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
    return Results.Ok(new UserInfoResponse(user.Email!, isAdmin, user.EmailConfirmed));
}
```

- [ ] **Step 3: Update any other `UserInfoResponse` usages**

In `AccountEndpoints.cs`, the `ConfirmEmailChange` method also returns `UserInfoResponse`. Update it:

```csharp
return Results.Ok(new UserInfoResponse(user.Email!, isAdmin, user.EmailConfirmed));
```

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Application/Dtos/AccountDtos.cs fasolt.Server/Api/Endpoints/AccountEndpoints.cs
git commit -m "feat: add emailConfirmed to /account/me response"
```

---

### Task 4: Add resend verification endpoint

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`

- [ ] **Step 1: Register the new endpoint**

In the `MapAccountEndpoints` method, add after the existing endpoints:

```csharp
group.MapPost("/resend-verification", ResendVerification).RequireAuthorization().RequireRateLimiting("auth");
```

- [ ] **Step 2: Implement the `ResendVerification` method**

Add this method to `AccountEndpoints`:

```csharp
private static async Task<IResult> ResendVerification(
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager,
    IEmailSender<AppUser> emailSender,
    IConfiguration configuration)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    if (user.EmailConfirmed) return Results.Ok();

    var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
    var baseUrl = configuration["App:BaseUrl"]!;
    var confirmLink = $"{baseUrl}/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";
    await emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmLink);

    return Results.Ok();
}
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/Endpoints/AccountEndpoints.cs
git commit -m "feat: add POST /api/account/resend-verification endpoint"
```

---

### Task 5: Add confirm email endpoint

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`
- Modify: `fasolt.Server/Application/Dtos/AccountDtos.cs`

- [ ] **Step 1: Add the DTO**

In `AccountDtos.cs`, add:

```csharp
public record ConfirmEmailRequest(string UserId, string Token);
```

- [ ] **Step 2: Register the endpoint**

In `MapAccountEndpoints`, add:

```csharp
group.MapPost("/confirm-email", ConfirmEmail).RequireRateLimiting("auth");
```

Note: No `.RequireAuthorization()` — the user may click the link in a different browser/session where they're not logged in.

- [ ] **Step 3: Implement the `ConfirmEmail` method**

```csharp
private static async Task<IResult> ConfirmEmail(
    ConfirmEmailRequest request,
    UserManager<AppUser> userManager)
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

    return Results.Ok();
}
```

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/AccountEndpoints.cs fasolt.Server/Application/Dtos/AccountDtos.cs
git commit -m "feat: add POST /api/account/confirm-email endpoint"
```

---

### Task 6: Add email verification middleware

**Files:**
- Create: `fasolt.Server/Api/Middleware/EmailVerificationMiddleware.cs`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Create the middleware**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Middleware;

public class EmailVerificationMiddleware(RequestDelegate next)
{
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/account/me",
        "/api/account/resend-verification",
        "/api/account/logout",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only check API routes (skip static files, OAuth pages, etc.)
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Skip paths that unverified users need access to
        if (AllowedPaths.Contains(path))
        {
            await next(context);
            return;
        }

        // Skip if not authenticated
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            await next(context);
            return;
        }

        var userManager = context.RequestServices.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByIdAsync(userId);
        if (user is not null && !user.EmailConfirmed)
        {
            context.Response.StatusCode = 403;
            return;
        }

        await next(context);
    }
}
```

- [ ] **Step 2: Register the middleware in Program.cs**

Add after `app.UseAuthorization();` (find the line and add right after):

```csharp
app.UseMiddleware<EmailVerificationMiddleware>();
```

This must be AFTER `UseAuthentication` and `UseAuthorization` so that `context.User` is populated.

- [ ] **Step 3: Verify the middleware placement**

Run: `dotnet build fasolt.Server`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Middleware/EmailVerificationMiddleware.cs fasolt.Server/Program.cs
git commit -m "feat: add middleware to block unverified users from API"
```

---

### Task 7: Gate OAuth authorize for unverified users

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs:118-127`

- [ ] **Step 1: Add email verification check to OAuth authorize**

In the `/oauth/authorize` endpoint, after the authentication check succeeds (line 122) and before the consent check, add a verification gate:

```csharp
// Authorization Endpoint
app.MapGet("/oauth/authorize", async (HttpContext context, AppDbContext db, IDataProtectionProvider dataProtection, UserManager<AppUser> userManager) =>
{
    var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
    if (result?.Principal is null)
    {
        var returnUrl = context.Request.QueryString.Value;
        return Results.Redirect($"/oauth/login?returnUrl={Uri.EscapeDataString("/oauth/authorize" + returnUrl)}");
    }

    // Block unverified users from authorizing OAuth clients
    var userId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var appUser = await userManager.FindByIdAsync(userId);
    if (appUser is not null && !appUser.EmailConfirmed)
    {
        return Results.Redirect("/verify-email");
    }

    var user = result.Principal;
    // ... rest of the method unchanged
```

Note: Only the first few lines change. Add `UserManager<AppUser> userManager` to the parameter list and the verification check after authentication. The `var user = result.Principal;` line and everything after it remains identical.

- [ ] **Step 2: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs
git commit -m "feat: block unverified users from OAuth authorization"
```

---

### Task 8: Add `emailConfirmed` to frontend auth store and send verification on register

**Files:**
- Modify: `fasolt.client/src/stores/auth.ts`

- [ ] **Step 1: Update the User interface and add resendVerification**

```typescript
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { apiFetch } from '@/api/client'

interface User {
  email: string
  isAdmin: boolean
  emailConfirmed: boolean
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const isLoading = ref(true)
  const isAuthenticated = computed(() => user.value !== null)
  const isAdmin = computed(() => user.value?.isAdmin ?? false)
  const isEmailConfirmed = computed(() => user.value?.emailConfirmed ?? false)

  async function fetchUser() {
    try {
      user.value = await apiFetch<User>('/account/me')
    } catch {
      user.value = null
    } finally {
      isLoading.value = false
    }
  }

  async function register(email: string, password: string) {
    await apiFetch('/identity/register', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    await login(email, password, false)
    // Send verification email after registration
    await apiFetch('/account/resend-verification', { method: 'POST' })
  }

  async function login(email: string, password: string, rememberMe: boolean) {
    const params = new URLSearchParams({
      useCookies: 'true',
      useSessionCookies: rememberMe ? 'false' : 'true',
    })
    await apiFetch(`/identity/login?${params}`, {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    })
    await fetchUser()
  }

  async function logout() {
    try {
      await apiFetch('/account/logout', {
        method: 'POST',
      })
    } finally {
      user.value = null
    }
  }

  async function changeEmail(newEmail: string, currentPassword: string) {
    const result = await apiFetch<User>('/account/email', {
      method: 'PUT',
      body: JSON.stringify({ newEmail, currentPassword }),
    })
    user.value = result
    return result
  }

  async function changePassword(currentPassword: string, newPassword: string) {
    await apiFetch('/account/password', {
      method: 'PUT',
      body: JSON.stringify({ currentPassword, newPassword }),
    })
  }

  async function forgotPassword(email: string) {
    await apiFetch('/account/forgot-password', {
      method: 'POST',
      body: JSON.stringify({ email }),
    })
  }

  async function resetPassword(email: string, token: string, newPassword: string) {
    await apiFetch('/account/reset-password', {
      method: 'POST',
      body: JSON.stringify({ email, token, newPassword }),
    })
  }

  async function resendVerification() {
    await apiFetch('/account/resend-verification', {
      method: 'POST',
    })
  }

  return {
    user,
    isLoading,
    isAuthenticated,
    isAdmin,
    isEmailConfirmed,
    fetchUser,
    register,
    login,
    logout,
    changeEmail,
    changePassword,
    forgotPassword,
    resetPassword,
    resendVerification,
  }
})
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/stores/auth.ts
git commit -m "feat: add emailConfirmed to auth store, send verification on register"
```

---

### Task 9: Create `EmailVerificationView.vue` gate page

**Files:**
- Create: `fasolt.client/src/views/EmailVerificationView.vue`

- [ ] **Step 1: Create the view**

```vue
<script setup lang="ts">
import { ref } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

const auth = useAuthStore()
const router = useRouter()

const resending = ref(false)
const resent = ref(false)
const cooldown = ref(0)
let timer: ReturnType<typeof setInterval> | null = null

async function handleResend() {
  resending.value = true
  try {
    await auth.resendVerification()
    resent.value = true
    cooldown.value = 60
    timer = setInterval(() => {
      cooldown.value--
      if (cooldown.value <= 0 && timer) {
        clearInterval(timer)
        timer = null
        resent.value = false
      }
    }, 1000)
  } finally {
    resending.value = false
  }
}

async function handleLogout() {
  await auth.logout()
  router.push('/login')
}
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Verify your email</CardTitle>
    </CardHeader>
    <CardContent class="flex flex-col items-center gap-4">
      <p class="text-center text-xs text-muted-foreground">
        We sent a verification link to <strong class="text-foreground">{{ auth.user?.email }}</strong>.
        Check your inbox and click the link to activate your account.
      </p>
      <Button
        variant="outline"
        class="w-full"
        :disabled="resending || cooldown > 0"
        @click="handleResend"
      >
        <template v-if="cooldown > 0">Resend in {{ cooldown }}s</template>
        <template v-else-if="resending">Sending...</template>
        <template v-else>Resend verification email</template>
      </Button>
      <button class="text-xs text-muted-foreground hover:underline" @click="handleLogout">
        Log out
      </button>
    </CardContent>
  </Card>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/views/EmailVerificationView.vue
git commit -m "feat: add email verification gate page"
```

---

### Task 10: Create `ConfirmEmailView.vue` landing page

**Files:**
- Create: `fasolt.client/src/views/ConfirmEmailView.vue`

- [ ] **Step 1: Create the view**

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { apiFetch, isApiError } from '@/api/client'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const loading = ref(true)
const error = ref('')

onMounted(async () => {
  const userId = route.query.userId as string
  const token = route.query.token as string

  if (!userId || !token) {
    error.value = 'Invalid confirmation link.'
    loading.value = false
    return
  }

  try {
    await apiFetch('/account/confirm-email', {
      method: 'POST',
      body: JSON.stringify({ userId, token }),
    })
    // Refresh user state so emailConfirmed updates
    await auth.fetchUser()
    // Redirect to app after short delay
    setTimeout(() => router.push('/study'), 1500)
  } catch (e) {
    if (isApiError(e) && e.errors) {
      error.value = Object.values(e.errors).flat().join(' ')
    } else {
      error.value = 'Something went wrong. Please try again.'
    }
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Email confirmation</CardTitle>
    </CardHeader>
    <CardContent>
      <div v-if="loading" class="text-center text-xs text-muted-foreground">
        Confirming your email...
      </div>
      <div v-else-if="error" class="flex flex-col items-center gap-4">
        <p class="text-center text-xs text-destructive">{{ error }}</p>
        <Button variant="outline" class="w-full" @click="router.push('/verify-email')">
          Request a new link
        </Button>
      </div>
      <div v-else class="flex flex-col items-center gap-2">
        <p class="text-center text-xs text-muted-foreground">
          Your email has been verified. Redirecting...
        </p>
      </div>
    </CardContent>
  </Card>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/views/ConfirmEmailView.vue
git commit -m "feat: add email confirmation landing page"
```

---

### Task 11: Add routes and router guard for email verification

**Files:**
- Modify: `fasolt.client/src/router/index.ts`

- [ ] **Step 1: Add the new routes and update the guard**

```typescript
import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import StudyView from '@/views/StudyView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    // Auth routes (public)
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { public: true, authRedirect: true },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/RegisterView.vue'),
      meta: { public: true, authRedirect: true },
    },
    {
      path: '/forgot-password',
      name: 'forgot-password',
      component: () => import('@/views/ForgotPasswordView.vue'),
      meta: { public: true },
    },
    {
      path: '/reset-password',
      name: 'reset-password',
      component: () => import('@/views/ResetPasswordView.vue'),
      meta: { public: true },
    },
    {
      path: '/verify-email',
      name: 'verify-email',
      component: () => import('@/views/EmailVerificationView.vue'),
      meta: { requiresAuth: true, skipVerificationCheck: true },
    },
    {
      path: '/confirm-email',
      name: 'confirm-email',
      component: () => import('@/views/ConfirmEmailView.vue'),
      meta: { public: true },
    },
    {
      path: '/oauth/consent',
      name: 'oauth-consent',
      component: () => import('@/views/OAuthConsentView.vue'),
      meta: { public: true },
    },
    // Landing page (public)
    {
      path: '/',
      name: 'landing',
      component: () => import('@/views/LandingView.vue'),
      meta: { public: true, authRedirect: true, landing: true },
    },
    {
      path: '/algorithm',
      name: 'algorithm',
      component: () => import('@/views/AlgorithmView.vue'),
      meta: { public: true },
    },
    {
      path: '/privacy',
      name: 'privacy',
      component: () => import('@/views/PrivacyPolicyView.vue'),
      meta: { public: true, landing: true },
    },
    // App routes (require auth)
    { path: '/study', name: 'study', component: StudyView },
    { path: '/sources', name: 'sources', component: () => import('@/views/SourcesView.vue') },
    { path: '/cards', name: 'cards', component: () => import('@/views/CardsView.vue') },
    { path: '/cards/:id', name: 'card-detail', component: () => import('@/views/CardDetailView.vue') },
    { path: '/decks', name: 'decks', component: () => import('@/views/DecksView.vue') },
    { path: '/decks/:id', name: 'deck-detail', component: () => import('@/views/DeckDetailView.vue') },
    { path: '/decks/:id/snapshots', name: 'deck-snapshots', component: () => import('@/views/DeckSnapshotsView.vue') },
    { path: '/review/:deckId?', name: 'review', component: () => import('@/views/ReviewView.vue') },
    { path: '/mcp-setup', name: 'mcp', component: () => import('@/views/McpView.vue') },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
    { path: '/admin', name: 'admin', component: () => import('@/views/AdminView.vue'), meta: { requiresAdmin: true } },
    { path: '/dashboard', redirect: '/study' },
    // Catch-all 404
    { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('@/views/NotFoundView.vue'), meta: { public: true } },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()

  // Wait for initial auth check
  if (auth.isLoading) {
    await auth.fetchUser()
  }

  const isPublic = to.meta.public === true

  if (!isPublic && !auth.isAuthenticated) {
    return { name: 'login' }
  }

  if (to.meta.authRedirect && auth.isAuthenticated) {
    return { name: 'study' }
  }

  // Redirect unverified users to verification gate
  if (auth.isAuthenticated && !auth.isEmailConfirmed && !isPublic && to.meta.skipVerificationCheck !== true) {
    return { name: 'verify-email' }
  }

  // Don't let verified users visit the verification page
  if (to.name === 'verify-email' && auth.isEmailConfirmed) {
    return { name: 'study' }
  }

  if (to.meta.requiresAdmin) {
    if (!auth.isAdmin) {
      return { name: 'study' }
    }
  }
})

export default router
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/router/index.ts
git commit -m "feat: add verification routes and router guard for unverified users"
```

---

### Task 12: Migrate existing users to EmailConfirmed = true

**Files:**
- Create: new EF Core migration

- [ ] **Step 1: Create a SQL migration to set existing users as verified**

```bash
cd /Users/phil/Projects/fasolt && dotnet ef migrations add SetExistingUsersEmailConfirmed --project fasolt.Server
```

- [ ] **Step 2: Edit the migration to update existing users**

In the newly created migration file (in `fasolt.Server/Infrastructure/Data/Migrations/`), replace the `Up` method contents:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql("UPDATE \"AspNetUsers\" SET \"EmailConfirmed\" = true WHERE \"EmailConfirmed\" = false;");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Intentionally empty — we can't know which users were previously unconfirmed
}
```

Remove any auto-generated schema changes from the migration (the model snapshot may need cleanup if EF generates unnecessary diffs).

- [ ] **Step 3: Apply the migration locally**

Run: `dotnet ef database update --project fasolt.Server`
Expected: Migration applies successfully

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat: migrate existing users to EmailConfirmed = true"
```

---

### Task 13: End-to-end testing with Playwright

**Files:** None (testing only)

- [ ] **Step 1: Start the full stack**

Ensure `./dev.sh` is running (or start backend + frontend manually).

- [ ] **Step 2: Test new user registration flow**

1. Navigate to `/register`
2. Register with a new email and password
3. Verify you're redirected to `/verify-email` gate page
4. Verify the gate page shows the email and resend button
5. Check backend logs for the verification link (DevEmailSender logs it)
6. Click the verification link (navigate to the `/confirm-email?userId=...&token=...` URL from the logs)
7. Verify you're redirected to `/study`
8. Verify you can access all app routes normally

- [ ] **Step 3: Test unverified user is blocked**

1. Register a new user (don't verify)
2. Try navigating to `/study`, `/cards`, `/decks` — verify redirect to `/verify-email`
3. Try calling API endpoints directly (e.g. `GET /api/cards`) — verify 403 response

- [ ] **Step 4: Test login with unverified account**

1. Log out
2. Log back in with the unverified account
3. Verify you're redirected to `/verify-email` again

- [ ] **Step 5: Test password reset for unverified user**

1. Go to `/forgot-password`
2. Enter the unverified user's email
3. Verify no reset email is sent (check backend logs — should be silent)
4. The UI still shows "If an account exists..." message (no enumeration)

- [ ] **Step 6: Test password reset for verified user**

1. Go to `/forgot-password`
2. Enter a verified user's email (e.g. `dev@fasolt.local`)
3. Check backend logs for the reset link
4. Verify the link is an absolute URL (starts with `http://localhost:5173/`)
5. Navigate to the reset link, set a new password, verify it works

- [ ] **Step 7: Test resend cooldown**

1. On the `/verify-email` page, click "Resend verification email"
2. Verify the button shows a 60-second countdown
3. Verify the button is disabled during cooldown

- [ ] **Step 8: Commit test results**

No files to commit — this is manual/Playwright verification.
