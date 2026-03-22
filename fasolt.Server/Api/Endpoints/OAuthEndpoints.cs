using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class OAuthEndpoints
{
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
        });

        // Authorization Endpoint
        app.MapGet("/oauth/authorize", async (HttpContext context) =>
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

        // OAuth Login Page (GET)
        app.MapGet("/oauth/login", [AllowAnonymous] (HttpContext context) =>
        {
            var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
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
                    body { font-family: system-ui, sans-serif; max-width: 380px; margin: 80px auto; padding: 0 16px; }
                    h1 { font-size: 1.25rem; margin-bottom: 4px; }
                    p.sub { color: #666; font-size: 0.875rem; margin-top: 0; }
                    label { display: block; font-size: 0.875rem; margin-top: 12px; }
                    input { width: 100%; padding: 8px; margin-top: 4px; border: 1px solid #ccc; border-radius: 4px; box-sizing: border-box; }
                    button { width: 100%; padding: 10px; margin-top: 16px; background: #18181b; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 0.875rem; }
                    button:hover { background: #27272a; }
                    .error { color: #dc2626; font-size: 0.875rem; margin-top: 8px; }
                </style>
            </head>
            <body>
                <h1>fasolt</h1>
                <p class="sub">Sign in to connect your AI client</p>
                {{errorHtml}}
                <form method="post" action="/oauth/login">
                    <input type="hidden" name="returnUrl" value="{{returnUrlEncoded}}" />
                    <label>Email<input type="email" name="email" required autofocus /></label>
                    <label>Password<input type="password" name="password" required /></label>
                    <button type="submit">Sign in</button>
                </form>
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
                return Results.Redirect(returnUrl);

            var error = result.IsLockedOut ? "Account locked. Try again later." : "Invalid email or password.";
            return Results.Redirect($"/oauth/login?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(error)}");
        });
    }
}

record ClientRegistrationRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("client_name")] string? ClientName,
    [property: System.Text.Json.Serialization.JsonPropertyName("redirect_uris")] string[]? RedirectUris);
