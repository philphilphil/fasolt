using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Api.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/search").RequireAuthorization();
        group.MapGet("/", Search);
    }

    private static async Task<IResult> Search(
        string? q,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return Results.Ok(new SearchResponse([], []));

        var term = q.Trim();

        var cards = await db.Database
            .SqlQueryRaw<CardSearchResult>("""
                SELECT c."Id",
                       ts_headline('english', c."Front", plainto_tsquery('english', {0}),
                           'StartSel=<mark>,StopSel=</mark>,MaxFragments=1') AS "Headline",
                       c."State"
                FROM "Cards" c
                WHERE c."UserId" = {1}
                  AND c."DeletedAt" IS NULL
                  AND c."SearchVector" @@ plainto_tsquery('english', {0})
                ORDER BY ts_rank(c."SearchVector", plainto_tsquery('english', {0})) DESC
                LIMIT 10
                """, term, user.Id)
            .ToListAsync();

        var decks = await db.Database
            .SqlQueryRaw<DeckSearchResult>("""
                SELECT d."Id",
                       ts_headline('english', d."Name", plainto_tsquery('english', {0}),
                           'StartSel=<mark>,StopSel=</mark>,MaxFragments=1') AS "Headline",
                       (SELECT COUNT(*) FROM "DeckCards" dc
                        INNER JOIN "Cards" card ON dc."CardId" = card."Id"
                        WHERE dc."DeckId" = d."Id" AND card."DeletedAt" IS NULL) AS "CardCount"
                FROM "Decks" d
                WHERE d."UserId" = {1}
                  AND d."SearchVector" @@ plainto_tsquery('english', {0})
                ORDER BY ts_rank(d."SearchVector", plainto_tsquery('english', {0})) DESC
                LIMIT 10
                """, term, user.Id)
            .ToListAsync();

        return Results.Ok(new SearchResponse(cards, decks));
    }
}

public record SearchResponse(
    List<CardSearchResult> Cards,
    List<DeckSearchResult> Decks);

public record CardSearchResult(Guid Id, string Headline, string State);
public record DeckSearchResult(Guid Id, string Headline, int CardCount);
