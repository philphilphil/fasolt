# Converge `/login` onto `/oauth/login` via Razor Pages

**Status:** design
**Branch:** `converge-login-onto-oauth`
**Depends on:** none (supersedes parts of PR #108 and #110 infrastructure decisions)
**Follow-ups filed after implementation:** Razor migration was itself one of the gaps; see Section 9 for remaining gaps that deliberately stay out of scope.

## Problem

Fasolt currently has **two login pages** for the same identity store:

1. **SPA `/login`** (`fasolt.client/src/views/LoginView.vue` + `AuthLayout.vue`) — shadcn/Tailwind, full branding, JetBrains Mono, `bg-grid` wallpaper, Remember Me checkbox, specific GitHub error-message mapping. Hits `POST /api/account/login` (JSON).

2. **Server `/oauth/login`** (`fasolt.Server/Api/Helpers/OAuthPages/OAuthLoginPage.cs` + `OAuthPageLayout.cs`) — hand-rolled HTML strings built via C# raw-string interpolation, zinc palette, system-ui font, no wallpaper, inline CSS block, generic error text. Used by iOS via `ASWebAuthenticationSession` and during any OAuth `/oauth/authorize` flow.

This duplication is a slow-motion divergence problem. Every future auth change (password policy tweak, CAPTCHA, MFA, a new consent field, localized error strings, a visual refresh) has to be made in two places. Every current change has to remember to do the same. The `OtpCodeStore<T>` extraction in PR #110 solved the same problem at a smaller scale by unifying two copies of an OTP state machine; this spec applies the same principle to the login page itself.

PR #110 also revealed three other structural problems with the current server-rendered pages that should be fixed while we're in the neighborhood:

1. **Hand-rolled HTML string templates** (`$$"""..."""` C# raw strings with `HttpUtility.HtmlEncode` calls by convention) are an XSS waiting to happen. The PR #108 comment explicitly rejects Razor with the justification "ASWebAuthenticationSession hits them cold" — but this conflates Razor Pages (server-rendered static HTML, no client dependency) with Blazor (SignalR-dependent, would break in ASWebAuthenticationSession). Microsoft's own guidance for SPA-backed .NET apps is exactly this pattern: **Razor Pages for server-rendered auth, JSON APIs for the SPA, cookie auth shared between them.** ServiceStack's canonical Vue+.NET template ships with "Tailwind Identity Auth Razor Pages" out of the box. What Fasolt does today — using minimal-API endpoints to hand-render HTML via string interpolation — is the outlier pattern.

2. **No shared stylesheet** with the SPA. The server pages' CSS is hand-maintained inline in C# code, re-translating shadcn tokens into raw CSS literals. Any future palette change has to be ported manually — the same divergence problem in miniature.

3. **No Content-Security-Policy header** on auth pages. CSP is considered defense-in-depth best practice for auth flows.

Finally, the user-visible gap that kicked this off: **after resetting a password, a web user lands on `/oauth/login` instead of the SPA `/login`**, because the reset success page links there. It works (same cookie, same `PasswordSignInAsync`), but the user sees a stripped-down page instead of the branded one they're used to. The "right fix" isn't to sniff the `returnUrl` and branch — it's to have one login page that looks right for everyone.

## Goals

1. **One login page** at `/oauth/login`, used by web users, iOS `ASWebAuthenticationSession`, and OAuth authorize flows. No sniffing, no branching, no second entry point.
2. **Server-rendered via Razor Pages**, not hand-rolled HTML strings. Auto-encoding, auto-antiforgery, model binding, tag helpers. Delete `OAuthPageLayout.cs` and all `OAuthPages/*.cs` helper files.
3. **Visual parity with the SPA aesthetic**: HSL token palette, logo + "fasolt" wordmark header above the card, fading `bg-grid` wallpaper, card chrome matching `border-border/60 rounded-lg shadow-sm`. Light theme only.
4. **Single source of truth for auth styling**: shared `auth.css` built by Vite from the same Tailwind config as the SPA, served from `wwwroot/css/auth.css`, linked from the Razor `_Layout.cshtml`. A palette change in `style.css` updates both surfaces on rebuild.
5. **CSP header** on all `/oauth/*` responses: `default-src 'self'`, no `unsafe-inline` anywhere (no inline styles, no inline scripts on auth pages).
6. **Modern SaaS session defaults**: `PasswordSignInAsync(isPersistent: true)` unconditionally. No Remember Me checkbox, no session cookie (matches Google, GitHub, Stripe, Linear, Notion, Vercel, Figma).
7. **Delete the SPA login stack**: `LoginView.vue`, `AuthLayout.vue`, `auth.login()` store method, `/login` SPA route, `POST /api/account/login` JSON endpoint, `LoginRequest` DTO.
8. **Migrate all `/oauth/*` HTML-rendering pages to Razor**, not just login — half-converged is worse than fully converged, and per-page styles would clash. Scope covers: Login, Register, VerifyEmail, ForgotPassword, ResetPassword, Consent.

## Non-goals

- **Dark mode on auth pages.** Dropped. Auth pages are short-lived; light-only matches most SaaS (Google, Stripe, Apple, Microsoft, Notion). Less CSS to maintain, zero dark-mode bridging problems. The SPA app-proper keeps its dark mode unchanged.
- **CAPTCHA / Turnstile on login POST.** Out of scope. Rate limiting via `RequireRateLimiting("auth")` is the existing bar. File as follow-up issue.
- **Session management settings page** ("see your active sessions, revoke"). Would normally pair with the persistent-session default; out of scope here.
- **Playwright visual regression.** Integration tests assert on HTML contents; visual parity is verified by manual review during implementation and by the E2E functional tests (which will catch missing form elements, broken flows, etc. — just not "did the color shade drift by 2%").
- **iOS changes.** The Razor login page is a drop-in replacement at the same URL with the same form contract. iOS hits `/oauth/authorize` → server redirects to `/oauth/login` → Razor page renders → form POST → cookie → redirect back → done. No Swift-side work.
- **`/oauth/authorize`, `/oauth/token`, `/oauth/userinfo`, `/oauth/logout`.** These are OAuth protocol endpoints that return JSON or perform redirects, not HTML. They stay as minimal-API endpoints in `OAuthEndpoints.cs`.
- **Legacy redirect endpoints** (`/login`, `/register`, `/forgot-password`, `/reset-password`, `/confirm-email`, `/verify-email`). They stay as minimal-API 3xx endpoints — not worth converting to Razor Pages for a three-line redirect.

## Architecture

### Layering

```
fasolt.Server/
  Pages/
    Oauth/
      _ViewImports.cshtml       — namespace, tag helpers
      _ViewStart.cshtml         — sets Layout
      _Layout.cshtml            — shell: logo + wordmark + @RenderBody()
      Login.cshtml              — form markup
      Login.cshtml.cs           — PageModel with [BindProperty] Input
      Register.cshtml + .cs
      VerifyEmail.cshtml + .cs
      ForgotPassword.cshtml + .cs
      ResetPassword.cshtml + .cs
      Consent.cshtml + .cs
  Api/
    Endpoints/
      OAuthEndpoints.cs         — keeps /authorize, /token, /userinfo, /logout, legacy redirects; loses every HTML-rendering page route
      AccountEndpoints.cs       — loses POST /login
    Middleware/
      ContentSecurityPolicyMiddleware.cs  — CSP header on /oauth/*
  wwwroot/
    css/
      auth.css                  — built by Vite from fasolt.client/src/auth.css

fasolt.client/
  src/
    auth.css                    — new Tailwind entry point for auth pages
  vite.config.ts                — second rollup entry, dev proxy additions
```

### URL preservation

Razor Pages route by file path by default (`Pages/Oauth/Login.cshtml` → `/Oauth/Login`, case-sensitive in some environments). To preserve the current lowercase URLs that iOS hardcodes, email links point at, and external clients expect, each page declares an explicit `@page "/oauth/login"` directive. The file-name default is ignored. URLs are grep-able and can't drift.

### Build pipeline

`fasolt.client/src/auth.css` is a new Vite entry point. It pulls in `style.css` for the HSL token layer, declares a small set of component classes via `@apply` (`.shell`, `.brand`, `.auth-card`, `.field`, `.btn`, `.btn-github`, `.error-block`, `.footer`), and ships as a ~8KB compiled stylesheet.

`fasolt.client/vite.config.ts` gets a second rollup input configured to emit an unhashed filename directly into `fasolt.Server/wwwroot/css/`:

```ts
build: {
  rollupOptions: {
    input: {
      main: 'index.html',
      auth: 'src/auth.css',
    },
    output: {
      // Main SPA bundle keeps its hashed names; auth.css emits as a stable
      // unhashed filename so the Razor _Layout.cshtml link is constant.
      // Cache busting is handled by ASP.NET's asp-append-version tag helper,
      // which hashes the file contents at serve time.
      assetFileNames: (assetInfo) =>
        assetInfo.name === 'auth.css'
          ? 'css/auth.css'
          : 'assets/[name]-[hash][extname]',
    },
  },
}
```

Vite's `outDir` is extended to also write into `fasolt.Server/wwwroot/` for the auth entry — either via a small post-build step in `package.json` or by configuring Vite's build output path. Exact mechanism picked during implementation based on the existing `fasolt.client/dist → wwwroot` flow.

In dev, `dev.sh` adds a `vite build --watch` step (or equivalent) so edits to `src/auth.css` rebuild `wwwroot/css/auth.css` continuously. ASP.NET serves it via `UseStaticFiles()` — same mechanism already used for the SPA's JS bundle.

The Razor `_Layout.cshtml` links the stylesheet via `<link rel="stylesheet" href="~/css/auth.css" asp-append-version="true">`. The `asp-append-version` tag helper computes a content hash at serve time and appends it as `?v=...`, so browsers re-fetch after every rebuild without a hashed filename.

### Data flow: web user logs in

1. Browser visits `/study`
2. SPA boots, `auth.fetchUser()` 401s
3. `router.beforeEach` → `window.location.href = '/oauth/login?returnUrl=%2Fstudy'`
4. Full-page navigation hits ASP.NET; Razor Pages route `/oauth/login` matches before `MapFallbackToFile("index.html")` in registration order
5. `LoginModel.OnGet("/study", providerHint: null)` runs, sets `Model.ReturnUrl`, returns the view
6. Razor renders `_Layout.cshtml` → pulls `/css/auth.css` → renders `Login.cshtml` body with antiforgery token injected automatically
7. User submits → `POST /oauth/login` → `LoginModel.OnPostAsync()` — `[ValidateAntiForgeryToken]` attribute handles CSRF, model binding populates `Input.Email`/`Input.Password` from form, `[Required, EmailAddress]` validation runs via `ModelState.IsValid`
8. `PasswordSignInAsync(Input.Email, Input.Password, isPersistent: true, lockoutOnFailure: true)` → Identity cookie → `Redirect(ReturnUrl)` (`/study`)
9. Browser hits `/study` with cookie; SPA boots fresh, `fetchUser()` succeeds, user lands on study

### Data flow: iOS user logs in (unchanged semantics)

1. iOS opens `ASWebAuthenticationSession` at `/oauth/authorize?client_id=...&redirect_uri=fasolt://...`
2. `/oauth/authorize` (minimal API, unchanged) sees the user isn't authenticated, redirects to `/oauth/login?returnUrl=/oauth/authorize?...`
3. Same Razor Page renders and processes the POST
4. After successful login, `LoginModel.OnPostAsync` redirects to the authorize URL, which now has a cookie; OpenIddict continues the flow to `/oauth/consent` → app custom URL scheme → sheet closes
5. iOS exchanges the auth code at `/oauth/token` → bearer token → done

**The key convergence:** both paths hit the exact same Razor Page, the exact same `_Layout.cshtml`, the exact same `auth.css`, and the exact same `PasswordSignInAsync` call. There is no web-vs-iOS branch anywhere in the auth HTML stack.

## Components

### New files

**`fasolt.Server/Pages/Oauth/_ViewImports.cshtml`**
```cshtml
@namespace Fasolt.Server.Pages.Oauth
@using Microsoft.AspNetCore.Mvc.RazorPages
@using Fasolt.Server.Domain.Entities
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

**`fasolt.Server/Pages/Oauth/_ViewStart.cshtml`**
```cshtml
@{
    Layout = "_Layout";
}
```

**`fasolt.Server/Pages/Oauth/_Layout.cshtml`** (~40 lines)
- `<head>` with viewport meta, title, `<link rel="stylesheet" href="~/css/auth.css" asp-append-version="true">`
- `<body>` with `<div class="shell"><div class="brand"><svg>...</svg><span>fasolt</span></div>@RenderBody()</div>`
- No inline `<style>`, no inline `<script>`, no `<meta>` beyond charset and viewport
- Logo SVG inline (small, not worth an extra HTTP request)

**`fasolt.Server/Pages/Oauth/Login.cshtml` + `Login.cshtml.cs`**
- `@page "/oauth/login"`
- `LoginModel : PageModel`:
  - `[BindProperty] public InputModel Input { get; set; } = new();`
  - `public string? ReturnUrl { get; set; }`
  - `public string? ErrorMessage { get; set; }`
  - `public bool GitHubEnabled { get; }` (set in ctor from `IConfiguration`)
  - `public class InputModel { [Required, EmailAddress] public string Email; [Required] public string Password; }`
  - `public IActionResult OnGet(string? returnUrl, string? providerHint, string? error)` — handles `provider_hint=github` → `Redirect("/api/account/github-login?returnUrl=...")`; handles `?error=github_auth_failed` → friendly message mapping (same map as current SPA `LoginView.vue:23-26`)
  - `public async Task<IActionResult> OnPostAsync(string? returnUrl)` — lifts `OAuthEndpoints.cs:345-411` logic verbatim, but replaces manual `ReadFormAsync()` with model binding, manual antiforgery checks with `[ValidateAntiForgeryToken]`, manual `HttpUtility.HtmlEncode` with Razor's auto-encoding, and `isPersistent: false` with `isPersistent: true`. Unverified-email branch still redirects to `/oauth/verify-email` with a freshly issued OTP (if resend window allows).
- Template: GitHub button (conditional on `@Model.GitHubEnabled`), `<form method="post">` with antiforgery auto-injected, `<input asp-for="Input.Email">`, `<span asp-validation-for="Input.Email">` for field errors, error block for `@Model.ErrorMessage`, footer with `<a asp-page="/Oauth/Register" asp-route-returnUrl="@Model.ReturnUrl">Create an account</a>` and `<a asp-page="/Oauth/ForgotPassword" asp-route-returnUrl="@Model.ReturnUrl">Forgot password?</a>`

**`fasolt.Server/Pages/Oauth/ForgotPassword.cshtml` + `.cs`**
- `@page "/oauth/forgot-password"`
- Replicates the current dual-state machine: GET with no `?sent` renders the entry form; GET with `?sent=1` renders the "check your email" confirmation
- `OnPostAsync` runs the enumeration-guarded flow from `OAuthEndpoints.cs:89-137` verbatim (generic redirect regardless of whether the email exists, external-provider, unverified, or throttled)

**`fasolt.Server/Pages/Oauth/ResetPassword.cshtml` + `.cs`**
- `@page "/oauth/reset-password"`
- `LoginModel.Success` flag toggles between form-rendering and "Password updated" view
- `OnPostAsync` runs the current reset flow: password policy pre-check (before user lookup, to avoid latency enumeration), external-provider rejection, OTP verify, `RemovePasswordAsync` + `AddPasswordAsync` (rotates SecurityStamp)
- **Named handler** `OnPostResendAsync` attached via `<button asp-page-handler="resend">` on a second form, replacing the current `/oauth/reset-password/resend` endpoint. Logic preserved verbatim from the post-PR-#110 enumeration-guarded version — all branches redirect to the same clean URL with no error param.

**`fasolt.Server/Pages/Oauth/Register.cshtml` + `.cs`**
- `@page "/oauth/register"`
- Migration of `OAuthRegisterPage.cs` and its GET/POST endpoints. Password rules live-validated client-side by the same small inline-script evaluator (moved into `Register.cshtml` — one exception to the "no inline scripts" rule, but see CSP note below).
- The inline password-rules script is a roughly 10-line pure-DOM evaluator, no dependencies, no data exfil. To keep CSP clean without `'unsafe-inline'`, the script moves to a static file `wwwroot/js/password-rules.js` and is included via `<script src="~/js/password-rules.js" asp-append-version="true" defer>`. No more inline scripts on any Razor auth page.

**`fasolt.Server/Pages/Oauth/VerifyEmail.cshtml` + `.cs`**
- `@page "/oauth/verify-email"`
- Migration of `OAuthVerifyEmailPage.cs`. Code input with `autocomplete="one-time-code"`, resend button as a named handler, lockout flash handling.

**`fasolt.Server/Pages/Oauth/Consent.cshtml` + `.cs`**
- `@page "/oauth/consent"`
- Migration of `OAuthConsentPage.cs`. Displays the client application name and requested scopes, approve/deny buttons, redirects back into the OpenIddict `/oauth/authorize` pipeline on approve.

**`fasolt.Server/Api/Middleware/ContentSecurityPolicyMiddleware.cs`** (~25 lines)
- Sets `Content-Security-Policy` header:
  ```
  default-src 'self';
  style-src 'self';
  script-src 'self';
  form-action 'self' https://github.com;
  frame-ancestors 'none';
  img-src 'self' data:;
  ```
- Registered in `Program.cs` via `app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/oauth"), b => b.UseMiddleware<ContentSecurityPolicyMiddleware>())`
- `form-action 'self' https://github.com` allows GitHub OAuth form POSTs; `data:` on `img-src` is for the inline SVG logo (actually, inline SVG is not an `img`, so this may not be needed — confirmed during implementation)

**`fasolt.client/src/auth.css`** (~100 lines)
- `@tailwind base; @tailwind components; @tailwind utilities;`
- `@import './style.css';` pulls in the HSL token layer
- `@layer components { ... }` block defines `.shell`, `.brand`, `.auth-card`, `.field`, `.btn`, `.btn-github`, `.error-block`, `.footer` via `@apply` rules using the same Tailwind utilities as the SPA's LoginView
- No `.dark` block — light theme only per Goal #6

**`fasolt.Server/wwwroot/js/password-rules.js`** (~15 lines)
- Vanilla JS password rule evaluator extracted from the current `OAuthResetPasswordPage.cs` inline script. Plus a mirror in Register for the same logic.
- Single file, cacheable, CSP-friendly.

### Modified files

**`fasolt.Server/Program.cs`**
- `builder.Services.AddRazorPages();`
- `app.MapRazorPages();` (placed *before* `MapFallbackToFile("index.html")` so Razor routes take precedence)
- Register CSP middleware conditionally on `/oauth/*`

**`fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`** — major deletions (see Section 4 rollout order)
- Keep: `/oauth/authorize`, `/oauth/token`, `/oauth/userinfo`, `/oauth/logout`, OpenIddict wiring, iOS Apple custom grant, OIDC discovery metadata
- Delete: every `MapGet/MapPost` for `/oauth/login`, `/oauth/register`, `/oauth/verify-email`, `/oauth/forgot-password`, `/oauth/reset-password`, `/oauth/reset-password/resend`, `/oauth/consent`
- Keep: legacy redirect endpoints `/login`, `/register`, `/verify-email`, `/forgot-password`, `/reset-password`, `/confirm-email` (3-line minimal-API redirects, not worth moving)

**`fasolt.Server/Api/Endpoints/AccountEndpoints.cs`**
- Delete `group.MapPost("/login", Login).RequireRateLimiting("auth")` at line 18
- Delete the `Login` method at lines 32-57

**`fasolt.Server/Application/Dtos/AccountDtos.cs`**
- Delete `LoginRequest` record (verified during implementation: no other references)

**`fasolt.client/vite.config.ts`**
- Add `auth: 'src/auth.css'` as a second rollup input
- Add `/oauth/login` to the dev-proxy allowlist

**`fasolt.client/src/router/index.ts`**
- Delete the `/login` route
- `router.beforeEach` unauthenticated branch:
  ```ts
  if (!isPublic && !auth.isAuthenticated) {
    window.location.href = `/oauth/login?returnUrl=${encodeURIComponent(to.fullPath)}`
    return false
  }
  ```

**`fasolt.client/src/stores/auth.ts`**
- Delete `login(email, password, rememberMe)` at lines 31-37
- Remove `login` from the returned object

**`fasolt.client/src/components/TopBar.vue`**
- Line 75: `router.push('/login')` → `window.location.href = '/oauth/login'`

**`fasolt.client/src/views/LandingView.vue`** (lines 33, 56) and **`fasolt.client/src/views/AlgorithmView.vue`** (line 29)
- `<RouterLink to="/login">` → `<a href="/oauth/login">`

**`dev.sh`**
- Add a `vite build --watch` or equivalent step so `auth.css` rebuilds on save during development. Exact mechanics determined during implementation — the requirement is "edit `src/auth.css`, see the Razor login page reload with new styles".

### Deleted files

- `fasolt.Server/Api/Helpers/OAuthPageLayout.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthLoginPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthRegisterPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthVerifyEmailPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthForgotPasswordPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthResetPasswordPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/OAuthConsentPage.cs`
- `fasolt.Server/Api/Helpers/OAuthPages/` (directory)
- `fasolt.client/src/views/LoginView.vue`
- `fasolt.client/src/layouts/AuthLayout.vue`

## Error handling

**Invalid email/password** → `ModelState.IsValid` passes (both fields present), `PasswordSignInAsync` returns `Succeeded=false`, `LoginModel` sets `ErrorMessage = "Invalid email or password."`, returns `Page()`. Error block renders at top of card.

**Account locked** → `result.IsLockedOut == true`, `ErrorMessage = "Account locked. Try again later."`, `Page()`.

**Unverified email** → Same semantics as the current endpoint: `SignOutAsync`, generate a fresh OTP via `IEmailVerificationCodeService` (respecting resend cooldown; swallow cap/cooldown races), redirect to `/oauth/verify-email?email=...&returnUrl=...`.

**Missing fields** → `ModelState.IsValid == false` (via `[Required]` on `InputModel`), return `Page()` with field-level errors surfaced via `<span asp-validation-for="Input.Email" class="field-error">` in the template. This is an **improvement** over the current code, which doesn't validate field presence at all — it would let a blank email reach `FindByEmailAsync` and fail with a generic "Invalid email or password."

**CSRF failure** → `[ValidateAntiForgeryToken]` on the handler returns 400 automatically. No manual `antiforgery.IsRequestValidAsync` check needed. Current code manually calls this in every endpoint; Razor does it for you.

**GitHub error callback** → `OnGet` reads `?error=github_auth_failed` (or `account_creation_failed`, etc.) from query, maps to a friendly message using the same map as the current SPA `LoginView.vue:23-26`, sets `ErrorMessage`, renders.

**Password reset success** (in `ResetPassword.cshtml.cs`) → sets `Model.Success = true`, returns `Page()`. Template renders "Password updated" view with `<a asp-page="/Oauth/Login" asp-route-returnUrl="@Model.ReturnUrl">Go to sign in</a>`. Resolves the user-visible UX gap that kicked this whole thing off — web users now land on the same branded page they started from.

**Rate limit exceeded** → `RequireRateLimiting("auth")` on the page's POST handler (via attribute or endpoint filter — determined during implementation) returns 429. Razor Pages support endpoint filters the same way minimal APIs do.

## Testing strategy

### Existing tests — update in place

- **`fasolt.Tests/Auth/OAuthForgotPasswordEndpointTests.cs`** — keep all tests. URLs are unchanged (`/oauth/forgot-password` still), HTML contents are substantively the same (form action, input names, CSRF token field name `__RequestVerificationToken`). Assertion updates limited to CSS class names if the test touches them — current tests only check for `"<form"`, `name=\"email\"`, `action=\"/oauth/forgot-password\"`, so they survive untouched.
- **`fasolt.Tests/Auth/OAuthResetPasswordEndpointTests.cs`** — same story. Including the `ResendPost_UnknownAndThrottledUsers_ProduceIdenticalRedirect` enumeration-guard test from PR #110.
- **`fasolt.Tests/Auth/OAuthRegisterEndpointTests.cs`** — update CSS selector assertions if any.
- **`fasolt.Tests/Auth/OAuthVerifyEmailEndpointTests.cs`** — update CSS selector assertions if any.

### New integration tests

**`fasolt.Tests/Auth/OAuthLoginPageTests.cs`** (new)
- `Get_Anonymous_RendersLoginForm` — GET `/oauth/login` → 200, body contains `<form action="/oauth/login"`, `name="__RequestVerificationToken"`, email/password inputs, "Sign in to fasolt" heading
- `Get_WithProviderHintGithub_RedirectsToGitHubLogin` — GET `?provider_hint=github` with GitHub configured → 302 to `/api/account/github-login?returnUrl=...`
- `Post_ValidCredentials_SetsCookieAndRedirectsToReturnUrl` — seed user, POST with CSRF, assert 302 to `returnUrl`, assert `Set-Cookie: .AspNetCore.Identity.Application=...` present
- `Post_InvalidPassword_RendersFormWithError` — assert 200, body contains "Invalid email or password."
- `Post_UnverifiedUser_RedirectsToVerifyEmail` — seed unverified user, POST valid credentials, assert 302 to `/oauth/verify-email?email=...`, assert a fresh `EmailVerificationCode` row exists in the DB
- `Post_MissingCsrf_Returns400` — POST without antiforgery token, assert 400
- `Post_LockedOutUser_RendersFormWithLockoutError` — force lockout via repeated failed attempts, assert body contains "Account locked."
- `Post_InvalidEmailFormat_RendersFieldLevelError` — POST with `email=notanemail`, assert `ModelState` error for Input.Email surfaces (tests the `[EmailAddress]` validation, which is an improvement over the current code)

**`fasolt.Tests/Auth/OAuthCspHeaderTests.cs`** (new, small)
- `Get_OAuthLogin_ReturnsCspHeader` — assert `Content-Security-Policy` header is present and contains `default-src 'self'`
- `Get_OAuthRegister_ReturnsCspHeader` — same
- `Get_NonOauthPath_DoesNotReturnCspHeader` — assert the middleware is scoped (GET `/api/health` → no CSP header)

### New E2E tests (Playwright via MCP)

Placed in `fasolt.client/e2e/` — a new directory. Fasolt does not currently have committed Playwright tests (CLAUDE.md says Playwright is invoked via MCP, ad-hoc, during development). This PR adds the first committed E2E suite, along with a `package.json` script to run it locally, and a `playwright.config.ts` configured to hit the dev stack on `http://localhost:5173`. CI integration is out of scope.

**`auth-login.spec.ts`**
1. **Happy path** — visit `/study` as anonymous → assert URL becomes `/oauth/login?returnUrl=%2Fstudy` → fill `dev@fasolt.local` / `Dev1234!` → click submit → assert URL is `/study` → assert authenticated UI element is visible (e.g. bottom nav, user menu)
2. **Wrong password** — fill valid email + wrong password → submit → assert error block visible with "Invalid email or password." → assert URL still `/oauth/login` → assert form is still editable (email field still has value, password field cleared or not — match Razor's re-render behavior)
3. **Logout returns to oauth login** — log in → click logout → assert URL is `/oauth/login` (full-page nav, confirmed by `window.location.href` not `router.push`)
4. **Landing CTA** — visit `/` as anonymous → click "Log in" CTA → assert URL is `/oauth/login`

**`auth-forgot-password.spec.ts`**
1. Login page → click "Forgot password?" → URL is `/oauth/forgot-password` → fill email → submit → assert "Check your email" screen → click "Enter reset code" → URL is `/oauth/reset-password`
2. Fill a code (captured from `DevEmailSender` — see fixture note below) + new password → submit → assert "Password updated" screen → click "Go to sign in" → URL is `/oauth/login` → fill email + new password → assert landing on `/study`

### E2E fixture: intercepting the reset email

`DevEmailSender.SendPasswordResetCodeAsync` currently logs the code via `ILogger<DevEmailSender>`. For E2E tests, the test fixture needs to read the code somehow. Three options, picked during implementation:

1. **Shared file sink** — `DevEmailSender` in test mode writes sent codes to `/tmp/fasolt-test-emails.jsonl`; Playwright reads the latest entry for the target email. Cheap, ugly.
2. **In-process queue** — `WebApplicationFactory` swaps `DevEmailSender` for a `TestEmailSender` that writes into a `ConcurrentQueue<EmailRecord>` exposed via a test-only endpoint `GET /api/test/last-email?email=...`. Test-only endpoint gated behind `ASPNETCORE_ENVIRONMENT == "Test"`. Cleaner, but requires Playwright to run against the `WebApplicationFactory` instance (not the real `./dev.sh` stack).
3. **Log scraping** — Playwright runs against a fresh `dotnet run` process and tails its stdout for the "[DEV EMAIL] Password reset code" log line. Simplest to wire up, most fragile.

I'd pick (2) — it's the pattern the existing integration tests already use (`WebApplicationFactory<Program>`), and extending it to E2E just means spinning up the factory's test server as a Playwright target. Decision deferred to implementation.

### Not doing

- **Visual regression tests.** No pixel-diff rig. Light theme only means less surface area, and manual review during implementation is sufficient.
- **Load tests.** Auth page latency isn't a concern; rate limiting caps the blast radius.
- **Fuzzing / property-based tests on the form inputs.** The CSRF + model validation + Identity's own password hashing already cover the interesting cases.

## Rollout

**Commit order inside the PR** to keep intermediate states buildable and reviewable:

1. **Add Razor Pages infrastructure.** `AddRazorPages()` + `MapRazorPages()` in Program.cs. Create `Pages/Oauth/_ViewImports.cshtml`, `_ViewStart.cshtml`, `_Layout.cshtml`. Link to a stub `auth.css` (just `body { background: white; }`). Build should pass with no actual Razor Pages yet — Razor Pages with zero pages is valid.
2. **Wire up the `auth.css` build pipeline.** Second Vite entry point, `dev.sh` update, Docker build spot-check. At this stage, `auth.css` is a real compiled stylesheet with the `.shell`/`.brand`/`.auth-card` classes from `fasolt.client/src/auth.css`. The `_Layout.cshtml` links to it. No functional test yet — nothing renders it.
3. **Migrate `Login`.** Create `Pages/Oauth/Login.cshtml` + `Login.cshtml.cs`. Delete the corresponding `OAuthLoginPage.cs` + `/oauth/login` endpoints in `OAuthEndpoints.cs`. Update existing integration tests if needed. Manual smoke test: `./dev.sh`, visit `http://localhost:5173/oauth/login`, see the Razor page with the new look, submit the dev seed user, confirm landing on `/study`.
4. **Migrate `ForgotPassword`, `ResetPassword`.** Delete the corresponding `OAuthForgotPasswordPage.cs`, `OAuthResetPasswordPage.cs`, and their endpoints. Existing tests in `OAuthForgotPasswordEndpointTests.cs` and `OAuthResetPasswordEndpointTests.cs` should continue to pass with minor selector tweaks.
5. **Migrate `Register`, `VerifyEmail`.** Delete corresponding helper files and endpoints. Update tests.
6. **Migrate `Consent`.** Delete corresponding files and endpoints.
7. **Delete `OAuthPageLayout.cs`.** Only possible after all six pages have migrated — nothing references it anymore.
8. **CSP middleware.** Add `ContentSecurityPolicyMiddleware.cs`, register in `Program.cs`. Add `OAuthCspHeaderTests.cs`. Verify no inline `<style>` or `<script>` slipped through (look for violation reports in browser devtools during manual testing).
9. **Retire SPA login.** Delete `LoginView.vue`, `AuthLayout.vue`, `auth.login()`, `/login` route, `POST /api/account/login`, `LoginRequest`. Update `router.beforeEach`, `TopBar.vue`, `LandingView.vue`, `AlgorithmView.vue`, Vite proxy. Full stack manual test: logged-out redirect to `/oauth/login`, post-logout redirect, landing CTA.
10. **Add E2E tests.** `auth-login.spec.ts`, `auth-forgot-password.spec.ts`. Run via Playwright MCP.
11. **PR description + follow-up issue filing.** See Section 9.

**Rollback posture.** Each commit in order (1-10) is a valid build. If a later commit needs to roll back, the earlier ones stay intact. In particular, commit 3 (migrate Login) is the most user-visible change — it can be reverted independently of the later Razor Page migrations. Commit 9 (retire SPA login) is the point of no return for the SPA side.

**Deployment concerns.** Production image build needs to include:
- The compiled `auth.css` from `fasolt.client/dist/assets/` into `wwwroot/css/`
- The Razor Pages compiled views (default .NET 10 behavior: compile at build, no runtime compilation dependency)
- `wwwroot/js/password-rules.js` if not already covered by the existing `COPY` rule

Spot-check the Dockerfile during implementation. No Cloudflare / Traefik changes.

## Security considerations

### What improves

- **XSS defense.** Razor's auto-encoding turns the current "remember to call `HttpUtility.HtmlEncode` on every interpolation" convention into an opt-out default. Finding an XSS now requires someone to explicitly call `@Html.Raw(userInput)`, which is greppable.
- **CSRF defense.** `[ValidateAntiForgeryToken]` replaces manual `antiforgery.IsRequestValidAsync(context)` calls. Can't be accidentally omitted on a new page — the attribute is either there or the handler rejects POSTs.
- **Content-Security-Policy.** Added as header middleware on `/oauth/*`. No inline styles (shared `auth.css`), no inline scripts (password rules extracted to `password-rules.js`), no external JS. Defense in depth against any future XSS.
- **Input validation.** `[Required, EmailAddress]` on `InputModel` catches blank submits and malformed emails at model-binding time. Current hand-rolled code doesn't validate.
- **One code path** for auth instead of two. Any future security fix applies to iOS and web by construction. Same argument as the `OtpCodeStore<T>` extraction at a larger scale.

### What stays the same

- **Session cookie properties.** `HttpOnly`, `SameSite`, `Secure` already set by ASP.NET Identity defaults.
- **Lockout policy.** Unchanged — `lockoutOnFailure: true` passed to `PasswordSignInAsync`.
- **Rate limiting.** `RequireRateLimiting("auth")` applies to the Razor Page POST handler.
- **Password policy.** Unchanged — ASP.NET Identity configuration.
- **Pepper, OTP hashing, advisory locks.** Unchanged — all still in `OtpCodeStore<T>`.
- **Enumeration guards.** The enumeration-guard patterns from PR #110 carry over verbatim to the Razor Page models.

### What this does NOT address

- **CAPTCHA on login.** Still no bot protection beyond rate limiting. File follow-up issue.
- **Session revocation UI.** Users still can't see or revoke active sessions. Follow-up.
- **Password breach check.** Pwned Passwords API integration is not done. Follow-up.
- **MFA / TOTP.** Single-factor only. Follow-up.

These were all non-goals of this spec; listing them here only to make the scope boundaries explicit.

### Session persistence tradeoff

Flipping `isPersistent: false` → `isPersistent: true` unconditionally extends the cookie lifetime from "session" (cleared on browser close) to the configured Identity default (14 days by default). This is the modern SaaS pattern (Google, GitHub, Stripe, Linear, Notion, Vercel, Figma all use long sessions without a checkbox) and it removes a UX regression ("please log in every morning"). Tradeoff: a stolen cookie on a shared machine stays valid longer. Mitigations already in place: HttpOnly + Secure cookies, SameSite, session-rotation on `RemovePassword`+`AddPassword` during reset (via SecurityStamp). Session revocation UI is a follow-up.

## Performance

Negligible. Razor Pages and minimal APIs have equivalent per-request overhead in .NET 10. The shared `auth.css` is ~8KB gzipped and cached across auth pages; current hand-rolled CSS is ~4KB inline in every HTML response, so network cost is actually slightly *better* after the first auth page visit. No DB schema changes. No additional queries.

## Follow-ups (out of scope, file as issues after merge)

1. **CAPTCHA / Turnstile on login and register POSTs** — bot deterrence beyond rate limiting.
2. **Session management settings page** — "See active sessions, revoke". Pairs naturally with the long-session default.
3. **Pwned Passwords API integration** — check new passwords against HIBP during registration and reset.
4. **MFA / TOTP** — optional second factor.
5. **Log-scraping or test-email endpoint** — if E2E fixture option (1) or (3) was chosen, consider upgrading to the cleaner (2).
6. **Auth page localization** — error messages are English-only. If Fasolt adds i18n, Razor's resource file support is the natural home.

## Open questions

None at design time. A few small decisions are deferred to implementation with explicit fallbacks noted inline:

- Exact mechanics of `auth.css` serving in dev mode (`vite build --watch` writing directly to `fasolt.Server/wwwroot/css/` is the leading candidate, but the final shape is decided once I see the existing `dev.sh` structure). Either way, the contract is "edit `src/auth.css`, see the Razor login page reload with new styles".
- E2E fixture mechanism for intercepting reset codes (file sink vs. in-process queue vs. log scraping). Preferred: in-process queue via `WebApplicationFactory` + test-only endpoint gated behind `ASPNETCORE_ENVIRONMENT == "Test"`.

## References

- PR #108 — "Extract shared chrome from server-rendered OAuth pages" — established the current hand-rolled `OAuthPageLayout.cs` pattern this spec replaces
- PR #110 — "Migrate password reset to OTP codes" — established the enumeration guards and dual-state page patterns that carry over into the Razor migrations
- [Microsoft Learn: Use Identity to secure a Web API backend for SPAs](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization)
- [Microsoft Learn: Choose between traditional web apps and single page apps](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/choose-between-traditional-web-and-single-page-apps)
- [Microsoft Learn: Razor Pages architecture and concepts](https://learn.microsoft.com/en-us/aspnet/core/razor-pages/)
- [ServiceStack: New Vue SPA Template](https://servicestack.net/posts/net8-vue-spa-template) — canonical Vue + .NET template ships with Tailwind Identity Auth Razor Pages, the pattern this spec adopts
- [Microsoft Q&A: Integrate ASP.NET Core Identity with Vue.js using Razor Pages](https://learn.microsoft.com/en-us/answers/questions/2117494/how-to-integrate-asp-net-core-identity-with-vue-js)
