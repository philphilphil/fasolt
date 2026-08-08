namespace Fasolt.Server.Domain.Entities;

public class ReviewLog
{
    public long Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    /// <summary>
    /// Null once the card is gone. Deleting a card must not erase the review from the
    /// reviewer's history: on a linked deck the card belongs to the author, and
    /// cascading would retroactively shrink every subscriber's streak and totals.
    /// </summary>
    public Guid? CardId { get; set; }
    public Card? Card { get; set; }
    public string Rating { get; set; } = default!;
    public DateTimeOffset ReviewedAt { get; set; }
    public DateTimeOffset? ScheduledDueAfter { get; set; }
    public string StateAfter { get; set; } = default!;
}
