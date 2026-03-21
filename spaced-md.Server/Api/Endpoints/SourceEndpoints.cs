using Microsoft.EntityFrameworkCore;
using SpacedMd.Server.Application.Dtos;
using SpacedMd.Server.Infrastructure.Data;
using System.Security.Claims;

namespace SpacedMd.Server.Api.Endpoints;

public static class SourceEndpoints
{
    public static void MapSourceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/sources").RequireAuthorization();
        group.MapGet("", List);
    }

    private static async Task<IResult> List(AppDbContext db, ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var now = DateTimeOffset.UtcNow;

        var sources = await db.Cards
            .Where(c => c.UserId == userId && c.SourceFile != null)
            .GroupBy(c => c.SourceFile!)
            .Select(g => new SourceItemDto(
                g.Key,
                g.Count(),
                g.Count(c => c.DueAt != null && c.DueAt <= now)))
            .OrderBy(s => s.SourceFile)
            .ToListAsync();

        return Results.Ok(new SourceListResponse(sources));
    }
}
