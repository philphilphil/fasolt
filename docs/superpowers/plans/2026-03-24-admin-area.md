# Admin Area Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add role-based admin area with user overview and lock/unlock, accessible only via web (not MCP/API).

**Architecture:** ASP.NET Core Identity Roles for RBAC. Admin endpoints at `/api/admin/*` with a cookie-only + Admin role policy. Vue frontend conditionally shows Admin tab. Config-driven admin seeding.

**Tech Stack:** ASP.NET Core Identity Roles, EF Core migrations, Vue 3 + Pinia + shadcn-vue

**Spec:** `docs/superpowers/specs/2026-03-24-admin-area-design.md`

---

## File Structure

**Backend — Create:**
- `fasolt.Server/Api/Endpoints/AdminEndpoints.cs` — Admin endpoint handlers (list users, lock, unlock)
- `fasolt.Server/Application/Dtos/AdminDtos.cs` — Request/response DTOs for admin endpoints
- `fasolt.Server/Application/Services/AdminService.cs` — Admin business logic (user list query, lock/unlock)

**Backend — Modify:**
- `fasolt.Server/Program.cs` — Add `.AddRoles<IdentityRole>()`, `AdminCookieOnly` policy, `MapAdminEndpoints()`
- `fasolt.Server/Infrastructure/Data/DevSeedData.cs` — Seed Admin role, assign to dev user
- `fasolt.Server/Application/Dtos/AccountDtos.cs` — Add `IsAdmin` to `UserInfoResponse`
- `fasolt.Server/Api/Endpoints/AccountEndpoints.cs` — Pass `isAdmin` to response

**Frontend — Create:**
- `fasolt.client/src/views/AdminView.vue` — Admin page with user table and lock/unlock

**Frontend — Modify:**
- `fasolt.client/src/stores/auth.ts` — Add `isAdmin` to user state
- `fasolt.client/src/router/index.ts` — Add `/admin` route with guard
- `fasolt.client/src/layouts/AppLayout.vue` — Conditional Admin tab
- `fasolt.client/src/components/BottomNav.vue` — Conditional Admin tab (mobile web)

**Database:**
- New EF Core migration for Identity role tables

---

### Task 1: Add Identity Roles to Backend

**Files:**
- Modify: `fasolt.Server/Program.cs:22-36` (Identity builder chain)
- Modify: `fasolt.Server/Program.cs:117-125` (authorization policies)

- [ ] **Step 1: Add `.AddRoles<IdentityRole>()` to Identity builder chain**

In `fasolt.Server/Program.cs`, change the Identity builder chain (lines 22-36) from:

```csharp
builder.Services
    .AddIdentityApiEndpoints<AppUser>(options =>
    {
        // ... options ...
    })
    .AddEntityFrameworkStores<AppDbContext>();
```

to:

```csharp
builder.Services
    .AddIdentityApiEndpoints<AppUser>(options =>
    {
        // ... options ...
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();
```

Add the `using Microsoft.AspNetCore.Identity;` import if not already present (it should be via `IdentityConstants`).

- [ ] **Step 2: Add `AdminCookieOnly` authorization policy**

In `fasolt.Server/Program.cs`, inside the `AddAuthorization` block (lines 117-125), add the new policy after the default policy:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes(
            IdentityConstants.ApplicationScheme,
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
        .Build();

    options.AddPolicy("AdminCookieOnly", policy =>
        policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
              .RequireRole("Admin"));
});
```

- [ ] **Step 3: Verify the backend compiles**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Program.cs
git commit -m "feat(admin): add Identity Roles and AdminCookieOnly policy"
```

---

### Task 2: Create EF Core Migration for Role Tables

**Files:**
- New migration in `fasolt.Server/Infrastructure/Data/Migrations/`

- [ ] **Step 1: Generate the migration**

Run: `dotnet ef migrations add AddIdentityRoles --project fasolt.Server`
Expected: Migration files created in `Infrastructure/Data/Migrations/`

- [ ] **Step 2: Apply the migration**

Run: `dotnet ef database update --project fasolt.Server`
Expected: Tables `AspNetRoles`, `AspNetUserRoles`, `AspNetRoleClaims` created

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat(admin): add EF Core migration for Identity role tables"
```

---

### Task 3: Seed Admin Role and Dev Admin User

**Files:**
- Modify: `fasolt.Server/Infrastructure/Data/DevSeedData.cs`
- Modify: `fasolt.Server/Program.cs:211-214` (seed section)

- [ ] **Step 1: Add admin role seeding to Program.cs**

Add role seeding that runs on every startup (not just dev). In `Program.cs`, the existing migration scope (lines 199-204) uses `using var scope` which disposes at block end. Add the role seeding between the migration block (line 204) and the `MapOpenApi` block (line 206). Place it inside the same `if (!app.Environment.IsEnvironment("Testing"))` block, after `await db.Database.MigrateAsync();`:

```csharp
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Seed Admin role
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }
}
```

This reuses the existing scope instead of creating a new one. The role seeding runs before `DevSeedData.SeedAsync` (line 213), which is required since DevSeedData now assigns the Admin role.

- [ ] **Step 2: Update DevSeedData to assign Admin role**

Modify `fasolt.Server/Infrastructure/Data/DevSeedData.cs` to accept `RoleManager` and assign the Admin role to the dev user:

```csharp
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Infrastructure.Data;

public static class DevSeedData
{
    public const string DevEmail = "dev@fasolt.local";
    public const string DevPassword = "Dev1234!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        var existing = await userManager.FindByEmailAsync(DevEmail);
        if (existing is not null)
        {
            // Ensure dev user has Admin role even if already created
            if (!await userManager.IsInRoleAsync(existing, "Admin"))
                await userManager.AddToRoleAsync(existing, "Admin");
            return;
        }

        var user = new AppUser
        {
            UserName = DevEmail,
            Email = DevEmail,
            DisplayName = "Dev User",
            EmailConfirmed = true,
        };

        await userManager.CreateAsync(user, DevPassword);
        await userManager.AddToRoleAsync(user, "Admin");
    }
}
```

- [ ] **Step 3: Add config-based admin seeding for production**

In `Program.cs`, after the dev seed block, add:

```csharp
// Promote configured admin email
var adminEmail = app.Configuration["Admin:Email"];
if (!string.IsNullOrEmpty(adminEmail))
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser is not null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
    {
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }
}
```

- [ ] **Step 4: Add config to appsettings.Development.json**

Add to `appsettings.Development.json`:

```json
"Admin": {
  "Email": "dev@fasolt.local"
}
```

- [ ] **Step 5: Verify the backend starts and seeds correctly**

Run: `dotnet run --project fasolt.Server`
Expected: No errors. Check logs for successful startup.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Infrastructure/Data/DevSeedData.cs fasolt.Server/Program.cs fasolt.Server/appsettings.Development.json
git commit -m "feat(admin): seed Admin role and assign to dev user"
```

---

### Task 4: Extend `/account/me` with `isAdmin`

**Files:**
- Modify: `fasolt.Server/Application/Dtos/AccountDtos.cs:3`
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs:30-36`

- [ ] **Step 1: Add `IsAdmin` to `UserInfoResponse`**

In `fasolt.Server/Application/Dtos/AccountDtos.cs`, change:

```csharp
public record UserInfoResponse(string Email, string? DisplayName);
```

to:

```csharp
public record UserInfoResponse(string Email, string? DisplayName, bool IsAdmin);
```

- [ ] **Step 2: Update all `UserInfoResponse` usages in AccountEndpoints.cs**

In `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`, the `GetMe` handler (line ~30-36) needs to check the admin role. Update the handler signature to inject `UserManager<AppUser>` (already there) and check the role:

```csharp
private static async Task<IResult> GetMe(
    ClaimsPrincipal principal,
    UserManager<AppUser> userManager)
{
    var user = await userManager.GetUserAsync(principal);
    if (user is null) return Results.Unauthorized();
    var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
    return Results.Ok(new UserInfoResponse(user.Email!, user.DisplayName, isAdmin));
}
```

Find all other places that construct `UserInfoResponse` in AccountEndpoints.cs (UpdateProfile ~line 50, ChangeEmail ~line 91) and add `isAdmin` parameter:

```csharp
var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
return Results.Ok(new UserInfoResponse(user.Email!, user.DisplayName, isAdmin));
```

- [ ] **Step 3: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded (no remaining references to the old 2-param constructor)

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Application/Dtos/AccountDtos.cs fasolt.Server/Api/Endpoints/AccountEndpoints.cs
git commit -m "feat(admin): add isAdmin to /account/me response"
```

---

### Task 5: Create Admin Service

**Files:**
- Create: `fasolt.Server/Application/Dtos/AdminDtos.cs`
- Create: `fasolt.Server/Application/Services/AdminService.cs`

- [ ] **Step 1: Create AdminDtos.cs**

Create `fasolt.Server/Application/Dtos/AdminDtos.cs`:

```csharp
namespace Fasolt.Server.Application.Dtos;

public record AdminUserDto(
    string Id,
    string Email,
    string? DisplayName,
    int CardCount,
    int DeckCount,
    bool IsLockedOut);

public record AdminUserListResponse(
    List<AdminUserDto> Users,
    int TotalCount,
    int Page,
    int PageSize);
```

- [ ] **Step 2: Create AdminService.cs**

Create `fasolt.Server/Application/Services/AdminService.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class AdminService
{
    private readonly AppDbContext _db;

    public AdminService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AdminUserListResponse> ListUsers(int page, int pageSize)
    {
        var totalCount = await _db.Users.CountAsync();

        var users = await _db.Users
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email!,
                u.DisplayName,
                _db.Cards.Count(c => c.UserId == u.Id),
                _db.Decks.Count(d => d.UserId == u.Id),
                u.LockoutEnabled && u.LockoutEnd > DateTimeOffset.UtcNow))
            .ToListAsync();

        return new AdminUserListResponse(users, totalCount, page, pageSize);
    }
}
```

- [ ] **Step 3: Register AdminService in DI**

In `fasolt.Server/Program.cs`, add with the other service registrations:

```csharp
builder.Services.AddScoped<AdminService>();
```

- [ ] **Step 4: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Application/Dtos/AdminDtos.cs fasolt.Server/Application/Services/AdminService.cs fasolt.Server/Program.cs
git commit -m "feat(admin): add AdminService with user list query"
```

---

### Task 6: Create Admin Endpoints

**Files:**
- Create: `fasolt.Server/Api/Endpoints/AdminEndpoints.cs`
- Modify: `fasolt.Server/Program.cs:324-331` (endpoint registration)

- [ ] **Step 1: Create AdminEndpoints.cs**

Create `fasolt.Server/Api/Endpoints/AdminEndpoints.cs`:

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization("AdminCookieOnly");

        group.MapGet("/users", ListUsers);
        group.MapPost("/users/{id}/lock", LockUser);
        group.MapPost("/users/{id}/unlock", UnlockUser);
    }

    private static async Task<IResult> ListUsers(
        int? page,
        int? pageSize,
        AdminService adminService)
    {
        var p = page ?? 1;
        var ps = Math.Clamp(pageSize ?? 50, 1, 100);
        var result = await adminService.ListUsers(p, ps);
        return Results.Ok(result);
    }

    private static async Task<IResult> LockUser(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var currentUser = await userManager.GetUserAsync(principal);
        if (currentUser is null) return Results.Unauthorized();

        if (currentUser.Id == id)
            return Results.BadRequest(new { error = "Cannot lock your own account." });

        var user = await userManager.FindByIdAsync(id);
        if (user is null) return Results.NotFound();

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        return Results.Ok();
    }

    private static async Task<IResult> UnlockUser(
        string id,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return Results.NotFound();

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        return Results.Ok();
    }
}
```

- [ ] **Step 2: Register admin endpoints in Program.cs**

In `fasolt.Server/Program.cs`, add before `app.MapMcp(...)` (around line 332):

```csharp
app.MapAdminEndpoints();
```

- [ ] **Step 3: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/AdminEndpoints.cs fasolt.Server/Program.cs
git commit -m "feat(admin): add admin endpoints for user list, lock, unlock"
```

---

### Task 7: Update Frontend Auth Store

**Files:**
- Modify: `fasolt.client/src/stores/auth.ts`

- [ ] **Step 1: Add `isAdmin` to User interface and store**

In `fasolt.client/src/stores/auth.ts`, update the `User` interface:

```typescript
interface User {
  email: string
  displayName: string | null
  isAdmin: boolean
}
```

Add a computed property after `isAuthenticated`:

```typescript
const isAdmin = computed(() => user.value?.isAdmin ?? false)
```

Add `isAdmin` to the return object:

```typescript
return {
    user,
    isLoading,
    isAuthenticated,
    isAdmin,
    fetchUser,
    // ... rest unchanged
}
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/stores/auth.ts
git commit -m "feat(admin): add isAdmin to auth store"
```

---

### Task 8: Add Admin Route

**Files:**
- Modify: `fasolt.client/src/router/index.ts`

- [ ] **Step 1: Add the `/admin` route**

In `fasolt.client/src/router/index.ts`, add the admin route alongside the other protected routes (after the `/settings` route, around line 61):

```typescript
{ path: '/admin', name: 'admin', component: () => import('@/views/AdminView.vue'), meta: { requiresAdmin: true } },
```

- [ ] **Step 2: Add admin guard to the router**

In the `router.beforeEach` guard, add an admin check after the existing auth checks (after the `authRedirect` check):

```typescript
if (to.meta.requiresAdmin) {
    if (!auth.isAdmin) {
        return { name: 'study' }
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/router/index.ts
git commit -m "feat(admin): add /admin route with admin guard"
```

---

### Task 9: Add Admin Tab to Navigation

**Files:**
- Modify: `fasolt.client/src/layouts/AppLayout.vue`
- Modify: `fasolt.client/src/components/BottomNav.vue`

- [ ] **Step 1: Make AppLayout tabs computed and conditional**

In `fasolt.client/src/layouts/AppLayout.vue`, the `computed` import already exists (line 2). Add the auth store import:

```typescript
import { useAuthStore } from '@/stores/auth'
```

Add the store instance:

```typescript
const auth = useAuthStore()
```

Replace the static `tabs` array (lines 13-20) with a computed:

```typescript
const tabs = computed(() => {
  const items = [
    { label: 'Study', value: '/study' },
    { label: 'Cards', value: '/cards' },
    { label: 'Decks', value: '/decks' },
    { label: 'Sources', value: '/sources' },
    { label: 'MCP', value: '/mcp' },
    { label: 'Settings', value: '/settings' },
  ]
  if (auth.isAdmin) {
    items.push({ label: 'Admin', value: '/admin' })
  }
  return items
})
```

**Important:** The `activeTab` computed (line 22-26) references `tabs` directly. Since `tabs` is now a computed ref, update line 24 to use `tabs.value`:

```typescript
const activeTab = computed(() => {
  const path = route.path
  const match = tabs.value.find(t => path === t.value || path.startsWith(t.value + '/'))
  return match?.value ?? path
})
```

- [ ] **Step 2: Update BottomNav.vue for mobile web**

In `fasolt.client/src/components/BottomNav.vue`, add computed import, auth store, and make tabs conditional:

```vue
<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const auth = useAuthStore()

const tabs = computed(() => {
  const items = [
    { name: 'Study', path: '/study', icon: '◉' },
    { name: 'Cards', path: '/cards', icon: '▤' },
    { name: 'Decks', path: '/decks', icon: '⊞' },
    { name: 'Sources', path: '/sources', icon: '◫' },
    { name: 'MCP', path: '/mcp', icon: '⏚' },
    { name: 'Settings', path: '/settings', icon: '⚙' },
  ]
  if (auth.isAdmin) {
    items.push({ name: 'Admin', path: '/admin', icon: '⛨' })
  }
  return items
})

function isActive(path: string) {
  return route.path === path
}
</script>
```

The template already uses `v-for="tab in tabs"` which works with computed refs in templates.

- [ ] **Step 3: Commit**

```bash
git add fasolt.client/src/layouts/AppLayout.vue fasolt.client/src/components/BottomNav.vue
git commit -m "feat(admin): add conditional Admin tab in navigation (desktop + mobile)"
```

---

### Task 10: Create AdminView Page

**Files:**
- Create: `fasolt.client/src/views/AdminView.vue`

- [ ] **Step 1: Create the AdminView component**

Create `fasolt.client/src/views/AdminView.vue`. Uses the existing `Dialog` component (not AlertDialog, which doesn't exist in this codebase) following the pattern in `DeckDetailView.vue`. Includes error handling with `useToast` for lock/unlock failures.

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { apiFetch } from '@/api/client'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/components/ui/dialog'
import { useToast } from '@/components/ui/toast'

const { toast } = useToast()

interface AdminUser {
  id: string
  email: string
  displayName: string | null
  cardCount: number
  deckCount: number
  isLockedOut: boolean
}

interface AdminUserListResponse {
  users: AdminUser[]
  totalCount: number
  page: number
  pageSize: number
}

const users = ref<AdminUser[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = 50
const isLoading = ref(false)
const lockDialogOpen = ref(false)
const lockTargetUser = ref<AdminUser | null>(null)

async function fetchUsers() {
  isLoading.value = true
  try {
    const data = await apiFetch<AdminUserListResponse>(
      `/admin/users?page=${page.value}&pageSize=${pageSize}`,
    )
    users.value = data.users
    totalCount.value = data.totalCount
  } finally {
    isLoading.value = false
  }
}

function confirmLock(user: AdminUser) {
  lockTargetUser.value = user
  lockDialogOpen.value = true
}

async function lockUser() {
  if (!lockTargetUser.value) return
  try {
    await apiFetch(`/admin/users/${lockTargetUser.value.id}/lock`, { method: 'POST' })
    lockDialogOpen.value = false
    await fetchUsers()
  } catch (e: any) {
    toast({ title: 'Failed to lock user', description: e.message ?? 'Unknown error', variant: 'destructive' })
  }
}

async function unlockUser(id: string) {
  try {
    await apiFetch(`/admin/users/${id}/unlock`, { method: 'POST' })
    await fetchUsers()
  } catch (e: any) {
    toast({ title: 'Failed to unlock user', description: e.message ?? 'Unknown error', variant: 'destructive' })
  }
}

const totalPages = () => Math.ceil(totalCount.value / pageSize)

function nextPage() {
  if (page.value < totalPages()) {
    page.value++
    fetchUsers()
  }
}

function prevPage() {
  if (page.value > 1) {
    page.value--
    fetchUsers()
  }
}

onMounted(fetchUsers)
</script>

<template>
  <div class="space-y-6">
    <div>
      <h1 class="text-2xl font-bold tracking-tight">Admin</h1>
      <p class="text-muted-foreground">Manage users and monitor usage.</p>
    </div>

    <div class="rounded-md border">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Email</TableHead>
            <TableHead>Display Name</TableHead>
            <TableHead class="text-right">Cards</TableHead>
            <TableHead class="text-right">Decks</TableHead>
            <TableHead>Status</TableHead>
            <TableHead class="text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-if="isLoading">
            <TableCell :colspan="6" class="text-center text-muted-foreground">
              Loading...
            </TableCell>
          </TableRow>
          <TableRow v-else-if="users.length === 0">
            <TableCell :colspan="6" class="text-center text-muted-foreground">
              No users found.
            </TableCell>
          </TableRow>
          <TableRow v-for="u in users" :key="u.id">
            <TableCell class="font-medium">{{ u.email }}</TableCell>
            <TableCell>{{ u.displayName ?? '—' }}</TableCell>
            <TableCell class="text-right">{{ u.cardCount }}</TableCell>
            <TableCell class="text-right">{{ u.deckCount }}</TableCell>
            <TableCell>
              <Badge v-if="u.isLockedOut" variant="destructive">Locked</Badge>
              <Badge v-else variant="secondary">Active</Badge>
            </TableCell>
            <TableCell class="text-right">
              <Button v-if="!u.isLockedOut" variant="destructive" size="sm" @click="confirmLock(u)">
                Lock
              </Button>
              <Button v-else variant="outline" size="sm" @click="unlockUser(u.id)">
                Unlock
              </Button>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>

    <div v-if="totalPages() > 1" class="flex items-center justify-between">
      <p class="text-sm text-muted-foreground">
        Page {{ page }} of {{ totalPages() }} ({{ totalCount }} users)
      </p>
      <div class="flex gap-2">
        <Button variant="outline" size="sm" :disabled="page <= 1" @click="prevPage">
          Previous
        </Button>
        <Button variant="outline" size="sm" :disabled="page >= totalPages()" @click="nextPage">
          Next
        </Button>
      </div>
    </div>

    <!-- Lock confirmation dialog -->
    <Dialog v-model:open="lockDialogOpen">
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Lock user account?</DialogTitle>
          <DialogDescription>
            This will prevent {{ lockTargetUser?.email }} from logging in. You can unlock them later.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter class="gap-2">
          <Button variant="outline" size="sm" @click="lockDialogOpen = false">Cancel</Button>
          <Button variant="destructive" size="sm" @click="lockUser">Lock Account</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  </div>
</template>
```

- [ ] **Step 2: Commit**

```bash
git add fasolt.client/src/views/AdminView.vue
git commit -m "feat(admin): add AdminView with user table and lock/unlock"
```

---

### Task 11: End-to-End Testing with Playwright

**Files:** None (browser testing)

- [ ] **Step 1: Start the full stack**

Run: `./dev.sh` (or ensure backend + frontend are running)

- [ ] **Step 2: Test admin login and tab visibility**

Using Playwright MCP:
1. Navigate to `http://localhost:5173/login`
2. Login as `dev@fasolt.local` / `Dev1234!`
3. Verify the "Admin" tab is visible in the navigation
4. Click the Admin tab
5. Verify the user table loads with at least the dev user

- [ ] **Step 3: Test lock/unlock flow**

Using Playwright MCP:
1. On the Admin page, verify the dev user row shows "Active" status
2. Note: Cannot test locking the dev user (self-lock prevention) — if there's a second user, test lock/unlock on them
3. Verify the Lock button shows a confirmation dialog

- [ ] **Step 4: Test non-admin cannot access admin**

1. Register a new user (or use a second test account)
2. Login as the non-admin user
3. Verify the "Admin" tab is NOT visible
4. Navigate directly to `/admin` — verify redirect to `/study`

- [ ] **Step 5: Test MCP cannot access admin endpoints**

Run: `curl -X GET http://localhost:8080/api/admin/users -H "Authorization: Bearer <any-token>" -v`
Expected: 401 or 403 (not 200)

- [ ] **Step 6: Commit any test fixes**

If any issues were found and fixed during testing, commit those fixes.

---

### Task 12: Move Requirement to Done

**Files:**
- Move: `docs/requirements/13-admin_area.md` → `docs/requirements/done/13-admin_area.md`

- [ ] **Step 1: Move the requirement file**

```bash
mv docs/requirements/13-admin_area.md docs/requirements/done/13-admin_area.md
```

- [ ] **Step 2: Commit**

```bash
git add docs/requirements/13-admin_area.md docs/requirements/done/13-admin_area.md
git commit -m "docs: move admin area requirement to done"
```
