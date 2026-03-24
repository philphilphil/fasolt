using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization("AdminCookieOnly");

        group.MapGet("/users", ListUsers);
        group.MapPost("/users/{id}/lock", LockUser);
        group.MapPost("/users/{id}/unlock", UnlockUser);
    }

    private static async Task<IResult> ListUsers(
        int? page,
        int? pageSize,
        AdminService adminService)
    {
        var p = page ?? 1;
        var ps = Math.Clamp(pageSize ?? 50, 1, 100);
        var result = await adminService.ListUsers(p, ps);
        return Results.Ok(result);
    }

    private static async Task<IResult> LockUser(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var currentUser = await userManager.GetUserAsync(principal);
        if (currentUser is null) return Results.Unauthorized();

        if (currentUser.Id == id)
            return Results.BadRequest(new { error = "Cannot lock your own account." });

        var user = await userManager.FindByIdAsync(id);
        if (user is null) return Results.NotFound();

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        return Results.Ok();
    }

    private static async Task<IResult> UnlockUser(
        string id,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return Results.NotFound();

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        return Results.Ok();
    }
}
