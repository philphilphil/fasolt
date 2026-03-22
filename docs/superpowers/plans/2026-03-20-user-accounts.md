# User Accounts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement user registration, login/logout, password reset, and profile settings (US-1.1 through US-1.4).

**Architecture:** Extend ASP.NET Core Identity with a custom `AppUser` entity. Keep `MapIdentityApi` for register/login/logout. Add custom `AccountEndpoints` for profile management and password reset. Build Vue 3 auth pages with Pinia store and route guards.

**Tech Stack:** .NET 10, ASP.NET Core Identity, EF Core + Npgsql, Vue 3, TypeScript, Pinia, Vue Router, shadcn-vue, Tailwind CSS 3

---

## File Map

### Backend — New Files
- `fasolt.Server/Domain/Entities/AppUser.cs` — Custom Identity user with DisplayName
- `fasolt.Server/Api/Endpoints/AccountEndpoints.cs` — /api/account endpoints (me, profile, email, password, forgot-password, reset-password)
- `fasolt.Server/Application/Dtos/AccountDtos.cs` — Request/response DTOs for account endpoints
- `fasolt.Server/Infrastructure/Services/DevEmailSender.cs` — Console-logging IEmailSender for development

### Backend — Modified Files
- `fasolt.Server/Infrastructure/Data/AppDbContext.cs` — Change to `IdentityDbContext<AppUser>`
- `fasolt.Server/Program.cs` — Identity options, cookie config, new endpoint mapping, email sender registration

### Frontend — New Files
- `fasolt.client/src/stores/auth.ts` — Pinia auth store
- `fasolt.client/src/views/LoginView.vue` — Login page
- `fasolt.client/src/views/RegisterView.vue` — Registration page
- `fasolt.client/src/views/ForgotPasswordView.vue` — Forgot password page
- `fasolt.client/src/views/ResetPasswordView.vue` — Reset password page
- `fasolt.client/src/layouts/AuthLayout.vue` — Centered layout for auth pages

### Frontend — Modified Files
- `fasolt.client/src/api/client.ts` — Add credentials, structured error parsing
- `fasolt.client/src/router/index.ts` — Auth routes, navigation guards
- `fasolt.client/src/components/TopBar.vue` — User dropdown menu
- `fasolt.client/src/views/SettingsView.vue` — Profile settings form
- `fasolt.client/src/App.vue` — Conditional layout based on auth state

---

## Task 1: AppUser Entity and DbContext Update

**Files:**
- Create: `fasolt.Server/Domain/Entities/AppUser.cs`
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs`

- [ ] **Step 1: Create AppUser entity**

```csharp
// fasolt.Server/Domain/Entities/AppUser.cs
using Microsoft.AspNetCore.Identity;

namespace Fasolt.Server.Domain.Entities;

public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
```

- [ ] **Step 2: Update AppDbContext to use AppUser**

Change `AppDbContext` from `IdentityDbContext` to `IdentityDbContext<AppUser>`:

```csharp
// fasolt.Server/Infrastructure/Data/AppDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
```

- [ ] **Step 3: Update Program.cs to use AppUser**

Replace `IdentityUser` with `AppUser` in both `AddIdentityApiEndpoints` and `MapIdentityApi`:

```csharp
// In Program.cs, change:
//   .AddIdentityApiEndpoints<IdentityUser>()
// to:
//   .AddIdentityApiEndpoints<AppUser>()
// and:
//   .MapIdentityApi<IdentityUser>()
// to:
//   .MapIdentityApi<AppUser>()
```

Add the using:
```csharp
using Fasolt.Server.Domain.Entities;
```

- [ ] **Step 4: Create EF migration**

Run:
```bash
cd fasolt.Server && dotnet ef migrations add AddAppUser
```

- [ ] **Step 5: Apply migration and verify**

Run:
```bash
cd fasolt.Server && dotnet ef database update
```

Then verify the app starts:
```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Domain/Entities/AppUser.cs fasolt.Server/Infrastructure/Data/AppDbContext.cs fasolt.Server/Program.cs fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat: add AppUser entity extending IdentityUser with DisplayName"
```

---

## Task 2: Identity Configuration (Password Policy, Cookies)

**Files:**
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Configure Identity options and cookie**

Add Identity options configuration right after `AddIdentityApiEndpoints<AppUser>()`:

```csharp
builder.Services
    .AddIdentityApiEndpoints<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = false;

        options.User.RequireUniqueEmail = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromDays(1);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
});
```

- [ ] **Step 2: Configure password reset token lifetime**

Add after the cookie configuration:

```csharp
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(1);
});
```

Add using:
```csharp
using Microsoft.AspNetCore.Identity;
```

(Note: this using may already exist — only add if missing.)

- [ ] **Step 3: Verify app still starts**

Run:
```bash
cd fasolt.Server && dotnet build && dotnet run &
sleep 3 && curl -s http://localhost:5000/api/health && kill %1
```

Expected: `{"status":"healthy"}`

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Program.cs
git commit -m "feat: configure Identity password policy, cookie settings, token lifetime"
```

---

## Task 3: DevEmailSender and Account DTOs

**Files:**
- Create: `fasolt.Server/Infrastructure/Services/DevEmailSender.cs`
- Create: `fasolt.Server/Application/Dtos/AccountDtos.cs`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Create DevEmailSender**

```csharp
// fasolt.Server/Infrastructure/Services/DevEmailSender.cs
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Infrastructure.Services;

public class DevEmailSender : IEmailSender<AppUser>
{
    private readonly ILogger<DevEmailSender> _logger;

    public DevEmailSender(ILogger<DevEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendConfirmationLinkAsync(AppUser user, string email, string confirmationLink)
    {
        _logger.LogInformation("Confirmation link for {Email}: {Link}", email, confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(AppUser user, string email, string resetLink)
    {
        _logger.LogInformation("Password reset link for {Email}: {Link}", email, resetLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(AppUser user, string email, string resetCode)
    {
        _logger.LogInformation("Password reset code for {Email}: {Code}", email, resetCode);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Register DevEmailSender in Program.cs**

Add after the Identity configuration block:

```csharp
builder.Services.AddTransient<IEmailSender<AppUser>, DevEmailSender>();
```

Add using:
```csharp
using Fasolt.Server.Infrastructure.Services;
```

- [ ] **Step 3: Create Account DTOs**

```csharp
// fasolt.Server/Application/Dtos/AccountDtos.cs
namespace Fasolt.Server.Application.Dtos;

public record UserInfoResponse(string Email, string? DisplayName);

public record UpdateProfileRequest(string? DisplayName);

public record ChangeEmailRequest(string NewEmail, string CurrentPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);
```

- [ ] **Step 4: Verify build**

Run:
```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Infrastructure/Services/DevEmailSender.cs fasolt.Server/Application/Dtos/AccountDtos.cs fasolt.Server/Program.cs
git commit -m "feat: add DevEmailSender and account DTOs"
```

---

## Task 4: Account Endpoints

**Files:**
- Create: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Create AccountEndpoints**

```csharp
// fasolt.Server/Api/Endpoints/AccountEndpoints.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/account");

        group.MapGet("/me", GetMe).RequireAuthorization();
        group.MapPut("/profile", UpdateProfile).RequireAuthorization();
        group.MapPut("/email", ChangeEmail).RequireAuthorization();
        group.MapPut("/password", ChangePassword).RequireAuthorization();
        group.MapPost("/forgot-password", ForgotPassword);
        group.MapPost("/reset-password", ResetPassword);
    }

    private static async Task<IResult> GetMe(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        return Results.Ok(new UserInfoResponse(user.Email!, user.DisplayName));
    }

    private static async Task<IResult> UpdateProfile(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        user.DisplayName = request.DisplayName;
        var result = await userManager.UpdateAsync(user);

        if (!result.Succeeded)
            return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

        return Results.Ok(new UserInfoResponse(user.Email!, user.DisplayName));
    }

    private static async Task<IResult> ChangeEmail(
        ChangeEmailRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["Current password is incorrect."]
            });

        var existingUser = await userManager.FindByEmailAsync(request.NewEmail);
        if (existingUser is not null && existingUser.Id != user.Id)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["newEmail"] = ["This email is already in use."]
            });

        var token = await userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
        var result = await userManager.ChangeEmailAsync(user, request.NewEmail, token);

        if (!result.Succeeded)
            return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

        user.UserName = request.NewEmail;
        await userManager.UpdateAsync(user);

        return Results.Ok(new UserInfoResponse(user.Email!, user.DisplayName));
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
            return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));

        return Results.Ok();
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request,
        UserManager<AppUser> userManager,
        IEmailSender<AppUser> emailSender)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = $"/reset-password?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(token)}";
            await emailSender.SendPasswordResetLinkAsync(user, request.Email, resetLink);
        }

        // Always return OK to prevent email enumeration
        return Results.Ok();
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Invalid or expired reset link."]
            });

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);

        if (!result.Succeeded)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Invalid or expired reset link."]
            });

        return Results.Ok();
    }
}
```

- [ ] **Step 2: Register endpoints in Program.cs**

Add before `app.Run()`:

```csharp
app.MapAccountEndpoints();
```

- [ ] **Step 3: Verify build**

Run:
```bash
cd fasolt.Server && dotnet build
```

- [ ] **Step 4: Smoke test the register and me endpoints**

Start the server and test:
```bash
cd fasolt.Server && dotnet run &
sleep 3

# Register
curl -s -X POST http://localhost:5000/api/identity/register \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"TestPass1"}' -w "\n%{http_code}"

# Login (cookie)
curl -s -c /tmp/cookies.txt -X POST "http://localhost:5000/api/identity/login?useCookies=true" \
  -H "Content-Type: application/json" \
  -d '{"email":"test@test.com","password":"TestPass1"}' -w "\n%{http_code}"

# Get me
curl -s -b /tmp/cookies.txt http://localhost:5000/api/account/me -w "\n%{http_code}"

kill %1
```

Expected: register returns 200, login sets cookie, /me returns `{"email":"test@test.com","displayName":null}`.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Api/Endpoints/AccountEndpoints.cs fasolt.Server/Program.cs
git commit -m "feat: add account endpoints (me, profile, email, password, forgot/reset)"
```

---

## Task 5: API Client Updates

**Files:**
- Modify: `fasolt.client/src/api/client.ts`

- [ ] **Step 1: Update apiFetch with credentials and error parsing**

Replace the contents of `src/api/client.ts`:

```typescript
const BASE_URL = '/api'

export interface ApiError {
  status: number
  errors: Record<string, string[]>
}

export function isApiError(error: unknown): error is ApiError {
  return typeof error === 'object' && error !== null && 'status' in error && 'errors' in error
}

export async function apiFetch<T>(path: string, options?: RequestInit): Promise<T> {
  const response = await fetch(`${BASE_URL}${path}`, {
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...options?.headers,
    },
    ...options,
  })

  if (!response.ok) {
    let errors: Record<string, string[]> = {}
    try {
      const body = await response.json()
      // ASP.NET Identity API returns { errors: { ... } } or { type, title, errors: { ... } }
      if (body.errors) {
        errors = body.errors
      }
    } catch {
      // No JSON body
    }
    throw { status: response.status, errors } as ApiError
  }

  const text = await response.text()
  if (!text) return undefined as T

  return JSON.parse(text)
}
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/api/client.ts
git commit -m "feat: add credentials and structured error parsing to API client"
```

---

## Task 6: Auth Pinia Store

**Files:**
- Create: `fasolt.client/src/stores/auth.ts`

- [ ] **Step 1: Create auth store**

```typescript
// fasolt.client/src/stores/auth.ts
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { apiFetch, type ApiError } from '@/api/client'

interface User {
  email: string
  displayName: string | null
}

export const useAuthStore = defineStore('auth', () => {
  const user = ref<User | null>(null)
  const isLoading = ref(true)
  const isAuthenticated = computed(() => user.value !== null)

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
      await apiFetch('/identity/logout', {
        method: 'POST',
        body: JSON.stringify({}),
      })
    } finally {
      user.value = null
    }
  }

  async function updateProfile(displayName: string | null) {
    const result = await apiFetch<User>('/account/profile', {
      method: 'PUT',
      body: JSON.stringify({ displayName }),
    })
    user.value = result
    return result
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

  return {
    user,
    isLoading,
    isAuthenticated,
    fetchUser,
    register,
    login,
    logout,
    updateProfile,
    changeEmail,
    changePassword,
    forgotPassword,
    resetPassword,
  }
})
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/stores/auth.ts
git commit -m "feat: add Pinia auth store with full account management"
```

---

## Task 7: Auth Layout and Login View

**Files:**
- Create: `fasolt.client/src/layouts/AuthLayout.vue`
- Create: `fasolt.client/src/views/LoginView.vue`

- [ ] **Step 1: Create AuthLayout**

```vue
<!-- fasolt.client/src/layouts/AuthLayout.vue -->
<script setup lang="ts">
import { useDarkMode } from '@/composables/useDarkMode'
useDarkMode()
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-background px-4">
    <div class="w-full max-w-sm">
      <div class="mb-8 text-center">
        <span class="font-mono text-lg font-bold text-foreground tracking-tight">fasolt</span>
      </div>
      <slot />
    </div>
  </div>
</template>
```

- [ ] **Step 2: Create LoginView**

```vue
<!-- fasolt.client/src/views/LoginView.vue -->
<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'

const router = useRouter()
const auth = useAuthStore()

const email = ref('')
const password = ref('')
const rememberMe = ref(false)
const error = ref('')
const loading = ref(false)

async function handleSubmit() {
  error.value = ''
  loading.value = true
  try {
    await auth.login(email.value, password.value, rememberMe.value)
    router.push('/')
  } catch (e) {
    if (isApiError(e) && e.status === 401) {
      error.value = 'Invalid email or password.'
    } else {
      error.value = 'Something went wrong. Please try again.'
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <Card>
    <CardHeader>
      <CardTitle class="text-center text-lg">Log in</CardTitle>
    </CardHeader>
    <CardContent>
      <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
        <div v-if="error" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {{ error }}
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="email" class="text-sm font-medium">Email</label>
          <Input id="email" v-model="email" type="email" required autocomplete="email" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="password" class="text-sm font-medium">Password</label>
          <Input id="password" v-model="password" type="password" required autocomplete="current-password" />
        </div>
        <label class="flex items-center gap-2 text-sm">
          <input v-model="rememberMe" type="checkbox" class="rounded" />
          Remember me
        </label>
        <Button type="submit" class="w-full" :disabled="loading">
          {{ loading ? 'Logging in…' : 'Log in' }}
        </Button>
        <div class="flex flex-col items-center gap-1 text-sm">
          <RouterLink to="/register" class="text-accent hover:underline">Create an account</RouterLink>
          <RouterLink to="/forgot-password" class="text-muted-foreground hover:underline">Forgot password?</RouterLink>
        </div>
      </form>
    </CardContent>
  </Card>
</template>
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/layouts/AuthLayout.vue fasolt.client/src/views/LoginView.vue
git commit -m "feat: add AuthLayout and LoginView"
```

---

## Task 8: Register View

**Files:**
- Create: `fasolt.client/src/views/RegisterView.vue`

- [ ] **Step 1: Create RegisterView**

```vue
<!-- fasolt.client/src/views/RegisterView.vue -->
<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'

const router = useRouter()
const auth = useAuthStore()

const email = ref('')
const password = ref('')
const confirmPassword = ref('')
const errors = ref<string[]>([])
const loading = ref(false)

const passwordRules = computed(() => [
  { label: 'At least 8 characters', valid: password.value.length >= 8 },
  { label: 'Uppercase letter', valid: /[A-Z]/.test(password.value) },
  { label: 'Lowercase letter', valid: /[a-z]/.test(password.value) },
  { label: 'Number', valid: /\d/.test(password.value) },
])

const passwordsMatch = computed(() => password.value === confirmPassword.value)

const canSubmit = computed(
  () =>
    email.value &&
    passwordRules.value.every((r) => r.valid) &&
    passwordsMatch.value &&
    !loading.value,
)

async function handleSubmit() {
  errors.value = []
  loading.value = true
  try {
    await auth.register(email.value, password.value)
    router.push('/')
  } catch (e) {
    if (isApiError(e) && e.errors) {
      errors.value = Object.values(e.errors).flat()
    } else {
      errors.value = ['Something went wrong. Please try again.']
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <Card>
    <CardHeader>
      <CardTitle class="text-center text-lg">Create account</CardTitle>
    </CardHeader>
    <CardContent>
      <form class="flex flex-col gap-4" @submit.prevent="handleSubmit">
        <div v-if="errors.length" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          <p v-for="err in errors" :key="err">{{ err }}</p>
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="email" class="text-sm font-medium">Email</label>
          <Input id="email" v-model="email" type="email" required autocomplete="email" />
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="password" class="text-sm font-medium">Password</label>
          <Input id="password" v-model="password" type="password" required autocomplete="new-password" />
          <ul v-if="password" class="mt-1 space-y-0.5 text-xs">
            <li v-for="rule in passwordRules" :key="rule.label" :class="rule.valid ? 'text-green-600' : 'text-muted-foreground'">
              {{ rule.valid ? '✓' : '○' }} {{ rule.label }}
            </li>
          </ul>
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="confirm-password" class="text-sm font-medium">Confirm password</label>
          <Input id="confirm-password" v-model="confirmPassword" type="password" required autocomplete="new-password" />
          <p v-if="confirmPassword && !passwordsMatch" class="text-xs text-destructive">Passwords do not match.</p>
        </div>
        <Button type="submit" class="w-full" :disabled="!canSubmit">
          {{ loading ? 'Creating account…' : 'Create account' }}
        </Button>
        <p class="text-center text-sm">
          Already have an account? <RouterLink to="/login" class="text-accent hover:underline">Log in</RouterLink>
        </p>
      </form>
    </CardContent>
  </Card>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/views/RegisterView.vue
git commit -m "feat: add RegisterView with client-side password validation"
```

---

## Task 9: Forgot Password and Reset Password Views

**Files:**
- Create: `fasolt.client/src/views/ForgotPasswordView.vue`
- Create: `fasolt.client/src/views/ResetPasswordView.vue`

- [ ] **Step 1: Create ForgotPasswordView**

```vue
<!-- fasolt.client/src/views/ForgotPasswordView.vue -->
<script setup lang="ts">
import { ref } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()

const email = ref('')
const sent = ref(false)
const loading = ref(false)

async function handleSubmit() {
  loading.value = true
  try {
    await auth.forgotPassword(email.value)
  } finally {
    sent.value = true
    loading.value = false
  }
}
</script>

<template>
  <Card>
    <CardHeader>
      <CardTitle class="text-center text-lg">Reset password</CardTitle>
    </CardHeader>
    <CardContent>
      <template v-if="sent">
        <p class="text-center text-sm text-muted-foreground">
          If an account exists for <strong>{{ email }}</strong>, we sent a password reset link.
        </p>
        <div class="mt-4 text-center">
          <RouterLink to="/login" class="text-sm text-accent hover:underline">Back to login</RouterLink>
        </div>
      </template>
      <form v-else class="flex flex-col gap-4" @submit.prevent="handleSubmit">
        <p class="text-sm text-muted-foreground">Enter your email and we'll send you a reset link.</p>
        <div class="flex flex-col gap-1.5">
          <label for="email" class="text-sm font-medium">Email</label>
          <Input id="email" v-model="email" type="email" required autocomplete="email" />
        </div>
        <Button type="submit" class="w-full" :disabled="loading">
          {{ loading ? 'Sending…' : 'Send reset link' }}
        </Button>
        <p class="text-center text-sm">
          <RouterLink to="/login" class="text-muted-foreground hover:underline">Back to login</RouterLink>
        </p>
      </form>
    </CardContent>
  </Card>
</template>
```

- [ ] **Step 2: Create ResetPasswordView**

```vue
<!-- fasolt.client/src/views/ResetPasswordView.vue -->
<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

const emailParam = (route.query.email as string) || ''
const tokenParam = (route.query.token as string) || ''

const password = ref('')
const confirmPassword = ref('')
const error = ref('')
const success = ref(false)
const loading = ref(false)

const passwordRules = computed(() => [
  { label: 'At least 8 characters', valid: password.value.length >= 8 },
  { label: 'Uppercase letter', valid: /[A-Z]/.test(password.value) },
  { label: 'Lowercase letter', valid: /[a-z]/.test(password.value) },
  { label: 'Number', valid: /\d/.test(password.value) },
])

const passwordsMatch = computed(() => password.value === confirmPassword.value)

const canSubmit = computed(
  () => passwordRules.value.every((r) => r.valid) && passwordsMatch.value && !loading.value,
)

async function handleSubmit() {
  error.value = ''
  loading.value = true
  try {
    await auth.resetPassword(emailParam, tokenParam, password.value)
    success.value = true
  } catch (e) {
    if (isApiError(e) && e.errors) {
      error.value = Object.values(e.errors).flat().join(' ')
    } else {
      error.value = 'Something went wrong. Please try again.'
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <Card>
    <CardHeader>
      <CardTitle class="text-center text-lg">Set new password</CardTitle>
    </CardHeader>
    <CardContent>
      <template v-if="success">
        <p class="text-center text-sm text-muted-foreground">
          Your password has been reset.
        </p>
        <div class="mt-4 text-center">
          <RouterLink to="/login" class="text-sm text-accent hover:underline">Log in</RouterLink>
        </div>
      </template>
      <template v-else-if="!emailParam || !tokenParam">
        <p class="text-center text-sm text-destructive">Invalid or missing reset link.</p>
        <div class="mt-4 text-center">
          <RouterLink to="/forgot-password" class="text-sm text-accent hover:underline">Request a new link</RouterLink>
        </div>
      </template>
      <form v-else class="flex flex-col gap-4" @submit.prevent="handleSubmit">
        <div v-if="error" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {{ error }}
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="password" class="text-sm font-medium">New password</label>
          <Input id="password" v-model="password" type="password" required autocomplete="new-password" />
          <ul v-if="password" class="mt-1 space-y-0.5 text-xs">
            <li v-for="rule in passwordRules" :key="rule.label" :class="rule.valid ? 'text-green-600' : 'text-muted-foreground'">
              {{ rule.valid ? '✓' : '○' }} {{ rule.label }}
            </li>
          </ul>
        </div>
        <div class="flex flex-col gap-1.5">
          <label for="confirm-password" class="text-sm font-medium">Confirm new password</label>
          <Input id="confirm-password" v-model="confirmPassword" type="password" required autocomplete="new-password" />
          <p v-if="confirmPassword && !passwordsMatch" class="text-xs text-destructive">Passwords do not match.</p>
        </div>
        <Button type="submit" class="w-full" :disabled="!canSubmit">
          {{ loading ? 'Resetting…' : 'Reset password' }}
        </Button>
      </form>
    </CardContent>
  </Card>
</template>
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/views/ForgotPasswordView.vue fasolt.client/src/views/ResetPasswordView.vue
git commit -m "feat: add ForgotPasswordView and ResetPasswordView"
```

---

## Task 10: Router with Auth Guards

**Files:**
- Modify: `fasolt.client/src/router/index.ts`
- Modify: `fasolt.client/src/main.ts`

- [ ] **Step 1: Update router with auth routes and guards**

Replace `src/router/index.ts`:

```typescript
import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import DashboardView from '@/views/DashboardView.vue'

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
    // App routes (require auth)
    { path: '/', name: 'dashboard', component: DashboardView },
    { path: '/files', name: 'files', component: () => import('@/views/FilesView.vue') },
    { path: '/groups', name: 'groups', component: () => import('@/views/GroupsView.vue') },
    { path: '/review/:deckId?', name: 'review', component: () => import('@/views/ReviewView.vue') },
    { path: '/settings', name: 'settings', component: () => import('@/views/SettingsView.vue') },
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
    return { name: 'dashboard' }
  }
})

export default router
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/router/index.ts
git commit -m "feat: add auth routes and navigation guards"
```

---

## Task 11: Conditional Layout in App.vue

**Files:**
- Modify: `fasolt.client/src/App.vue`

- [ ] **Step 1: Update App.vue to switch layouts**

```vue
<!-- fasolt.client/src/App.vue -->
<script setup lang="ts">
import { useRoute } from 'vue-router'
import { computed } from 'vue'
import AppLayout from '@/layouts/AppLayout.vue'
import AuthLayout from '@/layouts/AuthLayout.vue'

const route = useRoute()
const isAuthPage = computed(() => route.meta.public === true)
</script>

<template>
  <component :is="isAuthPage ? AuthLayout : AppLayout">
    <RouterView />
  </component>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/App.vue
git commit -m "feat: switch between AuthLayout and AppLayout based on route"
```

---

## Task 12: TopBar User Dropdown

**Files:**
- Modify: `fasolt.client/src/components/TopBar.vue`

- [ ] **Step 1: Replace avatar placeholder with dropdown menu**

Replace `TopBar.vue`:

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import KbdHint from '@/components/KbdHint.vue'
import { useDarkMode } from '@/composables/useDarkMode'
import { useAuthStore } from '@/stores/auth'

const { theme, toggle } = useDarkMode()
const auth = useAuthStore()
const router = useRouter()

const themeIcon = computed(() => {
  if (theme.value === 'dark') return '☾'
  if (theme.value === 'light') return '☀'
  return '◑'
})

const themeLabel = computed(() => {
  if (theme.value === 'dark') return 'Dark'
  if (theme.value === 'light') return 'Light'
  return 'System'
})

const userInitial = computed(() => {
  if (auth.user?.displayName) return auth.user.displayName[0].toUpperCase()
  if (auth.user?.email) return auth.user.email[0].toUpperCase()
  return '?'
})

const userLabel = computed(() => auth.user?.displayName || auth.user?.email || '')

async function handleLogout() {
  await auth.logout()
  router.push('/login')
}
</script>

<template>
  <header class="flex items-center justify-between border-b border-border px-5 py-3">
    <span class="font-mono text-[13px] font-bold text-foreground tracking-tight">
      fasolt
    </span>
    <div class="relative hidden sm:block">
      <Input
        type="text"
        placeholder="Search cards, files…"
        class="h-8 w-[200px] bg-secondary pl-8 text-xs"
        readonly
      />
      <div class="absolute left-2 top-1/2 -translate-y-1/2">
        <KbdHint keys="⌘K" />
      </div>
    </div>
    <div class="flex items-center gap-2">
      <Button
        variant="ghost"
        size="sm"
        class="h-8 gap-1.5 text-xs text-muted-foreground"
        :aria-label="`Theme: ${themeLabel}. Click to change.`"
        @click="toggle"
      >
        <span class="text-sm">{{ themeIcon }}</span>
        <span class="hidden sm:inline">{{ themeLabel }}</span>
      </Button>
      <DropdownMenu>
        <DropdownMenuTrigger as-child>
          <Button
            variant="ghost"
            size="sm"
            class="h-8 w-8 rounded-full bg-secondary p-0 text-xs font-medium"
            :aria-label="`User menu for ${userLabel}`"
          >
            {{ userInitial }}
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" class="w-48">
          <div class="px-2 py-1.5 text-xs text-muted-foreground truncate">
            {{ userLabel }}
          </div>
          <DropdownMenuSeparator />
          <DropdownMenuItem as-child>
            <RouterLink to="/settings" class="cursor-pointer">Settings</RouterLink>
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem class="cursor-pointer" @click="handleLogout">
            Log out
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  </header>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/components/TopBar.vue
git commit -m "feat: add user dropdown menu to TopBar with logout"
```

---

## Task 13: Settings View with Profile Management

**Files:**
- Modify: `fasolt.client/src/views/SettingsView.vue`

- [ ] **Step 1: Build out SettingsView with profile sections**

```vue
<!-- fasolt.client/src/views/SettingsView.vue -->
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/stores/auth'
import { isApiError } from '@/api/client'

const auth = useAuthStore()

// Display name
const displayName = ref('')
const displayNameSuccess = ref(false)
const displayNameError = ref('')

// Email
const newEmail = ref('')
const emailCurrentPassword = ref('')
const emailSuccess = ref(false)
const emailError = ref('')

// Password
const currentPassword = ref('')
const newPassword = ref('')
const confirmNewPassword = ref('')
const passwordSuccess = ref(false)
const passwordError = ref('')

onMounted(() => {
  displayName.value = auth.user?.displayName || ''
  newEmail.value = auth.user?.email || ''
})

async function saveDisplayName() {
  displayNameSuccess.value = false
  displayNameError.value = ''
  try {
    await auth.updateProfile(displayName.value || null)
    displayNameSuccess.value = true
  } catch (e) {
    displayNameError.value = 'Failed to update display name.'
  }
}

async function saveEmail() {
  emailSuccess.value = false
  emailError.value = ''
  try {
    await auth.changeEmail(newEmail.value, emailCurrentPassword.value)
    emailCurrentPassword.value = ''
    emailSuccess.value = true
  } catch (e) {
    if (isApiError(e) && e.errors) {
      emailError.value = Object.values(e.errors).flat().join(' ')
    } else {
      emailError.value = 'Failed to update email.'
    }
  }
}

async function savePassword() {
  passwordSuccess.value = false
  passwordError.value = ''
  if (newPassword.value !== confirmNewPassword.value) {
    passwordError.value = 'Passwords do not match.'
    return
  }
  try {
    await auth.changePassword(currentPassword.value, newPassword.value)
    currentPassword.value = ''
    newPassword.value = ''
    confirmNewPassword.value = ''
    passwordSuccess.value = true
  } catch (e) {
    if (isApiError(e) && e.errors) {
      passwordError.value = Object.values(e.errors).flat().join(' ')
    } else {
      passwordError.value = 'Failed to update password.'
    }
  }
}
</script>

<template>
  <div class="flex flex-col gap-6">
    <h1 class="text-lg font-semibold tracking-tight">Settings</h1>

    <!-- Display Name -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">Display name</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="saveDisplayName">
          <div v-if="displayNameSuccess" class="rounded-md bg-green-500/10 px-3 py-2 text-sm text-green-600">Saved.</div>
          <div v-if="displayNameError" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ displayNameError }}</div>
          <Input v-model="displayName" placeholder="Your display name" />
          <Button type="submit" size="sm" class="self-start">Save</Button>
        </form>
      </CardContent>
    </Card>

    <!-- Email -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">Email address</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="saveEmail">
          <div v-if="emailSuccess" class="rounded-md bg-green-500/10 px-3 py-2 text-sm text-green-600">Email updated.</div>
          <div v-if="emailError" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ emailError }}</div>
          <div class="flex flex-col gap-1.5">
            <label for="new-email" class="text-sm font-medium">New email</label>
            <Input id="new-email" v-model="newEmail" type="email" required />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="email-password" class="text-sm font-medium">Current password</label>
            <Input id="email-password" v-model="emailCurrentPassword" type="password" required autocomplete="current-password" />
          </div>
          <Button type="submit" size="sm" class="self-start">Update email</Button>
        </form>
      </CardContent>
    </Card>

    <!-- Password -->
    <Card>
      <CardHeader>
        <CardTitle class="text-base">Change password</CardTitle>
      </CardHeader>
      <CardContent>
        <form class="flex flex-col gap-3" @submit.prevent="savePassword">
          <div v-if="passwordSuccess" class="rounded-md bg-green-500/10 px-3 py-2 text-sm text-green-600">Password changed.</div>
          <div v-if="passwordError" class="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{{ passwordError }}</div>
          <div class="flex flex-col gap-1.5">
            <label for="current-password" class="text-sm font-medium">Current password</label>
            <Input id="current-password" v-model="currentPassword" type="password" required autocomplete="current-password" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="new-password" class="text-sm font-medium">New password</label>
            <Input id="new-password" v-model="newPassword" type="password" required autocomplete="new-password" />
          </div>
          <div class="flex flex-col gap-1.5">
            <label for="confirm-new-password" class="text-sm font-medium">Confirm new password</label>
            <Input id="confirm-new-password" v-model="confirmNewPassword" type="password" required autocomplete="new-password" />
          </div>
          <Button type="submit" size="sm" class="self-start">Change password</Button>
        </form>
      </CardContent>
    </Card>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/views/SettingsView.vue
git commit -m "feat: add profile settings (display name, email, password) to SettingsView"
```

---

## Task 13.5: Frontend Build Verification

- [ ] **Step 1: Verify TypeScript compilation**

Run:
```bash
cd fasolt.client && npx vue-tsc --noEmit
```

Expected: No errors. If there are type errors, fix them before proceeding.

- [ ] **Step 2: Verify Vite build**

Run:
```bash
cd fasolt.client && npm run build
```

Expected: Build succeeds with no errors.

---

## Task 14: E2E Smoke Test with Playwright

**Files:** None (uses Playwright MCP)

- [ ] **Step 1: Start the full stack**

```bash
./dev.sh &
```

Wait for both backend and frontend to be ready.

- [ ] **Step 2: Test registration flow**

Using Playwright MCP:
1. Navigate to `http://localhost:5173/register`
2. Verify the registration form is displayed
3. Fill email: `test@example.com`, password: `TestPass1`, confirm password: `TestPass1`
4. Click "Create account"
5. Verify redirect to dashboard (`/`)

- [ ] **Step 3: Test logout flow**

1. Click the user avatar/initial button in the top bar
2. Click "Log out" in dropdown
3. Verify redirect to `/login`

- [ ] **Step 4: Test login flow**

1. On `/login`, fill email: `test@example.com`, password: `TestPass1`
2. Click "Log in"
3. Verify redirect to dashboard

- [ ] **Step 5: Test route guard**

1. Log out
2. Navigate to `http://localhost:5173/files`
3. Verify redirect to `/login`

- [ ] **Step 6: Test settings page**

1. Log in
2. Navigate to `/settings`
3. Set display name to "Test User", click Save
4. Verify success message
5. Verify TopBar shows "T" initial

- [ ] **Step 7: Test forgot password flow**

1. Log out
2. Click "Forgot password?" on login page
3. Enter email, submit
4. Verify success message shows

- [ ] **Step 8: Stop dev server and commit any fixes**

```bash
kill %1
```
