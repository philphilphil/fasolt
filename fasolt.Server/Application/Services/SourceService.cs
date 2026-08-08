using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class SourceService(AppDbContext db)
{
    public async Task<SourceListResponse> ListSources(string userId)
    {
        var now = DateTimeOffset.UtcNow;

        // SRS state lives in ReviewStates; a card with no row is "new" (never due here,
        // since the due count has always required a non-null DueAt).
        var sources = await db.Database
            .SqlQueryRaw<SourceItemDto>("""
                SELECT c."SourceFile",
                       COUNT(*)::int AS "CardCount",
                       COUNT(*) FILTER (WHERE COALESCE(rs."IsSuspended", false) = false
                                          AND rs."DueAt" IS NOT NULL
                                          AND rs."DueAt" <= {0})::int AS "DueCount"
                FROM "Cards" c
                LEFT JOIN "ReviewStates" rs
                       ON rs."CardId" = c."Id" AND rs."UserId" = c."UserId"
                WHERE c."UserId" = {1}
                  AND c."SourceFile" IS NOT NULL
                GROUP BY c."SourceFile"
                ORDER BY c."SourceFile"
                """, now, userId)
            .ToListAsync();

        return new SourceListResponse(sources);
    }
}
