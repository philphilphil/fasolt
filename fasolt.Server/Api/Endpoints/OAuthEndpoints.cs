using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Api.Endpoints;

public static class OAuthEndpoints
{


    private static bool IsLocalUrl(string url) =>
        !string.IsNullOrEmpty(url) &&
        url.StartsWith('/') &&
        !url.StartsWith("//") &&
        !url.StartsWith("/\\");

    private static bool IsAllowedRedirectUri(string uri, string[] allowedPatterns) =>
        allowedPatterns.Any(pattern =>
            uri.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) &&
            (uri.Length == pattern.Length ||
             pattern.EndsWith("://") || // custom schemes — scheme is the security boundary
             uri[pattern.Length] is '/' or ':' or '?'));

    public static void MapOAuthEndpoints(this WebApplication app)
    {
        // Protected Resource Metadata (RFC 9728)
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

        // Dynamic Client Registration (RFC 7591)
        app.MapPost("/oauth/register", [AllowAnonymous] async (
            HttpContext context,
            IOpenIddictApplicationManager applicationManager,
            IConfiguration configuration) =>
        {
            var request = await context.Request.ReadFromJsonAsync<ClientRegistrationRequest>();
            if (request is null || string.IsNullOrEmpty(request.ClientName))
                return Results.BadRequest(new { error = "invalid_client_metadata", error_description = "client_name is required" });

            if (request.RedirectUris is null || request.RedirectUris.Length == 0)
                return Results.BadRequest(new { error = "invalid_client_metadata", error_description = "redirect_uris is required" });

            var allowedPatterns = configuration.GetSection("OAuth:AllowedRedirectPatterns").Get<string[]>()
                ?? throw new InvalidOperationException("OAuth:AllowedRedirectPatterns must be configured in appsettings.json");

            foreach (var uri in request.RedirectUris)
            {
                if (!IsAllowedRedirectUri(uri, allowedPatterns))
                    return Results.BadRequest(new
                    {
                        error = "invalid_client_metadata",
                        error_description = $"redirect_uri '{uri}' is not allowed. Must match: {string.Join(", ", allowedPatterns)}"
                    });
            }

            var clientId = Guid.NewGuid().ToString();

            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                DisplayName = request.ClientName,
                ClientType = ClientTypes.Public,
                ApplicationType = ApplicationTypes.Native,
            };

            foreach (var uri in request.RedirectUris)
            {
                if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
                    descriptor.RedirectUris.Add(parsed);
            }

            descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(Permissions.Endpoints.Token);
            descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(Permissions.ResponseTypes.Code);

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
        }).RequireRateLimiting("auth-strict");

        // Authorization Endpoint
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

        // Token Endpoint
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

                var identity = new ClaimsIdentity(
                    authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    nameType: Claims.Name,
                    roleType: Claims.Role);

                identity.SetClaim(Claims.Subject, userId);
                identity.SetClaim(ClaimTypes.NameIdentifier, userId);
                identity.SetClaim(Claims.Name, user.UserName);
                identity.SetScopes(principal.GetScopes());

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
            }

            return Results.BadRequest(new
            {
                error = Errors.UnsupportedGrantType,
                error_description = "The specified grant type is not supported.",
            });
        }).RequireRateLimiting("auth");

        // OAuth Login Page (GET)
        app.MapGet("/oauth/login", [AllowAnonymous] (HttpContext context) =>
        {
            var rawReturnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";
            var error = context.Request.Query["error"].FirstOrDefault();

            var errorHtml = error is not null
                ? $"<p class=\"error\">{System.Web.HttpUtility.HtmlEncode(error)}</p>"
                : "";
            var returnUrlEncoded = System.Web.HttpUtility.HtmlAttributeEncode(returnUrl);

            var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>Sign in — fasolt</title>
                <style>
                    * { box-sizing: border-box; margin: 0; padding: 0; }
                    body { font-family: system-ui, -apple-system, sans-serif; min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #fafafa; padding: 16px; }
                    .card { width: 100%; max-width: 380px; background: white; border: 1px solid #e5e7eb; border-radius: 12px; padding: 32px; box-shadow: 0 1px 3px rgba(0,0,0,0.04); }
                    .logo { font-size: 1.5rem; font-weight: 700; letter-spacing: -0.02em; color: #18181b; }
                    .subtitle { color: #71717a; font-size: 0.875rem; margin-top: 4px; }
                    .divider { height: 1px; background: #e5e7eb; margin: 20px 0; }
                    label { display: block; font-size: 0.8125rem; font-weight: 500; color: #374151; margin-bottom: 4px; }
                    input { width: 100%; padding: 9px 12px; border: 1px solid #d1d5db; border-radius: 8px; font-size: 0.875rem; outline: none; transition: border-color 0.15s; }
                    input:focus { border-color: #18181b; box-shadow: 0 0 0 3px rgba(24,24,27,0.06); }
                    .field { margin-bottom: 14px; }
                    button { width: 100%; padding: 10px; margin-top: 6px; background: #18181b; color: white; border: none; border-radius: 8px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: background 0.15s; }
                    button:hover { background: #27272a; }
                    button:active { background: #09090b; }
                    .error { color: #dc2626; font-size: 0.8125rem; margin-bottom: 12px; padding: 8px 12px; background: #fef2f2; border: 1px solid #fecaca; border-radius: 8px; }
                    .footer { text-align: center; margin-top: 16px; font-size: 0.75rem; color: #a1a1aa; }
                </style>
            </head>
            <body>
                <div class="card">
                    <div class="logo">fasolt</div>
                    <p class="subtitle">Sign in to connect your AI client</p>
                    <div class="divider"></div>
                    {{errorHtml}}
                    <form method="post" action="/oauth/login">
                        <input type="hidden" name="returnUrl" value="{{returnUrlEncoded}}" />
                        <div class="field">
                            <label for="email">Email</label>
                            <input type="email" id="email" name="email" placeholder="you@example.com" required autofocus />
                        </div>
                        <div class="field">
                            <label for="password">Password</label>
                            <input type="password" id="password" name="password" required />
                        </div>
                        <button type="submit">Sign in</button>
                    </form>
                    <p class="footer">You'll be redirected back to your AI client.</p>
                </div>
            </body>
            </html>
            """;
            return Results.Content(html, "text/html");
        });

        // OAuth Login Handler (POST)
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
                return Results.Redirect(IsLocalUrl(returnUrl) ? returnUrl : "/");

            var error = result.IsLockedOut ? "Account locked. Try again later." : "Invalid email or password.";
            return Results.Redirect($"/oauth/login?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(error)}");
        }).RequireRateLimiting("auth");

        // OAuth Consent Page (GET) — server-rendered for ASWebAuthenticationSession compatibility
        app.MapGet("/oauth/consent", async (
            HttpContext context,
            IOpenIddictApplicationManager applicationManager) =>
        {
            var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (result?.Principal is null)
                return Results.Redirect("/oauth/login");

            var clientId = context.Request.Query["client_id"].FirstOrDefault() ?? "";
            var application = await applicationManager.FindByClientIdAsync(clientId);
            var clientName = application is not null
                ? (await applicationManager.GetDisplayNameAsync(application) ?? clientId)
                : clientId;

            var clientIdEncoded = System.Web.HttpUtility.HtmlAttributeEncode(clientId);
            var clientNameEncoded = System.Web.HttpUtility.HtmlEncode(clientName);

            var html = $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>Authorize — fasolt</title>
                <style>
                    * { box-sizing: border-box; margin: 0; padding: 0; }
                    body { font-family: system-ui, -apple-system, sans-serif; min-height: 100vh; display: flex; align-items: center; justify-content: center; background: #fafafa; padding: 16px; }
                    .card { width: 100%; max-width: 380px; background: white; border: 1px solid #e5e7eb; border-radius: 12px; padding: 32px; box-shadow: 0 1px 3px rgba(0,0,0,0.04); }
                    .logo { font-size: 1.5rem; font-weight: 700; letter-spacing: -0.02em; color: #18181b; }
                    .subtitle { color: #71717a; font-size: 0.875rem; margin-top: 4px; }
                    .divider { height: 1px; background: #e5e7eb; margin: 20px 0; }
                    .app-name { font-weight: 600; color: #18181b; }
                    .prompt { font-size: 0.875rem; color: #374151; text-align: center; margin-bottom: 16px; }
                    .permissions { background: #f9fafb; border: 1px solid #e5e7eb; border-radius: 8px; padding: 12px 16px; margin-bottom: 20px; }
                    .permissions-title { font-size: 0.75rem; font-weight: 500; color: #6b7280; margin-bottom: 8px; }
                    .permissions ul { list-style: none; }
                    .permissions li { font-size: 0.8125rem; color: #374151; padding: 3px 0; }
                    .permissions li::before { content: "\2022"; color: #9ca3af; margin-right: 8px; }
                    .btn { width: 100%; padding: 10px; border: none; border-radius: 8px; cursor: pointer; font-size: 0.875rem; font-weight: 500; transition: background 0.15s; }
                    .btn-approve { background: #18181b; color: white; margin-bottom: 8px; }
                    .btn-approve:hover { background: #27272a; }
                    .btn-approve:active { background: #09090b; }
                    .btn-deny { background: white; color: #374151; border: 1px solid #d1d5db; }
                    .btn-deny:hover { background: #f9fafb; }
                    .btn-deny:active { background: #f3f4f6; }
                    .footer { text-align: center; margin-top: 16px; font-size: 0.75rem; color: #a1a1aa; }
                </style>
            </head>
            <body>
                <div class="card">
                    <div class="logo">fasolt</div>
                    <p class="subtitle">Authorize application</p>
                    <div class="divider"></div>
                    <p class="prompt"><span class="app-name">{{clientNameEncoded}}</span> wants to access your account.</p>
                    <div class="permissions">
                        <div class="permissions-title">This will allow the application to:</div>
                        <ul>
                            <li>Read and create flashcards and decks</li>
                            <li>View and manage sources</li>
                            <li>Review cards and track study progress</li>
                            <li>Stay signed in and refresh access</li>
                        </ul>
                    </div>
                    <form method="post" action="/oauth/consent">
                        <input type="hidden" name="client_id" value="{{clientIdEncoded}}" />
                        <button type="submit" name="decision" value="approve" class="btn btn-approve">Authorize</button>
                        <button type="submit" name="decision" value="deny" class="btn btn-deny">Deny</button>
                    </form>
                    <p class="footer">You'll be redirected back to your application.</p>
                </div>
            </body>
            </html>
            """;
            return Results.Content(html, "text/html");
        });

        // OAuth Consent Handler (POST) — server-rendered form submission
        app.MapPost("/oauth/consent", async (
            HttpContext context,
            IOpenIddictApplicationManager applicationManager,
            IDataProtectionProvider dataProtection,
            AppDbContext db) =>
        {
            var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (result?.Principal is null)
                return Results.Redirect("/oauth/login");

            var userId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var form = await context.Request.ReadFormAsync();
            var clientId = form["client_id"].FirstOrDefault() ?? "";
            var decision = form["decision"].FirstOrDefault() ?? "";

            var application = await applicationManager.FindByClientIdAsync(clientId);
            if (application is null)
                return Results.BadRequest("Unknown client");

            // Validate that an active OAuth flow exists (cookie must be present)
            var encryptedQuery = context.Request.Cookies["oauth_authorize_query"];
            if (string.IsNullOrEmpty(encryptedQuery))
                return Results.BadRequest("No active authorization flow");

            // Decrypt and validate the stored query string
            var protector = dataProtection.CreateProtector("OAuthAuthorizeQuery");
            string authorizeQuery;
            try
            {
                authorizeQuery = protector.Unprotect(encryptedQuery);
            }
            catch
            {
                return Results.BadRequest("Invalid or expired authorization flow");
            }

            // Verify the client_id in the cookie matches the consent form
            var queryParams = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(authorizeQuery.TrimStart('?'));
            if (!queryParams.TryGetValue("client_id", out var cookieClientId) || cookieClientId != clientId)
                return Results.BadRequest("Client ID mismatch");

            context.Response.Cookies.Delete("oauth_authorize_query");

            if (decision == "approve")
            {
                // Store consent grant
                var existing = await db.ConsentGrants
                    .FirstOrDefaultAsync(g => g.UserId == userId && g.ClientId == clientId);
                if (existing is null)
                {
                    db.ConsentGrants.Add(new ConsentGrant
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        ClientId = clientId,
                        GrantedAt = DateTimeOffset.UtcNow,
                    });
                    await db.SaveChangesAsync();
                }

                // Redirect back to authorize endpoint
                return Results.Redirect($"/oauth/authorize{authorizeQuery}");
            }
            else
            {
                // Deny — redirect to client with error
                var redirectUris = await applicationManager.GetRedirectUrisAsync(application);
                var clientRedirectUri = redirectUris.FirstOrDefault() ?? "/";
                var separator = clientRedirectUri.Contains('?') ? '&' : '?';
                return Results.Redirect($"{clientRedirectUri}{separator}error=access_denied");
            }
        });

        // Consent Info API (GET) — for Vue SPA fallback
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
                permissions = new[]
                {
                    "Read and create flashcards and decks",
                    "View and manage sources",
                    "Review cards and track study progress",
                    "Stay signed in and refresh access",
                },
            });
        }).RequireAuthorization();

        // Consent Decision API (POST) — for Vue SPA fallback
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

            var encryptedQuery = context.Request.Cookies["oauth_authorize_query"];
            if (string.IsNullOrEmpty(encryptedQuery))
                return Results.BadRequest(new { error = "No active authorization flow" });

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

            var queryParams = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(authorizeQuery.TrimStart('?'));
            if (!queryParams.TryGetValue("client_id", out var cookieClientId) || cookieClientId != request.ClientId)
                return Results.BadRequest(new { error = "Client ID mismatch" });

            context.Response.Cookies.Delete("oauth_authorize_query");

            if (request.Approved)
            {
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

                var redirectUrl = $"/oauth/authorize{authorizeQuery}";
                return Results.Ok(new { redirectUrl });
            }
            else
            {
                var redirectUris = await applicationManager.GetRedirectUrisAsync(application);
                var clientRedirectUri = redirectUris.FirstOrDefault() ?? "/";
                var separator = clientRedirectUri.Contains('?') ? '&' : '?';
                return Results.Ok(new { redirectUrl = $"{clientRedirectUri}{separator}error=access_denied" });
            }
        }).RequireAuthorization();
    }
}

record ClientRegistrationRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("client_name")] string? ClientName,
    [property: System.Text.Json.Serialization.JsonPropertyName("redirect_uris")] string[]? RedirectUris);

record ConsentDecisionRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("clientId")] string ClientId,
    [property: System.Text.Json.Serialization.JsonPropertyName("approved")] bool Approved);
