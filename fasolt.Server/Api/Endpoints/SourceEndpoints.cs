using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class SourceEndpoints
{
    public static void MapSourceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sources").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("", List);
    }

    private static async Task<IResult> List(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        SourceService sourceService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await sourceService.ListSources(user.Id);
        return Results.Ok(result);
    }
}
