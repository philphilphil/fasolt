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

        return ToDto(deck, 0, 0);
    }

    /// <summary>
    /// The caller's own decks plus the decks they have linked. Card and due counts
    /// are always computed from the caller's own <see cref="ReviewState"/> rows, so a
    /// linked deck shows the subscriber's progress, not the author's.
    /// </summary>
    public async Task<List<DeckDto>> ListDecks(string userId)
    {
        var now = DateTimeOffset.UtcNow;

        // Projected to an anonymous shape first: DeckVisibility.ToWire() has no SQL
        // translation, so the enum→string mapping happens client-side.
        var owned = await db.Decks
            .Where(d => d.UserId == userId)
            .Select(d => new
            {
                Deck = d,
                IsLinked = false,
                AuthorHandle = (string?)null,
                IsSuspended = d.IsSuspended,
                CardCount = d.Cards.Count,
                DueCount = d.Cards.Count(dc => !dc.Card.ReviewStates.Any(r =>
                    r.UserId == userId && (r.IsSuspended || r.DueAt > now))),
            })
            .ToListAsync();

        var linked = await db.DeckSubscriptions
            .Where(s => s.UserId == userId)
            .Select(s => new
            {
                Deck = s.Deck,
                IsLinked = true,
                AuthorHandle = s.Deck.User.Handle,
                // The subscriber's own pause, never the owner's.
                IsSuspended = s.IsSuspended,
                CardCount = s.Deck.Cards.Count,
                DueCount = s.Deck.Cards.Count(dc => !dc.Card.ReviewStates.Any(r =>
                    r.UserId == userId && (r.IsSuspended || r.DueAt > now))),
            })
            .ToListAsync();

        return owned.Concat(linked)
            .OrderBy(r => r.Deck.Name)
            .Select(r => ToDto(r.Deck, r.CardCount, r.DueCount, r.IsLinked, r.AuthorHandle, r.IsSuspended))
            .ToList();
    }

    private static DeckDto ToDto(
        Deck deck, int cardCount, int dueCount,
        bool isLinked = false, string? authorHandle = null, bool? isSuspended = null) => new(
        deck.PublicId,
        deck.Name,
        deck.Description,
        cardCount,
        dueCount,
        deck.CreatedAt,
        isSuspended ?? deck.IsSuspended,
        deck.Visibility.ToWire(),
        deck.PublishedAt,
        deck.CopyCount,
        deck.CopiedFromDeckPublicId,
        deck.CopiedFromHandle,
        isLinked,
        authorHandle);

    public async Task<DeckDetailDto?> GetDeck(string userId, string publicId)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

        DeckSubscription? subscription = null;
        if (deck is null)
        {
            subscription = await db.DeckSubscriptions
                .Include(s => s.Deck)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Deck.PublicId == publicId);

            if (subscription is null) return null;
            deck = subscription.Deck;
        }

        var isLinked = subscription is not null;
        var now = DateTimeOffset.UtcNow;

        var cards = await (
            from dc in db.DeckCards.Where(dc => dc.DeckId == deck.Id)
            join r in db.ReviewStates.Where(r => r.UserId == userId) on dc.CardId equals r.CardId into g
            from rs in g.DefaultIfEmpty()
            orderby rs.DueAt
            select new DeckCardDto(
                dc.Card.PublicId, dc.Card.Front, dc.Card.Back,
                // Never expose the author's vault path to a subscriber.
                isLinked ? null : dc.Card.SourceFile,
                rs.State ?? "new", rs.DueAt,
                rs != null && rs.IsSuspended,
                rs.Stability, rs.Difficulty, rs.Step, rs.LastReviewedAt,
                dc.Card.FrontSvg, dc.Card.BackSvg))
            .ToListAsync();

        var dueCount = cards.Count(c => !c.IsSuspended && (c.DueAt == null || c.DueAt <= now));

        var authorHandle = isLinked
            ? await db.Users.Where(u => u.Id == deck.UserId).Select(u => u.Handle).FirstOrDefaultAsync()
            : null;

        return new DeckDetailDto(
            deck.PublicId, deck.Name, deck.Description, cards.Count, dueCount, cards,
            subscription?.IsSuspended ?? deck.IsSuspended,
            deck.Visibility.ToWire(), deck.PublishedAt, deck.CopyCount,
            deck.CopiedFromDeckPublicId, deck.CopiedFromHandle,
            isLinked, authorHandle);
    }

    public async Task<DeckDto?> UpdateDeck(string userId, string publicId, string name, string? description)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

        if (deck is null)
        {
            await ThrowIfLinked(userId, publicId);
            return null;
        }

        deck.Name = name.Trim();
        deck.Description = description?.Trim();
        await db.SaveChangesAsync();

        return await ToDtoWithCounts(deck, userId);
    }

    /// <summary>
    /// Pauses or resumes a deck for the caller. On an owned deck that is the owner's
    /// <see cref="Deck.IsSuspended"/>; on a linked deck it is the caller's own
    /// <see cref="DeckSubscription.IsSuspended"/>, which leaves the author and every
    /// other subscriber alone.
    /// </summary>
    public async Task<DeckDto?> SetSuspended(string userId, string publicId, bool isSuspended)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

        if (deck is null)
        {
            var subscription = await db.DeckSubscriptions
                .Include(s => s.Deck)
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Deck.PublicId == publicId);

            if (subscription is null) return null;

            subscription.IsSuspended = isSuspended;
            await db.SaveChangesAsync();

            return await ToDtoWithCounts(subscription.Deck, userId, subscription);
        }

        deck.IsSuspended = isSuspended;
        await db.SaveChangesAsync();

        return await ToDtoWithCounts(deck, userId);
    }

    private async Task<DeckDto> ToDtoWithCounts(Deck deck, string userId, DeckSubscription? subscription = null)
    {
        var now = DateTimeOffset.UtcNow;
        var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id);
        var dueCount = await db.DeckCards.CountAsync(dc =>
            dc.DeckId == deck.Id && !dc.Card.ReviewStates.Any(r =>
                r.UserId == userId && (r.IsSuspended || r.DueAt > now)));

        if (subscription is null) return ToDto(deck, cardCount, dueCount);

        var authorHandle = await db.Users
            .Where(u => u.Id == deck.UserId)
            .Select(u => u.Handle)
            .FirstOrDefaultAsync();

        return ToDto(deck, cardCount, dueCount, isLinked: true, authorHandle, subscription.IsSuspended);
    }

    /// <summary>
    /// Turns the "not one of your decks" case into a 403 when the deck is one the
    /// caller has linked: it exists and they can see it, they just cannot change it.
    /// </summary>
    private async Task ThrowIfLinked(string userId, string deckPublicId)
    {
        var linked = await db.DeckSubscriptions
            .AnyAsync(s => s.UserId == userId && s.Deck.PublicId == deckPublicId);

        if (linked) throw LinkedContentException.Deck();
    }

    /// <summary>
    /// Same for cards: an id that names content the caller reaches through a linked
    /// deck is refused as linked rather than reported missing.
    /// </summary>
    private async Task ThrowIfAnyLinkedCard(string userId, List<string> cardPublicIds)
    {
        var linked = await LinkedDeckQuery.StudyableCards(db, userId)
            .AnyAsync(c => cardPublicIds.Contains(c.PublicId) && c.UserId != userId);

        if (linked) throw LinkedContentException.Card();
    }

    /// <returns>Result with Deleted flag and DeletedCardCount</returns>
    public async Task<DeleteDeckResult> DeleteDeck(string userId, string publicId, bool deleteCards = false)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == publicId && d.UserId == userId);

        if (deck is null)
        {
            await ThrowIfLinked(userId, publicId);
            return new DeleteDeckResult(false, 0);
        }

        var cardIds = deleteCards
            ? await db.DeckCards
                .Where(dc => dc.DeckId == deck.Id)
                .Select(dc => dc.CardId)
                .ToListAsync()
            : [];

        // Everything below is one transaction: unlinking the subscribers and wiping
        // their progress must not survive a failure that leaves the deck itself in
        // place, still published.
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Locked first, in the same order Subscribe takes its locks, so the two cannot
        // deadlock and a subscribe racing this delete is either seen by the cleanup
        // below or finds no deck at all.
        await DeckSubscriptionService.LockDeck(db, deck.Id);

        // Subscribers lose the deck with it. Done before the delete cascades the
        // subscription rows away, so their orphaned SRS rows can still be found.
        await DeckSubscriptionService.RemoveAllSubscriptions(db, deck.Id);

        // Delete snapshots for this deck
        await db.DeckSnapshots
            .Where(s => s.DeckId == deck.Id)
            .ExecuteDeleteAsync();

        db.Decks.Remove(deck);
        await db.SaveChangesAsync();

        var deletedCardCount = 0;
        if (cardIds.Count > 0)
        {
            deletedCardCount = await db.Cards
                .Where(c => cardIds.Contains(c.Id) && c.UserId == userId)
                .ExecuteDeleteAsync();
        }

        await transaction.CommitAsync();

        return new DeleteDeckResult(true, deletedCardCount);
    }

    /// <returns>AddCardsResult indicating Success, DeckNotFound, or CardsNotFound</returns>
    public async Task<AddCardsResult> AddCards(string userId, string deckPublicId, List<string> cardPublicIds)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);

        if (deck is null)
        {
            await ThrowIfLinked(userId, deckPublicId);
            return AddCardsResult.DeckNotFound;
        }

        var userCards = await db.Cards
            .Where(c => c.UserId == userId && cardPublicIds.Contains(c.PublicId))
            .Select(c => new { c.Id, c.PublicId })
            .ToListAsync();

        if (userCards.Count != cardPublicIds.Count)
        {
            // A card the caller only reaches through a subscription is not missing —
            // it belongs to the author and cannot be filed into a deck of the caller's
            // own. Saying so beats reporting a card they can read as non-existent.
            await ThrowIfAnyLinkedCard(userId, cardPublicIds);
            return AddCardsResult.CardsNotFound;
        }

        var userCardGuids = userCards.Select(c => c.Id).ToList();

        var existingCardIds = await db.DeckCards
            .Where(dc => dc.DeckId == deck.Id && userCardGuids.Contains(dc.CardId))
            .Select(dc => dc.CardId)
            .ToListAsync();

        var newCardIds = userCardGuids.Except(existingCardIds).ToList();

        if (await PublishingService.WouldExceedPublicCardCap(db, deck.Id, newCardIds.Count))
            return AddCardsResult.PublishedDeckFull;

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

        if (deck is null)
        {
            await ThrowIfLinked(userId, deckPublicId);
            return RemoveCardResult.DeckNotFound;
        }

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

    public async Task<RemoveCardsResult> RemoveCards(string userId, string deckPublicId, List<string> cardPublicIds)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);

        if (deck is null)
        {
            await ThrowIfLinked(userId, deckPublicId);
            return new RemoveCardsResult(false, 0);
        }

        var cardIds = await db.Cards
            .Where(c => c.UserId == userId && cardPublicIds.Contains(c.PublicId))
            .Select(c => c.Id)
            .ToListAsync();

        var removed = await db.DeckCards
            .Where(dc => dc.DeckId == deck.Id && cardIds.Contains(dc.CardId))
            .ExecuteDeleteAsync();

        return new RemoveCardsResult(true, removed);
    }
}

public record DeleteDeckResult(bool Deleted, int DeletedCardCount);
public enum AddCardsResult { Success, DeckNotFound, CardsNotFound, PublishedDeckFull }
public enum RemoveCardResult { Success, DeckNotFound, CardNotFound }
public record RemoveCardsResult(bool DeckFound, int RemovedCount);
