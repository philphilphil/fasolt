# Admin Area Design

## Overview

Add role-based access control (Admin/User) and an admin area for managing users. The admin area is web-only — not accessible via MCP or the normal API. It provides a user overview with stats and the ability to lock/unlock accounts.

## Requirements

From `docs/requirements/13-admin_area.md`:
- Add roles: Admin and User
- Admin area with user overview showing deck/card counts
- Admin endpoints must not be accessible via MCP or normal API
- Web only — no MCP or iOS support

## Backend

### Identity Roles

- Add `AddRoles<IdentityRole>()` to the Identity configuration in `Program.cs`
- This generates the standard Identity role tables: `AspNetRoles`, `AspNetUserRoles`, `AspNetRoleClaims`
- Seed "Admin" and "User" roles on application startup
- All new users are assigned the "User" role on registration

### Admin Seeding

- New config setting `Admin:Email` in `appsettings.json` / `appsettings.Development.json`
- On startup, if a user with that email exists, ensure they have the "Admin" role
- In development, the dev seed user (`dev@fasolt.local`) is configured as admin
- In production, set via environment variable `Admin__Email`

### Authorization Policy

New named policy `"AdminCookieOnly"`:
- Requires role = "Admin"
- Requires authentication scheme = `IdentityConstants.ApplicationScheme` (cookie auth)
- This blocks MCP access (which uses OAuth bearer tokens) and regular non-admin users in a single check

### Admin Endpoints

Route group: `/admin`, secured with `.RequireAuthorization("AdminCookieOnly")`

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/admin/users` | Paginated user list with stats |
| `POST` | `/admin/users/{id}/lock` | Lock a user account |
| `POST` | `/admin/users/{id}/unlock` | Unlock a user account |

**`GET /admin/users` response:**
```json
{
  "users": [
    {
      "id": "string",
      "email": "string",
      "displayName": "string | null",
      "registeredAt": "datetime",
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

The query joins `AspNetUsers` with `Cards` and `Decks` tables using subqueries/GroupBy to get per-user counts. No new entities needed.

**`POST /admin/users/{id}/lock`:**
- Sets `LockoutEnd` to `DateTimeOffset.MaxValue` and `LockoutEnabled` to `true`
- Returns 200 on success, 404 if user not found
- Cannot lock yourself (returns 400)

**`POST /admin/users/{id}/unlock`:**
- Sets `LockoutEnd` to `null`
- Returns 200 on success, 404 if user not found

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

- Conditionally show "Admin" tab as the last tab when `auth.isAdmin` is true

### AdminView Page

- Table displaying all users with columns: Email, Display Name, Registered, Cards, Decks, Status
- Pagination controls (offset/limit, page size 50)
- Lock/Unlock toggle button per row
- Confirmation dialog before locking a user
- Locked users shown with a visual indicator (e.g., badge or row styling)
- No search/filter (YAGNI for a small free SaaS)

### Vite Proxy

- Add `/admin` path to the Vite dev server proxy config (alongside `/api`)

## Database

### Migration

One new EF Core migration that adds the Identity role tables:
- `AspNetRoles`
- `AspNetUserRoles`
- `AspNetRoleClaims`

These are generated automatically by changing to `AddRoles<IdentityRole>()`.

Role and admin user seeding happens at application startup, not in the migration.

## Security

- **MCP blocked**: The `AdminCookieOnly` policy requires cookie authentication, which MCP (OAuth bearer) cannot satisfy
- **Normal API blocked**: The `/admin` prefix is separate from `/api`, and the policy requires the Admin role
- **Self-lock prevention**: Admin cannot lock their own account
- **No admin via MCP tools**: Admin endpoints are not registered as MCP tools

## Out of Scope

- User deletion (can be added later)
- Password reset by admin
- Role management UI (admin is config-driven)
- Search/filter on user list
- MCP or iOS access to admin features
