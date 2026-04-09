# iOS Onboarding Restructure + Apple Sign-In + Register Form Fix — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a single PR that (1) brings the iOS Create Account form up to parity with the web (live password rules, ToS, verify-email follow-up), (2) restructures `OnboardingView` so SSO providers sit above local-account actions, and (3) implements Sign in with Apple end-to-end on iOS plus a discoverable GitHub button — closes #100 and partially closes #99 (iOS slice only).

**Architecture:** Apple sign-in uses a custom OAuth grant type `urn:fasolt:apple` registered on the existing `/oauth/token` endpoint, so all iOS token issuance flows through one endpoint and refresh tokens come for free. The iOS GitHub button reuses the existing PKCE web flow with a new `provider_hint=github` query parameter that makes `/oauth/login` redirect straight to GitHub instead of rendering its HTML form. The register form is rewritten as a native SwiftUI view that mirrors `RegisterView.vue` field-for-field, with a `SFSafariViewController` sheet for the ToS link and a new `VerifyEmailView` pushed on success.

**Tech Stack:** .NET 10 / ASP.NET Core / OpenIddict / Microsoft.IdentityModel.Tokens (JWT validation) / xUnit + FluentAssertions (backend tests). SwiftUI / `AuthenticationServices` (Apple sign-in) / `SafariServices` / `URLSession` / Swift Testing or XCTest (iOS tests).

**Spec:** `docs/superpowers/specs/2026-04-07-ios-onboarding-restructure-design.md`

**Issues:** closes #100; partially closes #99 (iOS slice — leave #99 open for the web slice)

---

## File Structure

### Backend (new)

- `fasolt.Server/Application/Auth/AppleJwksCache.cs` — fetches Apple's JWKS document (`https://appleid.apple.com/auth/keys`), caches the parsed `JsonWebKeySet` in memory for an hour, refreshes on miss. Single responsibility: Apple key material.
- `fasolt.Server/Application/Auth/AppleAuthService.cs` — validates an Apple identity token using `AppleJwksCache`, resolves or creates an `AppUser`, returns the resolved user. Single responsibility: Apple identity-token → AppUser.

### Backend (modified)

- `fasolt.Server/Program.cs` — register Apple's HttpClient + the two new services (`AppleJwksCache`, `AppleAuthService`); register `urn:fasolt:apple` as an allowed custom flow on the OpenIddict server; ensure the `fasolt-ios` first-party client seed adds the new grant-type permission idempotently (so existing dev databases pick it up too).
- `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` — third branch in the `/oauth/token` POST handler matching `request.GrantType == "urn:fasolt:apple"`; new `provider_hint` handling at the top of the `/oauth/login` GET handler.
- `fasolt.Server/Api/Endpoints/HealthEndpoints.cs` — add `appleLogin` to the `features` object.
- `.env.example` — add `Apple__BundleId` placeholder.

### Backend tests (new)

- `fasolt.Tests/Auth/AppleAuthServiceTests.cs` — happy path, expired, wrong issuer, wrong audience, signature mismatch, new user creation, link by sub, link by verified email, refuse link on unverified email.
- `fasolt.Tests/Auth/AppleJwksCacheTests.cs` — fetch + cache + refresh after expiry.

### iOS (new)

- `fasolt.ios/Fasolt/Utilities/PasswordRules.swift` — pure function returning `[PasswordRule]` with the four web rules.
- `fasolt.ios/Fasolt/Views/Shared/SafariView.swift` — `UIViewControllerRepresentable` wrapper around `SFSafariViewController`.
- `fasolt.ios/Fasolt/Views/Onboarding/VerifyEmailView.swift` — "Check your email" screen pushed after successful registration.
- `fasolt.ios/Fasolt/Services/FeatureFlagsService.swift` — fetches `/api/health` once at app start, exposes observable `githubLogin` and `appleLogin` booleans.

### iOS (modified)

- `fasolt.ios/Fasolt/Views/Onboarding/RegisterView.swift` — full rewrite to mirror `RegisterView.vue`.
- `fasolt.ios/Fasolt/ViewModels/RegisterViewModel.swift` — uses `PasswordRules`, exposes `tosAccepted`, computes `canSubmit` to match web semantics, uses `registrationCompleted` flag instead of `registrationSuccess` so the success branch can navigate via SwiftUI navigation rather than dismiss.
- `fasolt.ios/Fasolt/Views/Onboarding/OnboardingView.swift` — restructured: SSO section (Apple + optional GitHub) → "or" divider → local account (Sign In + Create account) → self-host link.
- `fasolt.ios/Fasolt/Services/AuthService.swift` — new `signInWithApple(identityToken:)` method that POSTs to `/oauth/token` with `grant_type=urn:fasolt:apple`, parses the standard `TokenResponse`, persists tokens via the same logic as `exchangeCode(...)`. Also a new `signInWithGitHub(serverURL:)` method that calls `signIn(...)` with a `providerHint` parameter (existing `signIn` extended to accept the hint).
- `fasolt.ios/Fasolt/FasoltApp.swift` — instantiate `FeatureFlagsService`, kick off its initial fetch, inject into the environment.

### iOS tests (new)

- `fasolt.ios/FasoltTests/PasswordRulesTests.swift` — table-driven tests for the four rules.
- `fasolt.ios/FasoltTests/RegisterViewModelTests.swift` — `canSubmit` truth table including ToS gate.

---

## Coding rules for every task

- **TDD always**: write the failing test first, run it, watch it fail with the specific error you expect, *then* implement.
- **Commit after each task**: no batching commits across tasks. Use short conventional-commit-style messages (`feat:`, `fix:`, `test:`, `refactor:`).
- **Backend test isolation**: existing tests use `Fasolt.Tests.Helpers.TestDb` (see `GitHubAuthTests.cs` for the pattern — `IAsyncLifetime`, in-memory or test Postgres). Reuse that.
- **iOS naming**: follow existing repo style — `@Observable` view models in `ViewModels/`, services in `Services/`, view files named `<Thing>View.swift`.
- **No emojis** in code or commit messages.
- **Don't run `dotnet format`** unless a hook tells you to — match existing style (4-space indent, namespace `Fasolt.Server.*`).

---

## Phase 1 — Backend foundation for Apple sign-in

### Task 1: Add `Apple__BundleId` configuration plumbing

**Files:**
- Modify: `fasolt.Server/appsettings.json`
- Modify: `fasolt.Server/appsettings.Development.json`
- Modify: `.env.example`

- [ ] **Step 1: Add the configuration key to appsettings.json**

Open `fasolt.Server/appsettings.json` and add a top-level `Apple` section before the closing brace:

```json
"Apple": {
  "BundleId": ""
}
```

- [ ] **Step 2: Add the same key (empty) to appsettings.Development.json**

Same `"Apple": { "BundleId": "" }` block. Empty string disables the feature locally unless a developer fills it in.

- [ ] **Step 3: Document the env var in .env.example**

Append to `.env.example`:

```
# Sign in with Apple — bundle ID of the iOS app, validated as the audience on Apple identity tokens
Apple__BundleId=com.fasolt.app
```

- [ ] **Step 4: Verify the build still succeeds**

Run from repo root:

```bash
dotnet build fasolt.Server
```

Expected: build succeeds, no warnings about the new config key.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/appsettings.json fasolt.Server/appsettings.Development.json .env.example
git commit -m "feat(auth): add Apple__BundleId configuration plumbing"
```

---

### Task 2: Expose `appleLogin` feature flag on `/api/health`

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/HealthEndpoints.cs`
- Test: `fasolt.Tests/HealthEndpointsTests.cs` *(create if missing — there is no existing test file for this endpoint)*

- [ ] **Step 1: Write the failing test**

Create `fasolt.Tests/HealthEndpointsTests.cs`:

```csharp
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Fasolt.Tests;

public class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Apple:BundleId"] = "com.fasolt.app",
                    ["GITHUB_CLIENT_ID"] = "test-github-id",
                });
            });
        });
    }

    [Fact]
    public async Task Health_ReportsAppleAndGithubFeatureFlags()
    {
        var client = _factory.CreateClient();
        var response = await client.GetFromJsonAsync<HealthResponse>("/api/health");
        response.Should().NotBeNull();
        response!.Features.GithubLogin.Should().BeTrue();
        response.Features.AppleLogin.Should().BeTrue();
    }

    private record HealthResponse(string Status, string Version, FeaturesResponse Features);
    private record FeaturesResponse(bool GithubLogin, bool AppleLogin);
}
```

If the test project doesn't already reference `Microsoft.AspNetCore.Mvc.Testing`, add it:

```bash
dotnet add fasolt.Tests package Microsoft.AspNetCore.Mvc.Testing
```

- [ ] **Step 2: Run the test, expect failure**

```bash
dotnet test fasolt.Tests --filter HealthEndpointsTests
```

Expected: test fails because `appleLogin` doesn't exist on the response payload yet (or because `Program` isn't internal-visible — see Step 3 if so).

- [ ] **Step 3: If `Program` is not visible to tests, expose it**

If you get a compile error like "Program is inaccessible", add this at the very bottom of `fasolt.Server/Program.cs`:

```csharp
public partial class Program { }
```

Re-run the test and confirm it now fails on the assertion (not on compilation).

- [ ] **Step 4: Add the feature flag to the endpoint**

Edit `fasolt.Server/Api/Endpoints/HealthEndpoints.cs`. Replace the `features = new { ... }` block with:

```csharp
features = new
{
    githubLogin = !string.IsNullOrEmpty(configuration["GITHUB_CLIENT_ID"]),
    appleLogin = !string.IsNullOrEmpty(configuration["Apple:BundleId"]),
},
```

- [ ] **Step 5: Re-run the test, expect pass**

```bash
dotnet test fasolt.Tests --filter HealthEndpointsTests
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Api/Endpoints/HealthEndpoints.cs fasolt.Tests/HealthEndpointsTests.cs fasolt.Server/Program.cs fasolt.Tests/fasolt.Tests.csproj
git commit -m "feat(auth): expose appleLogin feature flag on /api/health"
```

---

### Task 3: Implement `AppleJwksCache`

Apple publishes the public keys used to sign identity tokens at `https://appleid.apple.com/auth/keys`. We fetch them once, cache for an hour, refresh on cache miss or after expiry.

**Files:**
- Create: `fasolt.Server/Application/Auth/AppleJwksCache.cs`
- Test: `fasolt.Tests/Auth/AppleJwksCacheTests.cs`

- [ ] **Step 1: Write the failing test**

Create `fasolt.Tests/Auth/AppleJwksCacheTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Fasolt.Server.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace Fasolt.Tests.Auth;

public class AppleJwksCacheTests
{
    private const string SampleJwks = """
    {
      "keys": [
        {
          "kty": "RSA",
          "kid": "abc123",
          "use": "sig",
          "alg": "RS256",
          "n": "xLzYzLN6...sample...IDcw",
          "e": "AQAB"
        }
      ]
    }
    """;

    [Fact]
    public async Task GetKeysAsync_FetchesAndCachesJwks()
    {
        var handler = new StubHandler(SampleJwks);
        var httpClient = new HttpClient(handler);
        var cache = new AppleJwksCache(httpClient, TimeProvider.System);

        var first = await cache.GetKeysAsync();
        var second = await cache.GetKeysAsync();

        first.Should().NotBeNull();
        first.Keys.Should().HaveCount(1);
        handler.CallCount.Should().Be(1, "the second call should be served from cache");
        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task GetKeysAsync_RefetchesAfterExpiry()
    {
        var handler = new StubHandler(SampleJwks);
        var httpClient = new HttpClient(handler);
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new AppleJwksCache(httpClient, time);

        await cache.GetKeysAsync();
        time.Advance(TimeSpan.FromHours(2));
        await cache.GetKeysAsync();

        handler.CallCount.Should().Be(2);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        private readonly string _body;
        public StubHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    }
}
```

- [ ] **Step 2: Run the test, expect compile failure**

```bash
dotnet test fasolt.Tests --filter AppleJwksCacheTests
```

Expected: compile error — `AppleJwksCache` does not exist.

- [ ] **Step 3: Add the `Microsoft.IdentityModel.Tokens` package if not present**

```bash
dotnet add fasolt.Server package Microsoft.IdentityModel.Tokens
dotnet add fasolt.Server package System.IdentityModel.Tokens.Jwt
```

(Skip if `dotnet list fasolt.Server package` already shows them.)

- [ ] **Step 4: Implement `AppleJwksCache`**

Create `fasolt.Server/Application/Auth/AppleJwksCache.cs`:

```csharp
using System.Net.Http.Json;
using Microsoft.IdentityModel.Tokens;

namespace Fasolt.Server.Application.Auth;

public sealed class AppleJwksCache
{
    private const string AppleJwksUrl = "https://appleid.apple.com/auth/keys";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private JsonWebKeySet? _cached;
    private DateTimeOffset _expiresAt;

    public AppleJwksCache(HttpClient httpClient, TimeProvider time)
    {
        _httpClient = httpClient;
        _time = time;
    }

    public async Task<JsonWebKeySet> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        if (_cached is not null && now < _expiresAt)
            return _cached;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            now = _time.GetUtcNow();
            if (_cached is not null && now < _expiresAt)
                return _cached;

            var json = await _httpClient.GetStringAsync(AppleJwksUrl, cancellationToken);
            var keys = new JsonWebKeySet(json);
            _cached = keys;
            _expiresAt = now.Add(CacheLifetime);
            return keys;
        }
        finally
        {
            _lock.Release();
        }
    }
}
```

- [ ] **Step 5: Run the test, expect pass**

```bash
dotnet test fasolt.Tests --filter AppleJwksCacheTests
```

Expected: both tests pass.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Application/Auth/AppleJwksCache.cs fasolt.Tests/Auth/AppleJwksCacheTests.cs fasolt.Server/fasolt.Server.csproj
git commit -m "feat(auth): add AppleJwksCache for Sign in with Apple key material"
```

---

### Task 4: Implement `AppleAuthService` — JWT validation + user resolution

This is the unit that turns an Apple identity token into a database user. The trickiest test is the JWT validation; we use `JwtSecurityTokenHandler.ValidateToken` with `IssuerSigningKeys` from the JWKS cache.

**Files:**
- Create: `fasolt.Server/Application/Auth/AppleAuthService.cs`
- Test: `fasolt.Tests/Auth/AppleAuthServiceTests.cs`
- Modify: `fasolt.Tests/Helpers/TestDb.cs` *(only if it does not already expose a way to instantiate UserManager — see step 3)*

- [ ] **Step 1: Write the failing tests**

Create `fasolt.Tests/Auth/AppleAuthServiceTests.cs`. Because we need a real signature, the test generates an RSA keypair, signs a JWT with it, and stuffs the public half into a stub `AppleJwksCache`. The stub allows us to test happy and negative paths without contacting Apple.

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests.Auth;

public class AppleAuthServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private RSA _signingKey = null!;
    private string _kid = "test-kid";
    private const string BundleId = "com.fasolt.app";

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _signingKey = RSA.Create(2048);
    }

    public async Task DisposeAsync()
    {
        _signingKey.Dispose();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task ResolveUserAsync_CreatesNewUser_WhenNotFound()
    {
        var token = CreateAppleToken(sub: "001", email: "user@icloud.com", emailVerified: true);
        var service = await CreateService();

        var user = await service.ResolveUserAsync(token);

        user.Should().NotBeNull();
        user!.ExternalProvider.Should().Be("Apple");
        user.ExternalProviderId.Should().Be("001");
        user.Email.Should().Be("user@icloud.com");
        user.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveUserAsync_ReusesExistingAppleUser()
    {
        var existingId = Guid.NewGuid().ToString();
        await using (var db = _db.CreateDbContext())
        {
            db.Users.Add(new AppUser
            {
                Id = existingId,
                UserName = "apple-001",
                NormalizedUserName = "APPLE-001",
                Email = "user@icloud.com",
                NormalizedEmail = "USER@ICLOUD.COM",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ExternalProvider = "Apple",
                ExternalProviderId = "001",
            });
            await db.SaveChangesAsync();
        }

        var token = CreateAppleToken(sub: "001", email: null, emailVerified: false);
        var service = await CreateService();

        var user = await service.ResolveUserAsync(token);
        user!.Id.Should().Be(existingId);
    }

    [Fact]
    public async Task ResolveUserAsync_LinksByVerifiedEmail()
    {
        var existingId = Guid.NewGuid().ToString();
        await using (var db = _db.CreateDbContext())
        {
            db.Users.Add(new AppUser
            {
                Id = existingId,
                UserName = "user@example.com",
                NormalizedUserName = "USER@EXAMPLE.COM",
                Email = "user@example.com",
                NormalizedEmail = "USER@EXAMPLE.COM",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            });
            await db.SaveChangesAsync();
        }

        var token = CreateAppleToken(sub: "002", email: "user@example.com", emailVerified: true);
        var service = await CreateService();

        var user = await service.ResolveUserAsync(token);

        user!.Id.Should().Be(existingId);
        user.ExternalProvider.Should().Be("Apple");
        user.ExternalProviderId.Should().Be("002");
    }

    [Fact]
    public async Task ResolveUserAsync_RefusesLinkOnUnverifiedEmail()
    {
        await using (var db = _db.CreateDbContext())
        {
            db.Users.Add(new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "user@example.com",
                NormalizedUserName = "USER@EXAMPLE.COM",
                Email = "user@example.com",
                NormalizedEmail = "USER@EXAMPLE.COM",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            });
            await db.SaveChangesAsync();
        }

        var token = CreateAppleToken(sub: "003", email: "user@example.com", emailVerified: false);
        var service = await CreateService();

        var act = async () => await service.ResolveUserAsync(token);
        await act.Should().ThrowAsync<AppleAuthException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task ResolveUserAsync_RejectsExpiredToken()
    {
        var token = CreateAppleToken(sub: "004", email: "x@x.com", emailVerified: true,
            issuedAt: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1));
        var service = await CreateService();

        var act = async () => await service.ResolveUserAsync(token);
        await act.Should().ThrowAsync<AppleAuthException>();
    }

    [Fact]
    public async Task ResolveUserAsync_RejectsWrongIssuer()
    {
        var token = CreateAppleToken(sub: "005", email: "x@x.com", emailVerified: true,
            issuer: "https://evil.example.com");
        var service = await CreateService();

        var act = async () => await service.ResolveUserAsync(token);
        await act.Should().ThrowAsync<AppleAuthException>();
    }

    [Fact]
    public async Task ResolveUserAsync_RejectsWrongAudience()
    {
        var token = CreateAppleToken(sub: "006", email: "x@x.com", emailVerified: true,
            audience: "com.somebody.else");
        var service = await CreateService();

        var act = async () => await service.ResolveUserAsync(token);
        await act.Should().ThrowAsync<AppleAuthException>();
    }

    // -- helpers --

    private async Task<AppleAuthService> CreateService()
    {
        var db = _db.CreateDbContext();
        var userStore = new UserStore<AppUser>(db);
        var userManager = TestIdentity.CreateUserManager(userStore);
        var jwks = new StubJwksCache(_signingKey, _kid);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Apple:BundleId"] = BundleId })
            .Build();
        return new AppleAuthService(jwks, userManager, config);
    }

    private string CreateAppleToken(
        string sub,
        string? email,
        bool emailVerified,
        string issuer = "https://appleid.apple.com",
        string audience = BundleId,
        DateTime? issuedAt = null,
        DateTime? expires = null)
    {
        var iat = issuedAt ?? DateTime.UtcNow;
        var exp = expires ?? DateTime.UtcNow.AddMinutes(10);
        var creds = new SigningCredentials(new RsaSecurityKey(_signingKey) { KeyId = _kid }, SecurityAlgorithms.RsaSha256);
        var claims = new List<Claim> { new("sub", sub) };
        if (email is not null) claims.Add(new("email", email));
        claims.Add(new("email_verified", emailVerified ? "true" : "false"));
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: iat,
            expires: exp,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class StubJwksCache : AppleJwksCache
    {
        private readonly JsonWebKeySet _keys;
        public StubJwksCache(RSA rsa, string kid)
            : base(new HttpClient(new System.Net.Http.HttpClientHandler()), TimeProvider.System)
        {
            var parameters = rsa.ExportParameters(false);
            var jwk = new JsonWebKey
            {
                Kty = "RSA",
                Kid = kid,
                Use = "sig",
                Alg = "RS256",
                N = Base64UrlEncoder.Encode(parameters.Modulus!),
                E = Base64UrlEncoder.Encode(parameters.Exponent!),
            };
            _keys = new JsonWebKeySet();
            _keys.Keys.Add(jwk);
        }

        public new Task<JsonWebKeySet> GetKeysAsync(CancellationToken ct = default) => Task.FromResult(_keys);
    }
}
```

> **Note on the stub:** `AppleJwksCache.GetKeysAsync` must be `virtual` so the stub can override it. Add `virtual` to the method signature in `AppleJwksCache.cs` (one-word change in Task 3's file). If you'd rather not subclass, extract a `IAppleJwksCache` interface — your call. The plan assumes `virtual`.

- [ ] **Step 2: Make `AppleJwksCache.GetKeysAsync` virtual**

In `fasolt.Server/Application/Auth/AppleJwksCache.cs` change:

```csharp
public async Task<JsonWebKeySet> GetKeysAsync(CancellationToken cancellationToken = default)
```

to:

```csharp
public virtual async Task<JsonWebKeySet> GetKeysAsync(CancellationToken cancellationToken = default)
```

- [ ] **Step 3: Add a `TestIdentity` helper if not present**

Check `fasolt.Tests/Helpers/` — if there's no helper for instantiating `UserManager<AppUser>`, create `fasolt.Tests/Helpers/TestIdentity.cs`:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Tests.Helpers;

public static class TestIdentity
{
    public static UserManager<AppUser> CreateUserManager(IUserStore<AppUser> store)
    {
        var options = Options.Create(new IdentityOptions());
        return new UserManager<AppUser>(
            store,
            options,
            new PasswordHasher<AppUser>(),
            Array.Empty<IUserValidator<AppUser>>(),
            Array.Empty<IPasswordValidator<AppUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services: null!,
            new NullLogger<UserManager<AppUser>>());
    }
}
```

If `Fasolt.Tests/Helpers/TestDb.cs` already has something equivalent, reuse it instead and adjust the test code.

- [ ] **Step 4: Run tests, expect compile failure**

```bash
dotnet test fasolt.Tests --filter AppleAuthServiceTests
```

Expected: compile error — `AppleAuthService` and `AppleAuthException` don't exist.

- [ ] **Step 5: Implement `AppleAuthService`**

Create `fasolt.Server/Application/Auth/AppleAuthService.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Application.Auth;

public sealed class AppleAuthException : Exception
{
    public AppleAuthException(string message) : base(message) { }
}

public sealed class AppleAuthService
{
    private const string AppleIssuer = "https://appleid.apple.com";
    private const string ProviderName = "Apple";

    private readonly AppleJwksCache _jwksCache;
    private readonly UserManager<AppUser> _userManager;
    private readonly string _audience;

    public AppleAuthService(AppleJwksCache jwksCache, UserManager<AppUser> userManager, IConfiguration configuration)
    {
        _jwksCache = jwksCache;
        _userManager = userManager;
        _audience = configuration["Apple:BundleId"]
            ?? throw new InvalidOperationException("Apple:BundleId is not configured");
    }

    public async Task<AppUser> ResolveUserAsync(string identityToken, CancellationToken cancellationToken = default)
    {
        var principal = await ValidateTokenAsync(identityToken, cancellationToken);

        var sub = principal.FindFirstValue("sub")
            ?? throw new AppleAuthException("Apple token is missing the 'sub' claim.");
        var email = principal.FindFirstValue("email");
        var emailVerified = principal.FindFirstValue("email_verified") == "true";

        // 1. Existing Apple user?
        var existing = await _userManager.Users
            .FirstOrDefaultAsync(u => u.ExternalProvider == ProviderName && u.ExternalProviderId == sub, cancellationToken);
        if (existing is not null)
            return existing;

        // 2. Link to existing local account if email matches AND Apple verified it
        if (!string.IsNullOrEmpty(email))
        {
            var byEmail = await _userManager.FindByEmailAsync(email);
            if (byEmail is not null)
            {
                if (!emailVerified)
                    throw new AppleAuthException(
                        "An account with this email already exists. Sign in with your password and link Apple from settings.");

                byEmail.ExternalProvider = ProviderName;
                byEmail.ExternalProviderId = sub;
                var update = await _userManager.UpdateAsync(byEmail);
                if (!update.Succeeded)
                    throw new AppleAuthException("Failed to link Apple account to existing user.");
                return byEmail;
            }
        }

        // 3. Create a new user
        var newUser = new AppUser
        {
            UserName = $"apple-{sub}",
            Email = email,
            EmailConfirmed = true,
            ExternalProvider = ProviderName,
            ExternalProviderId = sub,
        };
        var create = await _userManager.CreateAsync(newUser);
        if (!create.Succeeded)
            throw new AppleAuthException(
                "Failed to create user from Apple sign-in: " +
                string.Join(", ", create.Errors.Select(e => e.Description)));
        return newUser;
    }

    private async Task<ClaimsPrincipal> ValidateTokenAsync(string identityToken, CancellationToken cancellationToken)
    {
        JsonWebKeySet jwks;
        try
        {
            jwks = await _jwksCache.GetKeysAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new AppleAuthException("Could not fetch Apple signing keys: " + ex.Message);
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = AppleIssuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = jwks.Keys,
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            return handler.ValidateToken(identityToken, parameters, out _);
        }
        catch (SecurityTokenException ex)
        {
            throw new AppleAuthException("Apple identity token is invalid: " + ex.Message);
        }
    }
}
```

- [ ] **Step 6: Run tests, expect pass**

```bash
dotnet test fasolt.Tests --filter AppleAuthServiceTests
```

Expected: all 7 tests pass. If a test referencing `TestIdentity.CreateUserManager` fails because `UserStore<AppUser>` requires extra setup, swap the helper to wrap a real `AppDbContext` from `_db` (the existing `GitHubAuthTests` shows the pattern of using `db.Users` directly — you can also just use `userManager` against the real `_db` context).

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Application/Auth/AppleAuthService.cs fasolt.Server/Application/Auth/AppleJwksCache.cs fasolt.Tests/Auth/AppleAuthServiceTests.cs fasolt.Tests/Helpers/TestIdentity.cs
git commit -m "feat(auth): add AppleAuthService for identity token validation and user resolution"
```

---

### Task 5: Register `urn:fasolt:apple` as an OpenIddict custom flow + DI wiring

OpenIddict refuses any grant type that hasn't been allowed at the server level *and* permitted on the specific client. We need both. We also need to update the existing `fasolt-ios` first-party client seed to add the new permission idempotently — otherwise dev databases that already have the client won't pick it up.

**Files:**
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Register HttpClient + services in DI**

Open `fasolt.Server/Program.cs`. After the existing OpenIddict configuration block (around line 130, after `.AddValidation(...)`), add:

```csharp
// Sign in with Apple
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<Fasolt.Server.Application.Auth.AppleJwksCache>();
builder.Services.AddScoped<Fasolt.Server.Application.Auth.AppleAuthService>();
```

> The HttpClient typed-client registration also registers `AppleJwksCache` itself in DI as a singleton-via-HttpClientFactory. That's the lifetime we want — JWKS cache is process-wide.

Note: `AppleJwksCache` constructor takes `(HttpClient, TimeProvider)` — the typed-client overload only injects the HttpClient. Either:

(a) Change `AppleJwksCache` to default `TimeProvider` to `TimeProvider.System` if not provided:

```csharp
public AppleJwksCache(HttpClient httpClient, TimeProvider? time = null)
{
    _httpClient = httpClient;
    _time = time ?? TimeProvider.System;
}
```

(b) Or use a factory in DI:

```csharp
builder.Services.AddHttpClient<Fasolt.Server.Application.Auth.AppleJwksCache>((sp, http) =>
{
    // configure http here if needed
});
```

and pull `TimeProvider` from DI inside the cache. Pick (a) for simplicity. Update the cache file accordingly.

- [ ] **Step 2: Allow the custom grant type on the OpenIddict server**

In the `.AddServer(options => { ... })` block around line 83, after `.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow()`, add:

```csharp
options.AllowCustomFlow("urn:fasolt:apple");
```

- [ ] **Step 3: Add the grant-type permission to the seed for the iOS client**

Find the iOS client seed at line 332-352. Replace the `if (existing is null)` block with logic that both creates the client when missing and *updates* its permissions when present:

```csharp
const string iosClientId = "fasolt-ios";
const string appleGrantType = "urn:fasolt:apple";
var existing = await appManager.FindByClientIdAsync(iosClientId);

if (existing is null)
{
    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = iosClientId,
        DisplayName = "Fasolt iOS",
        ClientType = OpenIddictConstants.ClientTypes.Public,
        ApplicationType = OpenIddictConstants.ApplicationTypes.Native,
        ConsentType = OpenIddictConstants.ConsentTypes.Systematic,
    };
    descriptor.RedirectUris.Add(new Uri("fasolt://oauth/callback"));
    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.GrantType + appleGrantType);
    descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + OpenIddictConstants.Scopes.OfflineAccess);
    await appManager.CreateAsync(descriptor);
}
else
{
    // Idempotently ensure the Apple grant type permission is present on existing dev/prod clients.
    var permissions = await appManager.GetPermissionsAsync(existing);
    var appleGrantPermission = OpenIddictConstants.Permissions.Prefixes.GrantType + appleGrantType;
    if (!permissions.Contains(appleGrantPermission))
    {
        var descriptor = new OpenIddictApplicationDescriptor();
        await appManager.PopulateAsync(descriptor, existing);
        descriptor.Permissions.Add(appleGrantPermission);
        await appManager.UpdateAsync(existing, descriptor);
    }
}
```

- [ ] **Step 4: Build and run a smoke test**

```bash
dotnet build fasolt.Server
```

Expected: build succeeds. (We don't have a unit test that exercises OpenIddict's grant-type registration directly — Task 6's endpoint test will catch any wiring issue.)

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Program.cs fasolt.Server/Application/Auth/AppleJwksCache.cs
git commit -m "feat(auth): register urn:fasolt:apple custom grant on OpenIddict + iOS client"
```

---

### Task 6: Handle `urn:fasolt:apple` grant in `/oauth/token`

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`
- Test: `fasolt.Tests/Auth/AppleTokenEndpointTests.cs` *(new — integration test against `WebApplicationFactory<Program>`)*

- [ ] **Step 1: Write the failing integration test**

Create `fasolt.Tests/Auth/AppleTokenEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Fasolt.Server.Application.Auth;

namespace Fasolt.Tests.Auth;

public class AppleTokenEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string BundleId = "com.fasolt.app";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly RSA _signingKey;
    private const string Kid = "test-kid";

    public AppleTokenEndpointTests(WebApplicationFactory<Program> factory)
    {
        _signingKey = RSA.Create(2048);
        var key = _signingKey;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Apple:BundleId"] = BundleId,
                });
            });
            builder.ConfigureServices(services =>
            {
                // Replace the real cache with a stub backed by our local key
                services.AddSingleton<AppleJwksCache>(_ => new StubJwksCache(key, Kid));
            });
        });
    }

    [Fact]
    public async Task PostToken_WithValidAppleIdentityToken_ReturnsOAuthTokens()
    {
        var idToken = MakeAppleIdToken("apple-sub-001", "user@icloud.com", true);
        var client = _factory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:fasolt:apple",
            ["client_id"] = "fasolt-ios",
            ["identity_token"] = idToken,
        });

        var response = await client.PostAsync("/oauth/token", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        payload.Should().NotBeNull();
        payload!.AccessToken.Should().NotBeNullOrEmpty();
        payload.RefreshToken.Should().NotBeNullOrEmpty();
        payload.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task PostToken_WithInvalidAppleToken_ReturnsInvalidGrant()
    {
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:fasolt:apple",
            ["client_id"] = "fasolt-ios",
            ["identity_token"] = "not-a-real-jwt",
        });

        var response = await client.PostAsync("/oauth/token", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private string MakeAppleIdToken(string sub, string email, bool emailVerified)
    {
        var creds = new SigningCredentials(new RsaSecurityKey(_signingKey) { KeyId = Kid }, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: "https://appleid.apple.com",
            audience: BundleId,
            claims: new[]
            {
                new Claim("sub", sub),
                new Claim("email", email),
                new Claim("email_verified", emailVerified ? "true" : "false"),
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string? RefreshToken);

    private sealed class StubJwksCache : AppleJwksCache
    {
        private readonly JsonWebKeySet _keys;
        public StubJwksCache(RSA rsa, string kid)
            : base(new HttpClient(), TimeProvider.System)
        {
            var p = rsa.ExportParameters(false);
            var jwk = new JsonWebKey
            {
                Kty = "RSA", Kid = kid, Use = "sig", Alg = "RS256",
                N = Base64UrlEncoder.Encode(p.Modulus!),
                E = Base64UrlEncoder.Encode(p.Exponent!),
            };
            _keys = new JsonWebKeySet();
            _keys.Keys.Add(jwk);
        }
        public override Task<JsonWebKeySet> GetKeysAsync(CancellationToken ct = default) => Task.FromResult(_keys);
    }
}
```

- [ ] **Step 2: Run, expect failure**

```bash
dotnet test fasolt.Tests --filter AppleTokenEndpointTests
```

Expected: the happy-path test fails because the endpoint returns `unsupported_grant_type` (current behavior at line 253 of `OAuthEndpoints.cs`).

- [ ] **Step 3: Implement the third grant branch**

Edit `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` inside the `/oauth/token` handler. Locate the existing branches starting at line 195 (`if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())`) and add a new branch *before* the final `BadRequest` fallthrough:

```csharp
if (request.GrantType == "urn:fasolt:apple")
{
    var identityToken = request.GetParameter("identity_token")?.ToString();
    if (string.IsNullOrEmpty(identityToken))
        return Results.Forbid(
            authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
            properties: new(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidRequest,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "identity_token parameter is required.",
            }));

    var appleService = context.RequestServices.GetRequiredService<Fasolt.Server.Application.Auth.AppleAuthService>();
    Fasolt.Server.Domain.Entities.AppUser appleUser;
    try
    {
        appleUser = await appleService.ResolveUserAsync(identityToken);
    }
    catch (Fasolt.Server.Application.Auth.AppleAuthException ex)
    {
        return Results.Forbid(
            authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
            properties: new(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = ex.Message,
            }));
    }

    var appleIdentity = new ClaimsIdentity(
        authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        nameType: Claims.Name,
        roleType: Claims.Role);

    appleIdentity.SetClaim(Claims.Subject, appleUser.Id);
    appleIdentity.SetClaim(ClaimTypes.NameIdentifier, appleUser.Id);
    appleIdentity.SetClaim(Claims.Name, appleUser.UserName ?? appleUser.Email ?? appleUser.Id);
    appleIdentity.SetClaim("email_confirmed", "true");
    appleIdentity.SetScopes(Scopes.OfflineAccess);

    appleIdentity.SetDestinations(static claim => claim.Type switch
    {
        ClaimTypes.NameIdentifier => [Destinations.AccessToken],
        Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Name => [Destinations.AccessToken, Destinations.IdentityToken],
        "email_confirmed" => [Destinations.AccessToken],
        _ => [Destinations.AccessToken],
    });

    return Results.SignIn(new ClaimsPrincipal(appleIdentity),
        properties: null,
        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
```

- [ ] **Step 4: Run tests, expect pass**

```bash
dotnet test fasolt.Tests --filter AppleTokenEndpointTests
```

Expected: both tests pass. If the happy-path returns 400 with `unsupported_grant_type`, double-check Task 5 step 2 (`AllowCustomFlow`) and step 3 (client permission) — both are required.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs fasolt.Tests/Auth/AppleTokenEndpointTests.cs
git commit -m "feat(auth): handle urn:fasolt:apple grant on /oauth/token"
```

---

### Task 7: Add `provider_hint=github` support to `/oauth/login`

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`
- Test: `fasolt.Tests/Auth/OAuthProviderHintTests.cs` (new)

- [ ] **Step 1: Write the failing test**

Create `fasolt.Tests/Auth/OAuthProviderHintTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Fasolt.Tests.Auth;

public class OAuthProviderHintTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthProviderHintTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GITHUB_CLIENT_ID"] = "test-github-id",
                });
            });
        });
    }

    [Fact]
    public async Task OAuthLogin_WithGithubProviderHint_RedirectsToGithubLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/oauth/login?provider_hint=github&returnUrl=/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/api/account/github-login");
    }

    [Fact]
    public async Task OAuthLogin_WithoutHint_StillRendersHtml()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/oauth/login?returnUrl=/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("<form");
    }

    [Fact]
    public async Task OAuthLogin_WithUnknownHint_IgnoresHint()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/oauth/login?provider_hint=evilcorp&returnUrl=/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

- [ ] **Step 2: Run, expect failure**

```bash
dotnet test fasolt.Tests --filter OAuthProviderHintTests
```

Expected: the GitHub-redirect test fails (returns 200 with HTML).

- [ ] **Step 3: Implement the hint**

Edit `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`. At the very top of the `/oauth/login` GET handler (around line 263, just inside the lambda), add:

```csharp
var providerHint = context.Request.Query["provider_hint"].FirstOrDefault();
if (providerHint == "github" && !string.IsNullOrEmpty(configuration["GITHUB_CLIENT_ID"]))
{
    var rawReturnUrlForHint = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
    var safeReturnUrl = UrlHelpers.IsLocalUrl(rawReturnUrlForHint) ? rawReturnUrlForHint : "/";
    return Results.Redirect($"/api/account/github-login?returnUrl={Uri.EscapeDataString(safeReturnUrl)}");
}
```

> Place this **before** the existing `var rawReturnUrl = ...` line so the early-return short-circuits HTML rendering. Unknown hint values fall through to the existing HTML page.

Also confirm that the lambda's parameter list already includes `IConfiguration configuration` — at line 261 it does (`IConfiguration configuration` is already a parameter).

- [ ] **Step 4: Run tests, expect pass**

```bash
dotnet test fasolt.Tests --filter OAuthProviderHintTests
```

Expected: all three tests pass.

- [ ] **Step 5: Pass the hint through `/oauth/authorize`**

The iOS app will hit `/oauth/authorize?provider_hint=github&...`. The current `/oauth/authorize` handler at line 113 redirects unauthenticated users to `/oauth/login?returnUrl=<encoded original query string>` — and because the original query string includes `provider_hint=github`, the encoded returnUrl already carries it. **No code change needed at the authorize endpoint.** Verify by reading the existing line 118-119 of `OAuthEndpoints.cs` and confirming `returnUrl` is `context.Request.QueryString.Value` (the full original query). It is.

- [ ] **Step 6: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs fasolt.Tests/Auth/OAuthProviderHintTests.cs
git commit -m "feat(auth): support provider_hint=github on /oauth/login"
```

---

## Phase 2 — iOS register form fix (#100)

### Task 8: Add `PasswordRules` utility

**Files:**
- Create: `fasolt.ios/Fasolt/Utilities/PasswordRules.swift`
- Test: `fasolt.ios/FasoltTests/PasswordRulesTests.swift`

- [ ] **Step 1: Write the failing test**

Create `fasolt.ios/FasoltTests/PasswordRulesTests.swift`:

```swift
import Testing
@testable import Fasolt

struct PasswordRulesTests {
    @Test func empty_password_fails_all_rules() {
        let rules = PasswordRules.evaluate("")
        #expect(rules.allSatisfy { !$0.valid })
        #expect(rules.count == 4)
    }

    @Test func full_valid_password_passes_all_rules() {
        let rules = PasswordRules.evaluate("Abcdefg1")
        #expect(rules.allSatisfy { $0.valid })
    }

    @Test func short_password_fails_length_rule_only() {
        let rules = PasswordRules.evaluate("Abc1")
        #expect(rules.first(where: { $0.label == "At least 8 characters" })?.valid == false)
        #expect(rules.first(where: { $0.label == "Uppercase letter" })?.valid == true)
        #expect(rules.first(where: { $0.label == "Lowercase letter" })?.valid == true)
        #expect(rules.first(where: { $0.label == "Number" })?.valid == true)
    }

    @Test func no_uppercase_fails_only_uppercase_rule() {
        let rules = PasswordRules.evaluate("abcdefg1")
        #expect(rules.first(where: { $0.label == "Uppercase letter" })?.valid == false)
        #expect(rules.first(where: { $0.label == "At least 8 characters" })?.valid == true)
    }

    @Test func all_valid_returns_true_when_all_pass() {
        #expect(PasswordRules.allValid("Abcdefg1") == true)
        #expect(PasswordRules.allValid("abcdefg1") == false)
    }
}
```

- [ ] **Step 2: Run, expect failure (compile error)**

In Xcode, run the `FasoltTests` scheme (`Cmd+U`), or from CLI:

```bash
xcodebuild test -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16'
```

Expected: build error — `PasswordRules` does not exist.

- [ ] **Step 3: Implement `PasswordRules`**

Create `fasolt.ios/Fasolt/Utilities/PasswordRules.swift`:

```swift
import Foundation

struct PasswordRule: Equatable, Sendable {
    let label: String
    let valid: Bool
}

enum PasswordRules {
    static func evaluate(_ password: String) -> [PasswordRule] {
        [
            PasswordRule(label: "At least 8 characters", valid: password.count >= 8),
            PasswordRule(label: "Uppercase letter", valid: password.range(of: #"[A-Z]"#, options: .regularExpression) != nil),
            PasswordRule(label: "Lowercase letter", valid: password.range(of: #"[a-z]"#, options: .regularExpression) != nil),
            PasswordRule(label: "Number", valid: password.range(of: #"\d"#, options: .regularExpression) != nil),
        ]
    }

    static func allValid(_ password: String) -> Bool {
        evaluate(password).allSatisfy(\.valid)
    }
}
```

- [ ] **Step 4: Add the file to the Xcode target**

In Xcode, drag `PasswordRules.swift` into the `Fasolt/Utilities/` group and confirm it's included in the `Fasolt` target. If you're editing project files manually, add it to `fasolt.ios/Fasolt.xcodeproj/project.pbxproj` under the `Fasolt` build phase. (Xcode 16 auto-discovers if the folder is a synchronized group — check by running the build.)

- [ ] **Step 5: Re-run tests, expect pass**

```bash
xcodebuild test -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16'
```

Expected: all 5 PasswordRulesTests pass.

- [ ] **Step 6: Commit**

```bash
git add fasolt.ios/Fasolt/Utilities/PasswordRules.swift fasolt.ios/FasoltTests/PasswordRulesTests.swift fasolt.ios/Fasolt.xcodeproj
git commit -m "feat(ios): add PasswordRules utility matching web validation"
```

---

### Task 9: Update `RegisterViewModel` to use PasswordRules + ToS gate

**Files:**
- Modify: `fasolt.ios/Fasolt/ViewModels/RegisterViewModel.swift`
- Test: `fasolt.ios/FasoltTests/RegisterViewModelTests.swift`

- [ ] **Step 1: Write the failing test**

Create `fasolt.ios/FasoltTests/RegisterViewModelTests.swift`:

```swift
import Testing
@testable import Fasolt

@MainActor
struct RegisterViewModelTests {
    @Test func is_form_valid_requires_email_password_tos() {
        let vm = RegisterViewModel()
        #expect(vm.isFormValid == false)

        vm.email = "user@example.com"
        vm.password = "Abcdefg1"
        vm.confirmPassword = "Abcdefg1"
        #expect(vm.isFormValid == false, "ToS not yet accepted")

        vm.tosAccepted = true
        #expect(vm.isFormValid == true)
    }

    @Test func password_mismatch_blocks_submit() {
        let vm = RegisterViewModel()
        vm.email = "user@example.com"
        vm.password = "Abcdefg1"
        vm.confirmPassword = "Abcdefg2"
        vm.tosAccepted = true
        #expect(vm.isFormValid == false)
    }

    @Test func weak_password_blocks_submit() {
        let vm = RegisterViewModel()
        vm.email = "user@example.com"
        vm.password = "abc"
        vm.confirmPassword = "abc"
        vm.tosAccepted = true
        #expect(vm.isFormValid == false)
    }

    @Test func password_rules_reflect_current_password() {
        let vm = RegisterViewModel()
        vm.password = "Abcdefg1"
        #expect(vm.passwordRules.allSatisfy { $0.valid })
    }
}
```

- [ ] **Step 2: Run, expect failure**

```bash
xcodebuild test -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -only-testing:FasoltTests/RegisterViewModelTests
```

Expected: compile error — `tosAccepted` and `passwordRules` don't exist.

- [ ] **Step 3: Update the view model**

Replace the entire contents of `fasolt.ios/Fasolt/ViewModels/RegisterViewModel.swift` with:

```swift
import Foundation

@MainActor
@Observable
final class RegisterViewModel {
    var email = ""
    var password = ""
    var confirmPassword = ""
    var tosAccepted = false

    var passwordRules: [PasswordRule] {
        PasswordRules.evaluate(password)
    }

    var passwordsMatch: Bool {
        password == confirmPassword
    }

    var passwordMismatch: Bool {
        !confirmPassword.isEmpty && !passwordsMatch
    }

    var isFormValid: Bool {
        !email.isEmpty
            && email.contains("@")
            && PasswordRules.allValid(password)
            && passwordsMatch
            && tosAccepted
    }

    func register(authService: AuthService, serverURL: String) async {
        await authService.register(email: email, password: password, serverURL: serverURL)
    }
}
```

- [ ] **Step 4: Run tests, expect pass**

```bash
xcodebuild test -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -only-testing:FasoltTests/RegisterViewModelTests
```

Expected: all 4 tests pass. The existing `RegisterView.swift` will *not* compile against this new VM yet (it doesn't reference `tosAccepted`), but the build is fine because Swift only complains about the body of `RegisterView` if you actually compile it. **If the iOS app target build fails because RegisterView references the old VM**, that's expected — Task 11 fixes it. Skip to that task before re-running the full test suite.

- [ ] **Step 5: Commit**

```bash
git add fasolt.ios/Fasolt/ViewModels/RegisterViewModel.swift fasolt.ios/FasoltTests/RegisterViewModelTests.swift
git commit -m "feat(ios): RegisterViewModel uses PasswordRules and gates submit on ToS"
```

---

### Task 10: Add `SafariView` wrapper for SFSafariViewController

**Files:**
- Create: `fasolt.ios/Fasolt/Views/Shared/SafariView.swift`

- [ ] **Step 1: Implement the wrapper**

Create `fasolt.ios/Fasolt/Views/Shared/SafariView.swift`:

```swift
import SwiftUI
import SafariServices

struct SafariView: UIViewControllerRepresentable {
    let url: URL

    func makeUIViewController(context: Context) -> SFSafariViewController {
        let controller = SFSafariViewController(url: url)
        controller.preferredControlTintColor = UIColor.label
        return controller
    }

    func updateUIViewController(_ uiViewController: SFSafariViewController, context: Context) {}
}
```

- [ ] **Step 2: Add the file to the Xcode target**

Drag into `Fasolt/Views/Shared/` group, confirm membership in `Fasolt` target.

- [ ] **Step 3: Build to verify**

```bash
xcodebuild build -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16'
```

Expected: build succeeds (assuming Task 11 hasn't broken RegisterView yet — if it has, defer this verification).

- [ ] **Step 4: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Shared/SafariView.swift fasolt.ios/Fasolt.xcodeproj
git commit -m "feat(ios): add SafariView wrapper around SFSafariViewController"
```

---

### Task 11: Create `VerifyEmailView`

**Files:**
- Create: `fasolt.ios/Fasolt/Views/Onboarding/VerifyEmailView.swift`

- [ ] **Step 1: Implement the view**

Create `fasolt.ios/Fasolt/Views/Onboarding/VerifyEmailView.swift`:

```swift
import SwiftUI

struct VerifyEmailView: View {
    let email: String
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(spacing: 24) {
            Spacer()

            Image(systemName: "envelope.badge")
                .font(.system(size: 64))
                .foregroundStyle(.tint)

            VStack(spacing: 8) {
                Text("Check your email")
                    .font(.title.bold())
                Text("We've sent a verification link to **\(email)**. Open it on this device to confirm your address, then sign in.")
                    .font(.body)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal, 32)
            }

            Spacer()

            Button {
                dismiss()
            } label: {
                Text("Back to sign in")
                    .frame(maxWidth: .infinity)
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .padding(.horizontal)

            Spacer()
                .frame(height: 40)
        }
        .navigationTitle("Verify email")
        .navigationBarTitleDisplayMode(.inline)
    }
}

#Preview {
    NavigationStack {
        VerifyEmailView(email: "user@example.com")
    }
}
```

- [ ] **Step 2: Add to Xcode target + build**

Drag into `Fasolt/Views/Onboarding/` group, build.

```bash
xcodebuild build -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16'
```

(May fail if RegisterView is still on the old VM — that's fine, Task 12 fixes it.)

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Onboarding/VerifyEmailView.swift fasolt.ios/Fasolt.xcodeproj
git commit -m "feat(ios): add VerifyEmailView shown after successful registration"
```

---

### Task 12: Rewrite `RegisterView` with live rules, ToS, verify-email push

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Onboarding/RegisterView.swift`

- [ ] **Step 1: Rewrite the file**

Replace the entire contents of `fasolt.ios/Fasolt/Views/Onboarding/RegisterView.swift`:

```swift
import SwiftUI

struct RegisterView: View {
    @Environment(AuthService.self) private var authService
    @Environment(\.dismiss) private var dismiss
    @State private var viewModel = RegisterViewModel()
    @State private var showSafari = false
    @State private var showVerifyEmail = false
    let serverURL: String

    private var termsURL: URL {
        URL(string: "\(serverURL)/terms") ?? URL(string: "https://fasolt.app/terms")!
    }

    var body: some View {
        ScrollView {
            VStack(spacing: 24) {
                VStack(alignment: .leading, spacing: 4) {
                    Text("Create Account")
                        .font(.largeTitle.bold())
                    Text("Start learning with spaced repetition")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.horizontal)

                if let error = authService.errorMessage {
                    Text(error)
                        .font(.footnote)
                        .foregroundStyle(.red)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal)
                }

                VStack(spacing: 16) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("Email")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        TextField("you@example.com", text: $viewModel.email)
                            .textFieldStyle(.roundedBorder)
                            .textContentType(.emailAddress)
                            .keyboardType(.emailAddress)
                            .autocorrectionDisabled()
                            .textInputAutocapitalization(.never)
                    }

                    VStack(alignment: .leading, spacing: 4) {
                        Text("Password")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        SecureField("Password", text: $viewModel.password)
                            .textFieldStyle(.roundedBorder)
                            .textContentType(.newPassword)
                        if !viewModel.password.isEmpty {
                            VStack(alignment: .leading, spacing: 2) {
                                ForEach(viewModel.passwordRules, id: \.label) { rule in
                                    HStack(spacing: 6) {
                                        Image(systemName: rule.valid ? "checkmark.circle.fill" : "circle")
                                            .foregroundStyle(rule.valid ? Color.green : Color.secondary)
                                            .font(.caption2)
                                        Text(rule.label)
                                            .font(.caption2)
                                            .foregroundStyle(rule.valid ? Color.primary : Color.secondary)
                                    }
                                }
                            }
                            .padding(.top, 4)
                        }
                    }

                    VStack(alignment: .leading, spacing: 4) {
                        Text("Confirm Password")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        SecureField("Confirm password", text: $viewModel.confirmPassword)
                            .textFieldStyle(.roundedBorder)
                            .textContentType(.newPassword)
                        if viewModel.passwordMismatch {
                            Text("Passwords don't match")
                                .font(.caption2)
                                .foregroundStyle(.red)
                        }
                    }

                    HStack(alignment: .top, spacing: 10) {
                        Toggle("", isOn: $viewModel.tosAccepted)
                            .toggleStyle(.switch)
                            .labelsHidden()
                        VStack(alignment: .leading, spacing: 2) {
                            Text("I agree to the")
                                .font(.footnote)
                            Button {
                                showSafari = true
                            } label: {
                                Text("Terms of Service")
                                    .font(.footnote)
                                    .underline()
                            }
                        }
                        Spacer()
                    }
                    .padding(.top, 4)
                }
                .padding(.horizontal)

                Button {
                    Task {
                        await viewModel.register(authService: authService, serverURL: serverURL)
                    }
                } label: {
                    if authService.isLoading {
                        ProgressView()
                            .frame(maxWidth: .infinity)
                            .frame(height: 22)
                    } else {
                        Text("Create Account")
                            .frame(maxWidth: .infinity)
                    }
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .disabled(!viewModel.isFormValid || authService.isLoading)
                .padding(.horizontal)
            }
            .padding(.vertical)
        }
        .navigationTitle("Create Account")
        .navigationBarTitleDisplayMode(.inline)
        .sheet(isPresented: $showSafari) {
            SafariView(url: termsURL)
                .ignoresSafeArea()
        }
        .navigationDestination(isPresented: $showVerifyEmail) {
            VerifyEmailView(email: viewModel.email)
        }
        .onChange(of: authService.registrationSuccess) { _, success in
            if success {
                authService.registrationSuccess = false
                showVerifyEmail = true
            }
        }
    }
}

#Preview {
    NavigationStack {
        RegisterView(serverURL: "https://fasolt.app")
            .environment(AuthService())
    }
}
```

- [ ] **Step 2: Build the app target**

```bash
xcodebuild build -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16'
```

Expected: build succeeds.

- [ ] **Step 3: Run the existing test suite to confirm nothing else broke**

```bash
xcodebuild test -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16'
```

Expected: PasswordRulesTests + RegisterViewModelTests + existing tests all pass.

- [ ] **Step 4: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Onboarding/RegisterView.swift
git commit -m "fix(ios): rewrite RegisterView with live password rules, ToS, verify-email push (#100)"
```

---

## Phase 3 — iOS feature flags + AuthService Apple support

### Task 13: Implement `FeatureFlagsService`

**Files:**
- Create: `fasolt.ios/Fasolt/Services/FeatureFlagsService.swift`

- [ ] **Step 1: Implement the service**

Create `fasolt.ios/Fasolt/Services/FeatureFlagsService.swift`:

```swift
import Foundation
import os

private let featureLogger = Logger(subsystem: "com.fasolt.app", category: "Features")

@MainActor
@Observable
final class FeatureFlagsService {
    var githubLogin = false
    var appleLogin = false
    var hasLoaded = false

    func refresh(serverURL: String) async {
        guard let url = URL(string: serverURL + "/api/health") else { return }
        do {
            let (data, _) = try await URLSession.shared.data(from: url)
            let decoded = try JSONDecoder().decode(HealthResponse.self, from: data)
            self.githubLogin = decoded.features.githubLogin
            self.appleLogin = decoded.features.appleLogin
            self.hasLoaded = true
            featureLogger.info("Feature flags loaded — github=\(decoded.features.githubLogin) apple=\(decoded.features.appleLogin)")
        } catch {
            featureLogger.error("Failed to load feature flags: \(error.localizedDescription)")
        }
    }

    private struct HealthResponse: Decodable {
        let features: Features
        struct Features: Decodable {
            let githubLogin: Bool
            let appleLogin: Bool
        }
    }
}
```

- [ ] **Step 2: Add to Xcode target + build**

Drag into `Fasolt/Services/`. Build target.

```bash
xcodebuild build -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16'
```

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Services/FeatureFlagsService.swift fasolt.ios/Fasolt.xcodeproj
git commit -m "feat(ios): add FeatureFlagsService that fetches /api/health features"
```

---

### Task 14: Add `signInWithApple` and `signInWithGitHub` to `AuthService`

**Files:**
- Modify: `fasolt.ios/Fasolt/Services/AuthService.swift`

- [ ] **Step 1: Add the Apple sign-in method**

Open `fasolt.ios/Fasolt/Services/AuthService.swift`. Add a new method *after* `register(...)` (around line 140):

```swift
// MARK: - Apple Sign-In

func signInWithApple(identityToken: String, serverURL: String) async {
    isLoading = true
    errorMessage = nil

    let previousServerURL = keychain.retrieve("fasolt.serverURL")
    keychain.save(serverURL, forKey: "fasolt.serverURL")

    do {
        let params: [String: String] = [
            "grant_type": "urn:fasolt:apple",
            "client_id": Self.firstPartyClientId,
            "identity_token": identityToken,
        ]

        let tokenResponse: TokenResponse = try await apiClient.formPost("/oauth/token", params: params)
        keychain.save(Self.firstPartyClientId, forKey: "fasolt.clientId")
        keychain.save(tokenResponse.accessToken, forKey: "fasolt.accessToken")
        if let refreshToken = tokenResponse.refreshToken {
            keychain.save(refreshToken, forKey: "fasolt.refreshToken")
        }
        let expiry = Date.now.addingTimeInterval(TimeInterval(tokenResponse.expiresIn))
        keychain.save(DateFormatters.formatISO8601(expiry), forKey: "fasolt.tokenExpiry")
        authLogger.info("Apple sign-in complete")
        isAuthenticated = true
    } catch {
        authLogger.error("Apple sign-in failed: \(error)")
        restoreServerURL(previous: previousServerURL)
        errorMessage = "Could not sign in with Apple. Please try again."
    }

    isLoading = false
}
```

- [ ] **Step 2: Extend `signIn` to accept an optional provider hint**

Find the existing `signIn(serverURL:)` method (line 58). Change its signature to accept an optional `providerHint`:

```swift
func signIn(serverURL: String, providerHint: String? = nil) async {
```

Then in `openAuthSession(...)` (line 192), accept the hint and add it to the query string:

```swift
private func openAuthSession(
    serverURL: String,
    clientId: String,
    codeChallenge: String,
    providerHint: String? = nil
) async throws -> String {
    guard var components = URLComponents(string: serverURL + "/oauth/authorize") else {
        throw APIError.invalidURL
    }
    var queryItems: [URLQueryItem] = [
        URLQueryItem(name: "response_type", value: "code"),
        URLQueryItem(name: "client_id", value: clientId),
        URLQueryItem(name: "redirect_uri", value: Self.redirectURI),
        URLQueryItem(name: "code_challenge", value: codeChallenge),
        URLQueryItem(name: "code_challenge_method", value: "S256"),
        URLQueryItem(name: "scope", value: "offline_access"),
    ]
    if let providerHint {
        queryItems.append(URLQueryItem(name: "provider_hint", value: providerHint))
    }
    components.queryItems = queryItems
    // ... rest of existing function unchanged ...
```

In `signIn(...)` itself, pass the hint when calling `openAuthSession`:

```swift
let authCode = try await openAuthSession(
    serverURL: serverURL,
    clientId: clientId,
    codeChallenge: codeChallenge,
    providerHint: providerHint
)
```

- [ ] **Step 3: Build**

```bash
xcodebuild build -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16'
```

Expected: build succeeds. (No new test added for `signInWithApple` because it wraps `formPost` against a real network — we test the round trip in the manual end-to-end Task 17 and the backend integration test in Task 6 already covers the server side.)

- [ ] **Step 4: Commit**

```bash
git add fasolt.ios/Fasolt/Services/AuthService.swift
git commit -m "feat(ios): add signInWithApple + provider_hint support to AuthService"
```

---

## Phase 4 — iOS OnboardingView restructure

### Task 15: Restructure `OnboardingView` with SSO + local account sections

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Onboarding/OnboardingView.swift`
- Modify: `fasolt.ios/Fasolt/FasoltApp.swift`

- [ ] **Step 1: Wire `FeatureFlagsService` into the app**

Open `fasolt.ios/Fasolt/FasoltApp.swift`. Find where `AuthService` is instantiated and added to the environment. Add `FeatureFlagsService` next to it:

```swift
@State private var authService = AuthService()
@State private var featureFlags = FeatureFlagsService()
```

In the `WindowGroup` body, after `.environment(authService)`, add:

```swift
.environment(featureFlags)
.task {
    await featureFlags.refresh(serverURL: authService.serverURL)
}
```

(Adjust the exact location to match the existing structure — read the file first.)

- [ ] **Step 2: Rewrite `OnboardingView`**

Replace the entire contents of `fasolt.ios/Fasolt/Views/Onboarding/OnboardingView.swift`:

```swift
import SwiftUI
import AuthenticationServices

struct OnboardingView: View {
    @Environment(AuthService.self) private var authService
    @Environment(FeatureFlagsService.self) private var featureFlags
    @State private var showServerField = false
    @State private var serverURL = AuthService.defaultServerURL
    @State private var showRegistrationSuccess = false
    private static let selfHostDefault = "http://localhost:8080"

    var body: some View {
        NavigationStack {
            VStack(spacing: 28) {
                Spacer()

                VStack(spacing: 8) {
                    Image("FasoltLogo")
                        .resizable()
                        .aspectRatio(contentMode: .fit)
                        .frame(width: 96, height: 96)
                        .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
                    Text("Fasolt")
                        .font(.largeTitle.bold())
                    Text("Spaced repetition for your notes")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }

                Spacer()

                if showServerField {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("Server URL")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        TextField("https://fasolt.app", text: $serverURL)
                            .textFieldStyle(.roundedBorder)
                            .textContentType(.URL)
                            .autocorrectionDisabled()
                            .textInputAutocapitalization(.never)
                            .keyboardType(.URL)
                            .onChange(of: serverURL) { _, newValue in
                                Task { await featureFlags.refresh(serverURL: newValue) }
                            }
                    }
                    .padding(.horizontal)
                    .transition(.move(edge: .bottom).combined(with: .opacity))
                }

                // — SSO section —
                VStack(spacing: 10) {
                    if featureFlags.appleLogin {
                        SignInWithAppleButton(
                            .continue,
                            onRequest: { request in
                                request.requestedScopes = [.fullName, .email]
                            },
                            onCompletion: { result in
                                handleAppleResult(result)
                            }
                        )
                        .signInWithAppleButtonStyle(.black)
                        .frame(height: 48)
                        .cornerRadius(8)
                    }

                    if featureFlags.githubLogin {
                        Button {
                            Task {
                                await authService.signIn(serverURL: serverURL, providerHint: "github")
                            }
                        } label: {
                            HStack {
                                Image(systemName: "chevron.left.forwardslash.chevron.right")
                                Text("Continue with GitHub")
                                    .fontWeight(.medium)
                            }
                            .frame(maxWidth: .infinity)
                            .frame(height: 48)
                            .background(Color(red: 36/255, green: 41/255, blue: 47/255))
                            .foregroundStyle(.white)
                            .cornerRadius(8)
                        }
                    }
                }
                .padding(.horizontal)

                if featureFlags.appleLogin || featureFlags.githubLogin {
                    HStack {
                        VStack { Divider() }
                        Text("or")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        VStack { Divider() }
                    }
                    .padding(.horizontal)
                }

                // — Local account section —
                VStack(spacing: 10) {
                    Button {
                        Task {
                            await authService.signIn(serverURL: serverURL)
                        }
                    } label: {
                        if authService.isLoading {
                            ProgressView()
                                .frame(maxWidth: .infinity)
                                .frame(height: 22)
                        } else {
                            Text("Sign In")
                                .frame(maxWidth: .infinity)
                        }
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                    .disabled(authService.isLoading || serverURL.isEmpty)

                    NavigationLink {
                        RegisterView(serverURL: serverURL)
                    } label: {
                        Text("Create account")
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.large)
                    .disabled(serverURL.isEmpty)
                }
                .padding(.horizontal)

                if showRegistrationSuccess {
                    Text("Account created! Check your email to verify and sign in.")
                        .font(.caption)
                        .foregroundStyle(.green)
                        .multilineTextAlignment(.center)
                        .padding(.horizontal)
                }

                if let error = authService.errorMessage {
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.red)
                        .multilineTextAlignment(.center)
                        .padding(.horizontal)
                }

                if !showServerField {
                    Button("Self-hosting? Change server") {
                        withAnimation {
                            serverURL = Self.selfHostDefault
                            showServerField = true
                        }
                        Task { await featureFlags.refresh(serverURL: Self.selfHostDefault) }
                    }
                    .font(.caption)
                    .foregroundStyle(.secondary)
                }

                Spacer()
                    .frame(height: 32)
            }
            .onChange(of: authService.registrationSuccess) { _, success in
                if success {
                    showRegistrationSuccess = true
                    authService.registrationSuccess = false
                }
            }
        }
    }

    private func handleAppleResult(_ result: Result<ASAuthorization, Error>) {
        switch result {
        case .success(let authorization):
            guard let credential = authorization.credential as? ASAuthorizationAppleIDCredential,
                  let tokenData = credential.identityToken,
                  let identityToken = String(data: tokenData, encoding: .utf8) else {
                authService.errorMessage = "Could not read Apple credential."
                return
            }
            Task {
                await authService.signInWithApple(identityToken: identityToken, serverURL: serverURL)
            }
        case .failure(let error):
            // User cancellation: don't show an error
            if (error as NSError).code == ASAuthorizationError.canceled.rawValue {
                return
            }
            authService.errorMessage = "Apple sign-in failed: \(error.localizedDescription)"
        }
    }
}

#Preview {
    OnboardingView()
        .environment(AuthService())
        .environment(FeatureFlagsService())
}
```

- [ ] **Step 3: Build**

```bash
xcodebuild build -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16'
```

Expected: build succeeds.

- [ ] **Step 4: Run all iOS tests one more time**

```bash
xcodebuild test -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16'
```

Expected: every test passes.

- [ ] **Step 5: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Onboarding/OnboardingView.swift fasolt.ios/Fasolt/FasoltApp.swift
git commit -m "feat(ios): restructure OnboardingView with SSO + local account sections"
```

---

### Task 16: Add Sign in with Apple capability to the Xcode project

This is a project-file change Apple requires for `ASAuthorizationAppleIDProvider` to actually work on a real device.

**Files:**
- Modify: `fasolt.ios/Fasolt/Fasolt.entitlements` (create if missing)
- Modify: `fasolt.ios/Fasolt.xcodeproj/project.pbxproj` (only if entitlements file is new)

- [ ] **Step 1: Check current entitlements**

```bash
find fasolt.ios -name "*.entitlements"
```

If `Fasolt.entitlements` exists, open it and add the Apple sign-in entitlement. If not, create one.

- [ ] **Step 2: Add the entitlement**

`fasolt.ios/Fasolt/Fasolt.entitlements` should contain (at minimum):

```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.developer.applesignin</key>
    <array>
        <string>Default</string>
    </array>
</dict>
</plist>
```

If existing entitlements have other keys (push notifications etc.), preserve them and add only the `applesignin` array.

- [ ] **Step 3: Wire the entitlements file in the Xcode project**

In Xcode, select the `Fasolt` target → "Signing & Capabilities" → "+ Capability" → "Sign in with Apple". This adds the entitlements file path to the project's build settings (`CODE_SIGN_ENTITLEMENTS = Fasolt/Fasolt.entitlements`). Confirm by checking `project.pbxproj` for the new line.

> **Apple Developer Console:** Sign in with Apple also requires enabling the capability on the App ID at https://developer.apple.com/account/resources/identifiers — that's a manual click in the developer portal, not a code change. Note this in the PR description so it isn't forgotten before TestFlight.

- [ ] **Step 4: Build for a real device target**

```bash
xcodebuild build -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'generic/platform=iOS'
```

Expected: build succeeds. If code signing fails, your local dev account doesn't have the Apple sign-in capability enabled — note in the PR but don't block on it.

- [ ] **Step 5: Commit**

```bash
git add fasolt.ios/Fasolt/Fasolt.entitlements fasolt.ios/Fasolt.xcodeproj
git commit -m "feat(ios): enable Sign in with Apple capability"
```

---

## Phase 5 — Manual end-to-end + PR

### Task 17: Manual end-to-end checklist

This isn't automated — actually exercise each path on a running stack and document any issues that come up.

- [ ] **Step 1: Start the full stack**

```bash
./dev.sh
```

Expected: backend on `:8080`, frontend on `:5173`, Postgres up. Wait until you see the backend log "Now listening on".

- [ ] **Step 2: Verify `/api/health` reports both flags as expected**

```bash
curl -s http://localhost:8080/api/health | jq .features
```

Expected: `{"githubLogin": <bool>, "appleLogin": <bool>}` — exact values depend on your local `.env`. If you want to test the GitHub button, set `GITHUB_CLIENT_ID` in `.env`. For Apple, set `Apple__BundleId=com.fasolt.app`.

- [ ] **Step 3: Test the new register flow**

In the iOS Simulator or on a device pointing at `http://localhost:8080`:

1. Open the app, tap **Create account**
2. Type a weak password — confirm rules show grey/red
3. Type `Abcdefg1` in both password fields — confirm all four rules turn green and "Passwords don't match" disappears
4. Confirm the **Create Account** button is disabled until ToS toggle is on
5. Tap the **Terms of Service** link — confirm `SFSafariViewController` opens to `<serverURL>/terms`
6. Toggle ToS on, tap **Create Account**
7. Confirm `VerifyEmailView` is pushed and shows the typed email
8. Tap **Back to sign in** — confirm it pops back to OnboardingView

- [ ] **Step 4: Verify the user was actually created**

```bash
docker exec -i fasolt-postgres-1 psql -U fasolt -d fasolt -c "SELECT \"Email\", \"EmailConfirmed\", \"ExternalProvider\" FROM \"AspNetUsers\" ORDER BY \"Id\" DESC LIMIT 1;"
```

Expected: the new email, `EmailConfirmed = false`, `ExternalProvider = NULL`.

- [ ] **Step 5: Test the GitHub button (if `GITHUB_CLIENT_ID` configured)**

1. Re-launch the iOS app
2. Confirm "Continue with GitHub" appears in the SSO section
3. Tap it — confirm an `ASWebAuthenticationSession` opens directly on `github.com/login/oauth/authorize` (no detour through the email/password page)
4. Complete GitHub auth — confirm you land back in the iOS app, signed in, on the dashboard

- [ ] **Step 6: Test Sign in with Apple (real device only — does not work in Simulator without Apple ID)**

1. Build to a real device with a valid Apple Developer team
2. Tap **Continue with Apple**
3. Complete the native Face ID / Touch ID Apple sign-in sheet
4. Confirm you land on the dashboard
5. Verify the user row in Postgres has `ExternalProvider = 'Apple'` and `EmailConfirmed = true`

- [ ] **Step 7: Test self-host server change**

1. From OnboardingView, tap "Self-hosting? Change server"
2. Confirm `featureFlags.refresh(...)` re-runs (check Xcode console for the "Feature flags loaded" log line)
3. Confirm SSO buttons hide if your local server doesn't have the matching env vars

- [ ] **Step 8: Run all backend tests**

```bash
dotnet test
```

Expected: every test passes.

- [ ] **Step 9: Run all iOS tests**

```bash
xcodebuild test -project fasolt.ios/Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16'
```

Expected: every test passes.

- [ ] **Step 10: No commit for this task** — it's a verification gate, not a code change.

---

### Task 18: Open the pull request

- [ ] **Step 1: Push the branch**

```bash
git push -u origin <current-branch-name>
```

(If you started from `main` directly, create a feature branch first: `git checkout -b ios-onboarding-restructure && git push -u origin ios-onboarding-restructure`.)

- [ ] **Step 2: Create the PR**

```bash
gh pr create --title "iOS onboarding restructure + Sign in with Apple + register form fix" --body "$(cat <<'EOF'
## Summary

Closes #100, partially closes #99 (iOS slice only — leaves #99 open for the web slice).

- **Register form (#100)**: rewrites the iOS Create Account view to mirror the web — live password rule checklist, Terms of Service acceptance with `SFSafariViewController` link, and a new "Check your email" screen pushed on success.
- **OnboardingView restructure**: SSO providers (Sign in with Apple, optional Continue with GitHub) at the top, "or" divider, then local account actions (Sign In + Create account), then the self-host link.
- **Sign in with Apple (iOS slice of #99)**: native `ASAuthorizationController` button → POSTs the Apple identity token to `/oauth/token` with a new `urn:fasolt:apple` custom grant type → server validates the JWT against Apple's JWKS, finds-or-creates the `AppUser`, and OpenIddict mints the standard access + refresh token pair (no parallel auth path).
- **GitHub button on iOS**: reuses the existing PKCE flow with a new `provider_hint=github` query parameter that makes `/oauth/login` redirect straight to GitHub instead of rendering its HTML form.
- New `FeatureFlagsService` on iOS reads `/api/health` so SSO buttons can be hidden when the server doesn't have the matching credentials.

## Out of scope
- Web Sign in with Apple (#99 stays open)
- Account-linking UI in Settings — Apple links automatically only when `email_verified == true`, otherwise the request is refused with a clear message.

## Manual steps before TestFlight
- Enable "Sign in with Apple" capability on the App ID in the Apple Developer console (https://developer.apple.com/account/resources/identifiers)
- Set `Apple__BundleId=com.fasolt.app` in production environment

## Test plan
- [ ] `dotnet test` passes (added: AppleAuthServiceTests, AppleJwksCacheTests, AppleTokenEndpointTests, OAuthProviderHintTests, HealthEndpointsTests)
- [ ] iOS test suite passes (added: PasswordRulesTests, RegisterViewModelTests)
- [ ] Manual: register a new account via the iOS form, see the verify-email screen, verify the row was created in Postgres
- [ ] Manual: tap the Terms of Service link, confirm `SFSafariViewController` opens to `/terms`
- [ ] Manual: tap the GitHub button on iOS, confirm it goes straight to GitHub (no detour through `/oauth/login`)
- [ ] Manual (real device): tap Sign in with Apple, complete Face ID, confirm dashboard loads and Postgres row has `ExternalProvider = 'Apple'`
EOF
)"
```

- [ ] **Step 3: Return the PR URL to the user.**

---

## Self-Review

**Spec coverage:**

| Spec section | Task(s) |
|---|---|
| Register form fix (live rules, ToS, verify-email push) | 8, 9, 10, 11, 12 |
| OnboardingView restructure | 13, 15 |
| Apple sign-in iOS button | 15, 16 |
| Apple sign-in backend (JWT validation, user resolve) | 3, 4 |
| Apple sign-in OpenIddict custom grant on `/oauth/token` | 5, 6 |
| GitHub button on iOS with `provider_hint=github` | 7, 14, 15 |
| `features.appleLogin` on `/api/health` | 2 |
| `FeatureFlagsService` on iOS | 13, 15 |
| Apple Bundle ID config + env example | 1 |
| Xcode entitlement for Sign in with Apple | 16 |
| Manual end-to-end verification | 17 |
| PR creation | 18 |

All spec sections covered.

**Placeholder scan:** No "TBD", "TODO", "implement later" left in normative steps. The "Apple Developer Console" callout in Task 16 is a real out-of-band manual step, not a placeholder for code work.

**Type consistency:** `PasswordRule` is defined in Task 8 and consumed by Tasks 9 and 12 with the same shape. `RegisterViewModel.tosAccepted` introduced in Task 9 is consumed in Task 12. `AppleAuthService.ResolveUserAsync` returns `AppUser` in Task 4 and is consumed in Task 6. `AppleJwksCache.GetKeysAsync` is `virtual` (Task 4 step 2) so the test stub in Task 6 can override it. `signInWithApple(identityToken:serverURL:)` defined in Task 14 matches the call site in Task 15. `FeatureFlagsService.appleLogin` and `.githubLogin` are read in Task 15 with the same names defined in Task 13.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-07-ios-onboarding-restructure.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

Which approach?
