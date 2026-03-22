# User Accounts Design — US-1.1 through US-1.4

## Overview

Implement user accounts for fasolt: registration, login/logout, password reset, and profile settings. Uses ASP.NET Core Identity on the backend with cookie-based auth, and Vue 3 + Pinia on the frontend.

## Scope

| Story | Priority | Summary |
|-------|----------|---------|
| US-1.1 | P0 | Registration with email/password |
| US-1.2 | P0 | Login / Logout with cookie sessions |
| US-1.3 | P1 | Password reset via email |
| US-1.4 | P2 | Profile settings (display name, email, password) |

## Backend

### Custom User Entity

Extend `IdentityUser` to `AppUser` with a `DisplayName` property. Update `AppDbContext` to use `IdentityDbContext<AppUser>` and update `MapIdentityApi` accordingly. Add EF migration.

```csharp
// Domain/Entities/AppUser.cs
public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
```

### Identity Configuration

In `Program.cs`, configure:

- **Password policy**: `RequiredLength = 8`, `RequireUppercase = true`, `RequireLowercase = true`, `RequireDigit = true`, `RequireNonAlphanumeric = false`
- **Cookie options**: `HttpOnly = true`, `SameSite = Strict`, `ExpireTimeSpan = 24h` (default session), `SlidingExpiration = true`
- **Lockout**: default settings (5 attempts, 5 min lockout)

### Endpoints

Keep `MapIdentityApi<AppUser>()` at `/api/identity` for register/login/logout.

Add custom `AccountEndpoints` at `/api/account`:

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| GET | `/api/account/me` | Yes | Return current user email + display name |
| PUT | `/api/account/profile` | Yes | Update display name |
| PUT | `/api/account/email` | Yes | Change email (requires current password, validates uniqueness — returns structured error if taken) |
| PUT | `/api/account/password` | Yes | Change password (requires current password) |
| POST | `/api/account/forgot-password` | No | Send password reset email |
| POST | `/api/account/reset-password` | No | Reset password with token |

### Email (Password Reset)

Implement `IEmailSender<AppUser>`. In development, log the reset link to console. This allows a real email provider to be swapped in later without changing any endpoint code.

The forgot-password endpoint generates a token via `UserManager.GeneratePasswordResetTokenAsync()`, builds a reset URL (`/reset-password?email=...&token=...`), and sends it through `IEmailSender`.

The reset-password endpoint validates via `UserManager.ResetPasswordAsync()`. Token is single-use (Identity enforces this). Expiration is controlled by `DataProtectionTokenProviderOptions.TokenLifespan = 1 hour`.

### Error Responses

- Registration errors: return Identity's validation errors (duplicate email, weak password) as structured JSON
- Login errors: generic "Invalid email or password" — no field-specific hints
- Forgot password: always return 200 regardless of whether email exists (prevent enumeration)

## Frontend

### Auth Store (`stores/auth.ts`)

Pinia store managing auth state:

- **State**: `user: { email, displayName } | null`, `isLoading: boolean`
- **Getters**: `isAuthenticated`
- **Actions**: `register()`, `login()`, `logout()`, `fetchUser()`, `updateProfile()`, `changeEmail()`, `changePassword()`, `forgotPassword()`, `resetPassword()`

`fetchUser()` calls `GET /api/account/me` on app startup to restore session state.

### API Client Updates

- Add `credentials: 'include'` to all fetch calls for cookie support
- Replace generic `throw new Error` with structured error parsing that extracts Identity API validation errors
- Add 401 handling: clear auth state and redirect to login

### New Views

**`LoginView.vue`**:
- Email + password fields
- "Remember me" checkbox (controls `useSessionCookies` param)
- Link to register, link to forgot password
- Error display for invalid credentials
- Auto-redirect to dashboard on success
- Redirect away if already authenticated

**`RegisterView.vue`**:
- Email + password + confirm password fields
- Client-side validation: email format, password strength (8+ chars, mixed case, digit), passwords match
- Server-side error display (duplicate email, etc.)
- Auto-login after successful registration (register then login call)
- Redirect to dashboard

**`ForgotPasswordView.vue`**:
- Email input field
- Submit sends POST to forgot-password endpoint
- Always shows success message ("If an account exists, we sent a reset link")

**`ResetPasswordView.vue`**:
- Reads `email` and `token` from URL query params
- New password + confirm password fields
- Client-side password strength validation
- On success, redirect to login with success message

### Router Changes

**Public routes** (no auth required):
- `/login` → `LoginView`
- `/register` → `RegisterView`
- `/forgot-password` → `ForgotPasswordView`
- `/reset-password` → `ResetPasswordView`

**Navigation guard** (`beforeEach`):
- If route requires auth and user not authenticated → redirect to `/login`
- If route is auth-only (login/register) and user is authenticated → redirect to `/`
- On app load, call `fetchUser()` before first navigation

### Layout

Auth pages use a minimal centered layout (no sidebar/nav). The existing `AppLayout` with TopBar/BottomNav is for authenticated pages only.

**`AuthLayout.vue`**: Centered card with app logo, wraps auth form views.

### TopBar Update

Replace the placeholder avatar circle with a dropdown menu:
- Shows user display name or email
- Dropdown contains: "Settings" link, "Log out" button
- Logout calls `auth.logout()` which clears session and redirects to `/login` (serves as landing page for unauthenticated users)

### Settings View Update

Add profile management sections to existing `SettingsView.vue`:
- **Display name**: text input, save button
- **Change email**: new email + current password, save button
- **Change password**: current password + new password + confirm, save button
- Each section shows success/error feedback independently
- Require current password for email and password changes (per spec)

## Data Flow

### Registration
1. User fills form → client validates → POST `/api/identity/register`
2. If success → POST `/api/identity/login?useCookies=true` → cookie set → GET `/api/account/me` → redirect to dashboard
3. If error → display validation errors

### Login
1. User fills form → POST `/api/identity/login?useCookies=true&useSessionCookies={!rememberMe}`
2. Cookie set → GET `/api/account/me` → store user → redirect to dashboard
3. If error → "Invalid email or password"

### Password Reset
1. User enters email → POST `/api/account/forgot-password` → always shows success
2. Dev: reset link logged to console. User clicks link → `/reset-password?email=...&token=...`
3. User enters new password → POST `/api/account/reset-password` → redirect to login

## Testing

Use Playwright (via MCP) for E2E testing:
- Registration happy path + validation errors
- Login/logout flow
- Route guard redirects
- Password reset flow (in dev mode, read token from console)
- Profile settings updates
