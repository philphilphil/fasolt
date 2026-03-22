using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;
using System.Security.Claims;

namespace Fasolt.Server.Api.Endpoints;

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

        var sources = await db.Database
            .SqlQueryRaw<SourceItemDto>("""
                SELECT "SourceFile",
                       COUNT(*)::int AS "CardCount",
                       COUNT(*) FILTER (WHERE "DueAt" IS NOT NULL AND "DueAt" <= {0})::int AS "DueCount"
                FROM "Cards"
                WHERE "UserId" = {1}
                  AND "SourceFile" IS NOT NULL
                  AND "DeletedAt" IS NULL
                GROUP BY "SourceFile"
                ORDER BY "SourceFile"
                """, now, userId)
            .ToListAsync();

        return Results.Ok(new SourceListResponse(sources));
    }
}
