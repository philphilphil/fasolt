using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/account");

        group.MapPost("/logout", Logout).RequireAuthorization();
        group.MapGet("/me", GetMe).RequireAuthorization();
        group.MapPut("/email", ChangeEmail).RequireAuthorization();
        group.MapPost("/confirm-email-change", ConfirmEmailChange).RequireAuthorization();
        group.MapPut("/password", ChangePassword).RequireAuthorization();
        group.MapPost("/forgot-password", ForgotPassword).RequireRateLimiting("auth");
        group.MapPost("/reset-password", ResetPassword).RequireRateLimiting("auth");
        group.MapGet("/github-login", GitHubLogin).RequireRateLimiting("auth");
        group.MapGet("/github-callback", GitHubCallback).RequireRateLimiting("auth");
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
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
        return Results.Ok(new UserInfoResponse(user.Email!, isAdmin, user.ExternalProvider));
    }

    private static async Task<IResult> ChangeEmail(
        ChangeEmailRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        IEmailSender<AppUser> emailSender)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["Current password is incorrect."]
            });

        var token = await userManager.GenerateChangeEmailTokenAsync(user, request.NewEmail);
        var confirmLink = $"/settings?action=confirm-email&token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(request.NewEmail)}";
        await emailSender.SendConfirmationLinkAsync(user, request.NewEmail, confirmLink);

        return Results.Ok(new { message = "Verification email sent to the new address." });
    }

    private static async Task<IResult> ConfirmEmailChange(
        ConfirmEmailChangeRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await userManager.ChangeEmailAsync(user, request.NewEmail, request.Token);
        if (!result.Succeeded)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Invalid or expired confirmation token."]
            });

        user.UserName = request.NewEmail;
        await userManager.UpdateAsync(user);
        var isAdmin = await userManager.IsInRoleAsync(user, "Admin");
        return Results.Ok(new UserInfoResponse(user.Email!, isAdmin, user.ExternalProvider));
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        return Results.Ok();
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request,
        UserManager<AppUser> userManager,
        IEmailSender<AppUser> emailSender)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null && user.ExternalProvider is null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = $"/reset-password?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(token)}";
            await emailSender.SendPasswordResetLinkAsync(user, request.Email, resetLink);
        }
        // Always return OK to prevent email enumeration
        return Results.Ok();
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || user.ExternalProvider is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Invalid or expired reset link."]
            });
        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Invalid or expired reset link."]
            });
        return Results.Ok();
    }

    private static IResult GitHubLogin(HttpContext context, IConfiguration configuration)
    {
        // Only available when GitHub auth is configured
        if (string.IsNullOrEmpty(configuration["GitHub:ClientId"]))
            return Results.NotFound();

        var returnUrl = context.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
        // Validate returnUrl is local to prevent open redirect
        if (!IsLocalUrl(returnUrl))
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
        if (!IsLocalUrl(returnUrl))
            returnUrl = "/";

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var gitHubId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(gitHubId))
        {
            await context.SignOutAsync(IdentityConstants.ExternalScheme);
            return Results.Redirect("/login?error=github_no_email");
        }

        // Look up by GitHub provider ID first
        var user = await userManager.Users
            .FirstOrDefaultAsync(u => u.ExternalProvider == "GitHub" && u.ExternalProviderId == gitHubId);

        if (user is null)
        {
            // Check if email is already taken by a password-based account
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser is not null)
            {
                await context.SignOutAsync(IdentityConstants.ExternalScheme);
                return Results.Redirect($"/login?error=email_exists");
            }

            // Create new account
            user = new AppUser
            {
                UserName = email,
                Email = email,
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

        // Sign in with the application cookie
        await signInManager.SignInAsync(user, isPersistent: false);
        await context.SignOutAsync(IdentityConstants.ExternalScheme);

        return Results.Redirect(returnUrl);
    }

    private static bool IsLocalUrl(string url) =>
        !string.IsNullOrEmpty(url) &&
        url.StartsWith('/') &&
        !url.StartsWith("//") &&
        !url.StartsWith("/\\");
}
