using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class SchedulingSettingsEndpoints
{
    public static void MapSchedulingSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/settings/scheduling").RequireAuthorization("EmailVerified").RequireRateLimiting("api");

        group.MapGet("/", GetSettings);
        group.MapPut("/", UpdateSettings);
    }

    private static async Task<IResult> GetSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        SchedulingSettingsService service)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var settings = await service.GetSettings(user.Id);
        return Results.Ok(settings);
    }

    private static async Task<IResult> UpdateSettings(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        SchedulingSettingsService service,
        UpdateSchedulingSettingsRequest request)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await service.UpdateSettings(
            user.Id,
            request.DesiredRetention,
            request.MaximumInterval,
            request.DayStartHour,
            request.TimeZone);
        if (result is null)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["desiredRetention"] = ["Must be between 0.70 and 0.97."],
                ["maximumInterval"] = ["Must be between 1 and 36500."],
                ["dayStartHour"] = ["Must be between 0 and 23."],
                ["timeZone"] = ["Must be a valid IANA timezone identifier."],
            });

        return Results.Ok(result);
    }
}
