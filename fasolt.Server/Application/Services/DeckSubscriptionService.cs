using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

/// <summary>
/// Link mode: subscribing to a shared deck, unlinking again, and converting a
/// linked deck into an owned copy. A <see cref="DeckSubscription"/> row *is* the
/// linked deck — there are no cloned cards, so the subscriber always sees the
/// owner's current content.
/// </summary>
public class DeckSubscriptionService(AppDbContext db)
{
    /// <summary>
    /// Links a shared deck into the caller's account. Idempotent: subscribing to a
    /// deck twice keeps the first subscription (and its pause state) untouched.
    /// </summary>
    public async Task<SubscribeResult> Subscribe(string userId, string deckPublicId)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.Visibility != DeckVisibility.Private);

        if (deck is null) return new SubscribeResult(SubscribeError.NotFound, null, false);
        if (deck.UserId == userId) return new SubscribeResult(SubscribeError.OwnDeck, null, false);

        var subscription = await db.DeckSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.DeckId == deck.Id);

        var created = false;

        if (subscription is null)
        {
            subscription = new DeckSubscription
            {
                UserId = userId,
                DeckId = deck.Id,
                SubscribedAt = DateTimeOffset.UtcNow,
            };
            db.DeckSubscriptions.Add(subscription);

            try
            {
                await db.SaveChangesAsync();
                created = true;
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                // A concurrent subscribe won the race. Keep its row — subscribing is
                // idempotent, so this request reports the existing link as success.
                db.Entry(subscription).State = EntityState.Detached;
                subscription = await db.DeckSubscriptions
                    .FirstAsync(s => s.UserId == userId && s.DeckId == deck.Id);
            }
        }

        return new SubscribeResult(
            SubscribeError.None,
            await ToLinkedDto(deck, userId, subscription.IsSuspended),
            created);
    }

    /// <summary>
    /// Drops the link and the SRS rows that only existed because of it. Cards the
    /// caller still reaches through another linked deck keep their state, and cards
    /// the caller authored are never touched.
    /// </summary>
    public async Task<bool> Unsubscribe(string userId, string deckPublicId)
    {
        var subscription = await db.DeckSubscriptions
            .Include(s => s.Deck)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Deck.PublicId == deckPublicId);

        if (subscription is null) return false;

        await using var transaction = await db.Database.BeginTransactionAsync();

        await DeleteOrphanedReviewStates(db, userId, subscription.DeckId);
        db.DeckSubscriptions.Remove(subscription);
        await db.SaveChangesAsync();

        await transaction.CommitAsync();
        return true;
    }

    /// <summary>
    /// Turns a linked deck into an owned copy: clones the deck and its cards like a
    /// library copy, but carries the caller's SRS state over to the new cards instead
    /// of starting fresh. The clone, the re-keyed state and the dropped subscription
    /// all land in one transaction.
    /// </summary>
    public async Task<ConvertToCopyResult> ConvertToCopy(string userId, string deckPublicId)
    {
        var subscription = await db.DeckSubscriptions
            .Include(s => s.Deck)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Deck.PublicId == deckPublicId);

        if (subscription is null) return new ConvertToCopyResult(ConvertToCopyError.NotFound, null);

        var source = subscription.Deck;

        var sourceCards = await db.DeckCards
            .Where(dc => dc.DeckId == source.Id)
            .OrderBy(dc => dc.Card.CreatedAt)
            .Select(dc => new
            {
                dc.CardId,
                dc.Card.Front,
                dc.Card.Back,
                dc.Card.FrontSvg,
                dc.Card.BackSvg,
            })
            .ToListAsync();

        // Same ceiling as the copy import — a linked deck may have grown past it
        // after the owner unlisted and relisted it.
        if (sourceCards.Count > PublishingService.MaxCardsInPublicDeck)
            return new ConvertToCopyResult(ConvertToCopyError.DeckTooLarge, null);

        var authorHandle = await db.Users
            .Where(u => u.Id == source.UserId)
            .Select(u => u.Handle)
            .FirstOrDefaultAsync();

        var now = DateTimeOffset.UtcNow;
        var copy = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = userId,
            Name = source.Name,
            Description = source.Description,
            CreatedAt = now,
            Visibility = DeckVisibility.Private,
            CopiedFromDeckPublicId = source.PublicId,
            CopiedFromHandle = authorHandle,
        };

        await using var transaction = await db.Database.BeginTransactionAsync();

        db.Decks.Add(copy);

        var newCardIdBySourceId = new Dictionary<Guid, Guid>(sourceCards.Count);
        foreach (var sourceCard in sourceCards)
        {
            var card = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                UserId = userId,
                SourceFile = null,
                Front = sourceCard.Front,
                Back = sourceCard.Back,
                FrontSvg = sourceCard.FrontSvg,
                BackSvg = sourceCard.BackSvg,
                CreatedAt = now,
            };
            newCardIdBySourceId[sourceCard.CardId] = card.Id;
            db.Cards.Add(card);
            db.DeckCards.Add(new DeckCard { DeckId = copy.Id, CardId = card.Id });
        }

        var states = await ReviewStateQuery.LoadForCardsAsync(db, userId, newCardIdBySourceId.Keys.ToList());
        foreach (var (sourceCardId, state) in states)
        {
            db.ReviewStates.Add(new ReviewState
            {
                UserId = userId,
                CardId = newCardIdBySourceId[sourceCardId],
                Stability = state.Stability,
                Difficulty = state.Difficulty,
                Step = state.Step,
                DueAt = state.DueAt,
                State = state.State,
                LastReviewedAt = state.LastReviewedAt,
                IsSuspended = state.IsSuspended,
            });

            // The source rows are removed by the unlink cleanup below; stop tracking
            // them so nothing tries to write them back afterwards.
            db.Entry(state).State = EntityState.Detached;
        }

        await db.SaveChangesAsync();

        await DeleteOrphanedReviewStates(db, userId, source.Id);
        db.DeckSubscriptions.Remove(subscription);
        await db.SaveChangesAsync();

        // A conversion is an import like any other. Atomic increment for the same
        // reason as in LibraryService.CopyDeck.
        await db.Decks
            .Where(d => d.Id == source.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.CopyCount, d => d.CopyCount + 1));

        await transaction.CommitAsync();

        return new ConvertToCopyResult(
            ConvertToCopyError.None,
            new DeckDto(
                copy.PublicId, copy.Name, copy.Description, sourceCards.Count, sourceCards.Count,
                copy.CreatedAt, copy.IsSuspended,
                copy.Visibility.ToWire(), copy.PublishedAt, copy.CopyCount,
                copy.CopiedFromDeckPublicId, copy.CopiedFromHandle));
    }

    /// <summary>
    /// Drops every subscription to a deck and cleans up the subscribers' SRS rows.
    /// Called when the deck goes private or is deleted — a link that can no longer
    /// be resolved must not linger in anyone's deck list.
    /// </summary>
    public static async Task RemoveAllSubscriptions(AppDbContext db, Guid deckId)
    {
        var hasSubscribers = await db.DeckSubscriptions.AnyAsync(s => s.DeckId == deckId);
        if (!hasSubscribers) return;

        // Same rule as a single unlink, applied per subscriber: drop the rows for
        // cards this deck was the only route to, keep everything else.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "ReviewStates" rs
            USING "DeckSubscriptions" ds, "DeckCards" dc
            WHERE ds."DeckId" = {deckId}
              AND dc."DeckId" = {deckId}
              AND rs."UserId" = ds."UserId"
              AND rs."CardId" = dc."CardId"
              AND NOT EXISTS (
                  SELECT 1 FROM "Cards" c
                  WHERE c."Id" = rs."CardId" AND c."UserId" = rs."UserId")
              AND NOT EXISTS (
                  SELECT 1 FROM "DeckCards" dc2
                  JOIN "DeckSubscriptions" ds2 ON ds2."DeckId" = dc2."DeckId"
                  WHERE dc2."CardId" = rs."CardId"
                    AND ds2."UserId" = rs."UserId"
                    AND ds2."DeckId" <> {deckId})
            """);

        await db.DeckSubscriptions.Where(s => s.DeckId == deckId).ExecuteDeleteAsync();

        // ExecuteDelete bypasses the change tracker. Any subscription still tracked
        // here would be deleted a second time when the caller saves — e.g. as a
        // cascade of removing the deck itself — and fail on zero rows affected.
        foreach (var entry in db.ChangeTracker.Entries<DeckSubscription>()
                     .Where(e => e.Entity.DeckId == deckId)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <summary>
    /// Removes the caller's SRS rows for cards that this deck was their only route
    /// to. Authored cards and cards in the caller's other linked decks are kept.
    /// </summary>
    private static Task DeleteOrphanedReviewStates(AppDbContext db, string userId, Guid deckId) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "ReviewStates" rs
            USING "DeckCards" dc
            WHERE rs."UserId" = {userId}
              AND rs."CardId" = dc."CardId"
              AND dc."DeckId" = {deckId}
              AND NOT EXISTS (
                  SELECT 1 FROM "Cards" c
                  WHERE c."Id" = rs."CardId" AND c."UserId" = {userId})
              AND NOT EXISTS (
                  SELECT 1 FROM "DeckCards" dc2
                  JOIN "DeckSubscriptions" ds ON ds."DeckId" = dc2."DeckId"
                  WHERE dc2."CardId" = rs."CardId"
                    AND ds."UserId" = {userId}
                    AND ds."DeckId" <> {deckId})
            """);

    private async Task<DeckDto> ToLinkedDto(Deck deck, string userId, bool isPaused)
    {
        var now = DateTimeOffset.UtcNow;
        var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id);
        var dueCount = await db.DeckCards.CountAsync(dc =>
            dc.DeckId == deck.Id && !dc.Card.ReviewStates.Any(r =>
                r.UserId == userId && (r.IsSuspended || r.DueAt > now)));
        var authorHandle = await db.Users
            .Where(u => u.Id == deck.UserId)
            .Select(u => u.Handle)
            .FirstOrDefaultAsync();

        // IsSuspended is the subscriber's own pause — the owner's deck pause never
        // reaches subscribers.
        return new DeckDto(
            deck.PublicId, deck.Name, deck.Description, cardCount, dueCount,
            deck.CreatedAt, isPaused,
            deck.Visibility.ToWire(), deck.PublishedAt, deck.CopyCount,
            deck.CopiedFromDeckPublicId, deck.CopiedFromHandle,
            IsLinked: true, AuthorHandle: authorHandle);
    }
}

public enum SubscribeError { None, NotFound, OwnDeck }

/// <param name="Created">False when the caller was already subscribed.</param>
public record SubscribeResult(SubscribeError Error, DeckDto? Deck, bool Created);

public enum ConvertToCopyError { None, NotFound, DeckTooLarge }

public record ConvertToCopyResult(ConvertToCopyError Error, DeckDto? Deck);
