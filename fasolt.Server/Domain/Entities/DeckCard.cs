namespace Fasolt.Server.Domain.Entities;

public class DeckCard
{
    public Guid DeckId { get; set; }
    public Deck Deck { get; set; } = default!;
    public Guid CardId { get; set; }
    public Card Card { get; set; } = default!;
}
