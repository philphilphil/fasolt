# GitHub Social Login Design

## Summary

Add "Sign in with GitHub" as an alternative to email+password authentication. GitHub is the first social login provider — Google and Apple are future follow-ups (#59).

## Scope

- GitHub OAuth login only (no Google/Apple)
- No account linking or unlinking in settings
- Auto-create account on first GitHub login using verified GitHub email
- Reject login if email already exists as a password-based account
- Button on all three login surfaces: web login, web register, server-rendered OAuth login

## Auth Flow

1. User clicks "Sign in with GitHub"
2. Backend issues an ASP.NET Core authentication challenge → redirects user to GitHub's OAuth authorize URL
3. User authorizes on GitHub → GitHub redirects back to `/api/account/github-callback`
4. Backend exchanges authorization code for GitHub user info (email, ID, name)
5. Lookup:
   - **Email matches existing password-based account** → reject with error: "An account with this email already exists. Please sign in with your password."
   - **Email matches existing GitHub-linked account** → sign in via cookie
   - **No account exists** → create new `AppUser` with `EmailConfirmed = true`, `ExternalProvider = "GitHub"`, `ExternalProviderId = <github-user-id>`, sign in via cookie
6. Redirect to app (or resume OAuth flow if login was triggered mid-MCP-auth)

## Backend Changes

### NuGet Package

Add `AspNet.Security.OAuth.GitHub` — the community-maintained ASP.NET Core GitHub auth handler.

### Configuration

Add to `.env` / `.env.example`:

```
GitHub__ClientId=<from GitHub OAuth App>
GitHub__ClientSecret=<from GitHub OAuth App>
```

GitHub OAuth App settings:
- Homepage URL: `https://fasolt.app`
- Authorization callback URL: `https://fasolt.app/api/account/github-callback`
- In dev: `http://localhost:8080/api/account/github-callback`

### Program.cs

Register the GitHub authentication scheme:

```csharp
services.AddAuthentication()
    .AddGitHub(options =>
    {
        options.ClientId = config["GitHub:ClientId"];
        options.ClientSecret = config["GitHub:ClientSecret"];
        options.Scope.Add("user:email");
        options.CallbackPath = "/api/account/github-callback";
        options.SaveTokens = false;
    });
```

Only register the scheme when `GitHub:ClientId` is configured (same pattern as APNs — feature is opt-in per environment).

### AppUser Entity

Add two fields to `AppUser`:

- `ExternalProvider` (string, nullable) — e.g., `"GitHub"`. Null for password-based accounts.
- `ExternalProviderId` (string, nullable) — the GitHub user ID. Used for matching on subsequent logins.

EF migration to add these columns. No data migration needed — existing users have null (password-based).

### Account Endpoints

Add to `AccountEndpoints.cs`:

**`GET /api/account/github-login`** — initiates GitHub OAuth flow.
- Accepts optional `returnUrl` query parameter (for post-login redirect).
- Stores `returnUrl` in the authentication properties.
- Returns a `ChallengeResult` for the GitHub scheme.

**`GET /api/account/github-callback`** — handled by the ASP.NET Core GitHub auth middleware (via `CallbackPath`). After the middleware processes the callback, a custom handler:
1. Reads the GitHub claims (email, ID, name) from the external identity.
2. Looks up the user by `ExternalProvider = "GitHub"` and `ExternalProviderId`.
3. If not found, looks up by email:
   - If an account exists with that email → redirect to login page with error query param.
   - If no account exists → create new `AppUser`, set `ExternalProvider`/`ExternalProviderId`, `EmailConfirmed = true`, sign in.
4. If found → sign in with the existing GitHub-linked account.
5. Redirect to `returnUrl` or `/`.

### OAuth Login Page Integration

The server-rendered `/oauth/login` page currently shows email+password. Add a "Sign in with GitHub" link/button that points to `/api/account/github-login?returnUrl=/oauth/authorize?{original-query}`.

After GitHub auth completes, the user is redirected back to the OAuth authorize endpoint, which now sees an authenticated cookie and proceeds with the OpenIddict flow.

### Rate Limiting

The GitHub callback endpoint should use the existing `auth` rate limit policy.

## Frontend Changes

### LoginView.vue

Add a "Sign in with GitHub" button below the login form. The button is a simple `<a>` link to `/api/account/github-login` (full page navigation, not an API call — the browser needs to follow the redirect to GitHub).

Show an error message when the URL contains an error query param (e.g., `?error=email_exists`).

### RegisterView.vue

Same "Sign in with GitHub" button. Since GitHub auto-creates accounts, this works as both login and registration.

### Styling

- GitHub button uses a dark/black style with the GitHub octocat SVG icon
- Separator between the form and social button: "or" divider line
- Button text: "Sign in with GitHub"

## Server-Rendered OAuth Login Page

Add the same GitHub button to the inline HTML in `OAuthEndpoints.cs`. The `returnUrl` encodes the original OAuth authorize request so the flow resumes after GitHub auth.

## Config Summary

| Key | Required | Description |
|-----|----------|-------------|
| `GitHub:ClientId` | Yes (to enable) | GitHub OAuth App client ID |
| `GitHub:ClientSecret` | Yes (to enable) | GitHub OAuth App client secret |

Feature is disabled when credentials are not configured — no GitHub button shown, no scheme registered.

## Security Considerations

- Only accept verified emails from GitHub (GitHub's API indicates email verification status)
- GitHub user ID (`ExternalProviderId`) is the stable identifier — email can change on GitHub
- No auto-linking: existing password accounts cannot be taken over via GitHub
- CSRF: ASP.NET Core's external auth handler includes state parameter validation
- The callback endpoint validates the correlation cookie set during the challenge

## Out of Scope

- Account linking settings (add/remove GitHub from existing account)
- Google and Apple login (future follow-ups)
- Email verification gating (#58 — independent work)
- Multiple social providers per account
