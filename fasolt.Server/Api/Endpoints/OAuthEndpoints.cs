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
using Fasolt.Server.Api.Helpers;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Api.Endpoints;

public static class OAuthEndpoints
{

    private static bool IsAllowedRedirectUri(string uri, string[] allowedPatterns)
    {
        // Any HTTPS redirect URI is allowed — PKCE protects the flow, not the redirect URI.
        // This enables any MCP client (Claude.ai, Cursor, etc.) to register without a whitelist entry.
        if (uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return true;

        // For http:// and custom schemes, require explicit pattern match
        return allowedPatterns.Any(pattern =>
            uri.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) &&
            (uri.Length == pattern.Length ||
             pattern.EndsWith("://") || // custom schemes — scheme is the security boundary
             uri[pattern.Length] is '/' or ':' or '?'));
    }

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
        // Moved from /oauth/register to /oauth/clients/register to free up
        // /oauth/register for the user-facing HTML registration page.
        // The discovery document in Program.cs advertises this new path.
        app.MapPost("/oauth/clients/register", [AllowAnonymous] async (
            HttpContext context,
            IOpenIddictApplicationManager applicationManager,
            IConfiguration configuration) =>
        {
            var request = await context.Request.ReadFromJsonAsync<ClientRegistrationRequest>();
            if (request is null || string.IsNullOrEmpty(request.ClientName))
                return Results.BadRequest(new { error = "invalid_client_metadata", error_description = "client_name is required" });

            if (request.RedirectUris is null || request.RedirectUris.Length == 0)
                return Results.BadRequest(new { error = "invalid_client_metadata", error_description = "redirect_uris is required" });

            var allowedPatterns = configuration.GetSection("OAuth:AllowedNonHttpsRedirectPatterns").Get<string[]>()
                ?? throw new InvalidOperationException("OAuth:AllowedNonHttpsRedirectPatterns must be configured in appsettings.json");

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
                var openIddictReq = context.GetOpenIddictServerRequest();
                var hint = openIddictReq?.GetParameter("screen_hint")?.ToString();
                var target = hint == "signup" ? "/register" : "/login";
                return Results.Redirect($"{target}?returnUrl={Uri.EscapeDataString("/oauth/authorize" + returnUrl)}");
            }

            // Block unverified users from authorizing OAuth clients. Send them
            // to the server-rendered OTP page with the original authorize
            // query preserved as returnUrl, so verifying resumes the flow.
            var emailConfirmed = result.Principal.FindFirstValue("email_confirmed");
            if (emailConfirmed != "true")
            {
                var authorizeReturnUrl = "/oauth/authorize" + (context.Request.QueryString.Value ?? "");
                var userEmail = result.Principal.FindFirstValue(ClaimTypes.Email) ?? "";
                return Results.Redirect(
                    $"/oauth/verify-email?email={Uri.EscapeDataString(userEmail)}&returnUrl={Uri.EscapeDataString(authorizeReturnUrl)}");
            }

            var user = result.Principal;
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var userName = user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue(ClaimTypes.Email) ?? "";

            // Check for existing consent grant
            var openIddictRequest = context.GetOpenIddictServerRequest();
            var clientId = openIddictRequest?.ClientId;

            // First-party clients auto-consent silently
            var firstPartyClientIds = new HashSet<string> { "fasolt-ios" };

            if (clientId is not null)
            {
                var hasConsent = firstPartyClientIds.Contains(clientId)
                    || await db.ConsentGrants.AnyAsync(g => g.UserId == userId && g.ClientId == clientId);

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
            identity.SetClaim("email_confirmed", "true"); // Verified above — unverified users are redirected
            identity.SetScopes(Scopes.OfflineAccess);

            identity.SetDestinations(static claim => claim.Type switch
            {
                ClaimTypes.NameIdentifier => [Destinations.AccessToken],
                Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
                Claims.Name => [Destinations.AccessToken, Destinations.IdentityToken],
                "email_confirmed" => [Destinations.AccessToken],
                _ => [Destinations.AccessToken],
            });

            return Results.SignIn(new ClaimsPrincipal(identity),
                properties: null,
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        });

        // Token Endpoint
        app.MapPost("/oauth/token", async (HttpContext context, UserManager<AppUser> userManager) =>
        {
            var request = context.GetOpenIddictServerRequest();
            if (request is null)
                return Results.BadRequest(new { error = Errors.InvalidRequest, error_description = "The OpenID Connect request cannot be retrieved." });

            if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
            {
                var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                if (result?.Principal is null)
                    return Results.Forbid(
                        authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                        properties: new(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid.",
                        }));
                var principal = result.Principal;

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

                if (!user.EmailConfirmed)
                    return Results.Forbid(
                        authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                        properties: new(new Dictionary<string, string?>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Email address not verified.",
                        }));

                var identity = new ClaimsIdentity(
                    authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    nameType: Claims.Name,
                    roleType: Claims.Role);

                identity.SetClaim(Claims.Subject, userId);
                identity.SetClaim(ClaimTypes.NameIdentifier, userId);
                identity.SetClaim(Claims.Name, user.UserName);
                identity.SetClaim("email_confirmed", "true"); // Verified above — unverified users are rejected
                identity.SetScopes(principal.GetScopes());

                identity.SetDestinations(static claim => claim.Type switch
                {
                    ClaimTypes.NameIdentifier => [Destinations.AccessToken],
                    Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
                    Claims.Name => [Destinations.AccessToken, Destinations.IdentityToken],
                    "email_confirmed" => [Destinations.AccessToken],
                    _ => [Destinations.AccessToken],
                });

                return Results.SignIn(new ClaimsPrincipal(identity),
                    properties: null,
                    OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            // Custom grants use Results.Json directly because Results.Forbid through the
            // OpenIddict scheme only works for the built-in flows (authorization_code / refresh_token).
            if (request.GrantType == AppleAuthService.GrantType)
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AppleTokenEndpoint");
                var identityToken = request.GetParameter("identity_token")?.ToString();
                if (string.IsNullOrEmpty(identityToken))
                {
                    logger.LogWarning("Apple token request missing identity_token parameter");
                    return Results.Json(
                        new { error = Errors.InvalidRequest, error_description = "identity_token parameter is required." },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var appleService = context.RequestServices.GetRequiredService<AppleAuthService>();
                AppUser appleUser;
                try
                {
                    appleUser = await appleService.ResolveUserAsync(identityToken);
                    logger.LogInformation("Apple sign-in resolved user {UserId} ({Email})", appleUser.Id, appleUser.Email);
                }
                catch (AppleAuthException ex)
                {
                    logger.LogWarning("Apple sign-in failed: {Error}", ex.Message);
                    return Results.Json(
                        new { error = Errors.InvalidGrant, error_description = ex.Message },
                        statusCode: StatusCodes.Status400BadRequest);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Unexpected error during Apple sign-in");
                    return Results.Json(
                        new { error = Errors.ServerError, error_description = "An unexpected error occurred." },
                        statusCode: StatusCodes.Status500InternalServerError);
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

            return Results.BadRequest(new
            {
                error = Errors.UnsupportedGrantType,
                error_description = "The specified grant type is not supported.",
            });
        }).RequireRateLimiting("auth");
    }
}

record ClientRegistrationRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("client_name")] string? ClientName,
    [property: System.Text.Json.Serialization.JsonPropertyName("redirect_uris")] string[]? RedirectUris);
