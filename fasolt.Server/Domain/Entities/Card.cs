using NpgsqlTypes;

namespace Fasolt.Server.Domain.Entities;

public class Card
{
    public Guid Id { get; set; }
    public string PublicId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public string? SourceFile { get; set; }
    public string Front { get; set; } = default!;
    public string Back { get; set; } = default!;
    public string? FrontSvg { get; set; }
    public string? BackSvg { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<DeckCard> DeckCards { get; set; } = [];

    /// <summary>Per-user SRS state. At most one row per user; absent means "new".</summary>
    public List<ReviewState> ReviewStates { get; set; } = [];

    public NpgsqlTsVector SearchVector { get; set; } = null!;
}
