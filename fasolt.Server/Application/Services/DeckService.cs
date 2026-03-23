using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class DeckService(AppDbContext db)
{
    public async Task<DeckDto> CreateDeck(string userId, string name, string? description)
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = userId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Decks.Add(deck);
        await db.SaveChangesAsync();

        return new DeckDto(deck.PublicId, deck.Name, deck.Description, 0, 0, deck.CreatedAt);
    }

    public async Task<List<DeckDto>> ListDecks(string userId)
    {
        var now = DateTimeOffset.UtcNow;

        return await db.Decks
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.Name)
            .Select(d => new DeckDto(
                d.PublicId,
                d.Name,
                d.Description,
                d.Cards.Count,
                d.Cards.Count(dc => dc.Card.DueAt == null || dc.Card.DueAt <= now),
                d.CreatedAt))
            .ToListAsync();
    }

    public async Task<DeckDetailDto?> GetDeck(string userId, string publicId)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

        if (deck is null) return null;

        var now = DateTimeOffset.UtcNow;

        var cards = await db.DeckCards
            .Where(dc => dc.DeckId == deck.Id)
            .OrderBy(dc => dc.Card.DueAt)
            .Select(dc => new DeckCardDto(dc.Card.PublicId, dc.Card.Front, dc.Card.Back, dc.Card.SourceFile, dc.Card.SourceHeading, dc.Card.State, dc.Card.DueAt))
            .ToListAsync();

        var dueCount = cards.Count(c => c.DueAt == null || c.DueAt <= now);

        return new DeckDetailDto(deck.PublicId, deck.Name, deck.Description, cards.Count, dueCount, cards);
    }

    public async Task<DeckDto?> UpdateDeck(string userId, string publicId, string name, string? description)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

        if (deck is null) return null;

        deck.Name = name.Trim();
        deck.Description = description?.Trim();
        await db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id);
        var dueCount = await db.DeckCards.CountAsync(dc =>
            dc.DeckId == deck.Id && (dc.Card.DueAt == null || dc.Card.DueAt <= now));

        return new DeckDto(deck.PublicId, deck.Name, deck.Description, cardCount, dueCount, deck.CreatedAt);
    }

    /// <returns>Result with Deleted flag and DeletedCardCount</returns>
    public async Task<DeleteDeckResult> DeleteDeck(string userId, string publicId, bool deleteCards = false)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

        if (deck is null) return new DeleteDeckResult(false, 0);

        var cardIds = deleteCards
            ? await db.DeckCards
                .Where(dc => dc.DeckId == deck.Id)
                .Select(dc => dc.CardId)
                .ToListAsync()
            : [];

        db.Decks.Remove(deck);
        await db.SaveChangesAsync();

        var deletedCardCount = 0;
        if (cardIds.Count > 0)
        {
            deletedCardCount = await db.Cards
                .Where(c => cardIds.Contains(c.Id) && c.UserId == userId)
                .ExecuteDeleteAsync();
        }

        return new DeleteDeckResult(true, deletedCardCount);
    }

    /// <returns>AddCardsResult indicating Success, DeckNotFound, or CardsNotFound</returns>
    public async Task<AddCardsResult> AddCards(string userId, string deckPublicId, List<string> cardPublicIds)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);

        if (deck is null) return AddCardsResult.DeckNotFound;

        var userCards = await db.Cards
            .Where(c => c.UserId == userId && cardPublicIds.Contains(c.PublicId))
            .Select(c => new { c.Id, c.PublicId })
            .ToListAsync();

        if (userCards.Count != cardPublicIds.Count)
            return AddCardsResult.CardsNotFound;

        var userCardGuids = userCards.Select(c => c.Id).ToList();

        var existingCardIds = await db.DeckCards
            .Where(dc => dc.DeckId == deck.Id && userCardGuids.Contains(dc.CardId))
            .Select(dc => dc.CardId)
            .ToListAsync();

        var newCardIds = userCardGuids.Except(existingCardIds);

        foreach (var cardId in newCardIds)
        {
            db.DeckCards.Add(new DeckCard { DeckId = deck.Id, CardId = cardId });
        }

        await db.SaveChangesAsync();
        return AddCardsResult.Success;
    }

    /// <returns>RemoveCardResult indicating Success, DeckNotFound, or CardNotFound</returns>
    public async Task<RemoveCardResult> RemoveCard(string userId, string deckPublicId, string cardPublicId)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);

        if (deck is null) return RemoveCardResult.DeckNotFound;

        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.PublicId == cardPublicId && c.UserId == userId);

        if (card is null) return RemoveCardResult.CardNotFound;

        var deckCard = await db.DeckCards
            .FirstOrDefaultAsync(dc => dc.DeckId == deck.Id && dc.CardId == card.Id);

        if (deckCard is null) return RemoveCardResult.CardNotFound;

        db.DeckCards.Remove(deckCard);
        await db.SaveChangesAsync();
        return RemoveCardResult.Success;
    }
}

public record DeleteDeckResult(bool Deleted, int DeletedCardCount);
public enum AddCardsResult { Success, DeckNotFound, CardsNotFound }
public enum RemoveCardResult { Success, DeckNotFound, CardNotFound }
