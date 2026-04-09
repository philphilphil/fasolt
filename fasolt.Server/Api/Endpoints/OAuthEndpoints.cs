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

        // OAuth Login Page (GET)
        app.MapGet("/oauth/login", [AllowAnonymous] (HttpContext context, IAntiforgery antiforgery, IConfiguration configuration) =>
        {
            var providerHint = context.Request.Query["provider_hint"].FirstOrDefault();
            if (providerHint == "github" && !string.IsNullOrEmpty(configuration["GITHUB_CLIENT_ID"]))
            {
                var rawReturnUrlForHint = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
                var safeReturnUrl = UrlHelpers.IsLocalUrl(rawReturnUrlForHint) ? rawReturnUrlForHint : "/";
                return Results.Redirect($"/api/account/github-login?returnUrl={Uri.EscapeDataString(safeReturnUrl)}");
            }

            var rawReturnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";
            var error = context.Request.Query["error"].FirstOrDefault();

            var tokens = antiforgery.GetAndStoreTokens(context);
            var gitHubEnabled = !string.IsNullOrEmpty(configuration["GITHUB_CLIENT_ID"]);

            return Results.Content(
                OAuthLoginPage.Render(tokens.RequestToken!, returnUrl, error, gitHubEnabled),
                "text/html");
        });

        // OAuth Login Handler (POST)
        app.MapPost("/oauth/login", [AllowAnonymous] async (
            HttpContext context,
            SignInManager<AppUser> signInManager,
            IEmailVerificationCodeService otpService,
            IOtpEmailSender emailSender,
            IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest("Invalid request");

            var form = await context.Request.ReadFormAsync();
            var email = form["email"].FirstOrDefault() ?? "";
            var password = form["password"].FirstOrDefault() ?? "";
            var rawReturnUrl = form["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";

            var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                var user = await signInManager.UserManager.FindByEmailAsync(email);
                if (user is null)
                    return Results.Redirect($"/oauth/login?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString("Invalid email or password.")}");

                // Unverified accounts must complete OTP verification before we
                // hand out a cookie. Otherwise they can reach /oauth/authorize
                // with an email_confirmed=false claim and bounce through the
                // verify-email redirect loop, and they'd also be able to hit
                // any non-EmailVerified-gated endpoint with a valid cookie.
                // Sign them out of the PasswordSignIn, generate a fresh OTP
                // (respecting the resend window), and send them to the OTP
                // page — same UX as a fresh registration.
                if (!user.EmailConfirmed)
                {
                    await signInManager.SignOutAsync();

                    var canResend = await otpService.CanResendAsync(user.Id, context.RequestAborted);
                    if (canResend == ResendResult.Ok)
                    {
                        // CanResendAsync is advisory — GenerateAndStoreAsync
                        // re-checks cap/cooldown inside its advisory lock and
                        // throws if a concurrent caller won the race. Swallow
                        // that here: the user's other tab/click already got a
                        // fresh code, and silently falling through to the
                        // verify page is the right UX.
                        try
                        {
                            var code = await otpService.GenerateAndStoreAsync(user.Id, context.RequestAborted);
                            await emailSender.SendVerificationCodeAsync(user, user.Email!, code);
                        }
                        catch (InvalidOperationException)
                        {
                        }
                    }
                    // If we can't resend (cooldown / cap / lockout), fall through
                    // silently — the user still has whatever code we sent last
                    // time, and the verify page will surface any lockout error
                    // on its next submit.

                    return Results.Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
                }

                await signInManager.SignInAsync(user, isPersistent: false);
                return Results.Redirect(returnUrl);
            }

            var error = result.IsLockedOut ? "Account locked. Try again later." : "Invalid email or password.";
            return Results.Redirect($"/oauth/login?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(error)}");
        }).RequireRateLimiting("auth");

        // OAuth Register Page (GET) — server-rendered for ASWebAuthenticationSession compatibility
        app.MapGet("/oauth/register", [AllowAnonymous] (HttpContext context, IAntiforgery antiforgery) =>
        {
            var rawReturnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";
            var error = context.Request.Query["error"].FirstOrDefault();

            var tokens = antiforgery.GetAndStoreTokens(context);

            return Results.Content(
                OAuthRegisterPage.Render(tokens.RequestToken!, returnUrl, error),
                "text/html");
        });

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

            // Enforce the password policy before touching the user lookup. If
            // this ran after FindByEmailAsync, weak passwords + already-taken
            // emails would short-circuit to the verify-email redirect while
            // weak passwords + new emails would error — an enumeration oracle.
            // Running it first makes the error response identical regardless of
            // whether the email exists.
            var passwordProbe = new AppUser { UserName = email, Email = email };
            foreach (var validator in userManager.PasswordValidators)
            {
                var pwResult = await validator.ValidateAsync(userManager, passwordProbe, password);
                if (!pwResult.Succeeded)
                {
                    var pwMsg = string.Join("; ", pwResult.Errors.Select(e => e.Description));
                    return Results.Redirect($"/oauth/register?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(pwMsg)}");
                }
            }

            // Check if email exists. We never reveal whether a given address is
            // already registered (enumeration) and we never let an anonymous
            // POST regenerate OTPs on behalf of an existing user (griefing).
            // Both cases fall through to the same generic verify-email redirect
            // as a fresh signup — indistinguishable from the attacker's side.
            // If the real owner of an unconfirmed account lost their code, they
            // can request a new one from the verify-email page, which is
            // separately rate-limited and goes through CanResendAsync.
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null)
            {
                return Results.Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
            }

            var user = new AppUser { UserName = email, Email = email };
            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                // Should be rare now that PasswordValidators ran above, but
                // CreateAsync also enforces User.* policies (e.g. invalid email
                // characters) that we haven't pre-validated.
                var msg = string.Join("; ", createResult.Errors.Select(e => e.Description));
                return Results.Redirect($"/oauth/register?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(msg)}");
            }

            var code = await otpService.GenerateAndStoreAsync(user.Id, context.RequestAborted);
            await emailSender.SendVerificationCodeAsync(user, user.Email!, code);

            return Results.Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }).RequireRateLimiting("auth-strict");

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

        // OAuth Forgot Password Page (GET) — user requests a reset code.
        // Dual state machine keyed on ?sent=1: the entry form renders when
        // sent is absent; after POST we redirect back here with sent=1 to
        // show a generic "check your email" confirmation. One URL, two
        // views, so the POST→GET round trip survives refresh/back-button.
        app.MapGet("/oauth/forgot-password", [AllowAnonymous] (HttpContext context, IAntiforgery antiforgery) =>
        {
            var rawReturnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";
            var error = context.Request.Query["error"].FirstOrDefault();
            var sent = context.Request.Query["sent"].FirstOrDefault() == "1";
            var email = context.Request.Query["email"].FirstOrDefault();

            var tokens = antiforgery.GetAndStoreTokens(context);

            return Results.Content(
                OAuthForgotPasswordPage.Render(tokens.RequestToken!, returnUrl, error, sent, email),
                "text/html");
        });

        // OAuth Forgot Password Handler (POST)
        app.MapPost("/oauth/forgot-password", [AllowAnonymous] async (
            HttpContext context,
            UserManager<AppUser> userManager,
            IPasswordResetCodeService otpService,
            IOtpEmailSender emailSender,
            IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest("Invalid request");

            var form = await context.Request.ReadFormAsync();
            var email = form["email"].FirstOrDefault() ?? "";
            var rawReturnUrl = form["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";

            if (string.IsNullOrEmpty(email) || !email.Contains('@'))
                return Results.Redirect(
                    $"/oauth/forgot-password?returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString("Please enter a valid email address.")}");

            // Look up the user but never reveal whether an account exists —
            // that's an enumeration oracle. Also skip external-provider users
            // (GitHub/Apple) and unverified users: neither has a password we
            // can reset. Fall through to the generic "check your email" view
            // in every case so an attacker can't tell the difference.
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null && user.ExternalProvider is null && user.EmailConfirmed)
            {
                // Advisory check first; GenerateAndStoreAsync re-checks inside
                // the per-user lock. If a cap/cooldown is tripped, swallow and
                // still render the generic confirmation so timing/behaviour
                // can't leak account existence.
                var canResend = await otpService.CanResendAsync(user.Id, context.RequestAborted);
                if (canResend == ResendResult.Ok)
                {
                    try
                    {
                        var code = await otpService.GenerateAndStoreAsync(user.Id, context.RequestAborted);
                        await emailSender.SendPasswordResetCodeAsync(user, user.Email!, code);
                    }
                    catch (InvalidOperationException)
                    {
                        // Lost a cap/cooldown race. Still confirm silently.
                    }
                }
            }

            return Results.Redirect(
                $"/oauth/forgot-password?returnUrl={Uri.EscapeDataString(returnUrl)}&email={Uri.EscapeDataString(email)}&sent=1");
        }).RequireRateLimiting("auth-strict");

        // OAuth Reset Password Page (GET) — user pastes the code + new password
        app.MapGet("/oauth/reset-password", [AllowAnonymous] (HttpContext context, IAntiforgery antiforgery) =>
        {
            var email = context.Request.Query["email"].FirstOrDefault() ?? "";
            var rawReturnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";
            var error = context.Request.Query["error"].FirstOrDefault();

            var tokens = antiforgery.GetAndStoreTokens(context);

            return Results.Content(
                OAuthResetPasswordPage.Render(tokens.RequestToken!, email, returnUrl, error, success: false),
                "text/html");
        });

        // OAuth Reset Password Handler (POST)
        app.MapPost("/oauth/reset-password", [AllowAnonymous] async (
            HttpContext context,
            UserManager<AppUser> userManager,
            IPasswordResetCodeService otpService,
            IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
                return Results.BadRequest("Invalid request");

            var form = await context.Request.ReadFormAsync();
            var email = form["email"].FirstOrDefault() ?? "";
            var code = form["code"].FirstOrDefault() ?? "";
            var password = form["password"].FirstOrDefault() ?? "";
            var confirmPassword = form["confirmPassword"].FirstOrDefault() ?? "";
            var rawReturnUrl = form["returnUrl"].FirstOrDefault() ?? "/";
            var returnUrl = UrlHelpers.IsLocalUrl(rawReturnUrl) ? rawReturnUrl : "/";

            string ErrorRedirect(string msg)
                => $"/oauth/reset-password?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(msg)}";

            if (password != confirmPassword)
                return Results.Redirect(ErrorRedirect("Passwords don't match."));

            // Run the password policy BEFORE any account lookup so weak-password
            // + unknown-email and weak-password + known-email take identical
            // paths. Otherwise the policy check on a real user would behave
            // differently from the early-out on an unknown email — an
            // enumeration oracle via latency/errors.
            var passwordProbe = new AppUser { UserName = email, Email = email };
            foreach (var validator in userManager.PasswordValidators)
            {
                var pwResult = await validator.ValidateAsync(userManager, passwordProbe, password);
                if (!pwResult.Succeeded)
                {
                    var pwMsg = string.Join("; ", pwResult.Errors.Select(e => e.Description));
                    return Results.Redirect(ErrorRedirect(pwMsg));
                }
            }

            var user = await userManager.FindByEmailAsync(email);
            if (user is null || user.ExternalProvider is not null || !user.EmailConfirmed)
                return Results.Redirect(ErrorRedirect("That code has expired. Request a new one."));

            var verifyResult = await otpService.VerifyAsync(user.Id, code, context.RequestAborted);
            switch (verifyResult)
            {
                case VerifyResult.Ok:
                    break;
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

            // OTP is consumed (row deleted inside VerifyAsync). Now rotate the
            // password. Using RemovePasswordAsync + AddPasswordAsync rather
            // than ResetPasswordAsync so we don't need an Identity URL token —
            // the OTP is our authentication factor here, and removing+adding
            // bumps the SecurityStamp which invalidates existing sessions.
            var removeResult = await userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
                return Results.Redirect(ErrorRedirect("Something went wrong. Please try again."));
            var addResult = await userManager.AddPasswordAsync(user, password);
            if (!addResult.Succeeded)
            {
                var msg = string.Join("; ", addResult.Errors.Select(e => e.Description));
                return Results.Redirect(ErrorRedirect(msg));
            }

            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Content(
                OAuthResetPasswordPage.Render(tokens.RequestToken!, email, returnUrl, error: null, success: true),
                "text/html");
        }).RequireRateLimiting("auth");

        // OAuth Reset Password Resend Handler (POST)
        app.MapPost("/oauth/reset-password/resend", [AllowAnonymous] async (
            HttpContext context,
            UserManager<AppUser> userManager,
            IPasswordResetCodeService otpService,
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
                => $"/oauth/reset-password?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}&error={Uri.EscapeDataString(msg)}";

            var user = await userManager.FindByEmailAsync(email);
            if (user is null || user.ExternalProvider is not null || !user.EmailConfirmed)
                return Results.Redirect($"/oauth/reset-password?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}");

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

            try
            {
                var code = await otpService.GenerateAndStoreAsync(user.Id, context.RequestAborted);
                await emailSender.SendPasswordResetCodeAsync(user, user.Email!, code);
            }
            catch (InvalidOperationException)
            {
                return Results.Redirect(ErrorRedirect("Please wait before requesting another code."));
            }

            return Results.Redirect($"/oauth/reset-password?email={Uri.EscapeDataString(email)}&returnUrl={Uri.EscapeDataString(returnUrl)}");
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
