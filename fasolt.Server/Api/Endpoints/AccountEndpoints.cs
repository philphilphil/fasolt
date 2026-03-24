using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
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
        group.MapPut("/profile", UpdateProfile).RequireAuthorization();
        group.MapPut("/email", ChangeEmail).RequireAuthorization();
        group.MapPost("/confirm-email-change", ConfirmEmailChange).RequireAuthorization();
        group.MapPut("/password", ChangePassword).RequireAuthorization();
        group.MapPost("/forgot-password", ForgotPassword).RequireRateLimiting("auth");
        group.MapPost("/reset-password", ResetPassword).RequireRateLimiting("auth");
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
        return Results.Ok(new UserInfoResponse(user.Email!, user.DisplayName));
    }

    private static async Task<IResult> UpdateProfile(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();
        user.DisplayName = request.DisplayName;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return Results.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        return Results.Ok(new UserInfoResponse(user.Email!, user.DisplayName));
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
        return Results.Ok(new UserInfoResponse(user.Email!, user.DisplayName));
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
        if (user is not null)
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
        if (user is null)
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
}
