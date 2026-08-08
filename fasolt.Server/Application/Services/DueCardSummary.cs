using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public record DueCardSummary(int TotalDue, string Breakdown);

public static class DueCardQuery
{
    /// <summary>Bucket for due cards in no deck the user can see.</summary>
    private const string UnsortedName = "Unsorted";

    public static async Task<DueCardSummary> GetDueCardSummary(
        AppDbContext db, string userId, DateTimeOffset now, CancellationToken ct = default)
    {
        // The same set the study queue serves — authored cards plus the cards of every
        // deck the user links — so a push notification can never advertise a different
        // number from the one the app then shows.
        var dueCards = LinkedDeckQuery.StudyableCards(db, userId)
            .Where(ReviewStateQuery.DueBy(userId, now))
            .Where(ReviewStateQuery.NotSuspendedBy(userId))
            .Where(LinkedDeckQuery.NotDeckPausedFor(userId));

        // Counted over cards, not deck memberships: a card in two decks is one card due.
        var totalDue = await dueCards.CountAsync(ct);

        if (totalDue == 0) return new DueCardSummary(0, "");

        // Each due card is attributed to exactly one deck — the alphabetically first
        // of the visible decks holding it — so the parts of the breakdown sum to
        // totalDue. Attributing a card to every deck it sits in read as corrupt:
        // "3 cards due — 3 in A, 3 in B". Cards in no visible deck fall to Unsorted,
        // which is the same group query rather than a second count.
        var byDeck = await dueCards
            .GroupBy(c => c.DeckCards
                .Where(dc => dc.Deck.UserId == userId || dc.Deck.Subscriptions.Any(s => s.UserId == userId))
                .OrderBy(dc => dc.Deck.Name)
                .Select(dc => dc.Deck.Name)
                .FirstOrDefault())
            .Select(g => new { DeckName = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Unsorted keeps its old place: last among decks with an equal count.
        var breakdown = string.Join(", ", byDeck
            .Select(g => new { DeckName = g.DeckName ?? UnsortedName, g.Count })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.DeckName == UnsortedName)
            .Select(g => $"{g.Count} in {g.DeckName}"));

        return new DueCardSummary(totalDue, breakdown);
    }
}
