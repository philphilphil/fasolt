using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public record DueCardSummary(int TotalDue, string Breakdown);

public static class DueCardQuery
{
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

        // Counted over cards, not deck memberships: a card in two decks is one card
        // due, however often it shows up in the breakdown below.
        var totalDue = await dueCards.CountAsync(ct);

        if (totalDue == 0) return new DueCardSummary(0, "");

        var byDeck = await dueCards
            .SelectMany(
                c => c.DeckCards.Where(dc =>
                    dc.Deck.UserId == userId || dc.Deck.Subscriptions.Any(s => s.UserId == userId)),
                (card, deckCard) => deckCard.Deck.Name)
            .GroupBy(name => name)
            .Select(g => new { DeckName = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var unsorted = await dueCards.CountAsync(
            c => !c.DeckCards.Any(dc =>
                dc.Deck.UserId == userId || dc.Deck.Subscriptions.Any(s => s.UserId == userId)),
            ct);

        if (unsorted > 0)
            byDeck.Add(new { DeckName = "Unsorted", Count = unsorted });

        var breakdown = string.Join(", ", byDeck
            .OrderByDescending(g => g.Count)
            .Select(g => $"{g.Count} in {g.DeckName}"));

        return new DueCardSummary(totalDue, breakdown);
    }
}
