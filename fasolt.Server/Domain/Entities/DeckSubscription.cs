namespace Fasolt.Server.Domain.Entities;

/// <summary>
/// A linked deck: the subscriber studies the owner's actual cards, so the deck
/// stays in sync with whatever the owner changes. One row per (subscriber, deck)
/// — the row itself is the link. Removed when the owner unpublishes or deletes
/// the deck, and when the subscriber unlinks or converts it to a copy.
/// </summary>
public class DeckSubscription
{
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public Guid DeckId { get; set; }
    public Deck Deck { get; set; } = default!;

    public DateTimeOffset SubscribedAt { get; set; }

    /// <summary>
    /// The subscriber's own pause for this deck, independent of the owner's
    /// <see cref="Deck.IsSuspended"/> (which only ever applies to the owner).
    /// </summary>
    public bool IsSuspended { get; set; } = false;
}
