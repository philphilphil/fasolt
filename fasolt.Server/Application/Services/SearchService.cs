using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class SearchService(AppDbContext db)
{
    public async Task<SearchResponse> Search(string userId, string? query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            return new SearchResponse([], []);

        var escaped = query.Trim()
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
        var pattern = $"%{escaped}%";

        var cards = await db.Cards
            .Where(c => c.UserId == userId &&
                (EF.Functions.ILike(c.Front, pattern, "\\") || EF.Functions.ILike(c.Back, pattern, "\\")))
            .OrderByDescending(c => c.CreatedAt)
            .Take(10)
            .Select(c => new CardSearchResult(c.PublicId, c.Front, c.State))
            .ToListAsync();

        var decks = await db.Decks
            .Where(d => d.UserId == userId && EF.Functions.ILike(d.Name, pattern, "\\"))
            .OrderBy(d => d.Name)
            .Take(10)
            .Select(d => new DeckSearchResult(
                d.PublicId,
                d.Name,
                d.Cards.Count))
            .ToListAsync();

        return new SearchResponse(cards, decks);
    }
}
