# Auth Security Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 7 security vulnerabilities (SEC-1 through SEC-7) in the OAuth and auth system before production deployment.

**Architecture:** Backend-only changes for SEC-1/2/4/5/6/7. SEC-3 (consent screen) requires a new DB entity, EF migration, backend API endpoints, and a Vue SPA consent page. All fixes are independent and can be implemented in any order, except SEC-3 depends on its own entity/migration being created first.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, OpenIddict, EF Core + Npgsql, Vue 3 + TypeScript, shadcn-vue

**Spec:** `docs/superpowers/specs/2026-03-24-auth-security-hardening-design.md`

---

### Task 1: SEC-1 — Fix Open Redirect in OAuth Login

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs:218-234`

- [ ] **Step 1: Add IsLocalUrl helper and apply to POST handler**

Add the helper method inside `OAuthEndpoints` and use it in the POST `/oauth/login` handler:

```csharp
// Add inside OAuthEndpoints class, after MapOAuthEndpoints method
private static bool IsLocalUrl(string url) =>
    !string.IsNullOrEmpty(url) &&
    url.StartsWith('/') &&
    !url.StartsWith("//") &&
    !url.StartsWith("/\\");
```

In the POST `/oauth/login` handler (line 229-230), change:
```csharp
if (result.Succeeded)
    return Results.Redirect(returnUrl);
```
to:
```csharp
if (result.Succeeded)
    return Results.Redirect(IsLocalUrl(returnUrl) ? returnUrl : "/");
```

- [ ] **Step 2: Apply validation to GET handler's returnUrl**

In the GET `/oauth/login` handler (line 159), change:
```csharp
var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
```
to:
```csharp
var rawReturnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
var returnUrl = IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";
```

- [ ] **Step 3: Verify the fix manually**

Run: `dotnet build fasolt.Server`
Expected: Build succeeds.

Start the stack and test:
- Navigate to `/oauth/login?returnUrl=https://evil.com` — the hidden form field should show `/`
- Submit login — should redirect to `/`, not evil.com

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs
git commit -m "fix(auth): validate returnUrl in OAuth login to prevent open redirect (SEC-1)"
```

---

### Task 2: SEC-7 — Production Certificate Enforcement

**Files:**
- Modify: `fasolt.Server/Program.cs:54-66`

- [ ] **Step 1: Add environment check to cert loading**

Replace the existing if/else block (lines 57-66):

```csharp
if (encryptionCertPath is not null && signingCertPath is not null)
{
    options.AddEncryptionCertificate(System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(encryptionCertPath, null))
           .AddSigningCertificate(System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(signingCertPath, null));
}
else
{
    options.AddDevelopmentEncryptionCertificate()
           .AddDevelopmentSigningCertificate();
}
```

with:

```csharp
if (encryptionCertPath is not null && signingCertPath is not null)
{
    options.AddEncryptionCertificate(System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(encryptionCertPath, null))
           .AddSigningCertificate(System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(signingCertPath, null));
}
else if (builder.Environment.IsDevelopment())
{
    options.AddDevelopmentEncryptionCertificate()
           .AddDevelopmentSigningCertificate();
}
else
{
    throw new InvalidOperationException(
        "OpenIddict:EncryptionCertificatePath and OpenIddict:SigningCertificatePath " +
        "must be configured in non-development environments.");
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Build succeeds. Dev environment still works (uses dev certs).

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Program.cs
git commit -m "fix(auth): throw on startup if OpenIddict certs missing in non-dev (SEC-7)"
```

---

### Task 3: SEC-4 — Fix Claim Destination Oversharing (Token Endpoint Only)

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs:141-142`

Note: The authorize endpoint's `SetDestinations` will be fixed as part of Task 8 (SEC-3), which rewrites the entire authorize endpoint. This task only fixes the **token endpoint**.

- [ ] **Step 1: Replace destination logic in token endpoint**

Replace lines 141-142:
```csharp
identity.SetDestinations(static claim =>
    [Destinations.AccessToken, Destinations.IdentityToken]);
```

with:
```csharp
identity.SetDestinations(static claim => claim.Type switch
{
    ClaimTypes.NameIdentifier => [Destinations.AccessToken],
    Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
    Claims.Name => [Destinations.AccessToken, Destinations.IdentityToken],
    _ => [Destinations.AccessToken],
});
```

- [ ] **Step 2: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs
git commit -m "fix(auth): restrict NameIdentifier claim to access token only (SEC-4)"
```

---

### Task 4: SEC-5 — Redirect URI Pattern Validation

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs:30-74`
- Modify: `fasolt.Server/Program.cs` (add config binding)
- Modify: `fasolt.Server/appsettings.json` (add default patterns)

- [ ] **Step 1: Add default allowed patterns to appsettings.json**

Add to `fasolt.Server/appsettings.json`:
```json
{
  "OAuth": {
    "AllowedRedirectPatterns": ["fasolt://", "http://localhost", "http://127.0.0.1"]
  }
}
```

- [ ] **Step 2: Add validation helper and apply in register endpoint**

Add a helper method inside `OAuthEndpoints`:

```csharp
private static readonly string[] DefaultRedirectPatterns = ["fasolt://", "http://localhost", "http://127.0.0.1"];

private static bool IsAllowedRedirectUri(string uri, string[] allowedPatterns) =>
    allowedPatterns.Any(pattern =>
        uri.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) &&
        (uri.Length == pattern.Length ||
         uri[pattern.Length] is '/' or ':' or '?'));
```

In the register endpoint handler, add `IConfiguration configuration` as a parameter and validate redirect URIs. After the existing `RedirectUris` null check (line 38-39), add:

```csharp
var allowedPatterns = configuration.GetSection("OAuth:AllowedRedirectPatterns").Get<string[]>()
    ?? DefaultRedirectPatterns;

foreach (var uri in request.RedirectUris)
{
    if (!IsAllowedRedirectUri(uri, allowedPatterns))
        return Results.BadRequest(new
        {
            error = "invalid_client_metadata",
            error_description = $"redirect_uri '{uri}' is not allowed. Must match: {string.Join(", ", allowedPatterns)}"
        });
}
```

Remove the existing `Uri.TryCreate` loop (lines 51-55) and replace with:

```csharp
foreach (var uri in request.RedirectUris)
{
    if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        descriptor.RedirectUris.Add(parsed);
}
```

(Keep the URI parsing loop but it now runs after validation.)

- [ ] **Step 3: Verify build and test**

Run: `dotnet build fasolt.Server`
Expected: Build succeeds.

Test via curl:
```bash
curl -X POST http://localhost:8080/oauth/register \
  -H 'Content-Type: application/json' \
  -d '{"client_name":"evil","redirect_uris":["https://evil.com/callback"]}'
```
Expected: 400 with "redirect_uri ... is not allowed"

```bash
curl -X POST http://localhost:8080/oauth/register \
  -H 'Content-Type: application/json' \
  -d '{"client_name":"test","redirect_uris":["http://localhost:8080/callback"]}'
```
Expected: 200 with client_id

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs fasolt.Server/appsettings.json
git commit -m "fix(auth): validate redirect URIs against configurable allowlist (SEC-5)"
```

---

### Task 5: SEC-2 — Rate Limiting on OAuth and Account Endpoints

**Files:**
- Modify: `fasolt.Server/Program.cs:144-153,283-284`
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs` (add rate limiting to endpoint registrations)
- Modify: `fasolt.Server/Api/Endpoints/AccountEndpoints.cs:20-21`

- [ ] **Step 1: Replace global rate limiter with per-IP policies**

In `Program.cs`, replace lines 144-153:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
});
```

with:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    options.AddPolicy("auth-strict", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));
});
```

Ensure `using System.Threading.RateLimiting;` is already present (it is at line 1).

- [ ] **Step 2: Apply rate limiting to OAuth endpoints**

In `OAuthEndpoints.cs`, add `.RequireRateLimiting()` to the endpoint registrations:

- `app.MapPost("/oauth/register", ...)` — add `.RequireRateLimiting("auth-strict")` after the lambda
- `app.MapPost("/oauth/login", ...)` — add `.RequireRateLimiting("auth")`
- `app.MapPost("/oauth/token", ...)` — add `.RequireRateLimiting("auth")`

- [ ] **Step 3: Apply rate limiting to account endpoints**

In `AccountEndpoints.cs`, change the forgot-password and reset-password registrations to apply rate limiting individually. Change lines 20-21:

```csharp
group.MapPost("/forgot-password", ForgotPassword);
group.MapPost("/reset-password", ResetPassword);
```

to:

```csharp
group.MapPost("/forgot-password", ForgotPassword).RequireRateLimiting("auth");
group.MapPost("/reset-password", ResetPassword).RequireRateLimiting("auth");
```

- [ ] **Step 4: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Program.cs fasolt.Server/Api/Endpoints/OAuthEndpoints.cs fasolt.Server/Api/Endpoints/AccountEndpoints.cs
git commit -m "fix(auth): add per-IP rate limiting to OAuth and account endpoints (SEC-2)"
```

---

### Task 6: SEC-6 — Configurable Forwarded Headers Trust

**Files:**
- Modify: `fasolt.Server/Program.cs:196-205`
- Modify: `fasolt.Server/appsettings.json` (document config options)

- [ ] **Step 1: Replace hardcoded trust-all with configurable logic**

Replace lines 196-205 in `Program.cs`:

```csharp
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost,
};
// Trust all proxies in Docker/Cloudflare environments
forwardedHeadersOptions.KnownProxies.Clear();
forwardedHeadersOptions.KnownIPNetworks.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);
```

with:

```csharp
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost,
};

var trustAllProxies = builder.Configuration.GetValue("ReverseProxy:TrustAllProxies",
    builder.Environment.IsDevelopment());

if (trustAllProxies)
{
    forwardedHeadersOptions.KnownProxies.Clear();
    forwardedHeadersOptions.KnownIPNetworks.Clear();
    if (!builder.Environment.IsDevelopment())
        app.Logger.LogWarning("ReverseProxy:TrustAllProxies is true — trusting all proxy headers. Configure ReverseProxy:KnownNetworks for production.");
}
else
{
    var networks = builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>();
    if (networks is not null)
    {
        foreach (var cidr in networks)
        {
            var parts = cidr.Split('/');
            if (parts.Length == 2 && System.Net.IPAddress.TryParse(parts[0], out var address) && int.TryParse(parts[1], out var prefixLength))
                forwardedHeadersOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(address, prefixLength));
        }
    }
}

app.UseForwardedHeaders(forwardedHeadersOptions);
```

- [ ] **Step 2: Add development config for TrustAllProxies**

In `fasolt.Server/appsettings.Development.json`, add:
```json
"ReverseProxy": {
  "TrustAllProxies": true
}
```

(This makes dev behavior explicit rather than relying on the `IsDevelopment()` default.)

- [ ] **Step 3: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Build succeeds. Dev still works with trust-all.

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Program.cs fasolt.Server/appsettings.Development.json
git commit -m "fix(auth): make forwarded headers proxy trust configurable (SEC-6)"
```

---

### Task 7: SEC-3 — ConsentGrant Entity and Migration

**Files:**
- Create: `fasolt.Server/Domain/Entities/ConsentGrant.cs`
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs`
- Create: EF migration (auto-generated)

- [ ] **Step 1: Create ConsentGrant entity**

Create `fasolt.Server/Domain/Entities/ConsentGrant.cs`:

```csharp
namespace Fasolt.Server.Domain.Entities;

public class ConsentGrant
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public DateTimeOffset GrantedAt { get; set; }
}
```

- [ ] **Step 2: Add DbSet and configuration to AppDbContext**

In `AppDbContext.cs`, add the DbSet:

```csharp
public DbSet<ConsentGrant> ConsentGrants => Set<ConsentGrant>();
```

Add entity configuration inside `OnModelCreating`, after the `DeckCard` block:

```csharp
builder.Entity<ConsentGrant>(entity =>
{
    entity.HasKey(e => e.Id);
    entity.Property(e => e.ClientId).HasMaxLength(255).IsRequired();
    entity.HasIndex(e => new { e.UserId, e.ClientId }).IsUnique();
    entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 3: Generate EF migration**

Run:
```bash
dotnet ef migrations add AddConsentGrants --project fasolt.Server
```
Expected: Migration files created in `Infrastructure/Data/Migrations/`.

- [ ] **Step 4: Verify migration applies**

Run:
```bash
dotnet ef database update --project fasolt.Server
```
Expected: Migration applied, `ConsentGrants` table created.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Domain/Entities/ConsentGrant.cs fasolt.Server/Infrastructure/Data/AppDbContext.cs fasolt.Server/Infrastructure/Data/Migrations/
git commit -m "feat(auth): add ConsentGrant entity and migration (SEC-3)"
```

---

### Task 8: SEC-3 — Consent API Endpoints

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`

- [ ] **Step 1: Add consent-info endpoint**

Add after the existing `/oauth/login` POST handler, before the closing `}` of `MapOAuthEndpoints`:

```csharp
// Consent Info (GET)
app.MapGet("/api/oauth/consent-info", async (
    HttpContext context,
    IOpenIddictApplicationManager applicationManager,
    [FromQuery(Name = "client_id")] string clientId) =>
{
    var application = await applicationManager.FindByClientIdAsync(clientId);
    if (application is null)
        return Results.NotFound(new { error = "Client not found" });

    var displayName = await applicationManager.GetDisplayNameAsync(application);

    return Results.Ok(new
    {
        clientName = displayName ?? clientId,
        scopes = new[] { "offline_access" },
    });
}).RequireAuthorization();
```

Add these usings if not already present:
```csharp
using Microsoft.AspNetCore.Mvc;
```

- [ ] **Step 2: Add consent POST endpoint**

Add after the consent-info endpoint:

```csharp
// Consent Decision (POST)
app.MapPost("/api/oauth/consent", async (
    HttpContext context,
    ConsentDecisionRequest request,
    IOpenIddictApplicationManager applicationManager,
    IDataProtectionProvider dataProtection,
    AppDbContext db,
    ClaimsPrincipal principal) =>
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;

    var application = await applicationManager.FindByClientIdAsync(request.ClientId);
    if (application is null)
        return Results.NotFound(new { error = "Client not found" });

    // Validate that an active OAuth flow exists (cookie must be present)
    var encryptedQuery = context.Request.Cookies["oauth_authorize_query"];
    if (string.IsNullOrEmpty(encryptedQuery))
        return Results.BadRequest(new { error = "No active authorization flow" });

    // Decrypt and validate the stored query string
    var protector = dataProtection.CreateProtector("OAuthAuthorizeQuery");
    string authorizeQuery;
    try
    {
        authorizeQuery = protector.Unprotect(encryptedQuery);
    }
    catch
    {
        return Results.BadRequest(new { error = "Invalid or expired authorization flow" });
    }

    // Verify the client_id in the cookie matches the consent request
    var queryParams = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(authorizeQuery.TrimStart('?'));
    if (!queryParams.TryGetValue("client_id", out var cookieClientId) || cookieClientId != request.ClientId)
        return Results.BadRequest(new { error = "Client ID mismatch" });

    context.Response.Cookies.Delete("oauth_authorize_query");

    if (request.Approved)
    {
        // Store consent grant
        var existing = await db.ConsentGrants
            .FirstOrDefaultAsync(g => g.UserId == userId && g.ClientId == request.ClientId);
        if (existing is null)
        {
            db.ConsentGrants.Add(new ConsentGrant
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ClientId = request.ClientId,
                GrantedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        // Reconstruct authorize URL from the decrypted cookie
        var redirectUrl = $"/oauth/authorize{authorizeQuery}";
        return Results.Ok(new { redirectUrl });
    }
    else
    {
        // Get client's redirect URI to send error back
        var redirectUris = new List<string>();
        await foreach (var uri in applicationManager.GetRedirectUrisAsync(application))
            redirectUris.Add(uri);

        var clientRedirectUri = redirectUris.FirstOrDefault() ?? "/";
        var separator = clientRedirectUri.Contains('?') ? '&' : '?';
        return Results.Ok(new { redirectUrl = $"{clientRedirectUri}{separator}error=access_denied" });
    }
}).RequireAuthorization();
```

Add the request record at the bottom of the file, after `ClientRegistrationRequest`:

```csharp
record ConsentDecisionRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("clientId")] string ClientId,
    [property: System.Text.Json.Serialization.JsonPropertyName("approved")] bool Approved);
```

Add required usings at the top:
```csharp
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Infrastructure.Data;
```

- [ ] **Step 3: Modify authorize endpoint to check consent**

Replace the authorize endpoint (lines 77-106) with:

```csharp
app.MapGet("/oauth/authorize", async (HttpContext context, AppDbContext db, IDataProtectionProvider dataProtection) =>
{
    var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
    if (result?.Principal is null)
    {
        var returnUrl = context.Request.QueryString.Value;
        return Results.Redirect($"/oauth/login?returnUrl={Uri.EscapeDataString("/oauth/authorize" + returnUrl)}");
    }

    var user = result.Principal;
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var userName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue(ClaimTypes.Email) ?? "";

    // Check for existing consent grant
    var openIddictRequest = context.GetOpenIddictServerRequest();
    var clientId = openIddictRequest?.ClientId;

    if (clientId is not null)
    {
        var hasConsent = await db.ConsentGrants
            .AnyAsync(g => g.UserId == userId && g.ClientId == clientId);

        if (!hasConsent)
        {
            // Store the original query string in an encrypted cookie for reconstruction after consent
            var protector = dataProtection.CreateProtector("OAuthAuthorizeQuery");
            var encrypted = protector.Protect(context.Request.QueryString.Value ?? "");

            context.Response.Cookies.Append("oauth_authorize_query", encrypted, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = !context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
                MaxAge = TimeSpan.FromMinutes(10),
            });

            return Results.Redirect($"/oauth/consent?client_id={Uri.EscapeDataString(clientId)}");
        }
    }

    var identity = new ClaimsIdentity(
        authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        nameType: Claims.Name,
        roleType: Claims.Role);

    identity.SetClaim(Claims.Subject, userId);
    identity.SetClaim(ClaimTypes.NameIdentifier, userId);
    identity.SetClaim(Claims.Name, userName);
    identity.SetScopes(Scopes.OfflineAccess);

    identity.SetDestinations(static claim => claim.Type switch
    {
        ClaimTypes.NameIdentifier => [Destinations.AccessToken],
        Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Name => [Destinations.AccessToken, Destinations.IdentityToken],
        _ => [Destinations.AccessToken],
    });

    return Results.SignIn(new ClaimsPrincipal(identity),
        properties: null,
        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
});
```

Note: This also includes the SEC-4 claim destination fix for the authorize endpoint.

- [ ] **Step 4: Verify build**

Run: `dotnet build fasolt.Server`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs
git commit -m "feat(auth): add consent check and consent API endpoints (SEC-3)"
```

---

### Task 9: SEC-3 — Vue Consent Page

**Files:**
- Create: `fasolt.client/src/views/OAuthConsentView.vue`
- Modify: `fasolt.client/src/router/index.ts`

- [ ] **Step 1: Add consent route**

In `fasolt.client/src/router/index.ts`, add after the `/reset-password` route (line 32):

```typescript
{
  path: '/oauth/consent',
  name: 'oauth-consent',
  component: () => import('@/views/OAuthConsentView.vue'),
  meta: { public: true },
},
```

- [ ] **Step 2: Create OAuthConsentView.vue**

Create `fasolt.client/src/views/OAuthConsentView.vue`:

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button } from '@/components/ui/button'

const route = useRoute()

const clientName = ref('')
const scopes = ref<string[]>([])
const loading = ref(true)
const submitting = ref(false)
const error = ref('')

const clientId = route.query.client_id as string

onMounted(async () => {
  if (!clientId) {
    error.value = 'Missing client_id parameter.'
    loading.value = false
    return
  }

  try {
    const res = await fetch(`/api/oauth/consent-info?client_id=${encodeURIComponent(clientId)}`, {
      credentials: 'include',
    })
    if (!res.ok) {
      error.value = 'Unknown application.'
      loading.value = false
      return
    }
    const data = await res.json()
    clientName.value = data.clientName
    scopes.value = data.scopes ?? []
  } catch {
    error.value = 'Failed to load application info.'
  } finally {
    loading.value = false
  }
})

async function handleDecision(approved: boolean) {
  submitting.value = true
  error.value = ''
  try {
    const res = await fetch('/api/oauth/consent', {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ clientId, approved }),
    })
    if (!res.ok) {
      error.value = 'Something went wrong. Please try again.'
      return
    }
    const data = await res.json()
    window.location.href = data.redirectUrl
  } catch {
    error.value = 'Something went wrong. Please try again.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Card class="border-border/60">
    <CardHeader>
      <CardTitle class="text-center text-base">Authorize application</CardTitle>
    </CardHeader>
    <CardContent>
      <div v-if="loading" class="text-center text-sm text-muted-foreground py-4">
        Loading...
      </div>

      <div v-else-if="error" class="flex flex-col gap-4">
        <div class="rounded border border-destructive/20 bg-destructive/10 px-3 py-2 text-xs text-destructive">
          {{ error }}
        </div>
      </div>

      <div v-else class="flex flex-col gap-5">
        <div class="text-center">
          <p class="text-sm">
            <span class="font-semibold">{{ clientName }}</span>
            wants to access your account.
          </p>
        </div>

        <div v-if="scopes.length" class="rounded border border-border/60 bg-muted/30 px-3 py-2.5">
          <p class="text-xs font-medium text-muted-foreground mb-1.5">This will allow the application to:</p>
          <ul class="text-xs space-y-1">
            <li v-for="scope in scopes" :key="scope" class="flex items-center gap-1.5">
              <span class="text-muted-foreground">&#8226;</span>
              <span v-if="scope === 'offline_access'">Stay signed in and refresh access</span>
              <span v-else>{{ scope }}</span>
            </li>
          </ul>
        </div>

        <div class="flex flex-col gap-2">
          <Button
            class="w-full"
            :disabled="submitting"
            @click="handleDecision(true)"
          >
            {{ submitting ? 'Authorizing\u2026' : 'Authorize' }}
          </Button>
          <Button
            variant="outline"
            class="w-full"
            :disabled="submitting"
            @click="handleDecision(false)"
          >
            Deny
          </Button>
        </div>
      </div>
    </CardContent>
  </Card>
</template>
```

- [ ] **Step 3: Verify frontend builds**

Run:
```bash
cd fasolt.client && npm run build
```
Expected: Build succeeds with no errors.

- [ ] **Step 4: Test the full OAuth consent flow**

Start the full stack (`./dev.sh`). Test with an OAuth client:

1. Register a client: `curl -X POST http://localhost:8080/oauth/register -H 'Content-Type: application/json' -d '{"client_name":"Test App","redirect_uris":["http://localhost:8080/"]}'`
2. Navigate to `/oauth/authorize?client_id=<id>&response_type=code&redirect_uri=http://localhost:8080/&code_challenge=test&code_challenge_method=S256`
3. Should redirect to login, then to consent page showing "Test App wants to access your account"
4. Click Authorize — should redirect back through authorize and issue a code
5. Second time through — should skip consent (grant persisted)

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/src/views/OAuthConsentView.vue fasolt.client/src/router/index.ts
git commit -m "feat(auth): add OAuth consent screen Vue page (SEC-3)"
```

---

### Task 10: Playwright End-to-End Tests

**Files:**
- Test via Playwright MCP browser

- [ ] **Step 1: Test SEC-1 — Open redirect prevention**

Using Playwright:
1. Navigate to `http://localhost:5173/oauth/login?returnUrl=https://evil.com`
2. Inspect the hidden form field — value should be `/`, not `https://evil.com`

- [ ] **Step 2: Test SEC-2 — Rate limiting**

Using curl in rapid succession:
```bash
for i in $(seq 1 12); do curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:8080/oauth/login -d "email=test@test.com&password=wrong"; done
```
Expected: First 10 return 302 (redirect to login with error), last 2 return 429.

- [ ] **Step 3: Test SEC-3 — Consent screen flow**

Using Playwright:
1. Register a test client
2. Start OAuth flow — should show consent page with client name
3. Click Authorize — should complete the flow
4. Start OAuth flow again — should skip consent (already granted)

- [ ] **Step 4: Test SEC-5 — Redirect URI validation**

```bash
curl -s -X POST http://localhost:8080/oauth/register \
  -H 'Content-Type: application/json' \
  -d '{"client_name":"evil","redirect_uris":["https://evil.com/callback"]}'
```
Expected: 400 error about disallowed redirect_uri.

- [ ] **Step 5: Commit test results and move requirements**

```bash
mv docs/requirements/17_auth_security.md docs/requirements/done/
git add docs/requirements/done/17_auth_security.md
git commit -m "docs: move auth security requirements to done"
```
