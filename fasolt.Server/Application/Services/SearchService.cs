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

        var terms = query.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => "%" + t.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%")
            .ToList();

        var cardsQuery = db.Cards.Where(c => c.UserId == userId);
        foreach (var term in terms)
            cardsQuery = cardsQuery.Where(c =>
                EF.Functions.ILike(c.Front, term, "\\") || EF.Functions.ILike(c.Back, term, "\\"));

        var cards = await cardsQuery
            .OrderByDescending(c => c.CreatedAt)
            .Take(10)
            .Select(c => new CardSearchResult(c.PublicId, c.Front, c.State))
            .ToListAsync();

        var decksQuery = db.Decks.Where(d => d.UserId == userId);
        foreach (var term in terms)
            decksQuery = decksQuery.Where(d => EF.Functions.ILike(d.Name, term, "\\"));

        var decks = await decksQuery
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
