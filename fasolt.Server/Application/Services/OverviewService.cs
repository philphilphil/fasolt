using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class OverviewService(AppDbContext db)
{
    private static readonly string[] AllStates = ["new", "learning", "review", "relearning"];

    public async Task<OverviewDto> GetOverview(string userId)
    {
        var now = DateTimeOffset.UtcNow;

        // Study-active cards: no decks OR at least one active deck
        var activeCards = db.Cards
            .Where(c => c.UserId == userId)
            .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive));

        var totalCards = await activeCards.CountAsync();

        var dueCards = await activeCards.CountAsync(c =>
            c.DueAt == null || c.DueAt <= now);

        var stateCounts = await activeCards
            .GroupBy(c => c.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync();

        var cardsByState = AllStates.ToDictionary(
            s => s,
            s => stateCounts.FirstOrDefault(x => x.State == s)?.Count ?? 0);

        var totalDecks = await db.Decks.CountAsync(d => d.UserId == userId);

        var totalSources = await activeCards
            .Where(c => c.SourceFile != null)
            .Select(c => c.SourceFile)
            .Distinct()
            .CountAsync();

        return new OverviewDto(totalCards, dueCards, cardsByState, totalDecks, totalSources);
    }
}
