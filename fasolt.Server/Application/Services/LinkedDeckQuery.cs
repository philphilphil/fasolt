using System.Linq.Expressions;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

/// <summary>
/// Helpers for linked decks — decks the user studies through a
/// <see cref="DeckSubscription"/> rather than owning. Their cards belong to the
/// author, so everything the subscriber sees is derived from their own
/// <see cref="ReviewState"/> rows and their own subscription row.
/// </summary>
public static class LinkedDeckQuery
{
    /// <summary>
    /// Cards the user studies: the ones they authored plus every card in a deck
    /// they subscribe to. Card–deck membership is always owner-local (both the
    /// deck and the card belong to the same account), so a card reached through a
    /// subscription is never one of the caller's own.
    /// </summary>
    public static IQueryable<Card> StudyableCards(AppDbContext db, string userId) =>
        db.Cards.Where(c => c.UserId == userId
            || c.DeckCards.Any(dc => dc.Deck.Subscriptions.Any(s => s.UserId == userId)));

    /// <summary>
    /// Cards not paused through a deck. For authored cards that is the owner's own
    /// <see cref="Deck.IsSuspended"/> (a card in no deck is always active); for
    /// linked cards it is the subscriber's <see cref="DeckSubscription.IsSuspended"/> —
    /// the owner's deck pause never reaches subscribers.
    /// </summary>
    public static Expression<Func<Card, bool>> NotDeckPausedFor(string userId) =>
        c => (c.UserId == userId
                && (!c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended)))
            || (c.UserId != userId
                && c.DeckCards.Any(dc => dc.Deck.Subscriptions.Any(s => s.UserId == userId && !s.IsSuspended)));
}

/// <summary>
/// Thrown when the caller tries to mutate content they only reach through a
/// subscription. The deck and its cards belong to the author; the endpoints
/// translate this into a 403 rather than the 404 an unrelated deck would get.
/// </summary>
public class LinkedContentException(string message) : Exception(message)
{
    public const string ErrorCode = "linked_content";

    public static LinkedContentException Deck() => new(
        "This deck is linked from another account. Convert it to a copy to make changes.");

    public static LinkedContentException Card() => new(
        "This card belongs to a deck linked from another account. "
        + "Convert the deck to a copy to make changes.");
}
