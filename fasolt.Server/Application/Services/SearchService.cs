using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Api.Endpoints;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class SearchService(AppDbContext db)
{
    public async Task<SearchResponse> Search(string userId, string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return new SearchResponse([], []);

        var term = query.Trim();

        var cards = await db.Database
            .SqlQueryRaw<CardSearchResult>("""
                SELECT c."PublicId" AS "Id",
                       ts_headline('english', c."Front", plainto_tsquery('english', {0}),
                           'StartSel=<mark>,StopSel=</mark>,MaxFragments=1') AS "Headline",
                       c."State"
                FROM "Cards" c
                WHERE c."UserId" = {1}
                  AND c."SearchVector" @@ plainto_tsquery('english', {0})
                ORDER BY ts_rank(c."SearchVector", plainto_tsquery('english', {0})) DESC
                LIMIT 10
                """, term, userId)
            .ToListAsync();

        var decks = await db.Database
            .SqlQueryRaw<DeckSearchResult>("""
                SELECT d."PublicId" AS "Id",
                       ts_headline('english', d."Name", plainto_tsquery('english', {0}),
                           'StartSel=<mark>,StopSel=</mark>,MaxFragments=1') AS "Headline",
                       (SELECT COUNT(*) FROM "DeckCards" dc
                        INNER JOIN "Cards" card ON dc."CardId" = card."Id"
                        WHERE dc."DeckId" = d."Id") AS "CardCount"
                FROM "Decks" d
                WHERE d."UserId" = {1}
                  AND d."SearchVector" @@ plainto_tsquery('english', {0})
                ORDER BY ts_rank(d."SearchVector", plainto_tsquery('english', {0})) DESC
                LIMIT 10
                """, term, userId)
            .ToListAsync();

        return new SearchResponse(cards, decks);
    }
}
