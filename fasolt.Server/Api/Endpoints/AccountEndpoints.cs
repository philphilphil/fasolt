using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Api.Helpers;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/account");

        group.MapPost("/login", Login).RequireRateLimiting("auth");
        group.MapPost("/logout", Logout).RequireAuthorization();
        group.MapGet("/me", GetMe).RequireAuthorization();
        group.MapPut("/email", ChangeEmail).RequireAuthorization("EmailVerified");
        group.MapPost("/confirm-email-change", ConfirmEmailChange).RequireAuthorization();
        group.MapPut("/password", ChangePassword).RequireAuthorization("EmailVerified");
        // Password reset lives at /oauth/forgot-password and /oauth/reset-password
        // as an OTP flow (see OAuthEndpoints) — no JSON API surface.
        group.MapGet("/github-login", GitHubLogin).RequireRateLimiting("auth");
        group.MapGet("/github-callback", GitHubCallback).RequireRateLimiting("auth");
        group.MapGet("/export", ExportData).RequireAuthorization("EmailVerified").RequireRateLimiting("auth");
        group.MapDelete("/", DeleteAccount).RequireAuthorization("EmailVerified").RequireRateLimiting("auth");
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        SignInManager<AppUser> signInManager)
    {
        // Look up first so we can use CheckPasswordSignInAsync + a single
        // SignInAsync. PasswordSignInAsync would also issue a cookie, and
        // then we'd need a second SignInAsync to honor RememberMe — two
        // Set-Cookie headers on one response. This avoids that.
        var user = await signInManager.UserManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Results.Problem("Invalid email or password.", statusCode: 401);

        var result = await signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return Results.Problem("Account locked. Try again later.", statusCode: 429);
        if (!result.Succeeded)
            return Results.Problem("Invalid email or password.", statusCode: 401);

        // AppClaimsPrincipalFactory injects email_confirmed and
        // external_provider claims into the cookie.
        await signInManager.SignInAsync(user, request.RememberMe);

        return Results.Ok();
    }

    private static async Task<IResult> Logout(SignInManager<AppUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.Ok();
    }

    private static async Task<IResult> GetMe(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        // Fast path for cookie auth: the Identity cookie has Email, Role,
        // email_confirmed, and external_provider claims (populated by
        // AppClaimsPrincipalFactory). No DB hit on the hot path that fires
        // on every web page load. Tradeoff: a role promotion doesn't take
        // effect until the user signs out and back in. Admin authorization
        // enforcement lives on each admin endpoint via [Authorize] policies,
        // which also read the same role claim, so there's no staleness gap
        // in the security path — only in nav rendering.
        var claimEmail = principal.FindFirstValue(ClaimTypes.Email);
        if (!string.IsNullOrEmpty(claimEmail))
        {
            var externalProvider = principal.FindFirstValue("external_provider");
            var displayName = externalProvider is not null
                ? principal.FindFirstValue(ClaimTypes.Name)
                : null;
            return Results.Ok(new UserInfoResponse(
                claimEmail,
                principal.IsInRole("Admin"),
                principal.FindFirstValue("email_confirmed") == "true",
                externalProvider,
                displayName));
        }

        // Slow path for OAuth bearer tokens (iOS, MCP clients): access tokens
        // issued by OpenIddict only include sub/NameIdentifier/name/email_confirmed
        // (see OAuthEndpoints.cs SetDestinations blocks). No Email/Role/
        // external_provider in the token, so we need a user lookup. Called
        // roughly once per iOS app launch — one query is acceptable.
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
        var dbDisplayName = user.ExternalProvider is not null ? user.UserName : null;
        return Results.Ok(new UserInfoResponse(user.Email!, isAdmin, user.EmailConfirmed, user.ExternalProvider, dbDisplayName));
    }

    private static async Task<IResult> ChangeEmail(
        ChangeEmailRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        IEmailSender<AppUser> emailSender,
        IConfiguration configuration)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        if (user.ExternalProvider is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Email changes are not available for externally linked accounts."]
            });
        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["Current password is incorrect."]
            });

        var token = await userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
        var baseUrl = configuration["App:BaseUrl"]!;
        var confirmLink = $"{baseUrl}/confirm-email-change?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(request.NewEmail)}";
        await emailSender.SendConfirmationLinkAsync(user, request.NewEmail, confirmLink);

        return Results.Ok(new { message = "Verification email sent to the new address." });
    }

    private static async Task<IResult> ConfirmEmailChange(
        ConfirmEmailChangeRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        if (user.ExternalProvider is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Email changes are not available for externally linked accounts."]
            });

        var result = await userManager.ChangeEmailAsync(user, request.NewEmail, request.Token);
        if (!result.Succeeded)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Invalid or expired confirmation token."]
            });

        user.UserName = request.NewEmail;
        await userManager.UpdateAsync(user);

        // Refresh cookie — ChangeEmailAsync invalidates SecurityStamp
        await signInManager.SignInAsync(user, isPersistent: false);

        var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
        return Results.Ok(new UserInfoResponse(user.Email!, isAdmin, user.EmailConfirmed, user.ExternalProvider, null));
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        if (user.ExternalProvider is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Password changes are not available for externally linked accounts."]
            });
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        return Results.Ok();
    }

    private static IResult GitHubLogin(HttpContext context, IConfiguration configuration)
    {
        // Only available when GitHub auth is configured
        if (string.IsNullOrEmpty(configuration["GITHUB_CLIENT_ID"]))
            return Results.NotFound();

        var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
        // Validate returnUrl is local to prevent open redirect
        if (!UrlHelpers.IsLocalUrl(returnUrl))
            returnUrl = "/";

        var properties = new AuthenticationProperties
        {
            RedirectUri = $"/api/account/github-callback?returnUrl={Uri.EscapeDataString(returnUrl)}",
        };

        return Results.Challenge(properties, ["GitHub"]);
    }

    private static async Task<IResult> GitHubCallback(
        HttpContext context,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager)
    {
        var result = await context.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (result?.Principal is null)
            return Results.Redirect("/login?error=github_auth_failed");

        var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
        if (!UrlHelpers.IsLocalUrl(returnUrl))
            returnUrl = "/";

        var gitHubId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var gitHubUsername = result.Principal.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrEmpty(gitHubId) || string.IsNullOrEmpty(gitHubUsername))
        {
            await context.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Redirect("/login?error=github_auth_failed");
        }

        // Look up by GitHub provider ID first
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.ExternalProvider == "GitHub" && u.ExternalProviderId == gitHubId);

        if (user is null)
        {
            // Synthetic email to satisfy Identity's unique email requirement
            var syntheticEmail = $"{gitHubId}+{gitHubUsername}@users.noreply.github.com";

            // Create new account
            user = new AppUser
            {
                UserName = gitHubUsername,
                Email = syntheticEmail,
                EmailConfirmed = true,
                ExternalProvider = "GitHub",
                ExternalProviderId = gitHubId,
            };

            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                await context.SignOutAsync(IdentityConstants.ExternalScheme);
                return Results.Redirect("/login?error=account_creation_failed");
            }
        }

        // Update username if changed on GitHub
        if (user.UserName != gitHubUsername)
        {
            user.UserName = gitHubUsername;
            await userManager.UpdateAsync(user);
        }

        // Sign in with the application cookie
        await signInManager.SignInAsync(user, isPersistent: true);
        await context.SignOutAsync(IdentityConstants.ExternalScheme);

        return Results.Redirect(returnUrl);
    }

    private static async Task<IResult> ExportData(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AccountDataService accountDataService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var export = await accountDataService.ExportUserData(user);
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(export,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase, WriteIndented = true });

        var date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        return Results.File(json, "application/json", $"fasolt-export-{date}.json");
    }

    private static async Task<IResult> DeleteAccount(
        [Microsoft.AspNetCore.Mvc.FromBody] DeleteAccountRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        AccountDataService accountDataService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (user.ExternalProvider is not null)
        {
            // GitHub accounts: confirm by username
            if (string.IsNullOrEmpty(request.ConfirmIdentity) ||
                !string.Equals(request.ConfirmIdentity, user.UserName, StringComparison.OrdinalIgnoreCase))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["confirmIdentity"] = ["Username does not match your account."]
                });
        }
        else
        {
            // Local accounts: confirm by password
            if (string.IsNullOrEmpty(request.Password) || !await userManager.CheckPasswordAsync(user, request.Password))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = ["Password is incorrect."]
                });
        }

        await accountDataService.DeleteUserData(user.Id);
        await signInManager.SignOutAsync();
        return Results.Ok();
    }

}
