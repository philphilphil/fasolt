using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization().RequireRateLimiting("api");

        group.MapPut("/device-token", UpsertDeviceToken);
        group.MapDelete("/device-token", DeleteDeviceToken);
        group.MapGet("/settings", GetSettings);
        group.MapPut("/settings", UpdateSettings);
    }

    private static async Task<IResult> UpsertDeviceToken(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeviceTokenService service,
        UpsertDeviceTokenRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest("Token is required.");

        await service.UpsertDeviceToken(user.Id, request.Token);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteDeviceToken(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeviceTokenService service)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        await service.DeleteDeviceToken(user.Id);
        return Results.NoContent();
    }

    private static async Task<IResult> GetSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeviceTokenService service)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var settings = await service.GetSettings(user.Id);
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeviceTokenService service,
        UpdateNotificationSettingsRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var success = await service.UpdateSettings(user.Id, request.IntervalHours);
        if (!success)
            return Results.BadRequest($"intervalHours must be one of: 4, 6, 8, 10, 12, 24");

        return Results.NoContent();
    }
}
