# Admin Area Design

## Overview

Add role-based access control (Admin) and an admin area for managing users. The admin area is web-only — not accessible via MCP or the normal API. It provides a user overview with stats and the ability to lock/unlock accounts.

## Requirements

From `docs/requirements/13-admin_area.md`:
- Add roles: Admin and User
- Admin area with user overview showing deck/card counts
- Admin endpoints must not be accessible via MCP or normal API
- Web only — no MCP or iOS support

## Backend

### Identity Roles

- Add `.AddRoles<IdentityRole>()` to the Identity builder chain in `Program.cs`, between `AddIdentityApiEndpoints<AppUser>()` and `.AddEntityFrameworkStores<AppDbContext>()`:
  ```csharp
  .AddIdentityApiEndpoints<AppUser>(options => { ... })
  .AddRoles<IdentityRole>()
  .AddEntityFrameworkStores<AppDbContext>();
  ```
- `IdentityDbContext<AppUser>` already maps to `IdentityDbContext<AppUser, IdentityRole, string>` internally — no DbContext signature change needed
- This generates the standard Identity role tables: `AspNetRoles`, `AspNetUserRoles`, `AspNetRoleClaims`
- Seed the "Admin" role on application startup
- No "User" role needed — absence of the Admin role is sufficient to identify regular users, and hooking into the built-in Identity register endpoint to assign roles is non-trivial with no benefit

### Admin Seeding

- New config setting `Admin:Email` in `appsettings.json` / `appsettings.Development.json`
- On startup, if a user with that email exists, ensure they have the "Admin" role
- In development, the dev seed user (`dev@fasolt.local`) is configured as admin
- In production, set via environment variable `Admin__Email`

### Authorization Policy

New named policy `"AdminCookieOnly"`:
```csharp
options.AddPolicy("AdminCookieOnly", policy =>
    policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme)
          .RequireRole("Admin"));
```
- `AddAuthenticationSchemes` specifies which schemes to *evaluate* — only the cookie scheme is evaluated, so OAuth bearer tokens from MCP are ignored entirely
- `RequireRole("Admin")` ensures only admins pass
- This blocks both MCP access and regular non-admin users in a single check

### Admin Endpoints

Route group: `/api/admin`, secured with `.RequireAuthorization("AdminCookieOnly")`

Using `/api/admin` (not `/admin`) to match the existing codebase convention where all backend endpoints live under `/api/`, and to avoid conflicts with Vue SPA client-side routing.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/admin/users` | Paginated user list with stats |
| `POST` | `/api/admin/users/{id}/lock` | Lock a user account |
| `POST` | `/api/admin/users/{id}/unlock` | Unlock a user account |

**`GET /api/admin/users` response:**
```json
{
  "users": [
    {
      "id": "string",
      "email": "string",
      "displayName": "string | null",
      "cardCount": 0,
      "deckCount": 0,
      "isLockedOut": false
    }
  ],
  "totalCount": 0,
  "page": 1,
  "pageSize": 50
}
```

The query uses correlated subqueries for per-user counts (efficient and EF Core-friendly):
```csharp
db.Users.Select(u => new {
    u.Id, u.Email, u.DisplayName,
    CardCount = db.Cards.Count(c => c.UserId == u.Id),
    DeckCount = db.Decks.Count(d => d.UserId == u.Id),
    IsLockedOut = u.LockoutEnabled && u.LockoutEnd > DateTimeOffset.UtcNow
})
```

No new entities needed.

Note: `IdentityUser` does not have a `CreatedAt`/`RegisteredAt` field. Adding one would require a migration for a minor display benefit — omitted for now.

**`POST /api/admin/users/{id}/lock`:**
- Sets `LockoutEnd` to `DateTimeOffset.MaxValue` and `LockoutEnabled` to `true`
- Returns 200 on success, 404 if user not found
- Cannot lock yourself (returns 400)

**`POST /api/admin/users/{id}/unlock`:**
- Sets `LockoutEnd` to `null`
- Returns 200 on success, 404 if user not found

Non-admin or bearer-token requests return 403 Forbidden (ASP.NET Core default). The frontend should handle this gracefully.

### Account Endpoint Extension

Extend the existing `GET /account/me` response to include `isAdmin: bool` so the frontend can conditionally render the admin tab.

## Frontend

### Auth Store

- Store `isAdmin` from the `/account/me` response
- Expose `isAdmin` as a computed property

### Router

- New route: `/admin` → `AdminView` component
- Route guard: redirect to `/` if `!auth.isAdmin`

### Navigation (AppLayout.vue)

- Import the auth store into `AppLayout.vue`
- Make the tabs array a computed property
- Conditionally include "Admin" tab as the last tab when `auth.isAdmin` is true

### AdminView Page

- Table displaying all users with columns: Email, Display Name, Cards, Decks, Status
- Pagination controls (offset/limit, page size 50)
- Lock/Unlock toggle button per row
- Confirmation dialog before locking a user
- Locked users shown with a visual indicator (e.g., badge or row styling)
- No search/filter (YAGNI for a small free SaaS)

## Database

### Migration

One new EF Core migration that adds the Identity role tables:
- `AspNetRoles`
- `AspNetUserRoles`
- `AspNetRoleClaims`

These are generated automatically by adding `.AddRoles<IdentityRole>()` to the Identity builder chain.

Role and admin user seeding happens at application startup, not in the migration.

## Security

- **MCP blocked**: The `AdminCookieOnly` policy only evaluates the cookie auth scheme — OAuth bearer tokens are not considered
- **Normal users blocked**: The policy requires the Admin role
- **Self-lock prevention**: Admin cannot lock their own account
- **No admin via MCP tools**: Admin endpoints are not registered as MCP tools
- **403 on unauthorized access**: Non-admin or bearer-token requests receive 403 Forbidden

## Out of Scope

- User deletion (can be added later)
- Password reset by admin
- Role management UI (admin is config-driven)
- Search/filter on user list
- Registration date display (no `CreatedAt` on `IdentityUser`)
- MCP or iOS access to admin features
