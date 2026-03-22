# Remote MCP Server with OAuth Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a remote MCP endpoint at `/mcp` with OAuth 2.1 via OpenIddict so users can connect with a single URL — no .NET SDK, no tool install, no token generation.

**Architecture:** Add OpenIddict as OAuth authorization server to `fasolt.Server`, expose MCP tools over Streamable HTTP at `/mcp`, and update the frontend MCP setup page to be remote-first. The local stdio MCP server stays unchanged.

**Tech Stack:** ASP.NET Core, OpenIddict 7.0, ModelContextProtocol.AspNetCore, EF Core, Vue 3

**Spec:** `docs/superpowers/specs/2026-03-22-remote-mcp-server-design.md`

---

### Task 1: Add NuGet packages

**Files:**
- Modify: `fasolt.Server/fasolt.Server.csproj`

- [ ] **Step 1: Add OpenIddict and MCP ASP.NET Core packages**

```xml
<PackageReference Include="OpenIddict.AspNetCore" Version="7.0.0" />
<PackageReference Include="OpenIddict.EntityFrameworkCore" Version="7.0.0" />
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.0.0" />
```

- [ ] **Step 2: Restore and verify build**

Run: `dotnet restore fasolt.Server && dotnet build fasolt.Server --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/fasolt.Server.csproj
git commit -m "add OpenIddict and MCP ASP.NET Core packages"
```

---

### Task 2: Configure OpenIddict in DbContext and create migration

**Files:**
- Modify: `fasolt.Server/Infrastructure/Data/AppDbContext.cs`

- [ ] **Step 1: Add OpenIddict to DbContext OnModelCreating**

In `AppDbContext.cs`, add `options.UseOpenIddict()` to the `DbContextOptions` setup. This is done where the DbContext is registered in `Program.cs`, not in OnModelCreating.

In `Program.cs`, find the `AddDbContext<AppDbContext>` call and chain `.UseOpenIddict()`:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.UseOpenIddict();
});
```

- [ ] **Step 2: Create EF Core migration for OpenIddict tables**

Run: `dotnet ef migrations add AddOpenIddict --project fasolt.Server`
Expected: Migration created with OpenIddict tables (OpenIddictApplications, OpenIddictAuthorizations, OpenIddictScopes, OpenIddictTokens)

- [ ] **Step 3: Verify migration applies**

Run: `dotnet ef database update --project fasolt.Server`
Expected: Migration applied successfully

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/
git commit -m "add OpenIddict EF Core migration"
```

---

### Task 3: Configure OpenIddict server in Program.cs

**Files:**
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Add OpenIddict service registration**

Add after the existing Identity configuration in `Program.cs`:

```csharp
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<AppDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/oauth/authorize")
               .SetTokenEndpointUris("/oauth/token");

        options.AllowAuthorizationCodeFlow()
               .AllowRefreshTokenFlow();

        options.RequireProofKeyForCodeExchange();

        // Use dev certs in development; configure real certs in production
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough();

        // Set token lifetimes
        options.SetAccessTokenLifetime(TimeSpan.FromHours(1))
               .SetRefreshTokenLifetime(TimeSpan.FromDays(14));
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });
```

- [ ] **Step 2: Update authentication scheme configuration**

The existing auth config has cookie + custom bearer token. Add OpenIddict validation as a third scheme. Update the default authorization policy to accept any of the three:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddAuthenticationSchemes(
            IdentityConstants.ApplicationScheme,
            BearerTokenDefaults.AuthenticationScheme,
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
        .Build();
});
```

- [ ] **Step 3: Build and verify**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Program.cs
git commit -m "configure OpenIddict authorization server"
```

---

### Task 4: Protected Resource Metadata endpoint (RFC 9728)

**Files:**
- Create: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`

- [ ] **Step 1: Create the OAuth endpoints file with protected resource metadata**

This endpoint is required by the MCP spec. It tells MCP clients where to find the authorization server.

```csharp
using Microsoft.AspNetCore.Authorization;

namespace Fasolt.Server.Api.Endpoints;

public static class OAuthEndpoints
{
    public static void MapOAuthEndpoints(this WebApplication app)
    {
        // RFC 9728 - Protected Resource Metadata
        app.MapGet("/.well-known/oauth-protected-resource", [AllowAnonymous] (HttpContext context) =>
        {
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
            return Results.Json(new
            {
                resource = baseUrl,
                authorization_servers = new[] { baseUrl },
                bearer_methods_supported = new[] { "header" },
            });
        });
    }
}
```

- [ ] **Step 2: Register in Program.cs**

Add `app.MapOAuthEndpoints();` alongside the other `MapXxxEndpoints()` calls.

- [ ] **Step 3: Build and test manually**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs fasolt.Server/Program.cs
git commit -m "add protected resource metadata endpoint (RFC 9728)"
```

---

### Task 5: Dynamic Client Registration endpoint

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`

- [ ] **Step 1: Add dynamic client registration endpoint**

MCP clients need to register themselves to get a `client_id`. Add to `MapOAuthEndpoints`:

```csharp
using OpenIddict.Abstractions;

// In MapOAuthEndpoints method:

// RFC 7591 - Dynamic Client Registration
app.MapPost("/oauth/register", [AllowAnonymous] async (
    HttpContext context,
    IOpenIddictApplicationManager applicationManager) =>
{
    var request = await context.Request.ReadFromJsonAsync<ClientRegistrationRequest>();
    if (request is null || string.IsNullOrEmpty(request.ClientName))
        return Results.BadRequest(new { error = "invalid_client_metadata", error_description = "client_name is required" });

    if (request.RedirectUris is null || request.RedirectUris.Length == 0)
        return Results.BadRequest(new { error = "invalid_client_metadata", error_description = "redirect_uris is required" });

    var clientId = Guid.NewGuid().ToString();

    var descriptor = new OpenIddictApplicationDescriptor
    {
        ClientId = clientId,
        DisplayName = request.ClientName,
        ClientType = OpenIddictConstants.ClientTypes.Public,
        ApplicationType = OpenIddictConstants.ApplicationTypes.Native,
    };

    foreach (var uri in request.RedirectUris)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
        {
            descriptor.RedirectUris.Add(parsed);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
        }
    }

    await applicationManager.CreateAsync(descriptor);

    return Results.Ok(new
    {
        client_id = clientId,
        client_name = request.ClientName,
        redirect_uris = request.RedirectUris,
        grant_types = new[] { "authorization_code", "refresh_token" },
        response_types = new[] { "code" },
        token_endpoint_auth_method = "none",
    });
});

// DTO at bottom of file or in a separate file:
record ClientRegistrationRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("client_name")] string? ClientName,
    [property: System.Text.Json.Serialization.JsonPropertyName("redirect_uris")] string[]? RedirectUris);
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs
git commit -m "add dynamic client registration endpoint (RFC 7591)"
```

---

### Task 6: OAuth Authorization endpoint

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`

- [ ] **Step 1: Add the authorization endpoint handler**

This is a passthrough handler — OpenIddict validates the request first, then our code runs. If the user isn't logged in, redirect to a login page. If logged in, auto-approve and issue the code.

Add to `MapOAuthEndpoints`:

```csharp
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

// Authorization endpoint (passthrough)
app.MapGet("/oauth/authorize", async (HttpContext context) =>
{
    // Check if user is authenticated via cookie
    var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
    if (result?.Principal is null)
    {
        // Not logged in — redirect to OAuth login page
        var returnUrl = context.Request.QueryString.Value;
        return Results.Redirect($"/oauth/login?returnUrl={Uri.EscapeDataString("/oauth/authorize" + returnUrl)}");
    }

    var user = result.Principal;
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var userName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue(ClaimTypes.Email) ?? "";

    // Build OpenIddict claims identity
    var identity = new ClaimsIdentity(
        authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        nameType: Claims.Name,
        roleType: Claims.Role);

    identity.SetClaim(Claims.Subject, userId);
    identity.SetClaim(Claims.Name, userName);

    identity.SetDestinations(static claim => claim.Type switch
    {
        Claims.Name => [Destinations.AccessToken],
        _ => [Destinations.AccessToken],
    });

    return Results.SignIn(new ClaimsPrincipal(identity),
        properties: null,
        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
});
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs
git commit -m "add OAuth authorization endpoint"
```

---

### Task 7: OAuth Token endpoint

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`

- [ ] **Step 1: Add the token endpoint handler**

Handles authorization code exchange and refresh token grants:

```csharp
using OpenIddict.Abstractions;

// Token endpoint (passthrough)
app.MapPost("/oauth/token", async (HttpContext context, UserManager<AppUser> userManager) =>
{
    var request = context.GetOpenIddictServerRequest()
        ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

    if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
    {
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = result?.Principal
            ?? throw new InvalidOperationException("The authorization or refresh token is no longer valid.");

        var userId = principal.GetClaim(Claims.Subject)!;
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Results.Forbid(
                authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                properties: new(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user no longer exists.",
                }));

        // Rebuild identity for new token
        var identity = new ClaimsIdentity(principal.Claims,
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.SetDestinations(static claim => claim.Type switch
        {
            Claims.Name => [Destinations.AccessToken],
            _ => [Destinations.AccessToken],
        });

        return Results.SignIn(new ClaimsPrincipal(identity),
            properties: null,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    return Results.BadRequest(new
    {
        error = Errors.UnsupportedGrantType,
        error_description = "The specified grant type is not supported.",
    });
});
```

Note: Import `Fasolt.Server.Domain.Entities` for `AppUser`.

- [ ] **Step 2: Build and verify**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs
git commit -m "add OAuth token endpoint"
```

---

### Task 8: Server-rendered OAuth login page

**Files:**
- Modify: `fasolt.Server/Api/Endpoints/OAuthEndpoints.cs`

- [ ] **Step 1: Add login page GET and POST handlers**

When the OAuth authorize endpoint detects the user isn't logged in, it redirects here. This serves a minimal HTML form and handles login.

```csharp
using Microsoft.AspNetCore.Identity;

// OAuth login page — minimal server-rendered form
app.MapGet("/oauth/login", [AllowAnonymous] (HttpContext context) =>
{
    var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
    var error = context.Request.Query["error"].FirstOrDefault();

    var html = $"""
    <!DOCTYPE html>
    <html lang="en">
    <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <title>Sign in — fasolt</title>
        <style>
            body {{ font-family: system-ui, sans-serif; max-width: 380px; margin: 80px auto; padding: 0 16px; }}
            h1 {{ font-size: 1.25rem; margin-bottom: 4px; }}
            p.sub {{ color: #666; font-size: 0.875rem; margin-top: 0; }}
            label {{ display: block; font-size: 0.875rem; margin-top: 12px; }}
            input {{ width: 100%; padding: 8px; margin-top: 4px; border: 1px solid #ccc; border-radius: 4px; box-sizing: border-box; }}
            button {{ width: 100%; padding: 10px; margin-top: 16px; background: #18181b; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 0.875rem; }}
            button:hover {{ background: #27272a; }}
            .error {{ color: #dc2626; font-size: 0.875rem; margin-top: 8px; }}
        </style>
    </head>
    <body>
        <h1>fasolt</h1>
        <p class="sub">Sign in to connect your AI client</p>
        {(error is not null ? $"<p class=\"error\">{error}</p>" : "")}
        <form method="post" action="/oauth/login">
            <input type="hidden" name="returnUrl" value="{System.Web.HttpUtility.HtmlAttributeEncode(returnUrl)}" />
            <label>Email<input type="email" name="email" required autofocus /></label>
            <label>Password<input type="password" name="password" required /></label>
            <button type="submit">Sign in</button>
        </form>
    </body>
    </html>
    """;
    return Results.Content(html, "text/html");
});

app.MapPost("/oauth/login", [AllowAnonymous] async (
    HttpContext context,
    SignInManager<AppUser> signInManager) =>
{
    var form = await context.Request.ReadFormAsync();
    var email = form["email"].FirstOrDefault() ?? "";
    var password = form["password"].FirstOrDefault() ?? "";
    var returnUrl = form["returnUrl"].FirstOrDefault() ?? "/";

    var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: true);
    if (result.Succeeded)
        return Results.Redirect(returnUrl);

    var error = result.IsLockedOut ? "Account locked. Try again later." : "Invalid email or password.";
    return Results.Redirect($"/oauth/login?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(error)}");
});
```

- [ ] **Step 2: Build and verify**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add fasolt.Server/Api/Endpoints/OAuthEndpoints.cs
git commit -m "add server-rendered OAuth login page"
```

---

### Task 9: Extract service layer from endpoints

**Files:**
- Create: `fasolt.Server/Application/Services/CardService.cs`
- Create: `fasolt.Server/Application/Services/DeckService.cs`
- Create: `fasolt.Server/Application/Services/SearchService.cs`
- Create: `fasolt.Server/Application/Services/SourceService.cs`
- Modify: `fasolt.Server/Api/Endpoints/CardEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/DeckEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/SearchEndpoints.cs`
- Modify: `fasolt.Server/Api/Endpoints/SourceEndpoints.cs`
- Modify: `fasolt.Server/Program.cs`

Extract business logic from endpoint handlers into service classes. This prevents code duplication between REST endpoints and MCP tools. Services take `AppDbContext` via constructor injection and a `userId` parameter on each method. Endpoints become thin HTTP wrappers that call services.

- [ ] **Step 1: Create CardService.cs**

Extract from `CardEndpoints.cs`:
- `CreateCard(userId, front, back, sourceFile?, sourceHeading?)` → returns `CardDto`
- `BulkCreateCards(userId, cards[], sourceFile?, deckId?)` → returns `BulkCreateResult` (created + skipped)
- `ListCards(userId, sourceFile?, deckId?, limit?, afterCursor?)` → returns `PaginatedResponse<CardDto>`
- `GetCard(userId, cardId)` → returns `CardDto?`

Move all validation, duplicate detection, pagination cursor logic, and DTO transformation into the service. The `SourceFrontComparer` moves here too.

Register as scoped: `builder.Services.AddScoped<CardService>();`

- [ ] **Step 2: Create DeckService.cs**

Extract from `DeckEndpoints.cs`:
- `CreateDeck(userId, name, description?)` → returns `DeckDto`
- `ListDecks(userId)` → returns `List<DeckDto>`
- `GetDeck(userId, deckId)` → returns `DeckDetailDto?`

Move card/due count calculation logic here.

Register as scoped: `builder.Services.AddScoped<DeckService>();`

- [ ] **Step 3: Create SearchService.cs**

Extract from `SearchEndpoints.cs`:
- `Search(userId, query)` → returns `SearchResponse`

Move query validation, full-text search queries (cards + decks).

Register as scoped: `builder.Services.AddScoped<SearchService>();`

- [ ] **Step 4: Create SourceService.cs**

Extract from `SourceEndpoints.cs`:
- `ListSources(userId)` → returns `List<SourceItemDto>`

Move the raw SQL aggregation query.

Register as scoped: `builder.Services.AddScoped<SourceService>();`

- [ ] **Step 5: Update endpoints to call services**

Refactor each endpoint handler to inject the corresponding service and delegate to it. Endpoints keep only: HTTP parameter parsing, user ID extraction from claims, and `Results.Ok/Created/NotFound` wrapping.

Example pattern:
```csharp
private static async Task<IResult> List(
    [AsParameters] ListCardsQuery query,
    ClaimsPrincipal user,
    CardService cardService)
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var result = await cardService.ListCards(userId, query.SourceFile, query.DeckId, query.Limit, query.After);
    return Results.Ok(result);
}
```

- [ ] **Step 6: Register services in Program.cs**

```csharp
builder.Services.AddScoped<CardService>();
builder.Services.AddScoped<DeckService>();
builder.Services.AddScoped<SearchService>();
builder.Services.AddScoped<SourceService>();
```

- [ ] **Step 7: Build and run existing tests**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded. Endpoints behave identically (same HTTP contracts, same responses).

- [ ] **Step 8: Commit**

```bash
git add fasolt.Server/Application/Services/ fasolt.Server/Api/Endpoints/ fasolt.Server/Program.cs
git commit -m "extract service layer from endpoint handlers"
```

---

### Task 10: MCP tools using services

**Files:**
- Create: `fasolt.Server/Api/McpTools/CardTools.cs`
- Create: `fasolt.Server/Api/McpTools/DeckTools.cs`
- Create: `fasolt.Server/Api/McpTools/SourceTools.cs`

The MCP tools inject the same services as the endpoints. They resolve the user ID from the HTTP context (available via `IHttpContextAccessor` since the MCP server runs over HTTP).

- [ ] **Step 1: Create a helper for user ID resolution**

Create a static helper or inject `IHttpContextAccessor`:

```csharp
namespace Fasolt.Server.Api.McpTools;

public static class McpUserResolver
{
    public static string GetUserId(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        return user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("User not authenticated");
    }
}
```

- [ ] **Step 2: Create CardTools.cs**

```csharp
using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class CardTools(CardService cardService, SearchService searchService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("Search existing cards by query text")]
    public async Task<string> SearchCards(
        [Description("Search query (min 2 characters)")] string query)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await searchService.Search(userId, query);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("List cards, optionally filtered by source file or deck")]
    public async Task<string> ListCards(
        [Description("Filter by source file path")] string? sourceFile = null,
        [Description("Filter by deck ID")] Guid? deckId = null,
        [Description("Max cards to return (1-200, default 50)")] int? limit = null,
        [Description("Cursor for pagination")] Guid? after = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await cardService.ListCards(userId, sourceFile, deckId, limit, after);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Create one or more flashcards")]
    public async Task<string> CreateCards(
        [Description("Array of cards with front and back text")] CardInput[] cards,
        [Description("Source file path these cards came from")] string? sourceFile = null,
        [Description("Deck ID to add cards to")] Guid? deckId = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await cardService.BulkCreateCards(userId, cards, sourceFile, deckId);
        return JsonSerializer.Serialize(result);
    }
}

// Input DTO for MCP tool (matches what the local MCP server accepts)
public record CardInput(string Front, string Back, string? SourceFile = null, string? SourceHeading = null);
```

- [ ] **Step 3: Create DeckTools.cs**

```csharp
using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class DeckTools(DeckService deckService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("List all decks with card counts")]
    public async Task<string> ListDecks()
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await deckService.ListDecks(userId);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Create a new deck")]
    public async Task<string> CreateDeck(
        [Description("Deck name (max 100 characters)")] string name,
        [Description("Optional deck description")] string? description = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await deckService.CreateDeck(userId, name, description);
        return JsonSerializer.Serialize(result);
    }
}
```

- [ ] **Step 4: Create SourceTools.cs**

```csharp
using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class SourceTools(SourceService sourceService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("List all source files that cards were created from")]
    public async Task<string> ListSources()
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await sourceService.ListSources(userId);
        return JsonSerializer.Serialize(result);
    }
}
```

- [ ] **Step 5: Ensure IHttpContextAccessor is registered**

In `Program.cs`, add if not already present:
```csharp
builder.Services.AddHttpContextAccessor();
```

- [ ] **Step 6: Build and verify**

Run: `dotnet build fasolt.Server`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```bash
git add fasolt.Server/Api/McpTools/ fasolt.Server/Program.cs
git commit -m "add MCP tool implementations using service layer"
```

---

### Task 11: Register MCP server and map endpoint

**Files:**
- Modify: `fasolt.Server/Program.cs`

- [ ] **Step 1: Add MCP server registration**

Add after OpenIddict configuration:

```csharp
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithToolsFromAssembly();
```

- [ ] **Step 2: Map the MCP endpoint**

Add after the other endpoint mappings:

```csharp
app.MapMcp("/mcp");
```

- [ ] **Step 3: Build and run, test the endpoint**

Run: `dotnet build fasolt.Server && dotnet run --project fasolt.Server`

Test that `/mcp` returns 401 (unauthorized):
```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/mcp
```
Expected: 401

Test that `/.well-known/oauth-protected-resource` returns metadata:
```bash
curl -s http://localhost:8080/.well-known/oauth-protected-resource | jq .
```
Expected: JSON with `resource` and `authorization_servers`

- [ ] **Step 4: Commit**

```bash
git add fasolt.Server/Program.cs
git commit -m "register MCP server and map /mcp endpoint"
```

---

### Task 12: End-to-end OAuth flow test

**Files:**
- No files to create — manual verification

- [ ] **Step 1: Start the full stack**

Run: `./dev.sh` or manually start backend + frontend + Postgres

- [ ] **Step 2: Test the full OAuth discovery flow**

```bash
# 1. Protected resource metadata
curl -s http://localhost:8080/.well-known/oauth-protected-resource | jq .

# 2. Authorization server metadata (auto-generated by OpenIddict)
curl -s http://localhost:8080/.well-known/openid-configuration | jq .

# 3. Dynamic client registration
curl -s -X POST http://localhost:8080/oauth/register \
  -H 'Content-Type: application/json' \
  -d '{"client_name":"test-client","redirect_uris":["http://localhost:1234/callback"]}' | jq .

# 4. Verify the authorize endpoint redirects to login
# (open in browser: http://localhost:8080/oauth/authorize?response_type=code&client_id=<from-step-3>&redirect_uri=http://localhost:1234/callback&code_challenge=test&code_challenge_method=S256)
```

- [ ] **Step 3: Test MCP endpoint with OAuth token**

After obtaining a token through the full flow, test:
```bash
curl -s http://localhost:8080/mcp -H 'Authorization: Bearer <token>'
```

- [ ] **Step 4: Commit any fixes**

```bash
git commit -am "fix: OAuth flow adjustments from testing"
```

---

### Task 13: Update MCP setup page — remote-first

**Files:**
- Modify: `fasolt.client/src/views/McpView.vue`

- [ ] **Step 1: Restructure the page layout**

The page should now be remote-first. Rewrite `McpView.vue`:

- **How It Works** card — keep as-is
- **Setup** card — new primary section:
  - Heading: "Add to your AI client"
  - Note: "You'll be asked to log in when your AI client first connects."
  - **Claude Code**: code block with `claude mcp add fasolt --transport http {origin}/mcp`
  - **GitHub Copilot CLI**: JSON config with `"type": "http"` and `"url": "{origin}/mcp"`
  - No token generation needed
- **Advanced: Local MCP Server** — collapsible section at the bottom containing:
  - Prerequisites (.NET SDK, dotnet install)
  - Token generation button
  - Claude Code local command
  - Copilot CLI local JSON config
  - Manual configuration (nested collapsible)

- [ ] **Step 2: Update computed properties**

Replace the current token-dependent commands with simple URL-based commands:

```typescript
const remoteClaudeCommand = computed(() =>
  `claude mcp add fasolt --transport http ${origin.value}/mcp`
)

const remoteCopilotConfig = computed(() =>
  JSON.stringify({
    mcpServers: {
      fasolt: {
        type: 'http',
        url: `${origin.value}/mcp`,
      },
    },
  }, null, 2)
)
```

Keep the existing local config commands (with token) in the advanced section.

- [ ] **Step 3: Build and verify**

Run: `cd fasolt.client && npx vue-tsc --noEmit`
Expected: No errors

- [ ] **Step 4: Visual test with Playwright**

Navigate to `/mcp` and verify:
- Remote setup is primary and prominent
- Local setup is in a collapsible advanced section
- Copy buttons work

- [ ] **Step 5: Commit**

```bash
git add fasolt.client/src/views/McpView.vue
git commit -m "update MCP page to remote-first layout"
```

---

### Task 14: Move requirement to done

**Files:**
- Modify: `docs/requirements/14_mcp-page.md`

- [ ] **Step 1: Move requirement to done**

```bash
mv docs/requirements/14_mcp-page.md docs/requirements/done/14_mcp-page.md
```

- [ ] **Step 2: Commit**

```bash
git add docs/requirements/
git commit -m "move MCP page requirement to done"
```
