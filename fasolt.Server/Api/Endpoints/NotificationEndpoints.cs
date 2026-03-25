using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

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
        UpsertDeviceTokenRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db,
        TimeProvider timeProvider)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var existing = await db.DeviceTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        var now = timeProvider.GetUtcNow();

        if (existing is null)
        {
            db.DeviceTokens.Add(new DeviceToken
            {
                UserId = user.Id,
                Token = request.Token,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.Token = request.Token;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync();
        return Results.Ok();
    }

    private static async Task<IResult> DeleteDeviceToken(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var existing = await db.DeviceTokens.FirstOrDefaultAsync(t => t.UserId == user.Id);
        if (existing is not null)
        {
            db.DeviceTokens.Remove(existing);
            await db.SaveChangesAsync();
        }

        return Results.Ok();
    }

    private static async Task<IResult> GetSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        return Results.Ok(new NotificationSettingsResponse(user.NotificationIntervalHours));
    }

    private static async Task<IResult> UpdateSettings(
        NotificationSettingsRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (request.IntervalHours < 1 || request.IntervalHours > 24)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["intervalHours"] = ["Interval must be between 1 and 24 hours."]
            });

        user.NotificationIntervalHours = request.IntervalHours;
        await userManager.UpdateAsync(user);

        return Results.Ok(new NotificationSettingsResponse(user.NotificationIntervalHours));
    }
}

public record UpsertDeviceTokenRequest(string Token);
public record NotificationSettingsRequest(int IntervalHours);
public record NotificationSettingsResponse(int IntervalHours);
