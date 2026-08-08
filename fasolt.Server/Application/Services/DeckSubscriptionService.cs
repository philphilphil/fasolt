using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        await using var transaction = await db.Database.BeginTransactionAsync();

        // The deck row is locked for the rest of the transaction. Unpublishing takes
        // the same lock before it removes the links, so the deck cannot go private
        // between the visibility check and the insert below — which would leave a
        // subscription to a private deck that nothing ever cleans up. It also
        // serialises concurrent subscribes to the same deck, so the existence check
        // is enough to keep this idempotent.
        var deck = await LockDeck(db, deckPublicId);

        if (deck is null || deck.Visibility == DeckVisibility.Private)
            return new SubscribeResult(SubscribeError.NotFound, null, false);
        if (deck.UserId == userId) return new SubscribeResult(SubscribeError.OwnDeck, null, false);

        var subscription = await db.DeckSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.DeckId == deck.Id);

        var created = false;

        if (subscription is null)
        {
            // Same ceiling as the copy import. Without it an unlisted deck of any size
            // can be linked, and convert-to-copy — which does enforce the cap — would
            // then be permanently refused for it. Only new links are gated: a deck that
            // outgrew the cap after someone linked it must stay idempotent for them.
            var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id);
            if (cardCount > PublishingService.MaxCardsInPublicDeck)
                return new SubscribeResult(SubscribeError.DeckTooLarge, null, false);

            subscription = new DeckSubscription
            {
                UserId = userId,
                DeckId = deck.Id,
                SubscribedAt = DateTimeOffset.UtcNow,
            };
            db.DeckSubscriptions.Add(subscription);
            await db.SaveChangesAsync();
            created = true;
        }

        var dto = await ToLinkedDto(deck, userId, subscription.IsSuspended);

        await transaction.CommitAsync();

        return new SubscribeResult(SubscribeError.None, dto, created);
    }

    /// <summary>
    /// Loads a deck by public id and holds a row lock on it until the ambient
    /// transaction ends. Every path that adds or removes links to a deck takes this
    /// lock first, and always before touching <see cref="DeckSubscription"/> rows, so
    /// they serialise in a single order.
    /// </summary>
    internal static async Task<Deck?> LockDeck(AppDbContext db, string deckPublicId)
    {
        var decks = await db.Decks
            .FromSql($"""SELECT * FROM "Decks" WHERE "PublicId" = {deckPublicId} FOR UPDATE""")
            .ToListAsync();

        return decks.FirstOrDefault();
    }

    /// <inheritdoc cref="LockDeck(AppDbContext, string)"/>
    internal static Task LockDeck(AppDbContext db, Guid deckId) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""SELECT 1 FROM "Decks" WHERE "Id" = {deckId} FOR UPDATE""");

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

        // Same ceiling as the copy import — a linked deck may have grown past it after
        // the owner unlisted and relisted it. Counted before the cards are materialised,
        // so an oversized deck is refused without loading its SVG blobs.
        var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == source.Id);
        if (cardCount > PublishingService.MaxCardsInPublicDeck)
            return new ConvertToCopyResult(ConvertToCopyError.DeckTooLarge, null);

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
                dc.Card.CreatedAt,
            })
            .ToListAsync();

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
                // Exactly the moment the card became this user's to study while it was
                // linked (see StudyStatsService.LoadStudyableCardStarts), so converting
                // moves no due day in either direction. Stamping today would make the
                // carried-over review logs older than their own card and repaint every
                // missed day since as a rest day — a broken streak comes back to life.
                // Keeping the author's date on a deck written years ago is the mirror
                // image: days before the link existed were never this user's to miss.
                CreatedAt = sourceCard.CreatedAt > subscription.SubscribedAt
                    ? sourceCard.CreatedAt
                    : subscription.SubscribedAt,
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

        // The history follows the state onto the copy, otherwise it stays hanging off
        // the author's cards and cascade-deletes with them — shrinking the converter's
        // totals and streaks for a deck they now own outright.
        await MoveReviewLogsToCopy(db, userId, source.Id, newCardIdBySourceId);

        await DeleteOrphanedReviewStates(db, userId, source.Id);
        db.DeckSubscriptions.Remove(subscription);
        await db.SaveChangesAsync();

        // A conversion is an import like any other. Atomic increment for the same
        // reason as in LibraryService.CopyDeck.
        await db.Decks
            .Where(d => d.Id == source.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.CopyCount, d => d.CopyCount + 1));

        await transaction.CommitAsync();

        // The copy inherits the caller's schedule, so its due count is the same one
        // every other deck DTO reports — not "all cards", which would flash a wrong
        // badge until the next full refresh.
        var dueCount = sourceCards.Count(sc =>
            !states.TryGetValue(sc.CardId, out var carried)
            || (!carried.IsSuspended && (carried.DueAt is null || carried.DueAt <= now)));

        return new ConvertToCopyResult(
            ConvertToCopyError.None,
            new DeckDto(
                copy.PublicId, copy.Name, copy.Description, sourceCards.Count, dueCount,
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
        // The SRS cleanup and the link removal have to land together: half-applied,
        // this either wipes subscribers' progress while their links survive, or leaves
        // live links to a deck nobody can reach any more. Callers that sequence this
        // with a mutation of their own open the transaction themselves — this covers
        // the rest without nesting.
        await using IDbContextTransaction? transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync()
            : null;

        var hasSubscribers = await db.DeckSubscriptions.AnyAsync(s => s.DeckId == deckId);
        if (!hasSubscribers)
        {
            if (transaction is not null) await transaction.CommitAsync();
            return;
        }

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

        if (transaction is not null) await transaction.CommitAsync();
    }

    /// <summary>
    /// Re-points the caller's review log rows from the author's cards to their copies.
    /// Follows the same rule as the SRS cleanup: a card the caller still reaches
    /// through another linked deck keeps its history there, and a card they authored
    /// is never touched.
    /// </summary>
    private static Task MoveReviewLogsToCopy(
        AppDbContext db, string userId, Guid deckId, Dictionary<Guid, Guid> newCardIdBySourceId)
    {
        if (newCardIdBySourceId.Count == 0) return Task.CompletedTask;

        var sourceIds = newCardIdBySourceId.Keys.ToArray();
        var copyIds = sourceIds.Select(id => newCardIdBySourceId[id]).ToArray();

        return db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ReviewLogs" rl
            SET "CardId" = m.new_id
            FROM (
                SELECT * FROM unnest({sourceIds}::uuid[], {copyIds}::uuid[]) AS t(old_id, new_id)
            ) m
            WHERE rl."UserId" = {userId}
              AND rl."CardId" = m.old_id
              AND NOT EXISTS (
                  SELECT 1 FROM "Cards" c
                  WHERE c."Id" = rl."CardId" AND c."UserId" = {userId})
              AND NOT EXISTS (
                  SELECT 1 FROM "DeckCards" dc2
                  JOIN "DeckSubscriptions" ds ON ds."DeckId" = dc2."DeckId"
                  WHERE dc2."CardId" = rl."CardId"
                    AND ds."UserId" = {userId}
                    AND ds."DeckId" <> {deckId})
            """);
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

public enum SubscribeError { None, NotFound, OwnDeck, DeckTooLarge }

/// <param name="Created">False when the caller was already subscribed.</param>
public record SubscribeResult(SubscribeError Error, DeckDto? Deck, bool Created);

public enum ConvertToCopyError { None, NotFound, DeckTooLarge }

public record ConvertToCopyResult(ConvertToCopyError Error, DeckDto? Deck);
