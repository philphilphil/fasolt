using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class DeckService(AppDbContext db)
{
    public async Task<DeckDto> CreateDeck(string userId, string name, string? description)
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Decks.Add(deck);
        await db.SaveChangesAsync();

        return new DeckDto(deck.Id, deck.Name, deck.Description, 0, 0, deck.CreatedAt);
    }

    public async Task<List<DeckDto>> ListDecks(string userId)
    {
        var now = DateTimeOffset.UtcNow;

        return await db.Decks
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.Name)
            .Select(d => new DeckDto(
                d.Id,
                d.Name,
                d.Description,
                d.Cards.Count,
                d.Cards.Count(dc => dc.Card.DueAt == null || dc.Card.DueAt <= now),
                d.CreatedAt))
            .ToListAsync();
    }

    public async Task<DeckDetailDto?> GetDeck(string userId, Guid deckId)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.Id == deckId && d.UserId == userId);

        if (deck is null) return null;

        var now = DateTimeOffset.UtcNow;

        var cards = await db.DeckCards
            .Where(dc => dc.DeckId == deckId)
            .OrderBy(dc => dc.Card.DueAt)
            .Select(dc => new DeckCardDto(dc.CardId, dc.Card.Front, dc.Card.Back, dc.Card.SourceFile, dc.Card.SourceHeading, dc.Card.State, dc.Card.DueAt))
            .ToListAsync();

        var dueCount = cards.Count(c => c.DueAt == null || c.DueAt <= now);

        return new DeckDetailDto(deck.Id, deck.Name, deck.Description, cards.Count, dueCount, cards);
    }

    public async Task<DeckDto?> UpdateDeck(string userId, Guid deckId, string name, string? description)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.Id == deckId && d.UserId == userId);

        if (deck is null) return null;

        deck.Name = name.Trim();
        deck.Description = description?.Trim();
        await db.SaveChangesAsync();

        var now = DateTimeOffset.UtcNow;
        var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == deckId);
        var dueCount = await db.DeckCards.CountAsync(dc =>
            dc.DeckId == deckId && (dc.Card.DueAt == null || dc.Card.DueAt <= now));

        return new DeckDto(deck.Id, deck.Name, deck.Description, cardCount, dueCount, deck.CreatedAt);
    }

    /// <returns>true if deleted, false if not found</returns>
    public async Task<DeleteDeckResult> DeleteDeck(string userId, Guid deckId, bool deleteCards = false)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.Id == deckId && d.UserId == userId);

        if (deck is null) return new DeleteDeckResult(false, 0);

        var cardIds = deleteCards
            ? await db.DeckCards
                .Where(dc => dc.DeckId == deckId)
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

    /// <returns>null if deck not found, false if card validation failed, true if success</returns>
    public async Task<AddCardsResult> AddCards(string userId, Guid deckId, List<Guid> cardIds)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.Id == deckId && d.UserId == userId);

        if (deck is null) return AddCardsResult.DeckNotFound;

        var userCardIds = await db.Cards
            .Where(c => c.UserId == userId && cardIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        if (userCardIds.Count != cardIds.Count)
            return AddCardsResult.CardsNotFound;

        var existingCardIds = await db.DeckCards
            .Where(dc => dc.DeckId == deckId && cardIds.Contains(dc.CardId))
            .Select(dc => dc.CardId)
            .ToListAsync();

        var newCardIds = userCardIds.Except(existingCardIds);

        foreach (var cardId in newCardIds)
        {
            db.DeckCards.Add(new DeckCard { DeckId = deckId, CardId = cardId });
        }

        await db.SaveChangesAsync();
        return AddCardsResult.Success;
    }

    /// <returns>true if removed, false if deck or card-deck link not found</returns>
    public async Task<RemoveCardResult> RemoveCard(string userId, Guid deckId, Guid cardId)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.Id == deckId && d.UserId == userId);

        if (deck is null) return RemoveCardResult.DeckNotFound;

        var deckCard = await db.DeckCards
            .FirstOrDefaultAsync(dc => dc.DeckId == deckId && dc.CardId == cardId);

        if (deckCard is null) return RemoveCardResult.CardNotFound;

        db.DeckCards.Remove(deckCard);
        await db.SaveChangesAsync();
        return RemoveCardResult.Success;
    }
}

public record DeleteDeckResult(bool Deleted, int DeletedCardCount);
public enum AddCardsResult { Success, DeckNotFound, CardsNotFound }
public enum RemoveCardResult { Success, DeckNotFound, CardNotFound }
