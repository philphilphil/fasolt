using NpgsqlTypes;

namespace Fasolt.Server.Domain.Entities;

public class Card
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public string? SourceFile { get; set; }
    public string? SourceHeading { get; set; }
    public string Front { get; set; } = default!;
    public string Back { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }

    public double EaseFactor { get; set; } = 2.5;
    public int Interval { get; set; }
    public int Repetitions { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public string State { get; set; } = "new";
    public DateTimeOffset? LastReviewedAt { get; set; }
    public List<DeckCard> DeckCards { get; set; } = [];
    public NpgsqlTsVector SearchVector { get; set; } = null!;
}
