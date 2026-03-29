# Email Verification & Registration Flow Redesign

**Issue:** [#58](https://github.com/philphilphil/fasolt/issues/58)
**Date:** 2026-03-29

## Problem

1. Password reset link is relative (`/reset-password?...`), which doesn't resolve in email clients
2. Email is never verified — anyone can register with someone else's email
3. No protection against email squatting (`RequireUniqueEmail = true` means squatter blocks the real owner)

## Decision

Require email verification. Account is created and loginable immediately, but unverified users see a gate page until they confirm their email. Social login (#59) will provide the frictionless alternative later.

## Registration Flow

1. User submits email + password → account created (`EmailConfirmed = false`)
2. Verification email sent via Plunk: `{App:BaseUrl}/confirm-email?userId={id}&token={urlEncodedToken}`
3. User is auto-logged in but redirected to verification gate page
4. Gate page: "Check your email to verify your account" + resend button + logout link
5. User clicks link in email → email confirmed → redirected to app
6. Subsequent logins while unverified → gate page again

## Backend Changes

### Fix reset link (AccountEndpoints.cs)

Replace relative path with absolute URL using `App:BaseUrl` from configuration. Same config value used for both verification and reset links.

### New endpoint: `POST /api/account/resend-verification`

- Resends confirmation email for the authenticated user
- Rate-limited: 1 per minute per user
- Returns 200 always (no enumeration)

### Authorization gate

Authenticated but unverified users (`EmailConfirmed == false`) get 403 on all API endpoints except:
- `GET /api/account/me`
- `POST /api/account/resend-verification`
- `POST /api/account/logout`

### OAuth flow

`/oauth/authorize` rejects unverified users — redirects to verification gate instead of consent page.

### Token lifetime

Increase `DataProtectionTokenProviderOptions.TokenLifespan` from 1 hour to 24 hours (applies to both verification and reset tokens).

### Identity config

- `RequireUniqueEmail = true` — unchanged
- Password policy — unchanged
- Lockout policy — unchanged

### Existing users migration

Set `EmailConfirmed = true` for all existing users in a data migration so they don't get locked out.

## Frontend Changes

### New route: `/verify-email` → `EmailVerificationView.vue`

- "Check your email to verify your account" message
- Resend button with 60-second cooldown + countdown
- Logout link

### New route: `/confirm-email` → `ConfirmEmailView.vue`

- Reads `userId` and `token` from query params
- Calls Identity confirm-email endpoint
- Shows success → redirects to app
- Shows error on invalid/expired token with option to resend

### Router guard (router.ts)

- After login, check `emailConfirmed` from `/account/me`
- If `false` → redirect to `/verify-email`
- `/verify-email` is the only accessible route for unverified users (plus logout)

### Auth store (auth.ts)

- `fetchUser()` response includes `emailConfirmed`
- Add `resendVerification()` method

### `/account/me` response

Add `emailConfirmed: boolean` to the response DTO.

### Existing pages

No changes to RegisterView, LoginView, ForgotPasswordView, or ResetPasswordView.

## Email

### Verification email

- Sent via Plunk (prod) / logged to console (dev)
- Subject: "Verify your fasolt account"
- Body: simple text with verification link button
- Uses existing `SendConfirmationLinkAsync()` in `PlunkEmailSender`

### Base URL config

- `App:BaseUrl` in configuration (`https://fasolt.app` prod, `http://localhost:5173` dev)
- Used for all email links (verification + password reset)
- Fixes the relative URL bug from the original issue

## Edge Cases

- **Existing prod users:** Migration sets `EmailConfirmed = true` — no disruption
- **Dev seed users:** Already have `EmailConfirmed = true`
- **MCP clients:** Can't get OAuth tokens for unverified users
- **Password reset for unverified users:** Forgot-password endpoint returns 200 regardless (no enumeration), but the reset email is only sent if the email is verified. Unverified users must verify first.
- **Rate limiting on resend:** 1 per minute per user to prevent spam

## No Changes

- No new database tables or migrations (beyond the data migration for existing users)
- No changes to password policy, lockout, or `RequireUniqueEmail`
- No username concept — email remains the login identifier
- iOS app auth flow unchanged (uses OAuth, which gates on verification)
