using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
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
using Fasolt.Server.Api.Helpers.OAuthPages;
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
                var target = hint == "signup" ? "/oauth/register" : "/oauth/login";
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
                var identityToken = request.GetParameter("identity_token")?.ToString();
                if (string.IsNullOrEmpty(identityToken))
                    return Results.Json(
                        new { error = Errors.InvalidRequest, error_description = "identity_token parameter is required." },
                        statusCode: StatusCodes.Status400BadRequest);

                var appleService = context.RequestServices.GetRequiredService<AppleAuthService>();
                AppUser appleUser;
                try
                {
                    appleUser = await appleService.ResolveUserAsync(identityToken);
                }
                catch (AppleAuthException ex)
                {
                    return Results.Json(
                        new { error = Errors.InvalidGrant, error_description = ex.Message },
                        statusCode: StatusCodes.Status400BadRequest);
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

        // OAuth Verify Email Page (GET)
        app.MapGet("/oauth/verify-email", [AllowAnonymous] (HttpContext context, IAntiforgery antiforgery) =>
        {
            var email = context.Request.Query["email"].FirstOrDefault() ?? "";
            var rawReturnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";
            var error = context.Request.Query["error"].FirstOrDefault();

            var tokens = antiforgery.GetAndStoreTokens(context);

            return Results.Content(
                OAuthVerifyEmailPage.Render(tokens.RequestToken!, email, returnUrl, error),
                "text/html");
        });

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

            var result = await otpService.VerifyAsync(user.Id, code, context.RequestAborted);
            switch (result)
            {
                case VerifyResult.Ok:
                    // UpdateAsync must precede SignInAsync so the Identity cookie
                    // reflects the confirmed state. If UpdateAsync silently failed,
                    // we'd hand out a cookie claiming "email_confirmed" while the
                    // DB row still said otherwise — a confusing half-state, and
                    // the OTP row has already been consumed inside VerifyAsync.
                    user.EmailConfirmed = true;
                    var updateResult = await userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                        return Results.Redirect(ErrorRedirect("Something went wrong. Please try again."));
                    await signInManager.SignInAsync(user, isPersistent: false);
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

            var canResend = await otpService.CanResendAsync(user.Id, context.RequestAborted);
            switch (canResend)
            {
                case ResendResult.TooSoon:
                    return Results.Redirect(ErrorRedirect("Please wait before requesting another code."));
                case ResendResult.LockedOut:
                    return Results.Redirect(ErrorRedirect("Too many failed attempts. Try again in 10 minutes."));
                case ResendResult.TooManyAttempts:
                    return Results.Redirect(ErrorRedirect("Too many codes sent. Please wait and try again later."));
            }

            // CanResendAsync above is advisory; GenerateAndStoreAsync
            // re-checks cap/cooldown inside an advisory lock. If another
            // request won the race, translate the throw into the same
            // user-visible "too soon" error rather than a 500.
            try
            {
                var code = await otpService.GenerateAndStoreAsync(user.Id, context.RequestAborted);
                await emailSender.SendVerificationCodeAsync(user, user.Email!, code);
            }
            catch (InvalidOperationException)
            {
                return Results.Redirect(ErrorRedirect("Please wait before requesting another code."));
            }

            return Results.Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }).RequireRateLimiting("auth-strict");

        // OAuth Consent Page (GET) — server-rendered for ASWebAuthenticationSession compatibility
        app.MapGet("/oauth/consent", async (
            HttpContext context,
            IOpenIddictApplicationManager applicationManager,
            IAntiforgery antiforgery) =>
        {
            var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (result?.Principal is null)
                return Results.Redirect("/oauth/login");

            var clientId = context.Request.Query["client_id"].FirstOrDefault() ?? "";
            var application = await applicationManager.FindByClientIdAsync(clientId);
            var clientName = application is not null
                ? (await applicationManager.GetDisplayNameAsync(application) ?? clientId)
                : clientId;

            var tokens = antiforgery.GetAndStoreTokens(context);

            return Results.Content(
                OAuthConsentPage.Render(tokens.RequestToken!, clientId, clientName),
                "text/html");
        });

        // OAuth Consent Handler (POST) — server-rendered form submission
        app.MapPost("/oauth/consent", async (
            HttpContext context,
            IOpenIddictApplicationManager applicationManager,
            IDataProtectionProvider dataProtection,
            AppDbContext db,
            IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest("Invalid request");

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
