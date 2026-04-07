# iOS Onboarding Restructure + Apple Sign-In + Register Form Fix

**Issues:** closes #100, partially closes #99 (iOS slice only)

## Problem

Three related issues with iOS onboarding/auth:

1. **The iOS Create Account form is outdated and missing pieces (#100):**
   it has no Terms-of-Service acceptance, no live password rules, no
   "verify your email" follow-up screen, and the field error messages
   don't match what the web form provides. The web `RegisterView.vue` has
   evolved past the iOS version.

2. **The OnboardingView has no clear hierarchy.** Today it shows a
   single "Create Account" button next to "Sign In" with no SSO
   affordance. With Sign in with Apple coming, the layout needs a
   conventional structure (SSO providers on top, local account
   underneath).

3. **App Store guideline 4.8** requires Sign in with Apple on apps that
   offer any third-party social sign-in. As soon as iOS exposes a
   GitHub button, Apple sign-in becomes mandatory for App Store review.
   Issue #99 covers Apple sign-in for both iOS and Web; this spec ships
   only the iOS slice plus the backend pieces it needs.

## Goals

- iOS register form matches web in fields, validation feedback, ToS
  acceptance, and post-register flow.
- OnboardingView is restructured to: SSO buttons → divider → local
  account actions → self-host link.
- Sign in with Apple works on iOS end-to-end and persists OAuth tokens
  via the existing Keychain mechanism.
- "Continue with GitHub" button on iOS, feature-flagged the same way
  as the web (`/api/health` `features.githubLogin`).

## Non-goals

- Web Sign in with Apple (#99 stays open for that).
- Account-linking UI for users who already have a password account
  with the same email — auto-link silently if Apple's `email_verified`
  is true; otherwise create a separate account.
- Backend GitHub flow changes for iOS (the iOS GitHub button reuses the
  existing `ASWebAuthenticationSession` PKCE path; no provider-hint
  shortcut yet).
- Native email/password sign-in on iOS — Sign In stays as the existing
  PKCE web-view flow. Only Create Account is a native form.

## High-level design

### New OnboardingView layout

```
[ Logo + tagline ]

— SSO section —
[  Continue with Apple  ]      ← always visible on iOS 13+ (required)
[  Continue with GitHub ]      ← only if features.githubLogin

———  or  ———

— Email account section —
[  Sign In  ]                  ← opens existing OAuth web flow
[  Create account  ]           ← pushes new RegisterView

[ Self-hosting? Change server ] (small text link, current behaviour)
```

There are **no native email/password fields on OnboardingView**. The
Create Account button pushes a native registration form (which is the
only place users type credentials directly into native fields).
Sign In continues to open the existing PKCE web view.

### Register form (#100)

`RegisterView.swift` is rewritten to mirror `RegisterView.vue`:

- Email field
- Password field
- Confirm password field
- **Live password rule checklist** under the password field — four
  rules with green/grey indicators that update as the user types:
  - At least 8 characters
  - Uppercase letter
  - Lowercase letter
  - Number
  Logic lives in a small Swift equivalent of `usePasswordRules`.
- "Passwords don't match" inline error (only after confirm field is
  non-empty).
- **ToS acceptance checkbox** — `Toggle` styled as a checkbox, with
  inline `Button` text "Terms of Service" that opens
  `https://<serverURL>/terms` in an in-app `SFSafariViewController`
  sheet (via a thin `SafariView` `UIViewControllerRepresentable`
  wrapper).
- Submit button is disabled until email is non-empty, all four password
  rules pass, passwords match, and ToS is checked.
- API field errors are surfaced inline at the top of the form (matches
  web's `errors` block).
- On success, push a new **`VerifyEmailView`** screen instead of
  dismissing back to OnboardingView. The new screen explains "We've
  sent a verification email to <email>. Please check your inbox before
  signing in." with a single "Back to sign in" button that pops back to
  OnboardingView.

### Sign in with Apple (iOS slice of #99)

**iOS:**

- New `SignInWithAppleButton` (from `AuthenticationServices`) on
  OnboardingView.
- On tap → `ASAuthorizationAppleIDProvider().createRequest()` requests
  `[.fullName, .email]` scopes.
- `ASAuthorizationController` runs the flow; on success the credential
  yields `identityToken` (Apple-issued JWT), `authorizationCode`, and
  on first sign-in only: `email` and `fullName`.
- iOS POSTs to `/oauth/token` with a custom grant type
  `urn:fasolt:apple` and parameters
  `{client_id: "fasolt-ios", identity_token: <jwt>, full_name: <optional>}`.
- Server response is the standard OAuth `TokenResponse` (access +
  refresh + expiry). iOS stores them via the existing
  `exchangeCode`-style logic in `AuthService`.

**Backend:**

- Register a custom OpenIddict grant type `urn:fasolt:apple` in the
  existing `/oauth/token` endpoint (`OAuthEndpoints.cs`).
- New `AppleAuthService` (Application layer):
  1. Fetches Apple's JWKS from `https://appleid.apple.com/auth/keys`
     (cached for at least an hour).
  2. Validates the identity token: signature against JWKS, `iss ==
     https://appleid.apple.com`, `aud == <Apple__BundleId>`,
     `exp` in the future.
  3. Extracts `sub` (Apple's stable user ID), `email`, `email_verified`.
  4. Looks up `AppUser` by `ExternalProvider == "Apple"` and
     `ExternalProviderId == sub`.
  5. If not found and `email != null && email_verified == true`, looks
     up by email and links Apple to that account if no other external
     provider is set.
  6. If still not found, creates a new `AppUser` with
     `EmailConfirmed = true`, `ExternalProvider = "Apple"`,
     `ExternalProviderId = sub`, and a synthetic `UserName` derived
     from `sub`. If Apple returned a relay email
     (`@privaterelay.appleid.com`), store it as-is.
- The `/oauth/token` handler issues the OpenIddict token pair the same
  way it does for `authorization_code`, by signing in a
  `ClaimsPrincipal` for the resolved user.
- The Bundle ID and Apple Team ID come from new appsettings keys:
  `Apple__BundleId`, `Apple__TeamId` (TeamId only needed if we ever
  generate a client_secret JWT for Apple's REST API; not needed for
  identity-token validation, but listed here so we don't forget). The
  feature is gated on `Apple__BundleId` being non-empty.
- Add `features.appleLogin` to `/api/health`'s features object so the
  iOS button can hide itself in dev environments without Apple
  configured.

### GitHub button on iOS

- New "Continue with GitHub" button below the Apple button, only
  rendered when `/api/health` reports `features.githubLogin == true`.
- On tap it does the **same thing** as the existing Sign In button:
  opens `/oauth/authorize` in `ASWebAuthenticationSession`, completes
  PKCE, stores tokens. The user picks "Sign in with GitHub" on the
  rendered web login page. (A future improvement would be a
  `provider_hint=github` query param that auto-redirects, but that's
  out of scope here.)
- Mechanically the GitHub button is a *visual shortcut* for the same
  flow Sign In already runs — its only purpose is discoverability.

### Feature flags on iOS

The iOS app currently does not query `/api/health` for features. Add a
small `FeatureFlagsService` (or extend an existing one) that fetches
`features` once at app start and exposes `githubLogin` and `appleLogin`
as observable booleans. OnboardingView reads these to conditionally
render the SSO buttons.

For self-hosters whose server doesn't expose `appleLogin`, the Apple
button is hidden — but in production iOS, Apple sign-in is always
available because hosted fasolt.app will always have `Apple__BundleId`
configured.

## Components and files

**iOS — new files:**

- `fasolt.ios/Fasolt/Views/Onboarding/RegisterView.swift` (rewrite)
- `fasolt.ios/Fasolt/Views/Onboarding/VerifyEmailView.swift` (new)
- `fasolt.ios/Fasolt/Views/Shared/SafariView.swift` (new — thin
  `SFSafariViewController` wrapper)
- `fasolt.ios/Fasolt/Utilities/PasswordRules.swift` (new — pure
  function returning the four-rule list)
- `fasolt.ios/Fasolt/Services/FeatureFlagsService.swift` (new)

**iOS — modified:**

- `fasolt.ios/Fasolt/Views/Onboarding/OnboardingView.swift` (restructured
  layout, SSO buttons, divider, local account section)
- `fasolt.ios/Fasolt/ViewModels/RegisterViewModel.swift` (uses
  `PasswordRules`, exposes `tosAccepted`, computes `canSubmit` to
  match web)
- `fasolt.ios/Fasolt/Services/AuthService.swift` (new
  `signInWithApple(...)` method that POSTs to `/oauth/token` with the
  custom grant and persists tokens; reuses `exchangeCode` token
  storage path)
- `fasolt.ios/Fasolt/Models/APIModels.swift` (add request/response
  types for the Apple grant if needed)
- `fasolt.ios/Fasolt/FasoltApp.swift` (wire `FeatureFlagsService`)

**Backend — modified:**

- `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` — handle
  `grant_type == "urn:fasolt:apple"` in `/oauth/token`
- `fasolt.Server/Api/Endpoints/HealthEndpoints.cs` — add
  `features.appleLogin`

**Backend — new:**

- `fasolt.Server/Application/Auth/AppleAuthService.cs`
- `fasolt.Server/Application/Auth/AppleJwksCache.cs`

**Backend — config:**

- `fasolt.Server/appsettings.json` and `appsettings.Production.json` —
  add `Apple` section
- `.env.example` — add `Apple__BundleId`

**Tests:**

- `fasolt.Tests/Auth/AppleAuthServiceTests.cs` — JWT validation happy
  path, expired token, wrong audience, wrong issuer, missing/unverified
  email, new user creation, existing user link by sub, link by
  verified email
- `fasolt.ios/FasoltTests/PasswordRulesTests.swift` — four rule edge
  cases
- `fasolt.ios/FasoltTests/RegisterViewModelTests.swift` — `canSubmit`
  truth table including ToS gate
- Playwright (post-implementation): no web changes here, but run a
  smoke test of `/terms` to make sure the iOS deep link target
  renders.

## Data flow — Apple sign-in

```
iOS user taps "Continue with Apple"
  ↓
ASAuthorizationController → identityToken (JWT)
  ↓
POST /oauth/token
  grant_type = urn:fasolt:apple
  client_id  = fasolt-ios
  identity_token = <JWT>
  ↓
OAuthEndpoints handler dispatches custom grant to AppleAuthService
  ↓
AppleAuthService:
  validate JWT signature (JWKS, cached)
  validate iss/aud/exp
  extract sub, email, email_verified
  resolve or create AppUser
  ↓
OAuthEndpoints signs in ClaimsPrincipal → OpenIddict issues
  access_token + refresh_token
  ↓
iOS receives TokenResponse, stores in Keychain via existing path
  (clientId, accessToken, refreshToken, tokenExpiry)
  ↓
isAuthenticated = true → MainTabView
```

## Error handling

- **Apple JWT validation failure** → `/oauth/token` returns
  `invalid_grant`. iOS shows "Could not sign in with Apple. Please try
  again."
- **JWKS fetch failure** → 503 to client; iOS shows generic "Could not
  reach Apple. Check your connection."
- **Email taken by a password account, Apple email unverified** →
  refuse to auto-link, return `invalid_grant` with detail "An account
  with this email already exists. Sign in with your password and link
  Apple from settings." (Settings linking is future work — for now the
  message just stops the user.)
- **Register form**: existing `registrationErrorMessage(from:)` mapping
  in `AuthService` already covers duplicate-email, password-rule
  failures, etc. — extend if any new server messages are introduced.
- **ToS link cannot construct URL** (malformed `serverURL`) → fall back
  to `https://fasolt.app/terms`.

## Testing strategy

- Unit-test `PasswordRules` and `RegisterViewModel.canSubmit` on iOS.
- Unit-test `AppleAuthService` token validation paths with mocked
  JWKS.
- Manual end-to-end on a real device with a real Apple ID (Apple
  sign-in does not work in the simulator without a signed-in Apple
  account; document this in the PR).
- Manual end-to-end of the new register flow against a local backend:
  register → see verify-email screen → click verification link in
  inbox → sign in.
- Smoke-test the GitHub button on iOS against a backend with
  `GITHUB_CLIENT_ID` set.

## Open risks

- **Apple JWKS validation correctness.** Wrong audience or issuer
  validation is a common source of identity bypass. Tests must cover
  the negative paths explicitly.
- **OpenIddict custom grant wiring.** The exact extension point for
  custom `grant_type` values in the project's OpenIddict version
  needs to be confirmed during implementation; if the registered
  grant types are restricted, the fallback is a separate
  `/api/auth/apple` endpoint that internally calls the same OpenIddict
  token issuance code path.
- **Apple's relay email** (`@privaterelay.appleid.com`) means we may
  store an opaque email that can't receive normal product email until
  the user supplies a real one. Out of scope to handle here, but worth
  flagging.
- **App Store review** still requires the actual Apple Developer
  console configuration (App ID with Sign in with Apple capability,
  Services ID, key) — that's not a code change but it must be done
  before the PR can be shipped to TestFlight.
