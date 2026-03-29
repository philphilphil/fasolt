namespace Fasolt.Server.Domain.Entities;

public class DeckSnapshot
{
    public Guid Id { get; set; }
    public string PublicId { get; set; } = default!;
    public Guid? DeckId { get; set; }
    public Deck? Deck { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public int Version { get; set; } = 1;
    public int CardCount { get; set; }
    public string Data { get; set; } = default!; // jsonb
    public DateTimeOffset CreatedAt { get; set; }
}
