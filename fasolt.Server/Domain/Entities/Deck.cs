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
    public bool IsActive { get; set; } = true;
    public List<DeckCard> Cards { get; set; } = [];
    public NpgsqlTsVector SearchVector { get; set; } = null!;
}
