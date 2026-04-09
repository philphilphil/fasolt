# Onboarding v2 — Web-first auth with OTP verification

**Supersedes:** the register-form portion of the 2026-04-07 spec.
**Issues:** closes #100 (differently — by deleting the native form instead of polishing it), partially closes #99 (Apple slice already in flight), follow-up #103 (password reset OTP migration).

## Problem

PR #101 (on branch `ios-onboarding-restructure`) spent ~400 lines of
Swift rewriting the native iOS Create Account form to reach parity with
the web — live password rules, ToS acceptance, verify-email screen,
error message catalogue. Review surfaced a structural concern: **the
native form is a duplicate of a surface that already exists on the
server.**

Specifically:

1. **Login is already web-based.** `AuthService.signIn` opens
   `ASWebAuthenticationSession` pointed at `/oauth/authorize`, which
   redirects to the server-rendered `/oauth/login` HTML page. The user
   types credentials into a web view, not a native field. There is no
   native login form and never was.

2. **Register is the only outlier.** After PR #101, iOS has a native
   register form that POSTs to `/api/account/register`, plus a native
   "check your email" screen, plus inline password rule logic, plus a
   `SafariView` wrapper for the ToS link. Everything else on the auth
   path is web.

3. **The drift tax is real and recurring.** PR #101 was itself catch-up
   work — "rewrites the iOS Create Account form to mirror the web."
   Every future change (CAPTCHA, MFA, password breach check, ToS bump,
   localized error strings, a new consent field) would require matching
   Swift updates. For a solo-maintained SaaS, this is pure waste.

4. **Associated-domain plumbing is already live.** Commit `7b4d8a1`
   added the `webcredentials:fasolt.app` entitlement specifically so
   iCloud Keychain autofills across the iOS app and the OAuth web view.
   The one historical reason web-popup auth felt janky on iOS is fixed.

5. **URL-token email verification has a latent bug.** Corporate mail
   gateways (Safe Links, Proofpoint, Mimecast) pre-fetch URLs in inbound
   mail to sandbox-scan them, burning single-use tokens before users
   can click them. We have no users yet so no incident, but this will
   happen the first time a corporate user signs up.

## Goals

- **One server-rendered auth surface** used by both the iOS popup and
  the web app. Register, verify email, and login all render from the
  same HTML pages in `OAuthEndpoints.cs`.
- **Email verification uses 6-digit OTP codes, not URL-token links.**
  Works identically on web and inside `ASWebAuthenticationSession`.
- **iOS onboarding gets simpler, not fancier.** Two native SSO buttons
  (Apple, GitHub), an "or" divider, one "Continue with email" button,
  and a self-host link. That's it.
- **All three Apple-sign-in bugs from the PR #101 review are fixed in
  the same pass** — `AppleJwksCache` transient registration, the
  `email_verified` string-vs-boolean comparison, and the link-by-email
  clobber of existing external provider rows.
- **No user data migration required** — the product has no users yet,
  so we can hard-cut from URL tokens to OTP codes without a grace
  window.

## Non-goals

- **Password reset migration to OTP.** Filed as #103, separate PR.
  Password reset still uses ASP.NET Core Identity's URL-token flow
  after this ships. Temporarily asymmetric; will be fixed by #103.
- **Passwordless magic-link login** ("enter your email, get a code,
  sign in without a password"). Trivially cheap to add on top of the
  OTP infrastructure later, but out of scope here.
- **Web Sign in with Apple** (#99 remains open for the web slice).
- **i18n of the HTML auth pages.** Today they're English-only; this
  refactor doesn't add i18n but doesn't make it harder to add later.
- **Background cleanup job for stale OTP rows.** Expired codes are
  naturally replaced on resend/re-register. A nightly sweep can be
  added later if the table grows.
- **Customizable email templates.** Static template is fine for now.

## High-level design

### New iOS onboarding layout

```
┌──────────────────────────────────┐
│          [Fasolt Logo]           │
│             Fasolt               │
│   Spaced repetition for notes    │
│                                  │
│  ┌────────────────────────────┐  │
│  │   Sign in with Apple       │  │  ← native ASAuthorizationController
│  └────────────────────────────┘  │
│  ┌────────────────────────────┐  │
│  │   Continue with GitHub     │  │  ← SwiftUI button → ASWebAuth to
│  └────────────────────────────┘  │     /api/account/github-login
│                                  │
│          ─── or ───              │
│                                  │
│  ┌────────────────────────────┐  │
│  │  Continue with email       │  │  ← SwiftUI button → ASWebAuth to
│  └────────────────────────────┘  │     /oauth/authorize?screen_hint=signup
│                                  │
│   Self-hosting? Change server    │
└──────────────────────────────────┘
```

Both SSO buttons are first-class. Apple uses the native
`ASAuthorizationController` because App Store guideline 4.8 requires a
native Apple button whenever any third-party social sign-in is offered,
and because Face-ID-to-authenticated-in-one-tap is only achievable
natively. GitHub is a SwiftUI `Button` that opens
`ASWebAuthenticationSession` straight to `/api/account/github-login` —
no form, no intermediate page, just a one-tap trampoline to GitHub's
own OAuth. This is not a duplicate auth surface; it's a shortcut past
the `/oauth/login` HTML page that would otherwise have a GitHub button
inside it.

"Continue with email" opens the popup at `/oauth/authorize` with a new
`screen_hint=signup` query parameter. The server reads the hint and,
for unauthenticated users, redirects to `/oauth/register` instead of
`/oauth/login`. Existing users with an account still click this button,
see the register page, and tap the "Already have an account? Sign in"
link to get to `/oauth/login` — one extra tap for the return-visitor
case, which is acceptable because SSO covers most returning users and
the hot path for this button is genuinely new-user signup.

### Server-rendered auth pages

Three HTML pages in `OAuthEndpoints.cs`, all following the existing
inline-CSS, dark-mode-aware, safe-area-padded pattern set by the
current `/oauth/login` and `/oauth/consent` pages.

#### `/oauth/register` (new)

**GET** renders a card with:

- Email input (`type="email"`, `autocomplete="email"`, `required`)
- Password input (`type="password"`, `autocomplete="new-password"`,
  `required`)
- Live password rule checklist — four rules (≥8 chars, uppercase,
  lowercase, number) updated by an inline `<script>` on every `input`
  event. Same visual treatment as the current web `RegisterView.vue`.
- Confirm password input with inline "Passwords don't match" warning
  that only appears once the confirm field is non-empty
- ToS acceptance checkbox with a "Terms of Service" link that opens
  `/terms` in the same web view (not a new tab — inside the popup it
  has to be inline or a pushed page)
- "Create account" submit button, disabled until all gates pass
  (client-side UX only — server re-validates)
- Bottom link: *"Already have an account? Sign in"* →
  `/oauth/login?returnUrl=...`
- CSRF token, `returnUrl` preserved through a hidden field

**POST** (rate-limited via the existing `auth-strict` policy):

1. Validate CSRF, read form fields, validate email format and password
   policy (reuses Identity's `PasswordValidator` and `EmailValidator`)
2. `UserManager.CreateAsync` with `EmailConfirmed = false`
3. Generate a 6-digit OTP code, hash it, store an
   `EmailVerificationCode` row (see §OTP infrastructure)
4. Send the verification email via `IEmailSender<AppUser>`
5. Redirect to `/oauth/verify-email?email=<email>&returnUrl=<url>`

If the email is already taken by a *confirmed* account, show the form
with "An account with this email already exists. Sign in instead." and
highlight the sign-in link. If the email is taken by an *unconfirmed*
account (abandoned register), treat it as a resend: delete the old
code, generate a new one, send a new email, redirect to
`/oauth/verify-email` — this prevents an attacker from hijacking the
flow by pre-creating unconfirmed rows but also saves real users from
being locked out by their own abandoned attempts.

#### `/oauth/verify-email` (new)

**GET** renders a card with:

- Heading "Check your email"
- Subheading showing the email address
- **One six-digit input field** with `inputmode="numeric"`
  `autocomplete="one-time-code"` `pattern="[0-9]{6}"` `maxlength="6"`.
  On iOS (both Safari and `ASWebAuthenticationSession`), this surfaces
  the code from the incoming email above the keyboard as a one-tap
  autofill suggestion. One field beats six-separate-digit-boxes because
  the latter fights `autocomplete="one-time-code"` autofill.
- "Verify" submit button
- "Resend code" link with a client-side 30-second countdown. Disabled
  while the countdown is active. Server-side throttling is authoritative
  (see §OTP infrastructure); client timer is UX only.
- "Use a different email" link → `/oauth/register`
- Hidden fields: `email`, `returnUrl`, CSRF token

**POST**:

1. Validate CSRF, read `email` + `code` + `returnUrl`
2. Look up the `EmailVerificationCode` row for that user
3. If the row is missing, expired, or locked out — show the form with
   "That code has expired. Request a new one." and a prominent Resend
   button
4. Increment `Attempts`. If ≥5, set `LockedUntil = now + 10 minutes`,
   show lockout message
5. Constant-time compare the submitted code against the stored hash.
   On mismatch: show "Incorrect code, try again"
6. On match: delete the row (single-use), set
   `user.EmailConfirmed = true`, sign the user in via Identity cookie
   (`SignInManager.SignInAsync`), redirect to `returnUrl` (which is
   the original `/oauth/authorize?...` URL — OAuth resumes from there
   automatically)

**Resend POST** (`/oauth/verify-email/resend`):

1. Validate CSRF
2. Look up the user by email
3. Check the most recent `EmailVerificationCode` row: reject with
   "Please wait before requesting another code" if `CreatedAt >
   now - 30s` or if the user has sent ≥5 codes in the rolling hour
4. Delete any existing rows for the user, generate a fresh code, store
   it, send the email, redirect back to `/oauth/verify-email`

#### `/oauth/login` (edit existing)

The only change: add a "Create account" link underneath the sign-in
button → `/oauth/register?returnUrl=...`, preserving the current
`returnUrl`. Existing CSRF handling, GitHub button, PKCE pass-through
all stay as-is.

### `/oauth/authorize` `screen_hint` parameter

At `OAuthEndpoints.cs:114-121`, when the user is not authenticated:

```csharp
var hint = openIddictRequest?.GetParameter("screen_hint")?.ToString();
var loginOrRegister = hint == "signup" ? "/oauth/register" : "/oauth/login";
return Results.Redirect($"{loginOrRegister}?returnUrl={Uri.EscapeDataString("/oauth/authorize" + returnUrl)}");
```

The hint is consumed at the entry point and not preserved into the
`returnUrl` — a user who lands on `/oauth/register` and clicks "Sign
in" drops onto `/oauth/login` with the original `returnUrl`, and both
pages redirect to the same `/oauth/authorize?...` on completion. OAuth
resumption happens once at the end regardless of which path the user
took.

OpenIddict's request parser does not natively recognize `screen_hint`
as an OIDC parameter, but `GetParameter` reads arbitrary query string
values. No OpenIddict configuration change needed.

### OTP infrastructure

New EF Core entity in `fasolt.Server/Domain/Entities/`:

```csharp
public class EmailVerificationCode
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public string CodeHash { get; set; } = default!;
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }           // per-code, resets on resend
    public int SentCount { get; set; }           // cumulative this session, never resets
    public DateTimeOffset LastSentAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}
```

**One row per user at a time** — unique index on `UserId` with
`OnDelete(Cascade)` so deleting a user cleans up their code. On
resend, the row is updated in place (new `CodeHash`, new `ExpiresAt`,
`Attempts` reset to 0, `SentCount` incremented, `LastSentAt` updated).
On successful verify, the row is deleted. On fresh re-register (user
abandons the flow, comes back days later and registers again), any
existing row is deleted and a new one created — the abandoned
`SentCount` doesn't carry over.

New `EmailVerificationCodeService` in
`fasolt.Server/Application/Auth/`:

```csharp
public interface IEmailVerificationCodeService
{
    Task<string> GenerateAndStoreAsync(string userId, CancellationToken ct);
    Task<VerifyResult> VerifyAsync(string userId, string code, CancellationToken ct);
    Task<ResendResult> CanResendAsync(string userId, CancellationToken ct);
}

public enum VerifyResult { Ok, Incorrect, Expired, LockedOut }
public enum ResendResult { Ok, TooSoon, TooManyAttempts }
```

- **Code generation:** `RandomNumberGenerator.GetInt32(0, 1_000_000)`,
  zero-padded to 6 digits. Rejects `000000` as a safety measure (not
  cryptographically necessary, just avoids a visually confusing code).
- **Hashing:** HMAC-SHA256 with a server-side pepper read from
  configuration (`OTP_PEPPER`). Not PBKDF2 — OTP codes are short-lived
  (15 min) and already rate-limited, so HMAC is sufficient and faster.
  The pepper is loaded at startup; if not configured, startup fails in
  non-development (same treatment as the OpenIddict cert paths).
- **Verification:** constant-time compare via
  `CryptographicOperations.FixedTimeEquals` after HMAC-hashing the
  submitted code with the same pepper.
- **Expiry:** 15 minutes from `CreatedAt`.
- **Attempts:** 5 per code. Lockout of 10 minutes after 5 failures.
- **Resend throttle:** minimum 30 seconds between sends (check
  `LastSentAt`). Cap of 5 total sends per verification session (check
  `SentCount`). "Verification session" = the lifetime of a single
  `EmailVerificationCode` row, from initial register through either
  successful verify (row deleted) or fresh re-register (row replaced).
  Five total is generous enough that no legitimate user hits the cap;
  attackers hit it fast.
- **Service lifetime:** scoped (uses the EF `AppDbContext`).

Migration: one `EmailVerificationCodes` table, indexed on `UserId`.
Generate via `dotnet ef migrations add AddEmailVerificationCodes`.

### Email template

Single plaintext template, sent via the existing `IEmailSender<AppUser>`
abstraction:

```
Subject: Your Fasolt verification code

Your verification code is 123456.

It expires in 15 minutes. If you didn't request this, ignore this
email — no account was created.

— Fasolt
```

No clickable link. Plain text code. Dev sender logs to console; Plunk
sender sends through the existing template machinery. No new email
template or template ID in Plunk is required — use the existing
transactional email endpoint with a plain `text` body.

### OAuth flow resumption

End-to-end trace for iOS new-user register:

1. iOS: user taps "Continue with email"
2. iOS: `AuthService.signIn(serverURL:providerHint: "signup")` — new
   parameter passed through to `openAuthSession`, which translates
   "signup" into `screen_hint=signup` on the `/oauth/authorize` URL
3. `ASWebAuthenticationSession` opens, popup shows server-rendered
   `/oauth/register` page
4. User fills form, submits → server creates unverified user, sends
   OTP, redirects popup to `/oauth/verify-email?email=...&returnUrl=...`
5. User backgrounds the app, opens Mail, sees the code autosurface in
   the iOS autofill bar above the keyboard
6. User returns to the popup (still alive — `ASWebAuthenticationSession`
   survives backgrounding), taps the autofill suggestion, taps Verify
7. Server verifies code, signs user in via Identity cookie, redirects to
   `returnUrl` = `/oauth/authorize?<original-query>`
8. `/oauth/authorize` now sees the authenticated cookie + `fasolt-ios`
   first-party client (auto-consent), mints the authorization code,
   redirects to `fasolt://oauth/callback?code=...`
9. Popup closes, iOS extracts the code from the callback URL, exchanges
   it for tokens via PKCE at `/oauth/token`, saves to Keychain
10. `isAuthenticated = true`, `MainTabView` appears

For web users, steps 1–7 are identical (no `ASWebAuthenticationSession`;
just the normal browser). Step 8 redirects to whatever the web app's
return URL was (dashboard). No popup to close.

### Fixes from the PR #101 review (rolled in)

Three Apple-sign-in bugs identified in the 2026-04-08 code review of PR
#101 are fixed in the same PR:

1. **`AppleJwksCache` transient lifetime** — change `Program.cs:135`
   from `AddHttpClient<AppleJwksCache>()` to a named HttpClient plus
   singleton registration of `AppleJwksCache(IHttpClientFactory)`. Add
   a regression test that resolves the service twice and asserts same
   instance.

2. **`email_verified` string-vs-boolean comparison** —
   `AppleAuthService.cs:45` currently does
   `FindFirstValue("email_verified") == "true"`. Apple's docs permit
   the claim to arrive as either a JSON boolean or a string;
   `JwtSecurityTokenHandler` may surface bool-valued claims with varying
   casing. Replace with a case-insensitive bool parse. Add a test that
   builds a token with a boolean-valued `email_verified` claim.

3. **Link-by-email clobbers existing external provider** —
   `AppleAuthService.cs:62-68` overwrites
   `byEmail.ExternalProvider`/`ExternalProviderId` without checking
   whether they're already set. Add a guard: if
   `byEmail.ExternalProvider` is non-null and not `"Apple"`, throw an
   `AppleAuthException` with a message directing the user to sign in
   with their original provider and link Apple from settings (which
   doesn't exist yet, but the error message is forward-compatible).
   Add a test for the clobber-refusal case.

These three fixes are mechanically independent of the OTP refactor but
share the same branch because the whole auth surface is being reopened
and running one test/migration cycle is cheaper than two.

## Code deletions

**iOS (delete):**

- `fasolt.ios/Fasolt/Views/Onboarding/RegisterView.swift`
- `fasolt.ios/Fasolt/Views/Onboarding/VerifyEmailView.swift`
- `fasolt.ios/Fasolt/ViewModels/RegisterViewModel.swift`
- `fasolt.ios/Fasolt/Utilities/PasswordRules.swift`
- `fasolt.ios/Fasolt/Views/Shared/SafariView.swift` (verify no other
  consumers during implementation; if reused elsewhere, keep)
- `fasolt.ios/FasoltTests/RegisterViewModelTests.swift`
- `fasolt.ios/FasoltTests/PasswordRulesTests.swift`

**iOS (edit):**

- `fasolt.ios/Fasolt/Services/AuthService.swift` — remove
  `register(...)`, `registrationSuccess`, `lastRegisteredEmail`,
  `registrationErrorMessage`, `restoreServerURL` (if only used by
  register). Add `screen_hint=signup` support in `signIn` /
  `openAuthSession`.
- `fasolt.ios/Fasolt/Views/Onboarding/OnboardingView.swift` —
  restructure to the new layout. Drop `showVerifyEmail` state, the
  `fullScreenCover` for VerifyEmailView, `onChange` of
  `registrationSuccess`. Add the "Continue with email" button that
  calls `signIn(providerHint: "signup")`.

**Server and web client (delete):**

Since the web register flow also moves to `/oauth/register` as part of
this spec, the JSON API and Vue components used by the old web
register flow become unreachable. Before deleting, the implementation
plan will grep each file name for references to confirm no other
callers exist — but the expected state after this PR is:

- `/api/account/register` endpoint in `AccountEndpoints.cs` — deleted.
  No remaining callers once the Vue register view is gone and iOS no
  longer calls it.
- `/api/account/confirm-email` URL-token endpoint (if distinct from
  password reset's confirm endpoint — implementation plan will verify)
  — deleted.
- `fasolt.client/src/views/RegisterView.vue` — deleted. Router entry
  redirected to `/oauth/register`.
- `fasolt.client/src/views/ConfirmEmailView.vue` — deleted. Router
  entry redirected to `/oauth/verify-email`.
- Any remaining email-confirmation-token helpers in
  `AccountEndpoints.cs` that aren't also used by the password reset
  flow — deleted. Password reset still uses
  `DataProtectorTokenProvider` per #103 non-goal, so anything that
  flow depends on stays.

If any of these files turn out to have a second consumer we didn't
anticipate (e.g. an admin tool, a legacy mobile build), the plan falls
back to leaving the old surface in place and marking it `[Obsolete]`.

## Test coverage

### New backend tests (fasolt.Tests)

- `OAuthRegisterEndpointTests`
  - GET renders the register form with CSRF token
  - POST with valid fields creates unverified user, sends email,
    redirects to verify-email page
  - POST with mismatched passwords returns the form with an error
  - POST with an already-confirmed email returns the form with an error
  - POST with an unconfirmed-existing email replaces the OTP row
  - Rate limit enforced (11th request in a minute → 429)
- `OAuthVerifyEmailEndpointTests`
  - GET renders the verify form with the email pre-filled
  - POST with correct code sets `EmailConfirmed`, signs in, redirects
    to `returnUrl`
  - POST with wrong code decrements attempts, shows error
  - POST with expired code shows "request a new one"
  - POST after 5 wrong attempts locks out for 10 minutes
  - Resend POST rejects if `LastSentAt > now - 30s`
  - Resend POST rejects after `SentCount >= 5`
- `EmailVerificationCodeServiceTests`
  - `GenerateAndStoreAsync` produces a 6-digit string, stores a row
  - Hash roundtrips: verify succeeds for the right code, fails for
    wrong code
  - Constant-time compare (smoke test — time a match vs a mismatch)
  - Expiry: `VerifyAsync` returns `Expired` after 15 minutes
  - Attempt counting: returns `LockedOut` after 5 wrong attempts
  - Resend counting: `CanResendAsync` returns `TooSoon` within 30s of
    `LastSentAt`, `TooManyAttempts` once `SentCount >= 5`
- `OAuthAuthorizeScreenHintTests`
  - Unauthenticated + `screen_hint=signup` → redirect to
    `/oauth/register`
  - Unauthenticated + no hint → redirect to `/oauth/login` (existing
    behaviour)
  - Authenticated + `screen_hint=signup` → ignored, OAuth flow proceeds
- Apple bug fix tests:
  - `AppleJwksCacheDiTests` — resolve twice, assert `ReferenceEquals`
  - `AppleAuthServiceTests.ResolveUserAsync_AcceptsBooleanEmailVerifiedClaim`
  - `AppleAuthServiceTests.ResolveUserAsync_RefusesClobberOfExistingProvider`

### Removed tests

- `fasolt.ios/FasoltTests/PasswordRulesTests.swift` — the Swift rules
  are gone; server-side Identity rules are already tested
- `fasolt.ios/FasoltTests/RegisterViewModelTests.swift` — the view
  model is gone

### Playwright coverage

Per `CLAUDE.md`: "Always run Playwright browser tests after implementing
a feature." Add a Playwright flow that registers via `/oauth/register`,
reads the OTP from the dev email sender's log, enters it on
`/oauth/verify-email`, and asserts redirect to the dashboard.

## Open questions and follow-ups

None blocking. Tracked as issues:

- #103 — password reset migration to OTP (filed, not blocking this PR)
- Potential future: passwordless magic-login on top of the OTP
  infrastructure (no issue filed; notional)
- Potential future: nightly cleanup job for expired
  `EmailVerificationCode` rows (no issue filed; wait to see if the
  table grows)

## Implementation sequencing

Rough order, to be expanded into a task plan by writing-plans:

1. **Server-side OTP infrastructure** — entity, migration, service,
   tests. No UI changes yet; service is unused.
2. **`/oauth/register` + `/oauth/verify-email` HTML pages and handlers**
   with tests. Still not reachable from any UI.
3. **`screen_hint=signup` support on `/oauth/authorize`.** Now the web
   can do end-to-end register via a test harness.
4. **Fix the three Apple-sign-in bugs.** Small, independent, lands
   alongside the OTP work.
5. **Delete the native iOS register surface** — files listed above.
   Update `AuthService` and `OnboardingView`. Add `signIn(providerHint:
   "signup")` path. Rebuild iOS target.
6. **Delete the Vue `RegisterView.vue` / `ConfirmEmailView.vue` and
   `/api/account/register` endpoint.** Redirect any leftover links to
   `/oauth/register`.
7. **Playwright end-to-end test** registering a new user through the
   unified flow.
8. **Manual verification on a real iOS device** — register via popup,
   receive code, autofill, verify, land on dashboard signed in. Apple
   sign-in, GitHub sign-in.

Each step is independently mergeable if we ever need to back out.
