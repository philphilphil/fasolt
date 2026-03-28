using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public record DueCardSummary(int TotalDue, string Breakdown);

public static class DueCardQuery
{
    public static async Task<DueCardSummary> GetDueCardSummary(
        AppDbContext db, string userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var dueCardsByDeck = await db.Cards
            .Where(c => c.UserId == userId && (c.DueAt == null || c.DueAt <= now))
            .Where(c => !c.IsSuspended)
            .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended))
            .SelectMany(c => c.DeckCards.DefaultIfEmpty(),
                (card, deckCard) => new { DeckName = deckCard != null ? deckCard.Deck.Name : null })
            .GroupBy(x => x.DeckName ?? "Unsorted")
            .Select(g => new { DeckName = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalDue = dueCardsByDeck.Sum(g => g.Count);
        var breakdown = totalDue == 0
            ? ""
            : string.Join(", ", dueCardsByDeck.OrderByDescending(g => g.Count).Select(g => $"{g.Count} in {g.DeckName}"));

        return new DueCardSummary(totalDue, breakdown);
    }
}
