using NpgsqlTypes;

namespace Fasolt.Server.Domain.Entities;

public class Deck
{
    public Guid Id { get; set; }
    public string PublicId { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsSuspended { get; set; } = false;

    public DeckVisibility Visibility { get; set; } = DeckVisibility.Private;

    /// <summary>Set when the deck first became non-private; cleared when it goes back to Private.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>How many times this deck has been copied. Drives the library's "popular" sort.</summary>
    public int CopyCount { get; set; } = 0;

    /// <summary>Public id of the deck this one was copied from, if any.</summary>
    public string? CopiedFromDeckPublicId { get; set; }

    /// <summary>Author handle at copy time — a snapshot, not a foreign key.</summary>
    public string? CopiedFromHandle { get; set; }

    public List<DeckCard> Cards { get; set; } = [];
    public NpgsqlTsVector SearchVector { get; set; } = null!;
}
