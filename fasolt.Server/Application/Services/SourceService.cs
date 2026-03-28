using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class SourceService(AppDbContext db)
{
    public async Task<SourceListResponse> ListSources(string userId)
    {
        var now = DateTimeOffset.UtcNow;

        var sources = await db.Database
            .SqlQueryRaw<SourceItemDto>("""
                SELECT "SourceFile",
                       COUNT(*)::int AS "CardCount",
                       COUNT(*) FILTER (WHERE "IsSuspended" = false AND "DueAt" IS NOT NULL AND "DueAt" <= {0})::int AS "DueCount"
                FROM "Cards"
                WHERE "UserId" = {1}
                  AND "SourceFile" IS NOT NULL
                GROUP BY "SourceFile"
                ORDER BY "SourceFile"
                """, now, userId)
            .ToListAsync();

        return new SourceListResponse(sources);
    }
}
