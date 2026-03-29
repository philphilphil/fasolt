using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Api.Helpers;
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
        group.MapPost("/resend-verification", ResendVerification).RequireAuthorization().RequireRateLimiting("auth");
        group.MapPost("/confirm-email", ConfirmEmail).RequireRateLimiting("auth");
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
        var displayName = user.ExternalProvider is not null ? user.UserName : null;
        return Results.Ok(new UserInfoResponse(user.Email!, isAdmin, user.EmailConfirmed, user.ExternalProvider, displayName));
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
        var confirmLink = $"{baseUrl}/settings?action=confirm-email&token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(request.NewEmail)}";
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

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request,
        UserManager<AppUser> userManager,
        IEmailSender<AppUser> emailSender,
        IConfiguration configuration)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null && user.ExternalProvider is null && user.EmailConfirmed)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var baseUrl = configuration["App:BaseUrl"]!;
            var resetLink = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(request.Email)}&token={Uri.EscapeDataString(token)}";
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

    private static async Task<IResult> ResendVerification(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        IEmailSender<AppUser> emailSender,
        IConfiguration configuration)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        if (user.EmailConfirmed) return Results.Ok();

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var baseUrl = configuration["App:BaseUrl"]!;
        var confirmLink = $"{baseUrl}/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";
        await emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmLink);

        return Results.Ok();
    }

    private static async Task<IResult> ConfirmEmail(
        ConfirmEmailRequest request,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Invalid or expired confirmation link."]
            });

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Invalid or expired confirmation link."]
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

}
