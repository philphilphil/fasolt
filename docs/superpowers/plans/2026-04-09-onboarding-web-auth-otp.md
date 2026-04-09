# Onboarding v2 — Web-first auth with OTP verification — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the native iOS register form with a server-rendered OTP-based email verification flow shared between iOS (via ASWebAuthenticationSession popup) and the web app, and fix three Apple-sign-in bugs from the PR #101 review in the same pass.

**Architecture:** New `EmailVerificationCode` entity + `EmailVerificationCodeService` (HMAC-SHA256 hashed 6-digit codes, per-user row with in-place updates on resend). Three new server-rendered HTML pages (`/oauth/register`, `/oauth/verify-email`, `/oauth/verify-email/resend`) styled to match the existing `/oauth/login`. iOS `AuthService.signIn()` gains a `providerHint: "signup"` parameter that sets `screen_hint=signup` on the `/oauth/authorize` URL; the server reads the hint and redirects unauthenticated users to `/oauth/register` instead of `/oauth/login`. After verification, the Identity cookie is set and the OAuth flow resumes naturally via `returnUrl`. Native iOS register surface (~400 LOC of Swift + tests) and legacy Vue register/confirm views are deleted.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, EF Core + Npgsql, OpenIddict, ASP.NET Identity, Swift 5.9+ / SwiftUI / SwiftTesting, Vue 3 + TypeScript, xUnit + FluentAssertions, Playwright MCP.

**Spec:** `docs/superpowers/specs/2026-04-09-onboarding-web-auth-otp-design.md`

---

## Pre-flight

- [ ] **Step 0.1: Verify tests are green on branch**

Run: `dotnet test fasolt.Tests`
Expected: 188/188 passing (baseline from PR #101).

Run: `cd fasolt.ios && xcodebuild test -scheme Fasolt -destination "platform=iOS Simulator,name=iPhone 17"` (optional, slower)
Expected: 39/39 passing.

If anything is red, stop and diagnose before starting the plan.

- [ ] **Step 0.2: Start Postgres for integration tests**

Run: `docker compose up -d`
Expected: `postgres` container running on localhost:5432.

---

## Task 1: Fix `AppleJwksCache` DI registration (bug fix from review)

**Context:** `AddHttpClient<TClient>()` registers the typed client as **transient**, which means `AppleJwksCache`'s in-memory cache (`_cached`, `_expiresAt`, `_lock`) is recreated on every DI resolution. Every `/oauth/token` call with the Apple grant re-fetches Apple's JWKS. The cache is effectively dead code until this is fixed. See PR #101 review finding #1.

**Files:**
- Modify: `fasolt.Server/Application/Auth/AppleJwksCache.cs`
- Modify: `fasolt.Server/Program.cs:135`
- Create: `fasolt.Tests/Auth/AppleJwksCacheDiTests.cs`

- [ ] **Step 1.1: Write the failing DI lifetime test**

Create `fasolt.Tests/Auth/AppleJwksCacheDiTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Application.Auth;

namespace Fasolt.Tests.Auth;

public class AppleJwksCacheDiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AppleJwksCacheDiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                });
            });
        });
    }

    [Fact]
    public void AppleJwksCache_IsRegisteredAsSingleton()
    {
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        var first = scope1.ServiceProvider.GetRequiredService<AppleJwksCache>();
        var second = scope2.ServiceProvider.GetRequiredService<AppleJwksCache>();

        first.Should().BeSameAs(second,
            "AppleJwksCache must be a singleton so its in-memory JWKS cache survives across requests");
    }
}
```

- [ ] **Step 1.2: Run the test and watch it fail**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~AppleJwksCacheDiTests"`
Expected: FAIL — the current `AddHttpClient<AppleJwksCache>()` registers the type as transient, so `first` and `second` are different instances.

- [ ] **Step 1.3: Change `AppleJwksCache` to take `IHttpClientFactory`**

Edit `fasolt.Server/Application/Auth/AppleJwksCache.cs`:

```csharp
using Microsoft.IdentityModel.Tokens;

namespace Fasolt.Server.Application.Auth;

public class AppleJwksCache
{
    public const string HttpClientName = "AppleJwks";

    private const string AppleJwksUrl = "https://appleid.apple.com/auth/keys";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private JsonWebKeySet? _cached;
    private DateTimeOffset _expiresAt;

    public AppleJwksCache(IHttpClientFactory httpClientFactory, TimeProvider? time = null)
    {
        _httpClientFactory = httpClientFactory;
        _time = time ?? TimeProvider.System;
    }

    public virtual async Task<JsonWebKeySet> GetKeysAsync(CancellationToken cancellationToken = default)
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

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var json = await client.GetStringAsync(AppleJwksUrl, cancellationToken);
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

- [ ] **Step 1.4: Update DI registration in Program.cs**

Edit `fasolt.Server/Program.cs:134-136`:

```csharp
// Sign in with Apple — JWKS cache must be singleton so the in-memory
// cache survives across requests. Uses a named HttpClient so the
// typed-client's transient lifetime doesn't leak.
builder.Services.AddHttpClient(Fasolt.Server.Application.Auth.AppleJwksCache.HttpClientName);
builder.Services.AddSingleton<Fasolt.Server.Application.Auth.AppleJwksCache>();
builder.Services.AddScoped<Fasolt.Server.Application.Auth.AppleAuthService>();
```

- [ ] **Step 1.5: Update existing test stubs to match the new constructor**

Edit `fasolt.Tests/Auth/AppleAuthServiceTests.cs:215-218`, replace the `StubJwksCache` constructor:

```csharp
private sealed class StubJwksCache : AppleJwksCache
{
    private readonly JsonWebKeySet _keys;
    public StubJwksCache(RSA rsa, string kid)
        : base(new StubHttpClientFactory(), TimeProvider.System)
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

private sealed class StubHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new();
}
```

Do the same for `AppleTokenEndpointTests.cs:110-126` — update the `StubJwksCache` base call to use `StubHttpClientFactory` the same way.

Also edit `fasolt.Tests/Auth/AppleJwksCacheTests.cs:27-48` — the existing tests instantiate `AppleJwksCache` directly with an `HttpClient`. Update to use a factory:

```csharp
[Fact]
public async Task GetKeysAsync_FetchesAndCachesJwks()
{
    var handler = new StubHandler(SampleJwks);
    var factory = new StubHttpClientFactory(handler);
    var cache = new AppleJwksCache(factory, TimeProvider.System);

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
    var factory = new StubHttpClientFactory(handler);
    var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
    var cache = new AppleJwksCache(factory, time);

    await cache.GetKeysAsync();
    time.Advance(TimeSpan.FromHours(2));
    await cache.GetKeysAsync();

    handler.CallCount.Should().Be(2);
}

private sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;
    public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
    public HttpClient CreateClient(string name) => new(_handler);
}
```

- [ ] **Step 1.6: Run all affected tests**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~Apple"`
Expected: All Apple tests pass, including the new `AppleJwksCacheDiTests`.

- [ ] **Step 1.7: Commit**

```bash
git add fasolt.Server/Application/Auth/AppleJwksCache.cs \
        fasolt.Server/Program.cs \
        fasolt.Tests/Auth/AppleJwksCacheDiTests.cs \
        fasolt.Tests/Auth/AppleJwksCacheTests.cs \
        fasolt.Tests/Auth/AppleAuthServiceTests.cs \
        fasolt.Tests/Auth/AppleTokenEndpointTests.cs
git commit -m "fix(auth): AppleJwksCache must be singleton so the JWKS cache survives across requests

AddHttpClient<TClient>() registers the typed client as transient, which
meant every /oauth/token call with the Apple grant re-fetched Apple's
JWKS. Switched to a named HttpClient + singleton AppleJwksCache that
takes IHttpClientFactory. Added AppleJwksCacheDiTests to guard against
regression.

Found in PR #101 review."
```

---

## Task 2: Fix `email_verified` string-vs-boolean claim parsing (bug fix from review)

**Context:** Apple's identity token may contain `email_verified` as either a JSON string (`"true"` / `"false"`) or a JSON boolean. `JwtSecurityTokenHandler` surfaces bool-valued claims as strings with inconsistent casing (`"True"`, `"true"`, sometimes literal `"true"`). The current comparison `FindFirstValue("email_verified") == "true"` works for string claims but silently fails for boolean claims, forcing every affected user down the "refuse to link" branch. See PR #101 review finding #2.

**Files:**
- Modify: `fasolt.Server/Application/Auth/AppleAuthService.cs:45`
- Modify: `fasolt.Tests/Auth/AppleAuthServiceTests.cs` (add new test method)

- [ ] **Step 2.1: Write a failing test for a boolean-valued claim**

Add to `fasolt.Tests/Auth/AppleAuthServiceTests.cs`, after the existing `ResolveUserAsync_LinksByVerifiedEmail` test:

```csharp
[Fact]
public async Task ResolveUserAsync_LinksByVerifiedEmail_WhenClaimIsBoolean()
{
    var existingId = Guid.NewGuid().ToString();
    await using (var db = _db.CreateDbContext())
    {
        db.Users.Add(new AppUser
        {
            Id = existingId,
            UserName = "bool@example.com",
            NormalizedUserName = "BOOL@EXAMPLE.COM",
            Email = "bool@example.com",
            NormalizedEmail = "BOOL@EXAMPLE.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();
    }

    // Build a token where email_verified is a JSON boolean, not a string
    var token = CreateAppleTokenWithBooleanEmailVerified(
        sub: "bool-sub", email: "bool@example.com", emailVerified: true);
    var (service, ctx) = CreateService();
    await using var _ = ctx;

    var user = await service.ResolveUserAsync(token);

    user.Id.Should().Be(existingId);
    user.ExternalProvider.Should().Be("Apple");
    user.ExternalProviderId.Should().Be("bool-sub");
}

private string CreateAppleTokenWithBooleanEmailVerified(string sub, string email, bool emailVerified)
{
    var creds = new SigningCredentials(new RsaSecurityKey(_signingKey) { KeyId = Kid }, SecurityAlgorithms.RsaSha256);
    var payload = new JwtPayload(
        issuer: "https://appleid.apple.com",
        audience: BundleId,
        claims: new[] { new Claim("sub", sub), new Claim("email", email) },
        notBefore: DateTime.UtcNow,
        expires: DateTime.UtcNow.AddMinutes(10));
    // Add as a real JSON boolean, not a string
    payload["email_verified"] = emailVerified;
    var header = new JwtHeader(creds);
    var token = new JwtSecurityToken(header, payload);
    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

- [ ] **Step 2.2: Run the test and watch it fail**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~ResolveUserAsync_LinksByVerifiedEmail_WhenClaimIsBoolean"`
Expected: FAIL with "An account with this email already exists..." because the boolean-valued claim doesn't match the string `"true"`.

- [ ] **Step 2.3: Fix the comparison in `AppleAuthService`**

Edit `fasolt.Server/Application/Auth/AppleAuthService.cs:45`:

```csharp
var emailVerifiedRaw = principal.FindFirstValue("email_verified");
var emailVerified = bool.TryParse(emailVerifiedRaw, out var parsed) && parsed;
```

This handles `"true"`, `"True"`, `"TRUE"`, `"false"`, and all variants — `bool.TryParse` is case-insensitive.

- [ ] **Step 2.4: Run all Apple auth tests**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~AppleAuthServiceTests"`
Expected: All tests pass, including the new boolean-claim test and the existing string-claim tests.

- [ ] **Step 2.5: Commit**

```bash
git add fasolt.Server/Application/Auth/AppleAuthService.cs \
        fasolt.Tests/Auth/AppleAuthServiceTests.cs
git commit -m "fix(auth): handle boolean email_verified claim from Apple identity tokens

Apple's ID token spec allows email_verified to arrive as either a JSON
string or a JSON boolean. JwtSecurityTokenHandler surfaces bool-valued
claims as strings with inconsistent casing, so the previous string
equality check silently failed for boolean claims and forced affected
users down the 'refuse to link' branch.

Found in PR #101 review."
```

---

## Task 3: Refuse to clobber existing external provider link (bug fix from review)

**Context:** `AppleAuthService.ResolveUserAsync` currently overwrites `byEmail.ExternalProvider` and `ExternalProviderId` unconditionally when linking by verified email. If a user signed up via GitHub and later signs in with Apple using the same verified email, the GitHub link is silently lost. See PR #101 review finding #3.

**Files:**
- Modify: `fasolt.Server/Application/Auth/AppleAuthService.cs:62-68`
- Modify: `fasolt.Tests/Auth/AppleAuthServiceTests.cs` (add new test method)

- [ ] **Step 3.1: Write the failing clobber-refusal test**

Add to `fasolt.Tests/Auth/AppleAuthServiceTests.cs`:

```csharp
[Fact]
public async Task ResolveUserAsync_RefusesClobberOfExistingProvider()
{
    await using (var db = _db.CreateDbContext())
    {
        db.Users.Add(new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "github-user@example.com",
            NormalizedUserName = "GITHUB-USER@EXAMPLE.COM",
            Email = "github-user@example.com",
            NormalizedEmail = "GITHUB-USER@EXAMPLE.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ExternalProvider = "GitHub",
            ExternalProviderId = "gh-123",
        });
        await db.SaveChangesAsync();
    }

    var token = CreateAppleToken(sub: "apple-sub-x", email: "github-user@example.com", emailVerified: true);
    var (service, ctx) = CreateService();
    await using var _ = ctx;

    var act = async () => await service.ResolveUserAsync(token);
    await act.Should().ThrowAsync<AppleAuthException>()
        .WithMessage("*already linked to GitHub*");
}
```

- [ ] **Step 3.2: Run the test and watch it fail**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~ResolveUserAsync_RefusesClobberOfExistingProvider"`
Expected: FAIL — the test expects a throw, but the current implementation silently overwrites the GitHub link and returns the user.

- [ ] **Step 3.3: Add the clobber guard**

Edit `fasolt.Server/Application/Auth/AppleAuthService.cs:53-70`:

```csharp
// 2. Link to existing local account if email matches AND Apple verified it
if (!string.IsNullOrEmpty(email))
{
    var byEmail = await _userManager.FindByEmailAsync(email);
    if (byEmail is not null)
    {
        if (!emailVerified)
            throw new AppleAuthException(
                "An account with this email already exists. Sign in with your password and link Apple from settings.");

        // Refuse to overwrite an existing external provider link — the user
        // must sign in with their original provider and explicitly link
        // Apple from Settings (which does not exist yet; see #99 follow-up).
        if (byEmail.ExternalProvider is not null && byEmail.ExternalProvider != ProviderName)
            throw new AppleAuthException(
                $"This email is already linked to {byEmail.ExternalProvider}. Sign in with {byEmail.ExternalProvider} and link Apple from settings.");

        byEmail.ExternalProvider = ProviderName;
        byEmail.ExternalProviderId = sub;
        var update = await _userManager.UpdateAsync(byEmail);
        if (!update.Succeeded)
            throw new AppleAuthException("Failed to link Apple account to existing user.");
        return byEmail;
    }
}
```

- [ ] **Step 3.4: Run all Apple auth tests**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~AppleAuthServiceTests"`
Expected: All Apple tests pass.

- [ ] **Step 3.5: Commit**

```bash
git add fasolt.Server/Application/Auth/AppleAuthService.cs \
        fasolt.Tests/Auth/AppleAuthServiceTests.cs
git commit -m "fix(auth): refuse to clobber existing external provider link on Apple sign-in

Previously, signing in with Apple using an email that was already linked
to GitHub would silently overwrite the GitHub link. Now we throw a
descriptive AppleAuthException directing the user to sign in with their
original provider and link Apple from settings.

Found in PR #101 review."
```

---

## Task 4: Create `EmailVerificationCode` entity + EF migration

**Context:** The OTP storage model per the spec — one row per user at a time, updated in place on resend, deleted on successful verify.

**Files:**
- Create: `fasolt.Server/Domain/Entities/EmailVerificationCode.cs`
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs`
- Generate: `fasolt.Server/Infrastructure/Data/Migrations/<timestamp>_AddEmailVerificationCodes.cs`

- [ ] **Step 4.1: Create the entity class**

Create `fasolt.Server/Domain/Entities/EmailVerificationCode.cs`:

```csharp
namespace Fasolt.Server.Domain.Entities;

public class EmailVerificationCode
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;

    /// <summary>
    /// HMAC-SHA256 of the 6-digit code using the server pepper.
    /// Hex-encoded for convenient equality comparison.
    /// </summary>
    public string CodeHash { get; set; } = default!;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Failed verify attempts against the current code. Resets on resend.</summary>
    public int Attempts { get; set; }

    /// <summary>Total sends in this verification session. Never resets until row is deleted.</summary>
    public int SentCount { get; set; }

    public DateTimeOffset LastSentAt { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
}
```

- [ ] **Step 4.2: Register the DbSet and configure the entity**

Edit `fasolt.Server/Infrastructure/Data/AppDbContext.cs`. Add after line 21 (`public DbSet<AppLog> Logs => Set<AppLog>();`):

```csharp
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
```

Add inside `OnModelCreating` after the `AppLog` block (around line 124):

```csharp
        builder.Entity<EmailVerificationCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CodeHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });
```

The unique index on `UserId` enforces "one row per user at a time" at the database level.

- [ ] **Step 4.3: Generate the migration**

Run: `dotnet ef migrations add AddEmailVerificationCodes --project fasolt.Server`
Expected: Two new files appear under `fasolt.Server/Infrastructure/Data/Migrations/`:
- `<timestamp>_AddEmailVerificationCodes.cs`
- `<timestamp>_AddEmailVerificationCodes.Designer.cs`

And `AppDbContextModelSnapshot.cs` is updated.

Inspect the generated migration to confirm it creates an `EmailVerificationCodes` table with the expected columns, a unique index on `UserId`, and an FK to `AspNetUsers` with `ON DELETE CASCADE`.

- [ ] **Step 4.4: Apply the migration to the dev database**

Run: `dotnet ef database update --project fasolt.Server`
Expected: "Done." and the new table exists. Verify with:

`docker compose exec postgres psql -U fasolt -d fasolt -c "\d \"EmailVerificationCodes\""`
Expected: column listing showing `Id`, `UserId`, `CodeHash`, `ExpiresAt`, `Attempts`, `SentCount`, `LastSentAt`, `LockedUntil`.

- [ ] **Step 4.5: Run existing tests to confirm no regression**

Run: `dotnet test fasolt.Tests`
Expected: All tests pass (the migration shouldn't break anything; the new table is unused so far).

- [ ] **Step 4.6: Commit**

```bash
git add fasolt.Server/Domain/Entities/EmailVerificationCode.cs \
        fasolt.Server/Infrastructure/Data/AppDbContext.cs \
        fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat(auth): add EmailVerificationCode entity and migration for OTP verification"
```

---

## Task 5: Build `EmailVerificationCodeService` with tests

**Context:** The service that generates, stores, verifies, and throttles OTP codes. Hashes codes with HMAC-SHA256 + server pepper. Scoped lifetime (uses `AppDbContext`).

**Files:**
- Create: `fasolt.Server/Application/Auth/IEmailVerificationCodeService.cs`
- Create: `fasolt.Server/Application/Auth/EmailVerificationCodeService.cs`
- Create: `fasolt.Tests/Auth/EmailVerificationCodeServiceTests.cs`
- Modify: `fasolt.Tests/fasolt.Tests.csproj`

- [ ] **Step 5.0: Add the `Microsoft.Extensions.TimeProvider.Testing` NuGet package**

The tests in this task need `FakeTimeProvider` from `Microsoft.Extensions.Time.Testing`. Add it before writing the tests so they at least compile:

Run: `dotnet add fasolt.Tests package Microsoft.Extensions.TimeProvider.Testing`
Expected: Package added to `fasolt.Tests.csproj` and `packages.lock.json` updated.

- [ ] **Step 5.1: Write the failing tests first — happy path**

Create `fasolt.Tests/Auth/EmailVerificationCodeServiceTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests.Auth;

public class EmailVerificationCodeServiceTests : IAsyncLifetime
{
    private const string Pepper = "test-pepper-not-for-production";
    private readonly TestDb _db = new();

    public Task InitializeAsync() => _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GenerateAndStoreAsync_ReturnsSixDigitCode_AndPersistsRow()
    {
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        var code = await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        code.Should().MatchRegex(@"^\d{6}$");

        await using var read = _db.CreateDbContext();
        var row = await read.EmailVerificationCodes.SingleAsync(r => r.UserId == _db.UserId);
        row.CodeHash.Should().NotBeEmpty();
        row.CodeHash.Should().NotContain(code, "code must be hashed, not stored plaintext");
        row.SentCount.Should().Be(1);
        row.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsOk_ForCorrectCode_AndDeletesRow()
    {
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        var code = await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        var result = await service.VerifyAsync(_db.UserId, code, CancellationToken.None);

        result.Should().Be(VerifyResult.Ok);

        await using var read = _db.CreateDbContext();
        var row = await read.EmailVerificationCodes.SingleOrDefaultAsync(r => r.UserId == _db.UserId);
        row.Should().BeNull("row must be deleted on successful verify");
    }

    [Fact]
    public async Task VerifyAsync_ReturnsIncorrect_ForWrongCode_AndIncrementsAttempts()
    {
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        var result = await service.VerifyAsync(_db.UserId, "000001", CancellationToken.None);

        result.Should().Be(VerifyResult.Incorrect);

        await using var read = _db.CreateDbContext();
        var row = await read.EmailVerificationCodes.SingleAsync(r => r.UserId == _db.UserId);
        row.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task VerifyAsync_LocksOut_AfterFiveWrongAttempts()
    {
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        for (var i = 0; i < 5; i++)
            await service.VerifyAsync(_db.UserId, "000001", CancellationToken.None);

        var result = await service.VerifyAsync(_db.UserId, "000001", CancellationToken.None);
        result.Should().Be(VerifyResult.LockedOut);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsExpired_AfterExpiryTime()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (service, ctx) = CreateService(time);
        await using var _ = ctx;

        var code = await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        time.Advance(TimeSpan.FromMinutes(16));

        var result = await service.VerifyAsync(_db.UserId, code, CancellationToken.None);
        result.Should().Be(VerifyResult.Expired);
    }

    [Fact]
    public async Task CanResendAsync_ReturnsTooSoon_Within30Seconds()
    {
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);

        var result = await service.CanResendAsync(_db.UserId, CancellationToken.None);
        result.Should().Be(ResendResult.TooSoon);
    }

    [Fact]
    public async Task CanResendAsync_ReturnsOk_After30Seconds()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (service, ctx) = CreateService(time);
        await using var _ = ctx;

        await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);
        time.Advance(TimeSpan.FromSeconds(31));

        var result = await service.CanResendAsync(_db.UserId, CancellationToken.None);
        result.Should().Be(ResendResult.Ok);
    }

    [Fact]
    public async Task GenerateAndStoreAsync_RejectsResend_AfterFiveSends()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var (service, ctx) = CreateService(time);
        await using var _ = ctx;

        for (var i = 0; i < 5; i++)
        {
            await service.GenerateAndStoreAsync(_db.UserId, CancellationToken.None);
            time.Advance(TimeSpan.FromSeconds(31));
        }

        var result = await service.CanResendAsync(_db.UserId, CancellationToken.None);
        result.Should().Be(ResendResult.TooManyAttempts);
    }

    private (EmailVerificationCodeService service, AppDbContext db) CreateService(TimeProvider? time = null)
    {
        var db = _db.CreateDbContext();
        var service = new EmailVerificationCodeService(db, Pepper, time ?? TimeProvider.System);
        return (service, db);
    }
}
```

- [ ] **Step 5.2: Run the tests and watch them fail**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~EmailVerificationCodeServiceTests"`
Expected: FAIL — the service and enums don't exist yet.

- [ ] **Step 5.3: Create the service interface and enums**

Create `fasolt.Server/Application/Auth/IEmailVerificationCodeService.cs`:

```csharp
namespace Fasolt.Server.Application.Auth;

public enum VerifyResult { Ok, Incorrect, Expired, LockedOut, NotFound }
public enum ResendResult { Ok, TooSoon, TooManyAttempts }

public interface IEmailVerificationCodeService
{
    Task<string> GenerateAndStoreAsync(string userId, CancellationToken cancellationToken);
    Task<VerifyResult> VerifyAsync(string userId, string code, CancellationToken cancellationToken);
    Task<ResendResult> CanResendAsync(string userId, CancellationToken cancellationToken);
}
```

- [ ] **Step 5.4: Implement the service**

Create `fasolt.Server/Application/Auth/EmailVerificationCodeService.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Auth;

public class EmailVerificationCodeService : IEmailVerificationCodeService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(10);
    private const int MaxAttempts = 5;
    private const int MaxSendsPerSession = 5;

    private readonly AppDbContext _db;
    private readonly byte[] _pepper;
    private readonly TimeProvider _time;

    public EmailVerificationCodeService(AppDbContext db, string pepper, TimeProvider time)
    {
        _db = db;
        _pepper = Encoding.UTF8.GetBytes(pepper);
        _time = time;
    }

    public async Task<string> GenerateAndStoreAsync(string userId, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var code = GenerateCode();
        var hash = Hash(code);

        var existing = await _db.EmailVerificationCodes
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (existing is null)
        {
            _db.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                UserId = userId,
                CodeHash = hash,
                ExpiresAt = now.Add(CodeLifetime),
                Attempts = 0,
                SentCount = 1,
                LastSentAt = now,
                LockedUntil = null,
            });
        }
        else
        {
            existing.CodeHash = hash;
            existing.ExpiresAt = now.Add(CodeLifetime);
            existing.Attempts = 0;
            existing.SentCount += 1;
            existing.LastSentAt = now;
            existing.LockedUntil = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return code;
    }

    public async Task<VerifyResult> VerifyAsync(string userId, string code, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var row = await _db.EmailVerificationCodes
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (row is null)
            return VerifyResult.NotFound;

        if (row.LockedUntil is { } lockedUntil && lockedUntil > now)
            return VerifyResult.LockedOut;

        if (row.ExpiresAt <= now)
            return VerifyResult.Expired;

        var submittedHash = Hash(code);
        var stored = Convert.FromHexString(row.CodeHash);
        var submitted = Convert.FromHexString(submittedHash);

        if (CryptographicOperations.FixedTimeEquals(stored, submitted))
        {
            _db.EmailVerificationCodes.Remove(row);
            await _db.SaveChangesAsync(cancellationToken);
            return VerifyResult.Ok;
        }

        row.Attempts += 1;
        if (row.Attempts >= MaxAttempts)
        {
            row.LockedUntil = now.Add(LockoutDuration);
            await _db.SaveChangesAsync(cancellationToken);
            return VerifyResult.LockedOut;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return VerifyResult.Incorrect;
    }

    public async Task<ResendResult> CanResendAsync(string userId, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        var row = await _db.EmailVerificationCodes
            .FirstOrDefaultAsync(r => r.UserId == userId, cancellationToken);

        if (row is null)
            return ResendResult.Ok;

        if (row.SentCount >= MaxSendsPerSession)
            return ResendResult.TooManyAttempts;

        if (now - row.LastSentAt < ResendCooldown)
            return ResendResult.TooSoon;

        return ResendResult.Ok;
    }

    private static string GenerateCode()
    {
        while (true)
        {
            var n = RandomNumberGenerator.GetInt32(0, 1_000_000);
            if (n == 0) continue; // avoid visually confusing "000000"
            return n.ToString("D6");
        }
    }

    private string Hash(string code)
    {
        using var hmac = new HMACSHA256(_pepper);
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
}
```

- [ ] **Step 5.5: Run the tests — one or two may still fail**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~EmailVerificationCodeServiceTests"`
Expected: The `GenerateAndStoreAsync_RejectsResend_AfterFiveSends` test may fail if the service allows a sixth send. Re-read the test — it generates 5 times then asserts `CanResendAsync` returns `TooManyAttempts`. The service checks `SentCount >= MaxSendsPerSession`, which is `>= 5`, so after 5 sends `SentCount == 5`, the check `5 >= 5` returns `TooManyAttempts`. Should pass.

If any test fails, read the assertion failure carefully and fix the service. Do not modify the tests.

- [ ] **Step 5.6: Register the service in DI and add the pepper config**

Edit `.env.example`, add at the end:

```
# OTP pepper for email verification codes (HMAC-SHA256 key)
# Generate with: openssl rand -hex 32
OTP_PEPPER=
```

Edit `fasolt.Server/appsettings.Development.json`, add (alongside other dev secrets):

```json
  "OTP_PEPPER": "dev-otp-pepper-not-for-production-abcdef0123456789"
```

Edit `fasolt.Server/Program.cs` after the Apple sign-in wiring (around line 137):

```csharp
// Email verification OTP service
var otpPepper = builder.Configuration["OTP_PEPPER"]
    ?? throw new InvalidOperationException("OTP_PEPPER is not configured. Generate with 'openssl rand -hex 32' and set in .env or environment.");
builder.Services.AddScoped<Fasolt.Server.Application.Auth.IEmailVerificationCodeService>(sp =>
    new Fasolt.Server.Application.Auth.EmailVerificationCodeService(
        sp.GetRequiredService<Fasolt.Server.Infrastructure.Data.AppDbContext>(),
        otpPepper,
        sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton(TimeProvider.System);
```

Note: `AddSingleton(TimeProvider.System)` may already be implicit in .NET 10, but register it explicitly to be safe. If another registration already exists, skip this line.

- [ ] **Step 5.7: Run all tests**

Run: `dotnet test fasolt.Tests`
Expected: All tests pass, including the full `EmailVerificationCodeServiceTests` suite.

- [ ] **Step 5.8: Commit**

```bash
git add fasolt.Server/Application/Auth/IEmailVerificationCodeService.cs \
        fasolt.Server/Application/Auth/EmailVerificationCodeService.cs \
        fasolt.Tests/Auth/EmailVerificationCodeServiceTests.cs \
        fasolt.Server/Program.cs \
        fasolt.Server/appsettings.Development.json \
        fasolt.Tests/fasolt.Tests.csproj \
        fasolt.Tests/packages.lock.json \
        .env.example
git commit -m "feat(auth): EmailVerificationCodeService — HMAC-hashed 6-digit OTPs with resend throttling"
```

---

## Task 6: Add `SendVerificationCodeAsync` to email senders

**Context:** The OTP email template. Adds a new method to both `PlunkEmailSender` and `DevEmailSender` exposed via a new `IOtpEmailSender` interface. The existing `IEmailSender<AppUser>` binding stays as-is.

**Files:**
- Create: `fasolt.Server/Application/Auth/IOtpEmailSender.cs`
- Modify: `fasolt.Server/Infrastructure/Services/PlunkEmailSender.cs`
- Modify: `fasolt.Server/Infrastructure/Services/DevEmailSender.cs`
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 6.1: Create the new interface**

Create `fasolt.Server/Application/Auth/IOtpEmailSender.cs`:

```csharp
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Application.Auth;

public interface IOtpEmailSender
{
    Task SendVerificationCodeAsync(AppUser user, string email, string code);
}
```

- [ ] **Step 6.2: Implement on `PlunkEmailSender`**

Edit `fasolt.Server/Infrastructure/Services/PlunkEmailSender.cs`. Change the class declaration to implement both interfaces:

```csharp
public class PlunkEmailSender : IEmailSender<AppUser>, Fasolt.Server.Application.Auth.IOtpEmailSender
```

Add the new method (after the existing `SendPasswordResetCodeAsync` method):

```csharp
    public Task SendVerificationCodeAsync(AppUser user, string email, string code)
    {
        var body = $"""
            <p>Welcome to Fasolt!</p>
            <p>Your verification code is: <strong>{code}</strong></p>
            <p>It expires in 15 minutes. If you didn't request this, you can safely ignore this email — no account was created.</p>
            """;

        return SendAsync(email, "Your Fasolt verification code", body);
    }
```

- [ ] **Step 6.3: Implement on `DevEmailSender`**

Edit `fasolt.Server/Infrastructure/Services/DevEmailSender.cs`. Change the class declaration:

```csharp
public class DevEmailSender : IEmailSender<AppUser>, Fasolt.Server.Application.Auth.IOtpEmailSender
```

Add the new method:

```csharp
    public Task SendVerificationCodeAsync(AppUser user, string email, string code)
    {
        _logger.LogWarning("[DEV EMAIL] Verification code for {Email}: {Code}", email, code);
        return Task.CompletedTask;
    }
```

- [ ] **Step 6.4: Register `IOtpEmailSender` in DI**

Edit `fasolt.Server/Program.cs` around line 175 where `IEmailSender<AppUser>` is registered. Replace the existing email sender registration block with:

```csharp
var plunkApiKey = builder.Configuration["PLUNK_API_KEY"];
if (!builder.Environment.IsDevelopment() && !string.IsNullOrEmpty(plunkApiKey))
{
    builder.Services.AddHttpClient<PlunkEmailSender>((sp, client) =>
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", plunkApiKey);
    });
    builder.Services.AddTransient<IEmailSender<AppUser>>(sp => sp.GetRequiredService<PlunkEmailSender>());
    builder.Services.AddTransient<Fasolt.Server.Application.Auth.IOtpEmailSender>(sp => sp.GetRequiredService<PlunkEmailSender>());
}
else
{
    builder.Services.AddTransient<DevEmailSender>();
    builder.Services.AddTransient<IEmailSender<AppUser>>(sp => sp.GetRequiredService<DevEmailSender>());
    builder.Services.AddTransient<Fasolt.Server.Application.Auth.IOtpEmailSender>(sp => sp.GetRequiredService<DevEmailSender>());
}
```

This registers each concrete sender as its own service and binds both interfaces to it, so a single instance handles both Identity emails and OTP emails.

Add the required using statement at the top of Program.cs if not already present:

```csharp
using Fasolt.Server.Infrastructure.Services;
```

- [ ] **Step 6.5: Build to verify**

Run: `dotnet build fasolt.Server`
Expected: Build succeeds with no errors.

- [ ] **Step 6.6: Run all tests**

Run: `dotnet test fasolt.Tests`
Expected: All tests pass.

- [ ] **Step 6.7: Commit**

```bash
git add fasolt.Server/Application/Auth/IOtpEmailSender.cs \
        fasolt.Server/Infrastructure/Services/PlunkEmailSender.cs \
        fasolt.Server/Infrastructure/Services/DevEmailSender.cs \
        fasolt.Server/Program.cs
git commit -m "feat(auth): IOtpEmailSender interface with SendVerificationCodeAsync for Plunk + dev senders"
```

---

## Task 7: Add `screen_hint=signup` support to `/oauth/authorize`

**Context:** When iOS opens the popup with `screen_hint=signup`, the server should redirect unauthenticated users to `/oauth/register` (which doesn't exist yet — Task 8 builds it) instead of `/oauth/login`. For this task we add the redirect code but point it at a placeholder route; Task 8 will create the real page.

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs:114-121`
- Create: `fasolt.Tests/Auth/OAuthAuthorizeScreenHintTests.cs`

- [ ] **Step 7.1: Write the failing tests**

Create `fasolt.Tests/Auth/OAuthAuthorizeScreenHintTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fasolt.Tests.Auth;

public class OAuthAuthorizeScreenHintTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthAuthorizeScreenHintTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                    ["OTP_PEPPER"] = "test-pepper",
                });
            });
        });
    }

    [Fact]
    public async Task Authorize_WithSignupHint_RedirectsToRegister_WhenUnauthenticated()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            "/oauth/authorize?response_type=code&client_id=fasolt-ios&redirect_uri=fasolt://oauth/callback&code_challenge=abc&code_challenge_method=S256&screen_hint=signup");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/oauth/register");
    }

    [Fact]
    public async Task Authorize_WithoutHint_StillRedirectsToLogin_WhenUnauthenticated()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            "/oauth/authorize?response_type=code&client_id=fasolt-ios&redirect_uri=fasolt://oauth/callback&code_challenge=abc&code_challenge_method=S256");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/oauth/login");
    }
}
```

- [ ] **Step 7.2: Run the tests and watch them fail**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthAuthorizeScreenHintTests"`
Expected: The first test fails (redirects to `/oauth/login` regardless of hint), the second passes.

- [ ] **Step 7.3: Add the hint branch to the authorize handler**

Edit `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs:114-121`. Replace the unauthenticated redirect block:

```csharp
        // Authorization Endpoint
        app.MapGet("/oauth/authorize", async (HttpContext context, AppDbContext db, IDataProtectionProvider dataProtection) =>
        {
            var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (result?.Principal is null)
            {
                var returnUrl = context.Request.QueryString.Value;
                var openIddictReq = context.GetOpenIddictServerRequest();
                var hint = openIddictReq?.GetParameter("screen_hint")?.ToString();
                var target = hint == "signup" ? "/oauth/register" : "/oauth/login";
                return Results.Redirect($"{target}?returnUrl={Uri.EscapeDataString("/oauth/authorize" + returnUrl)}");
            }
```

The `screen_hint` parameter is read from the OpenIddict request; `GetParameter` handles arbitrary query string values that aren't in OpenIddict's standard set.

- [ ] **Step 7.4: Run the tests — expect the hint test to pass but the register-redirect to 404**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthAuthorizeScreenHintTests"`
Expected: First test now redirects to `/oauth/register`. The test asserts only that the Location header starts with `/oauth/register` (it doesn't follow the redirect), so it passes even though `/oauth/register` doesn't exist yet.

- [ ] **Step 7.5: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs \
        fasolt.Tests/Auth/OAuthAuthorizeScreenHintTests.cs
git commit -m "feat(oauth): screen_hint=signup parameter redirects unauthenticated users to /oauth/register"
```

---

## Task 8: Build `/oauth/register` GET + POST

**Context:** The server-rendered register page with live password rules, ToS checkbox, and form POST that creates an unverified user and sends an OTP. Styled to match `/oauth/login`.

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` (add new endpoints)
- Create: `fasolt.Tests/Auth/OAuthRegisterEndpointTests.cs`

- [ ] **Step 8.1: Write failing tests for the GET and POST handlers**

Create `fasolt.Tests/Auth/OAuthRegisterEndpointTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Tests.Auth;

public class OAuthRegisterEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthRegisterEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OTP_PEPPER"] = "test-pepper",
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                });
            });
        });
    }

    [Fact]
    public async Task Get_RendersRegisterForm()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/oauth/register?returnUrl=%2Foauth%2Fauthorize%3F...");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("<form");
        body.Should().Contain("name=\"email\"");
        body.Should().Contain("name=\"password\"");
        body.Should().Contain("name=\"confirmPassword\"");
        body.Should().Contain("name=\"tosAccepted\"");
        body.Should().Contain("Already have an account?");
    }

    [Fact]
    public async Task Post_WithValidFields_CreatesUnverifiedUser_AndRedirectsToVerify()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // Prime CSRF by GETting the form first
        var getResponse = await client.GetAsync("/oauth/register?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var email = $"register-test-{Guid.NewGuid():N}@example.com";
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["password"] = "Abcdefg1",
            ["confirmPassword"] = "Abcdefg1",
            ["tosAccepted"] = "true",
            ["returnUrl"] = "/",
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/register") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/oauth/verify-email");
        response.Headers.Location!.OriginalString.Should().Contain(Uri.EscapeDataString(email));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        user.Should().NotBeNull();
        user!.EmailConfirmed.Should().BeFalse();

        var codeRow = await db.EmailVerificationCodes.FirstOrDefaultAsync(r => r.UserId == user.Id);
        codeRow.Should().NotBeNull();
    }

    [Fact]
    public async Task Post_WithMismatchedPasswords_ReturnsFormWithError()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var getResponse = await client.GetAsync("/oauth/register?returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = "mismatch@example.com",
            ["password"] = "Abcdefg1",
            ["confirmPassword"] = "Different2",
            ["tosAccepted"] = "true",
            ["returnUrl"] = "/",
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/register") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("don't match");
    }

    private static string ExtractCsrfToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\" value=\"";
        var idx = html.IndexOf(marker);
        if (idx < 0) throw new InvalidOperationException("CSRF token not found");
        var start = idx + marker.Length;
        var end = html.IndexOf("\"", start);
        return html.Substring(start, end - start);
    }
}
```

- [ ] **Step 8.2: Run the tests and watch them fail with 404**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthRegisterEndpointTests"`
Expected: All tests fail because `/oauth/register` doesn't exist yet.

- [ ] **Step 8.3: Add the GET handler**

Edit `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`. Add a new endpoint after the `/oauth/login` POST handler (after line 537). Use the same inline-CSS style as `/oauth/login`. The full block is long; copy the CSS block from the existing `/oauth/login` page and reuse it via a local `const string sharedStyles = ...` if practical, OR duplicate for now and refactor later:

```csharp
        // OAuth Register Page (GET) — server-rendered for ASWebAuthenticationSession compatibility
        app.MapGet("/oauth/register", [AllowAnonymous] (HttpContext context, IAntiforgery antiforgery) =>
        {
            var rawReturnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";
            var error = context.Request.Query["error"].FirstOrDefault();

            var tokens = antiforgery.GetAndStoreTokens(context);
            var csrfToken = System.Web.HttpUtility.HtmlAttributeEncode(tokens.RequestToken!);
            var returnUrlEncoded = System.Web.HttpUtility.HtmlAttributeEncode(returnUrl);

            var errorHtml = error is not null
                ? $"<p class=\"error\">{System.Web.HttpUtility.HtmlEncode(error)}</p>"
                : "";

            var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
                <title>Create account — fasolt</title>
                <style>
                    * { box-sizing: border-box; margin: 0; padding: 0; }
                    html, body { height: 100%; }
                    body {
                        font-family: -apple-system, system-ui, sans-serif;
                        background: #fafafa;
                        color: #18181b;
                        display: flex;
                        align-items: flex-start;
                        justify-content: center;
                        padding: max(env(safe-area-inset-top), 16px) 16px max(env(safe-area-inset-bottom), 16px);
                        -webkit-font-smoothing: antialiased;
                    }
                    @media (min-height: 720px) { body { align-items: center; } }
                    .card {
                        width: 100%;
                        max-width: 380px;
                        background: white;
                        border: 1px solid #e5e7eb;
                        border-radius: 14px;
                        padding: 24px 22px;
                        box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04);
                    }
                    .header { display: flex; flex-direction: column; align-items: center; gap: 8px; margin-bottom: 18px; }
                    .header h1 { font-size: 1.125rem; font-weight: 600; letter-spacing: -0.01em; color: #18181b; }
                    label { display: block; font-size: 0.75rem; font-weight: 500; color: #374151; margin-bottom: 4px; }
                    input[type=email], input[type=password] {
                        width: 100%;
                        padding: 10px 12px;
                        border: 1px solid #d1d5db;
                        border-radius: 8px;
                        font-size: 0.9375rem;
                        outline: none;
                        background: white;
                        -webkit-appearance: none;
                    }
                    input:focus { border-color: #18181b; box-shadow: 0 0 0 3px rgba(24, 24, 27, 0.08); }
                    .field { margin-bottom: 10px; }
                    button {
                        width: 100%;
                        padding: 11px;
                        margin-top: 4px;
                        background: #18181b;
                        color: white;
                        border: none;
                        border-radius: 8px;
                        cursor: pointer;
                        font-size: 0.9375rem;
                        font-weight: 500;
                    }
                    button:disabled { background: #a1a1aa; cursor: not-allowed; }
                    .error {
                        color: #b91c1c;
                        font-size: 0.8125rem;
                        margin-bottom: 10px;
                        padding: 8px 12px;
                        background: #fef2f2;
                        border: 1px solid #fecaca;
                        border-radius: 8px;
                    }
                    .footer { text-align: center; margin-top: 14px; font-size: 0.8125rem; color: #71717a; }
                    .footer a { color: #18181b; font-weight: 500; text-decoration: none; }
                    .rules { margin-top: 6px; font-size: 0.75rem; color: #71717a; }
                    .rules li { list-style: none; padding: 2px 0; }
                    .rules li.ok { color: #059669; }
                    .rules li.ok::before { content: "✓ "; }
                    .rules li.pending::before { content: "○ "; }
                    .mismatch { color: #b91c1c; font-size: 0.75rem; margin-top: 4px; }
                    .tos { display: flex; align-items: flex-start; gap: 8px; margin: 12px 0; font-size: 0.8125rem; color: #374151; }
                    .tos input { margin-top: 2px; }
                    .tos a { color: #18181b; font-weight: 500; }
                    @media (prefers-color-scheme: dark) {
                        body { background: #0a0a0a; color: #fafafa; }
                        .card { background: #18181b; border-color: #27272a; }
                        .header h1 { color: #fafafa; }
                        label { color: #d4d4d8; }
                        input[type=email], input[type=password] { background: #0a0a0a; border-color: #3f3f46; color: #fafafa; }
                        button { background: #fafafa; color: #18181b; }
                        .footer { color: #a1a1aa; }
                        .footer a { color: #fafafa; }
                        .tos { color: #d4d4d8; }
                        .tos a { color: #fafafa; }
                    }
                </style>
            </head>
            <body>
                <main class="card">
                    <div class="header">
                        <h1>Create your Fasolt account</h1>
                    </div>
                    {{errorHtml}}
                    <form method="post" action="/oauth/register" id="registerForm">
                        <input type="hidden" name="__RequestVerificationToken" value="{{csrfToken}}" />
                        <input type="hidden" name="returnUrl" value="{{returnUrlEncoded}}" />
                        <div class="field">
                            <label for="email">Email</label>
                            <input type="email" id="email" name="email" placeholder="you@example.com" autocomplete="email" required autofocus />
                        </div>
                        <div class="field">
                            <label for="password">Password</label>
                            <input type="password" id="password" name="password" autocomplete="new-password" required />
                            <ul class="rules" id="rules">
                                <li class="pending" data-rule="length">At least 8 characters</li>
                                <li class="pending" data-rule="upper">Uppercase letter</li>
                                <li class="pending" data-rule="lower">Lowercase letter</li>
                                <li class="pending" data-rule="digit">Number</li>
                            </ul>
                        </div>
                        <div class="field">
                            <label for="confirmPassword">Confirm password</label>
                            <input type="password" id="confirmPassword" name="confirmPassword" autocomplete="new-password" required />
                            <div class="mismatch" id="mismatch" style="display:none">Passwords don't match</div>
                        </div>
                        <label class="tos">
                            <input type="checkbox" name="tosAccepted" value="true" id="tos" required />
                            <span>I agree to the <a href="/terms" target="_blank">Terms of Service</a></span>
                        </label>
                        <button type="submit" id="submit">Create account</button>
                    </form>
                    <p class="footer">Already have an account? <a href="/oauth/login?returnUrl={{returnUrlEncoded}}">Sign in</a></p>
                </main>
                <script>
                    const pwd = document.getElementById('password');
                    const confirm = document.getElementById('confirmPassword');
                    const rules = document.getElementById('rules');
                    const mismatch = document.getElementById('mismatch');
                    function evaluate() {
                        const v = pwd.value;
                        const checks = {
                            length: v.length >= 8,
                            upper: /[A-Z]/.test(v),
                            lower: /[a-z]/.test(v),
                            digit: /[0-9]/.test(v)
                        };
                        for (const li of rules.children) {
                            const r = li.dataset.rule;
                            li.className = checks[r] ? 'ok' : 'pending';
                        }
                        mismatch.style.display = (confirm.value && confirm.value !== v) ? 'block' : 'none';
                    }
                    pwd.addEventListener('input', evaluate);
                    confirm.addEventListener('input', evaluate);
                </script>
            </body>
            </html>
            """;
            return Results.Content(html, "text/html");
        });
```

- [ ] **Step 8.4: Add the POST handler**

Add right after the GET handler:

```csharp
        // OAuth Register Handler (POST)
        app.MapPost("/oauth/register", [AllowAnonymous] async (
            HttpContext context,
            UserManager<AppUser> userManager,
            IEmailVerificationCodeService otpService,
            IOtpEmailSender emailSender,
            IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest("Invalid request");

            var form = await context.Request.ReadFormAsync();
            var email = form["email"].FirstOrDefault() ?? "";
            var password = form["password"].FirstOrDefault() ?? "";
            var confirmPassword = form["confirmPassword"].FirstOrDefault() ?? "";
            var tosAccepted = form["tosAccepted"].FirstOrDefault() == "true";
            var rawReturnUrl = form["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";

            string? error = null;
            if (!tosAccepted) error = "You must accept the Terms of Service.";
            else if (password != confirmPassword) error = "Passwords don't match.";
            else if (string.IsNullOrEmpty(email) || !email.Contains('@')) error = "Please enter a valid email address.";

            if (error is not null)
                return Results.Redirect($"/oauth/register?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(error)}");

            // Check if email exists
            var existing = await userManager.FindByEmailAsync(email);
            AppUser user;
            if (existing is not null)
            {
                if (existing.EmailConfirmed)
                    return Results.Redirect($"/oauth/register?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString("An account with this email already exists. Sign in instead.")}");

                // Unconfirmed existing user — reuse the row, regenerate OTP
                user = existing;
            }
            else
            {
                user = new AppUser { UserName = email, Email = email };
                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    var msg = string.Join("; ", createResult.Errors.Select(e => e.Description));
                    return Results.Redirect($"/oauth/register?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(msg)}");
                }
            }

            var code = await otpService.GenerateAndStoreAsync(user.Id, CancellationToken.None);
            await emailSender.SendVerificationCodeAsync(user, user.Email!, code);

            return Results.Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }).RequireRateLimiting("auth-strict");
```

Add the required `using` statements at the top of `OAuthEndpoints.cs` if not already present:

```csharp
using Microsoft.AspNetCore.Authorization;
```

(Most of these are already present — check before adding.)

- [ ] **Step 8.5: Run the register tests**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthRegisterEndpointTests"`
Expected: GET test passes. POST tests may fail if `/oauth/verify-email` doesn't exist yet — but the redirect assertion only checks the Location header, so it should pass.

- [ ] **Step 8.6: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs \
        fasolt.Tests/Auth/OAuthRegisterEndpointTests.cs
git commit -m "feat(oauth): /oauth/register GET + POST with OTP dispatch"
```

---

## Task 9: Build `/oauth/verify-email` GET + POST + resend

**Context:** The OTP input screen. User types/autofills the 6-digit code, submits, gets signed in via Identity cookie, OAuth flow resumes.

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`
- Create: `fasolt.Tests/Auth/OAuthVerifyEmailEndpointTests.cs`

- [ ] **Step 9.1: Write failing tests**

Create `fasolt.Tests/Auth/OAuthVerifyEmailEndpointTests.cs`:

```csharp
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Tests.Auth;

public class OAuthVerifyEmailEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthVerifyEmailEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OTP_PEPPER"] = "test-pepper",
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                });
            });
        });
    }

    [Fact]
    public async Task Get_RendersVerifyForm_WithEmailPrefilled()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/oauth/verify-email?email=foo@example.com&returnUrl=%2F");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("foo@example.com");
        body.Should().Contain("autocomplete=\"one-time-code\"");
        body.Should().Contain("name=\"code\"");
    }

    [Fact]
    public async Task Post_WithCorrectCode_ConfirmsEmail_AndRedirectsToReturnUrl()
    {
        var email = $"verify-{Guid.NewGuid():N}@example.com";
        string code;
        string userId;

        // Seed a user with a fresh OTP
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email };
            await userManager.CreateAsync(user, "Abcdefg1");
            userId = user.Id;
            var otp = scope.ServiceProvider.GetRequiredService<IEmailVerificationCodeService>();
            code = await otp.GenerateAndStoreAsync(user.Id, CancellationToken.None);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // Prime CSRF
        var getResponse = await client.GetAsync($"/oauth/verify-email?email={email}&returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["code"] = code,
            ["returnUrl"] = "/",
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/verify-email") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/");

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        user.EmailConfirmed.Should().BeTrue();
        var otpRow = await db.EmailVerificationCodes.FirstOrDefaultAsync(r => r.UserId == userId);
        otpRow.Should().BeNull();
    }

    [Fact]
    public async Task Post_WithWrongCode_ShowsError()
    {
        var email = $"wrong-{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
            var user = new AppUser { UserName = email, Email = email };
            await userManager.CreateAsync(user, "Abcdefg1");
            var otp = scope.ServiceProvider.GetRequiredService<IEmailVerificationCodeService>();
            await otp.GenerateAndStoreAsync(user.Id, CancellationToken.None);
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var getResponse = await client.GetAsync($"/oauth/verify-email?email={email}&returnUrl=%2F");
        var csrfToken = ExtractCsrfToken(await getResponse.Content.ReadAsStringAsync());
        var cookieHeader = getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "";

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["email"] = email,
            ["code"] = "000001",
            ["returnUrl"] = "/",
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/oauth/verify-email") { Content = content };
        request.Headers.Add("Cookie", cookieHeader);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("error=");
        response.Headers.Location!.OriginalString.Should().Contain("Incorrect");
    }

    private static string ExtractCsrfToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\" value=\"";
        var idx = html.IndexOf(marker);
        if (idx < 0) throw new InvalidOperationException("CSRF token not found");
        var start = idx + marker.Length;
        var end = html.IndexOf("\"", start);
        return html.Substring(start, end - start);
    }
}
```

- [ ] **Step 9.2: Run the tests and watch them fail**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthVerifyEmailEndpointTests"`
Expected: All fail because `/oauth/verify-email` doesn't exist yet.

- [ ] **Step 9.3: Add the GET handler**

Edit `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`. Add after the `/oauth/register` POST handler from Task 8:

```csharp
        // OAuth Verify Email Page (GET)
        app.MapGet("/oauth/verify-email", [AllowAnonymous] (HttpContext context, IAntiforgery antiforgery) =>
        {
            var email = context.Request.Query["email"].FirstOrDefault() ?? "";
            var rawReturnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";
            var error = context.Request.Query["error"].FirstOrDefault();

            var tokens = antiforgery.GetAndStoreTokens(context);
            var csrfToken = System.Web.HttpUtility.HtmlAttributeEncode(tokens.RequestToken!);
            var emailEncoded = System.Web.HttpUtility.HtmlAttributeEncode(email);
            var emailDisplay = System.Web.HttpUtility.HtmlEncode(email);
            var returnUrlEncoded = System.Web.HttpUtility.HtmlAttributeEncode(returnUrl);

            var errorHtml = error is not null
                ? $"<p class=\"error\">{System.Web.HttpUtility.HtmlEncode(error)}</p>"
                : "";

            var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
                <title>Verify email — fasolt</title>
                <style>
                    * { box-sizing: border-box; margin: 0; padding: 0; }
                    html, body { height: 100%; }
                    body {
                        font-family: -apple-system, system-ui, sans-serif;
                        background: #fafafa;
                        color: #18181b;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        padding: max(env(safe-area-inset-top), 16px) 16px max(env(safe-area-inset-bottom), 16px);
                    }
                    .card {
                        width: 100%;
                        max-width: 380px;
                        background: white;
                        border: 1px solid #e5e7eb;
                        border-radius: 14px;
                        padding: 32px 24px;
                        box-shadow: 0 1px 2px rgba(15, 23, 42, 0.04);
                        text-align: center;
                    }
                    .card h1 { font-size: 1.25rem; font-weight: 600; margin-bottom: 8px; color: #18181b; }
                    .card p { color: #71717a; font-size: 0.875rem; margin-bottom: 20px; }
                    .card p strong { color: #18181b; }
                    input[type=text] {
                        width: 100%;
                        padding: 14px;
                        font-size: 1.5rem;
                        text-align: center;
                        letter-spacing: 0.5em;
                        font-family: ui-monospace, "SF Mono", Menlo, monospace;
                        border: 1px solid #d1d5db;
                        border-radius: 10px;
                        background: white;
                        outline: none;
                    }
                    input[type=text]:focus { border-color: #18181b; box-shadow: 0 0 0 3px rgba(24,24,27,0.08); }
                    button {
                        width: 100%;
                        padding: 11px;
                        margin-top: 16px;
                        background: #18181b;
                        color: white;
                        border: none;
                        border-radius: 8px;
                        cursor: pointer;
                        font-size: 0.9375rem;
                        font-weight: 500;
                    }
                    .resend { margin-top: 16px; font-size: 0.8125rem; color: #71717a; }
                    .resend a { color: #18181b; font-weight: 500; text-decoration: none; }
                    .resend form { display: inline; }
                    .resend button {
                        display: inline;
                        width: auto;
                        padding: 0;
                        margin: 0;
                        background: transparent;
                        color: #18181b;
                        font-weight: 500;
                        text-decoration: underline;
                    }
                    .error {
                        color: #b91c1c;
                        font-size: 0.8125rem;
                        margin-bottom: 10px;
                        padding: 8px 12px;
                        background: #fef2f2;
                        border: 1px solid #fecaca;
                        border-radius: 8px;
                    }
                    @media (prefers-color-scheme: dark) {
                        body { background: #0a0a0a; color: #fafafa; }
                        .card { background: #18181b; border-color: #27272a; }
                        .card h1 { color: #fafafa; }
                        .card p { color: #a1a1aa; }
                        .card p strong { color: #fafafa; }
                        input[type=text] { background: #0a0a0a; border-color: #3f3f46; color: #fafafa; }
                        button { background: #fafafa; color: #18181b; }
                        .resend { color: #a1a1aa; }
                        .resend a, .resend button { color: #fafafa; }
                    }
                </style>
            </head>
            <body>
                <main class="card">
                    <h1>Check your email</h1>
                    <p>We sent a 6-digit code to <strong>{{emailDisplay}}</strong></p>
                    {{errorHtml}}
                    <form method="post" action="/oauth/verify-email">
                        <input type="hidden" name="__RequestVerificationToken" value="{{csrfToken}}" />
                        <input type="hidden" name="email" value="{{emailEncoded}}" />
                        <input type="hidden" name="returnUrl" value="{{returnUrlEncoded}}" />
                        <input type="text" name="code" inputmode="numeric" autocomplete="one-time-code" pattern="[0-9]{6}" maxlength="6" autofocus required />
                        <button type="submit">Verify</button>
                    </form>
                    <p class="resend">
                        Didn't get it?
                        <form method="post" action="/oauth/verify-email/resend">
                            <input type="hidden" name="__RequestVerificationToken" value="{{csrfToken}}" />
                            <input type="hidden" name="email" value="{{emailEncoded}}" />
                            <input type="hidden" name="returnUrl" value="{{returnUrlEncoded}}" />
                            <button type="submit">Resend code</button>
                        </form>
                    </p>
                    <p class="resend"><a href="/oauth/register?returnUrl={{returnUrlEncoded}}">Use a different email</a></p>
                </main>
            </body>
            </html>
            """;
            return Results.Content(html, "text/html");
        });
```

- [ ] **Step 9.4: Add the POST handler**

Right after the GET:

```csharp
        // OAuth Verify Email Handler (POST)
        app.MapPost("/oauth/verify-email", [AllowAnonymous] async (
            HttpContext context,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IEmailVerificationCodeService otpService,
            IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest("Invalid request");

            var form = await context.Request.ReadFormAsync();
            var email = form["email"].FirstOrDefault() ?? "";
            var code = form["code"].FirstOrDefault() ?? "";
            var rawReturnUrl = form["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";

            string ErrorRedirect(string msg)
                => $"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(msg)}";

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                return Results.Redirect(ErrorRedirect("That code has expired. Request a new one."));

            var result = await otpService.VerifyAsync(user.Id, code, CancellationToken.None);
            switch (result)
            {
                case VerifyResult.Ok:
                    user.EmailConfirmed = true;
                    await userManager.UpdateAsync(user);
                    await AccountEndpoints.SignInWithEmailClaimAsync(signInManager, user, isPersistent: false);
                    return Results.Redirect(returnUrl);
                case VerifyResult.Incorrect:
                    return Results.Redirect(ErrorRedirect("Incorrect code, try again."));
                case VerifyResult.Expired:
                case VerifyResult.NotFound:
                    return Results.Redirect(ErrorRedirect("That code has expired. Request a new one."));
                case VerifyResult.LockedOut:
                    return Results.Redirect(ErrorRedirect("Too many failed attempts. Try again in 10 minutes."));
                default:
                    return Results.Redirect(ErrorRedirect("Something went wrong. Please try again."));
            }
        }).RequireRateLimiting("auth");
```

- [ ] **Step 9.5: Add the resend POST handler**

Right after:

```csharp
        // OAuth Verify Email Resend Handler (POST)
        app.MapPost("/oauth/verify-email/resend", [AllowAnonymous] async (
            HttpContext context,
            UserManager<AppUser> userManager,
            IEmailVerificationCodeService otpService,
            IOtpEmailSender emailSender,
            IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest("Invalid request");

            var form = await context.Request.ReadFormAsync();
            var email = form["email"].FirstOrDefault() ?? "";
            var rawReturnUrl = form["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";

            string ErrorRedirect(string msg)
                => $"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(msg)}";

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
                return Results.Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}");

            var canResend = await otpService.CanResendAsync(user.Id, CancellationToken.None);
            switch (canResend)
            {
                case ResendResult.TooSoon:
                    return Results.Redirect(ErrorRedirect("Please wait before requesting another code."));
                case ResendResult.TooManyAttempts:
                    return Results.Redirect(ErrorRedirect("Too many codes sent. Please start over with 'Use a different email'."));
            }

            var code = await otpService.GenerateAndStoreAsync(user.Id, CancellationToken.None);
            await emailSender.SendVerificationCodeAsync(user, user.Email!, code);

            return Results.Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }).RequireRateLimiting("auth-strict");
```

- [ ] **Step 9.6: Run the verify-email tests**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthVerifyEmailEndpointTests"`
Expected: All tests pass.

- [ ] **Step 9.7: Run the full register + verify end-to-end tests together**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthRegister|FullyQualifiedName~OAuthVerifyEmail"`
Expected: Both suites green.

- [ ] **Step 9.8: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs \
        fasolt.Tests/Auth/OAuthVerifyEmailEndpointTests.cs
git commit -m "feat(oauth): /oauth/verify-email GET + POST + resend with OTP verification"
```

---

## Task 10: Add "Create account" link to `/oauth/login`

**Context:** Small edit to the existing `/oauth/login` GET handler so users who land on login can switch to register.

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs:503-504`

- [ ] **Step 10.1: Edit the login page footer**

In `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`, find the footer line in the login page HTML (around line 504):

```html
                    <p class="footer">Continue to your client after signing in.</p>
```

Replace it with:

```html
                    <p class="footer">New to Fasolt? <a href="/oauth/register?returnUrl={{returnUrlEncoded}}" style="color:inherit;font-weight:500;">Create an account</a></p>
```

- [ ] **Step 10.2: Add a test that verifies the link is present**

Add to `fasolt.Tests/Auth/OAuthProviderHintTests.cs` (since it already covers `/oauth/login`):

```csharp
    [Fact]
    public async Task OAuthLogin_ContainsCreateAccountLink()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/oauth/login?returnUrl=/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("/oauth/register");
        body.Should().Contain("Create an account");
    }
```

- [ ] **Step 10.3: Run the test**

Run: `dotnet test fasolt.Tests --filter "FullyQualifiedName~OAuthLogin_ContainsCreateAccountLink"`
Expected: PASS.

- [ ] **Step 10.4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs \
        fasolt.Tests/Auth/OAuthProviderHintTests.cs
git commit -m "feat(oauth): add 'Create account' link to /oauth/login page"
```

---

## Task 11: iOS — add `providerHint: \"signup\"` support in `AuthService.signIn`

**Context:** The `signIn` method already accepts a `providerHint` parameter (added in PR #101 for GitHub). It translates the hint into a `provider_hint` query parameter. We need it to also emit `screen_hint=signup` when the hint is `"signup"`. The cleanest approach is to split the two: `providerHint` keeps its current `"github"` semantics, and we add a new `screenHint` parameter for `"signup"`.

**Files:**
- Modify: `fasolt.ios/Fasolt/Services/AuthService.swift`

- [ ] **Step 11.1: Update `signIn` and `openAuthSession` signatures**

Edit `fasolt.ios/Fasolt/Services/AuthService.swift`. Find `func signIn(serverURL: String, providerHint: String? = nil)` at line 58 and rename the argument pass-through:

```swift
    func signIn(serverURL: String, providerHint: String? = nil, screenHint: String? = nil) async {
        isLoading = true
        errorMessage = nil

        let previousServerURL = keychain.retrieve("fasolt.serverURL")
        keychain.save(serverURL, forKey: "fasolt.serverURL")

        do {
            authLogger.info("Starting sign-in to \(serverURL)")
            let clientId = Self.firstPartyClientId

            let codeVerifier = Self.generateCodeVerifier()
            let codeChallenge = Self.generateCodeChallenge(from: codeVerifier)

            let authCode = try await openAuthSession(
                serverURL: serverURL,
                clientId: clientId,
                codeChallenge: codeChallenge,
                providerHint: providerHint,
                screenHint: screenHint
            )

            try await exchangeCode(authCode, clientId: clientId, codeVerifier: codeVerifier)

            authLogger.info("Sign-in complete")
            isAuthenticated = true
        } catch let error as ASWebAuthenticationSessionError where error.code == .canceledLogin {
            authLogger.info("User cancelled sign-in")
            if let previousServerURL {
                keychain.save(previousServerURL, forKey: "fasolt.serverURL")
            } else {
                keychain.delete("fasolt.serverURL")
            }
            errorMessage = nil
        } catch {
            authLogger.error("Sign-in failed: \(error)")
            if let previousServerURL {
                keychain.save(previousServerURL, forKey: "fasolt.serverURL")
            } else {
                keychain.delete("fasolt.serverURL")
            }
            errorMessage = "Could not connect. Check your server URL and try again."
        }

        isLoading = false
    }
```

Edit `openAuthSession` at line 234. Add `screenHint` parameter and thread it into the URL query:

```swift
    private func openAuthSession(
        serverURL: String,
        clientId: String,
        codeChallenge: String,
        providerHint: String? = nil,
        screenHint: String? = nil
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
        if let screenHint {
            queryItems.append(URLQueryItem(name: "screen_hint", value: screenHint))
        }
        components.queryItems = queryItems
        // ... rest of the method unchanged
```

Leave the rest of the method body (callback handling, etc.) as it was.

- [ ] **Step 11.2: Remove `AuthService.register` and its helpers**

The old `register(email:password:serverURL:)` method and its support (`registrationSuccess`, `lastRegisteredEmail`, `registrationErrorMessage`, `restoreServerURL` if only used there) is no longer needed.

Delete from `AuthService.swift`:

- Lines 114-118 (the `registrationSuccess` / `lastRegisteredEmail` properties and comments)
- Lines 120-147 (the entire `register(...)` method)
- Lines 184-215 (the `registrationErrorMessage` static helper)
- Lines 217-223 (the `restoreServerURL` helper) — **but first search for other callers**

To check callers:

Run: `grep -rn "restoreServerURL" fasolt.ios/Fasolt/`
Expected: only references inside `AuthService.swift` itself. If there are no other callers, delete it.

Also remove any imports that are now unused (e.g., `RegisterRequest` if only used in `register`).

- [ ] **Step 11.3: Build to verify**

Run: `cd fasolt.ios && xcodebuild -scheme Fasolt -destination "platform=iOS Simulator,name=iPhone 17" build -quiet 2>&1 | tail -20`
Expected: Build succeeds. The build will fail if `OnboardingView.swift` or other files still reference the deleted methods — that's expected, Task 12 fixes it.

If the build fails only due to `OnboardingView.swift` / `RegisterView.swift` references, that's fine — proceed to the next task. If it fails for any other reason (e.g., `AuthService.swift` internal inconsistency), fix it here.

- [ ] **Step 11.4: Commit**

```bash
git add fasolt.ios/Fasolt/Services/AuthService.swift
git commit -m "refactor(ios): AuthService.signIn gains screenHint parameter, register() removed"
```

---

## Task 12: iOS — restructure `OnboardingView` and delete native register surface

**Context:** New layout: Apple button, GitHub button, "or" divider, "Continue with email" button, self-host link. All native register views, view models, and tests are deleted.

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Onboarding/OnboardingView.swift`
- Delete: `fasolt.ios/Fasolt/Views/Onboarding/RegisterView.swift`
- Delete: `fasolt.ios/Fasolt/Views/Onboarding/VerifyEmailView.swift`
- Delete: `fasolt.ios/Fasolt/ViewModels/RegisterViewModel.swift`
- Delete: `fasolt.ios/Fasolt/Utilities/PasswordRules.swift`
- Delete: `fasolt.ios/Fasolt/Views/Shared/SafariView.swift` (conditional — see Step 12.2)
- Delete: `fasolt.ios/FasoltTests/RegisterViewModelTests.swift`
- Delete: `fasolt.ios/FasoltTests/PasswordRulesTests.swift`
- Modify: `fasolt.ios/Fasolt.xcodeproj/project.pbxproj` (automatically when files are removed in Xcode or by the script step)

- [ ] **Step 12.1: Rewrite `OnboardingView.swift`**

Replace the entire contents of `fasolt.ios/Fasolt/Views/Onboarding/OnboardingView.swift`:

```swift
import SwiftUI
import AuthenticationServices

struct OnboardingView: View {
    @Environment(AuthService.self) private var authService
    @Environment(FeatureFlagsService.self) private var featureFlags
    @State private var showServerField = false
    @State private var serverURL = AuthService.defaultServerURL
    private static let selfHostDefault = "http://localhost:8080"

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 28) {
                    Spacer().frame(height: 40)

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

                    // SSO providers — always first-class
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

                    // Email — single button that opens the web popup
                    Button {
                        Task {
                            await authService.signIn(serverURL: serverURL, screenHint: "signup")
                        }
                    } label: {
                        if authService.isLoading {
                            ProgressView()
                                .frame(maxWidth: .infinity)
                                .frame(height: 22)
                        } else {
                            Text("Continue with email")
                                .frame(maxWidth: .infinity)
                        }
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                    .disabled(authService.isLoading || serverURL.isEmpty)
                    .padding(.horizontal)

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

                    Spacer().frame(height: 32)
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

- [ ] **Step 12.2: Check if `SafariView` has other consumers before deleting**

Run: `grep -rn "SafariView" fasolt.ios/Fasolt/ --include="*.swift"`
Expected: If the only remaining references are the definition in `SafariView.swift` itself, it's safe to delete. If `Views/Settings/` or any other view imports it, leave it in place.

- [ ] **Step 12.3: Delete the native register files**

Run:

```bash
rm fasolt.ios/Fasolt/Views/Onboarding/RegisterView.swift
rm fasolt.ios/Fasolt/Views/Onboarding/VerifyEmailView.swift
rm fasolt.ios/Fasolt/ViewModels/RegisterViewModel.swift
rm fasolt.ios/Fasolt/Utilities/PasswordRules.swift
rm fasolt.ios/FasoltTests/RegisterViewModelTests.swift
rm fasolt.ios/FasoltTests/PasswordRulesTests.swift
```

If Step 12.2 confirmed no other consumers of `SafariView`:

```bash
rm fasolt.ios/Fasolt/Views/Shared/SafariView.swift
```

- [ ] **Step 12.4: Update `project.pbxproj` references**

Xcode tracks file references in `project.pbxproj`. When files are deleted from disk without being removed via Xcode, the project will warn about missing files on the next build. Update the project file:

Run: `cd fasolt.ios && xcodebuild -scheme Fasolt -destination "platform=iOS Simulator,name=iPhone 17" build -quiet 2>&1 | grep -E "error|warning" | head -20`
Expected: The output will list which files are missing. If `project.pbxproj` still references the deleted files, they need to be removed manually.

To remove manually, edit `fasolt.ios/Fasolt.xcodeproj/project.pbxproj` and:

1. Search for each deleted filename (`RegisterView.swift`, etc.)
2. Remove the matching lines in:
   - `PBXBuildFile` section (lines like `XXXXXXXX /* RegisterView.swift in Sources */ = {...};`)
   - `PBXFileReference` section (lines like `YYYYYYYY /* RegisterView.swift */ = {...};`)
   - `PBXGroup` children lists (references by the `YYYYYYYY` GUIDs)
   - `PBXSourcesBuildPhase` files list (references by the `XXXXXXXX` GUIDs)

A safer alternative: open `Fasolt.xcodeproj` in Xcode, right-click the missing (red) files in the file navigator, and choose "Delete" → "Remove Reference". Xcode rewrites `project.pbxproj` cleanly.

- [ ] **Step 12.5: Build iOS**

Run: `cd fasolt.ios && xcodebuild -scheme Fasolt -destination "platform=iOS Simulator,name=iPhone 17" build -quiet 2>&1 | tail -20`
Expected: `BUILD SUCCEEDED`.

If the build fails due to a leftover reference (e.g., `FasoltApp.swift` or another view still imports a deleted file), fix the import by removing the reference and ensuring no remaining code depends on the deleted types.

- [ ] **Step 12.6: Run iOS tests**

Run: `cd fasolt.ios && xcodebuild test -scheme Fasolt -destination "platform=iOS Simulator,name=iPhone 17" -quiet 2>&1 | tail -20`
Expected: All remaining tests pass. The old `PasswordRulesTests` and `RegisterViewModelTests` are gone; ~33 tests should remain (the original 39 minus the 6 deleted rule/view-model tests).

- [ ] **Step 12.7: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Onboarding/OnboardingView.swift \
        fasolt.ios/Fasolt.xcodeproj/project.pbxproj
git add -u fasolt.ios/  # captures deletions
git commit -m "refactor(ios): OnboardingView uses web popup for email, delete native register surface

RegisterView, VerifyEmailView, RegisterViewModel, PasswordRules and their
tests are removed. 'Continue with email' opens ASWebAuthenticationSession
pointed at /oauth/authorize?screen_hint=signup so the server renders the
register form and OTP verification."
```

---

## Task 13: Web — delete Vue register / confirm-email / verify-email views

**Context:** The Vue register flow is replaced by the server-rendered `/oauth/register` page. The legacy URL-token confirm route is replaced by OTP. The "verify email" gate (authenticated but unverified) is unreachable after OTP because users can't be authenticated without having verified.

**Files:**
- Delete: `fasolt.client/src/views/RegisterView.vue`
- Delete: `fasolt.client/src/views/ConfirmEmailView.vue`
- Delete: `fasolt.client/src/views/EmailVerificationView.vue`
- Modify: `fasolt.client/src/router/index.ts`
- Modify: `fasolt.Server/Program.cs` (add 301 redirects before SPA fallback)

- [ ] **Step 13.1: Confirm no other callers before deleting**

Run: `grep -rn "RegisterView\.vue\|ConfirmEmailView\.vue\|EmailVerificationView\.vue" fasolt.client/src/ --include="*.ts" --include="*.vue"`
Expected: Only references should be in the router. If any component imports these views directly, investigate before deleting.

Also search for `useAuthStore().register` or `api.register` usages to find code that calls `/api/account/register`:

Run: `grep -rn "account/register\|authStore.register" fasolt.client/src/`
Expected: Calls from `RegisterView.vue` only. If the auth store has a `register` method used elsewhere, we'll need to leave it (but nothing else should call it).

- [ ] **Step 13.2: Delete the Vue files**

```bash
rm fasolt.client/src/views/RegisterView.vue
rm fasolt.client/src/views/ConfirmEmailView.vue
rm fasolt.client/src/views/EmailVerificationView.vue
```

- [ ] **Step 13.3: Remove routes from Vue router**

Edit `fasolt.client/src/router/index.ts`. Delete these route entries (around lines 22-50):

- The `/register` route (lines 22-27)
- The `/verify-email` route (lines 40-44)
- The `/confirm-email` route (lines 46-50)

The `beforeEach` guard at line 131-134 references `/verify-email`:

```typescript
  // Redirect unverified users to verification gate
  if (auth.isAuthenticated && !auth.isEmailConfirmed && !isPublic && to.meta.skipVerificationCheck !== true) {
    return { name: 'verify-email' }
  }

  // Don't let verified users visit the verification page
  if (to.name === 'verify-email' && auth.isEmailConfirmed) {
    return { name: 'study' }
  }
```

With OTP, there's no "authenticated but unverified" state — verification happens before sign-in. Delete these two blocks from the guard.

- [ ] **Step 13.4: Add server-side 301 redirects**

Edit `fasolt.Server/Program.cs`. Add BEFORE `app.MapFallbackToFile("index.html");` (around line 566):

```csharp
// Legacy auth routes — 301 to the new server-rendered OAuth pages
app.MapGet("/register", [Microsoft.AspNetCore.Authorization.AllowAnonymous] (HttpContext ctx) =>
{
    var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
    return Results.Redirect($"/oauth/register?returnUrl={Uri.EscapeDataString(returnUrl)}", permanent: true);
});
app.MapGet("/verify-email", [Microsoft.AspNetCore.Authorization.AllowAnonymous] (HttpContext ctx) =>
{
    var email = ctx.Request.Query["email"].FirstOrDefault() ?? "";
    var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
    return Results.Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}", permanent: true);
});
app.MapGet("/confirm-email", [Microsoft.AspNetCore.Authorization.AllowAnonymous] (HttpContext ctx) =>
{
    // Legacy URL-token confirmation is dead; redirect to verify page with error
    return Results.Redirect("/oauth/verify-email?error=This+link+has+expired.+Please+request+a+new+code.", permanent: false);
});
```

The server endpoint routing runs before the SPA fallback, so these URLs never reach Vue.

- [ ] **Step 13.5: Build web client**

Run: `cd fasolt.client && npm run build 2>&1 | tail -20`
Expected: Build succeeds. If any references to the deleted views remain in the router or elsewhere, TypeScript/Vue will catch them.

- [ ] **Step 13.6: Build server**

Run: `dotnet build fasolt.Server`
Expected: Build succeeds.

- [ ] **Step 13.7: Commit**

```bash
git add fasolt.client/src/router/index.ts \
        fasolt.Server/Program.cs
git add -u fasolt.client/  # captures deletions
git commit -m "refactor(web): delete Vue register/confirm views, 301 /register to /oauth/register

RegisterView.vue, ConfirmEmailView.vue, and EmailVerificationView.vue
are replaced by the server-rendered /oauth/register and /oauth/verify-email
pages that both the iOS popup and web users share. Legacy URLs 301
redirect so any stale bookmarks still work."
```

---

## Task 14: Server — delete legacy `/api/account/register` and related endpoints

**Context:** With the web Vue register view deleted and iOS no longer calling `/api/account/register`, the JSON register endpoint has no remaining callers. Same for `/api/account/confirm-email` (the URL-token one).

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`
- Modify: `fasolt.Tests/` — any tests targeting these endpoints

- [ ] **Step 14.1: Confirm no other callers**

Run: `grep -rn "api/account/register\|api/account/confirm-email" fasolt.Server/ fasolt.client/ fasolt.ios/ fasolt.Tests/ --include="*.cs" --include="*.swift" --include="*.ts" --include="*.vue"`
Expected: Only references should be the endpoint definitions themselves and any tests. If a real caller exists, investigate before deleting.

- [ ] **Step 14.2: Delete the endpoint definitions**

Edit `fasolt.Server/Api/Endpoints/AccountEndpoints.cs`:

1. Remove line 18: `group.MapPost("/register", Register).RequireRateLimiting("auth");`
2. Remove line 28: `group.MapPost("/confirm-email", ConfirmEmail).RequireRateLimiting("auth");`
3. Delete the `Register` method (lines 35-54)
4. Delete the `ConfirmEmail` method (around lines 225-260 — use the full file read earlier as reference)

Also delete the `ResendVerification` endpoint (line 27) and its method (around lines 207-223) since it's tied to the old URL-token flow. If any other authenticated caller uses it, leave it and investigate. The OTP resend is handled by `/oauth/verify-email/resend` for unauthenticated users during the register flow.

**Search first:**

Run: `grep -rn "resend-verification" fasolt.client/ fasolt.Tests/`
Expected: If only test references (and a Vue `SettingsView` calling it for logged-in users who want to re-verify), keep it. Otherwise delete. **Conservative default: leave `ResendVerification` in place; its authenticated-user context means it's an orthogonal path.**

- [ ] **Step 14.3: Delete associated DTOs**

Check `fasolt.Server/Application/Dtos/` for `RegisterRequest` and `ConfirmEmailRequest`:

Run: `grep -rn "class RegisterRequest\|record RegisterRequest\|class ConfirmEmailRequest\|record ConfirmEmailRequest" fasolt.Server/Application/Dtos/`
Expected: One match for each.

Run: `grep -rn "RegisterRequest\|ConfirmEmailRequest" fasolt.Server/ fasolt.Tests/ --include="*.cs" | grep -v "Dtos/"`
Expected: No other references after the endpoint deletions in Step 14.2.

If no other references, delete `RegisterRequest` and `ConfirmEmailRequest` from the DTO file. If the DTO file becomes empty, delete it.

- [ ] **Step 14.4: Delete / fix affected tests**

Run: `grep -rn "api/account/register\|ConfirmEmailRequest\|RegisterRequest" fasolt.Tests/ --include="*.cs"`
Expected: Any test that targeted the old register endpoint.

For each affected test file:

- If the test is solely testing the deleted endpoint, delete the test file
- If the test uses `/api/account/register` as a convenience helper to seed a user, rewrite it to use `UserManager.CreateAsync` directly (see `AppleAuthServiceTests.cs` for the pattern)

Common candidates to check (verify via grep):

- `fasolt.Tests/AccountEndpointsTests.cs` (if it exists)
- Any integration test that calls `POST /api/account/register` to seed data

- [ ] **Step 14.5: Run all backend tests**

Run: `dotnet test fasolt.Tests`
Expected: All tests pass. Expected count: ~200 (original 188 + new ~12 from OTP/register/verify tests, minus any tests deleted in 14.4).

- [ ] **Step 14.6: Commit**

```bash
git add fasolt.Server/Api/Endpoints/AccountEndpoints.cs \
        fasolt.Server/Application/Dtos/ \
        fasolt.Tests/
git commit -m "refactor(server): delete legacy /api/account/register and /api/account/confirm-email endpoints

The Vue register view is gone and iOS no longer calls these endpoints.
The OTP-based /oauth/register and /oauth/verify-email are the only
remaining register paths. ResendVerification (authenticated) is kept
for the orthogonal 'logged-in user wants to re-verify' path, if it has
any live callers."
```

---

## Task 15: Playwright end-to-end test

**Context:** Per `CLAUDE.md`: *"Always run Playwright browser tests after implementing a feature."* This task drives the full register → OTP → dashboard flow in a real browser via the Playwright MCP.

**Files:**
- No code changes — this is a manual verification step using the Playwright MCP tools

- [ ] **Step 15.1: Start the full stack**

Run: `./dev.sh` in a separate terminal, OR manually:

```bash
docker compose up -d
dotnet run --project fasolt.Server &
cd fasolt.client && npm run dev &
```

Wait for the server to log `Now listening on: http://localhost:8080` and the Vite dev server to log `ready in XXX ms`.

- [ ] **Step 15.2: Navigate to the register page via the Playwright MCP**

Use `mcp__plugin_playwright_playwright__browser_navigate` with url `http://localhost:5173/register`.

Expected: The 301 redirect from Step 13.4 fires and the browser lands on `http://localhost:8080/oauth/register?returnUrl=/`.

Take a screenshot via `mcp__plugin_playwright_playwright__browser_take_screenshot` to verify the page renders.

- [ ] **Step 15.3: Fill the register form**

Use `mcp__plugin_playwright_playwright__browser_fill_form` with:

- `email`: `playwright-test-{timestamp}@fasolt.test`
- `password`: `Abcdefg1`
- `confirmPassword`: `Abcdefg1`
- `tosAccepted`: `true`

Then click the submit button via `mcp__plugin_playwright_playwright__browser_click`.

Expected: Navigation to `/oauth/verify-email?email=...&returnUrl=/`.

- [ ] **Step 15.4: Read the OTP from the dev email sender log**

The `DevEmailSender` logs the code to the server's stdout as:
`[DEV EMAIL] Verification code for {email}: {code}`

Capture the server log output (via the terminal running `dotnet run`) and extract the 6-digit code. If running the server in a background process, redirect stdout to a file in Step 15.1 and read it here:

```bash
tail -50 /tmp/fasolt-server.log | grep "Verification code" | tail -1
```

Extract the 6-digit code from the output.

- [ ] **Step 15.5: Type the code and verify**

Use `mcp__plugin_playwright_playwright__browser_type` targeting the `code` input with the captured code, then click Verify.

Expected: Navigation to `/` (or whatever `returnUrl` was passed), and the user is signed in. Dashboard loads.

- [ ] **Step 15.6: Verify the database state**

Run:

```bash
docker compose exec postgres psql -U fasolt -d fasolt -c "SELECT \"Email\", \"EmailConfirmed\" FROM \"AspNetUsers\" WHERE \"Email\" LIKE 'playwright-test-%' ORDER BY \"Id\" DESC LIMIT 1;"
```

Expected: One row with `EmailConfirmed = t`.

Run:

```bash
docker compose exec postgres psql -U fasolt -d fasolt -c "SELECT COUNT(*) FROM \"EmailVerificationCodes\";"
```

Expected: Count is 0 (the row was deleted on successful verify) — or whatever it was before this test minus 0 + 0.

- [ ] **Step 15.7: Commit the plan-completion marker**

No code changes in this task. If there are any leftover artifacts (e.g., `/tmp/fasolt-server.log`), clean them up.

```bash
git status
```

Expected: clean working tree (all previous tasks committed).

---

## Task 16: Manual iOS device verification (optional but strongly recommended)

**Context:** The full iOS popup flow can only be verified on a real device or simulator with network access.

**Files:** None — this is a manual test.

- [ ] **Step 16.1: Run the app on a simulator**

Run: `cd fasolt.ios && xcodebuild -scheme Fasolt -destination "platform=iOS Simulator,name=iPhone 17" build`
Then open `Fasolt.xcodeproj` in Xcode and Cmd+R.

- [ ] **Step 16.2: Register a new account via the email flow**

In the simulator:

1. Tap "Self-hosting? Change server" and change to `http://localhost:8080` (simulator can reach localhost)
2. Tap "Continue with email"
3. Web popup opens with the `/oauth/register` form
4. Fill email, password (`Abcdefg1`), confirm, check ToS, submit
5. Popup navigates to `/oauth/verify-email`
6. Read the code from the server log (`tail -f /tmp/fasolt-server.log` or the `dotnet run` terminal)
7. Type the code, tap Verify
8. Popup closes, app lands on dashboard signed in

Expected: No errors, tokens in Keychain (verify by force-quitting and re-launching — should stay signed in).

- [ ] **Step 16.3: Test "Continue with GitHub" (if configured)**

Only if `GITHUB_CLIENT_ID` is set in `.env`. Tap "Continue with GitHub" — popup opens directly to GitHub's OAuth page (not `/oauth/login`).

- [ ] **Step 16.4: Test "Sign in with Apple" (real device only)**

Sign in with Apple can't be meaningfully tested in the simulator for the first time. Deploy to a real device via Xcode → Run, or defer this to TestFlight.

- [ ] **Step 16.5: No commit — this is verification only**

---

## Post-plan: self-review summary

**Tasks 1-3:** Fix the three Apple-sign-in bugs from PR #101 review. Each has a failing regression test, a fix, and green tests.

**Tasks 4-6:** Build the OTP backend — entity, migration, service, email sender extension.

**Tasks 7-10:** Build the server-rendered HTML auth surface — `screen_hint=signup` support, `/oauth/register` (GET + POST), `/oauth/verify-email` (GET + POST + resend), and a "Create account" link on `/oauth/login`.

**Tasks 11-12:** Update iOS — `AuthService.signIn` learns `screenHint`, delete the entire native register stack, rewrite `OnboardingView` to the new layout.

**Tasks 13-14:** Delete legacy web Vue views and the JSON register/confirm endpoints.

**Task 15:** Playwright end-to-end verification.

**Task 16:** Manual iOS device verification (optional).

## Final commit + PR

After all tasks pass:

```bash
git log --oneline main..HEAD
```

Expected: ~15-20 commits covering the tasks above.

Create the PR on this branch:

```bash
gh pr view 101
```

If PR #101 is still open, decide with the user whether to:

- **Close #101 and open a new PR** on a fresh branch (cleanest history, but loses the review thread)
- **Force-push to `ios-onboarding-restructure`** to replace #101's commits (keeps the PR number and review thread, but rewrites history — user must approve)
- **Add these commits to #101** as additional work (bigger diff but additive — no history rewrite)

The user's stated preference is direct branch checkout (no worktrees). Decision defers to user.

---

## Running totals

- **New files (server):** `EmailVerificationCode.cs`, `IEmailVerificationCodeService.cs`, `EmailVerificationCodeService.cs`, `IOtpEmailSender.cs`, one migration
- **New files (tests):** `EmailVerificationCodeServiceTests.cs`, `OAuthRegisterEndpointTests.cs`, `OAuthVerifyEmailEndpointTests.cs`, `OAuthAuthorizeScreenHintTests.cs`, `AppleJwksCacheDiTests.cs`
- **Modified files (server):** `AppleAuthService.cs`, `AppleJwksCache.cs`, `Program.cs`, `AppDbContext.cs`, `OAuthEndpoints.cs`, `AccountEndpoints.cs`, `PlunkEmailSender.cs`, `DevEmailSender.cs`, `appsettings.Development.json`, `.env.example`
- **Modified files (iOS):** `AuthService.swift`, `OnboardingView.swift`, `project.pbxproj`
- **Modified files (web):** `router/index.ts`
- **Deleted files (iOS):** `RegisterView.swift`, `VerifyEmailView.swift`, `RegisterViewModel.swift`, `PasswordRules.swift`, `SafariView.swift` (conditional), `RegisterViewModelTests.swift`, `PasswordRulesTests.swift`
- **Deleted files (web):** `RegisterView.vue`, `ConfirmEmailView.vue`, `EmailVerificationView.vue`

Approximate net LOC: **-800 to -1000** (deletions dominate).

Expected final test count: **~200 backend tests** (188 baseline + ~12 new), **~33 iOS tests** (39 baseline - 6 deleted).
