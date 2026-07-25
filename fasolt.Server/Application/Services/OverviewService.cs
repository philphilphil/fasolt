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

        // Study-active cards: authored or linked, minus everything the user paused
        var activeCards = LinkedDeckQuery.StudyableCards(db, userId)
            .Where(ReviewStateQuery.NotSuspendedBy(userId))
            .Where(LinkedDeckQuery.NotDeckPausedFor(userId));

        var totalCards = await activeCards.CountAsync();

        var dueCards = await activeCards.CountAsync(ReviewStateQuery.DueBy(userId, now));

        var stateCounts = await (
                from c in activeCards
                join r in db.ReviewStates.Where(r => r.UserId == userId) on c.Id equals r.CardId into g
                from rs in g.DefaultIfEmpty()
                group c by rs.State ?? "new" into grouped
                select new { State = grouped.Key, Count = grouped.Count() })
            .ToListAsync();

        var cardsByState = AllStates.ToDictionary(
            s => s,
            s => stateCounts.FirstOrDefault(x => x.State == s)?.Count ?? 0);

        var totalDecks = await db.Decks.CountAsync(d => d.UserId == userId)
            + await db.DeckSubscriptions.CountAsync(s => s.UserId == userId);

        // Sources stay authored-only: a linked card's SourceFile belongs to its author.
        var totalSources = await activeCards
            .Where(c => c.UserId == userId)
            .Where(c => c.SourceFile != null)
            .Select(c => c.SourceFile)
            .Distinct()
            .CountAsync();

        return new OverviewDto(totalCards, dueCards, cardsByState, totalDecks, totalSources);
    }

    public async Task<OverviewIdentityDto?> GetIdentity(string userId)
    {
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new OverviewIdentityDto(
                u.Email!,
                u.ExternalProvider != null ? u.UserName : null,
                u.ExternalProvider))
            .FirstOrDefaultAsync();
    }
}
