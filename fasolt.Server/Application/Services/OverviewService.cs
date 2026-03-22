using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class OverviewService(AppDbContext db)
{
    private static readonly string[] AllStates = ["new", "learning", "review"];

    public async Task<OverviewDto> GetOverview(string userId)
    {
        var now = DateTimeOffset.UtcNow;

        var totalCards = await db.Cards.CountAsync(c => c.UserId == userId);

        var dueCards = await db.Cards.CountAsync(c =>
            c.UserId == userId && (c.DueAt == null || c.DueAt <= now));

        var stateCounts = await db.Cards
            .Where(c => c.UserId == userId)
            .GroupBy(c => c.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync();

        var cardsByState = AllStates.ToDictionary(
            s => s,
            s => stateCounts.FirstOrDefault(x => x.State == s)?.Count ?? 0);

        var totalDecks = await db.Decks.CountAsync(d => d.UserId == userId);

        var totalSources = await db.Cards
            .Where(c => c.UserId == userId && c.SourceFile != null)
            .Select(c => c.SourceFile)
            .Distinct()
            .CountAsync();

        return new OverviewDto(totalCards, dueCards, cardsByState, totalDecks, totalSources);
    }
}
