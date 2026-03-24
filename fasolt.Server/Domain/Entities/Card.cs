using NpgsqlTypes;

namespace Fasolt.Server.Domain.Entities;

public class Card
{
    public Guid Id { get; set; }
    public string PublicId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public string? SourceFile { get; set; }
    public string? SourceHeading { get; set; }
    public string Front { get; set; } = default!;
    public string Back { get; set; } = default!;
    public string? FrontSvg { get; set; }
    public string? BackSvg { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public double? Stability { get; set; }
    public double? Difficulty { get; set; }
    public int? Step { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public string State { get; set; } = "new";
    public DateTimeOffset? LastReviewedAt { get; set; }
    public List<DeckCard> DeckCards { get; set; } = [];
    public NpgsqlTsVector SearchVector { get; set; } = null!;
}
