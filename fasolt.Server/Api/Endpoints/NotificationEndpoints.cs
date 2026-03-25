using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Api.Endpoints;

public static class NotificationEndpoints
{
    private static readonly int[] AllowedIntervals = [4, 6, 8, 10, 12, 24];

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
        AppDbContext db,
        UpsertDeviceTokenRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest("Token is required.");

        var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == user.Id);
        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.Token = request.Token;
            existing.UpdatedAt = now;
        }
        else
        {
            db.DeviceTokens.Add(new DeviceToken
            {
                UserId = user.Id,
                Token = request.Token,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteDeviceToken(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == user.Id);
        if (existing is not null)
        {
            db.DeviceTokens.Remove(existing);
            await db.SaveChangesAsync();
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var hasToken = await db.DeviceTokens.AnyAsync(d => d.UserId == user.Id);

        return Results.Ok(new NotificationSettingsResponse(
            user.NotificationIntervalHours,
            hasToken));
    }

    private static async Task<IResult> UpdateSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        UpdateNotificationSettingsRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (!AllowedIntervals.Contains(request.IntervalHours))
            return Results.BadRequest($"intervalHours must be one of: {string.Join(", ", AllowedIntervals)}");

        user.NotificationIntervalHours = request.IntervalHours;
        await userManager.UpdateAsync(user);

        return Results.NoContent();
    }
}

public record UpsertDeviceTokenRequest(string Token);
public record UpdateNotificationSettingsRequest(int IntervalHours);
public record NotificationSettingsResponse(int IntervalHours, bool HasDeviceToken);
