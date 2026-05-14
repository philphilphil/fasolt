namespace Fasolt.Server.Domain.Entities;

public class ReviewLog
{
    public long Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public Guid CardId { get; set; }
    public Card Card { get; set; } = default!;
    public string Rating { get; set; } = default!;
    public DateTimeOffset ReviewedAt { get; set; }
    public DateTimeOffset? ScheduledDueAfter { get; set; }
    public string StateAfter { get; set; } = default!;
}
