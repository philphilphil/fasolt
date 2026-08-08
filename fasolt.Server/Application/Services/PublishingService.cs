using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

/// <summary>
/// Author-side publishing: claiming an account handle and moving a deck between
/// visibilities. The caps live here so the API, MCP tools and admin surface all
/// agree on the same numbers.
/// </summary>
public partial class PublishingService(AppDbContext db)
{
    public const int MaxCardsInPublicDeck = 1000;
    public const int MaxPublicDecksPerUser = 20;
    public const int MinHandleLength = 3;
    public const int MaxHandleLength = 30;

    /// <summary>Keep in sync with <see cref="MinHandleLength"/>/<see cref="MaxHandleLength"/>.</summary>
    [GeneratedRegex("^[a-z0-9-]{3,30}$")]
    private static partial Regex HandlePattern();

    /// <summary>
    /// Normalizes a candidate handle (trim + lowercase) and checks it against the
    /// 3–30 char lowercase-alphanumeric-plus-hyphen rule.
    /// </summary>
    public static bool TryNormalizeHandle(string? raw, out string handle)
    {
        handle = raw?.Trim().ToLowerInvariant() ?? string.Empty;
        return HandlePattern().IsMatch(handle);
    }

    public async Task<HandleResponse?> GetHandle(string userId)
    {
        return await db.Users
            .Where(u => u.Id == userId)
            .Select(u => new HandleResponse(u.Handle, u.CanPublish))
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Claims or changes the caller's handle. Changing is allowed; uniqueness is
    /// enforced by a filtered unique index, so a lost race still fails cleanly.
    /// </summary>
    public async Task<SetHandleResult> SetHandle(string userId, string? rawHandle)
    {
        if (!TryNormalizeHandle(rawHandle, out var handle))
            return new SetHandleResult(SetHandleError.Invalid, null);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return new SetHandleResult(SetHandleError.UserNotFound, null);

        if (user.Handle == handle)
            return new SetHandleResult(SetHandleError.None, new HandleResponse(user.Handle, user.CanPublish));

        var taken = await db.Users.AnyAsync(u => u.Handle == handle && u.Id != userId);
        if (taken) return new SetHandleResult(SetHandleError.Taken, null);

        user.Handle = handle;

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            db.Entry(user).State = EntityState.Detached;
            return new SetHandleResult(SetHandleError.Taken, null);
        }

        return new SetHandleResult(SetHandleError.None, new HandleResponse(user.Handle, user.CanPublish));
    }

    /// <summary>
    /// Moves one of the caller's decks to a new visibility. Leaving <c>Private</c>
    /// needs publishing rights; <c>Public</c> additionally needs a handle and both
    /// caps to hold. Going back to <c>Private</c> always succeeds.
    /// </summary>
    public async Task<SetVisibilityResult> SetVisibility(string userId, string deckPublicId, DeckVisibility visibility)
    {
        var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);
        if (deck is null)
        {
            // Publishing someone else's deck is forbidden, not merely missing.
            var linked = await db.DeckSubscriptions
                .AnyAsync(s => s.UserId == userId && s.Deck.PublicId == deckPublicId);
            if (linked) throw LinkedContentException.Deck();

            return new SetVisibilityResult(SetVisibilityError.DeckNotFound, null);
        }

        // A ban has to cover unlisted too: an unlisted deck's share link resolves for
        // anyone holding it, and copy/subscribe accept every non-private deck, so
        // gating only the public transition leaves the ban routable around.
        var leavingPrivate = deck.Visibility == DeckVisibility.Private && visibility != DeckVisibility.Private;
        var becomingPublic = visibility == DeckVisibility.Public && deck.Visibility != DeckVisibility.Public;

        if (leavingPrivate || becomingPublic)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return new SetVisibilityResult(SetVisibilityError.DeckNotFound, null);

            if (!user.CanPublish)
                return new SetVisibilityResult(SetVisibilityError.PublishingDisabled, null);

            // The handle and both caps stay public-only — an unlisted deck is never
            // listed under an author, and never appears in the library.
            if (becomingPublic)
            {
                if (string.IsNullOrEmpty(user.Handle))
                    return new SetVisibilityResult(SetVisibilityError.HandleRequired, null);

                var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id);
                if (cardCount > MaxCardsInPublicDeck)
                    return new SetVisibilityResult(SetVisibilityError.DeckTooLarge, null);

                var publicDeckCount = await db.Decks.CountAsync(d =>
                    d.UserId == userId && d.Visibility == DeckVisibility.Public && d.Id != deck.Id);
                if (publicDeckCount >= MaxPublicDecksPerUser)
                    return new SetVisibilityResult(SetVisibilityError.PublicDeckLimit, null);
            }
        }

        // A private deck can no longer be resolved by anyone else, so the links to
        // it go with it. Both halves land in one transaction — committing the
        // visibility alone would leave subscribers studying a deck the owner believes
        // is private, with no surface to revoke it.
        if (visibility == DeckVisibility.Private)
        {
            await using var transaction = await db.Database.BeginTransactionAsync();

            // Locks the deck row before any subscription is touched, in the same order
            // Subscribe takes its locks, so a subscribe racing this either lands before
            // the cleanup (and is removed by it) or blocks and then sees the deck as
            // private.
            ApplyVisibility(deck, visibility);
            await db.SaveChangesAsync();

            await DeckSubscriptionService.RemoveAllSubscriptions(db, deck.Id);

            await transaction.CommitAsync();
        }
        else
        {
            ApplyVisibility(deck, visibility);
            await db.SaveChangesAsync();
        }

        return new SetVisibilityResult(SetVisibilityError.None, await ToDto(deck, userId));
    }

    /// <summary>
    /// Re-checks the published-deck card cap on the paths that add cards to a deck.
    /// <see cref="SetVisibility"/> only sees a deck as it stands at publish time, so
    /// without this a deck published just under the cap could grow without limit
    /// while still listed in the library.
    /// </summary>
    /// <returns>
    /// True when adding <paramref name="adding"/> cards would push a <c>Public</c>
    /// deck past <see cref="MaxCardsInPublicDeck"/>. Private and unlisted decks are
    /// never capped — the same asymmetry as at publish time.
    /// </returns>
    public static async Task<bool> WouldExceedPublicCardCap(AppDbContext db, Guid deckId, int adding)
    {
        if (adding <= 0) return false;

        var isPublic = await db.Decks
            .AnyAsync(d => d.Id == deckId && d.Visibility == DeckVisibility.Public);
        if (!isPublic) return false;

        var current = await db.DeckCards.CountAsync(dc => dc.DeckId == deckId);
        return current + adding > MaxCardsInPublicDeck;
    }

    /// <summary>Admin action: force a deck back to Private, whoever owns it.</summary>
    public async Task<bool> Unlist(string deckPublicId)
    {
        var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckPublicId);
        if (deck is null) return false;

        // One transaction, for the same reason as SetVisibility's private path.
        await using var transaction = await db.Database.BeginTransactionAsync();

        ApplyVisibility(deck, DeckVisibility.Private);
        await db.SaveChangesAsync();
        await DeckSubscriptionService.RemoveAllSubscriptions(db, deck.Id);

        await transaction.CommitAsync();
        return true;
    }

    /// <summary>
    /// Admin action: ban or unban a user from publishing. Already-listed decks are
    /// left alone — taking those down is the separate unlist action, so a ban stays
    /// reversible.
    /// </summary>
    public async Task<bool> SetCanPublish(string userId, bool canPublish)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return false;

        user.CanPublish = canPublish;
        await db.SaveChangesAsync();
        return true;
    }

    private static void ApplyVisibility(Deck deck, DeckVisibility visibility)
    {
        // PublishedAt is the "shared since" date the library sorts and the public page
        // shows. Going private clears it; the first step out of private stamps it. It is
        // re-stamped when a deck first becomes public, because a deck that sat unlisted
        // for months would otherwise enter the library backdated to the unlisting and
        // sort as if it had been there all along. Public -> Unlisted keeps the date, so
        // hiding a deck and re-listing it does not reset its age.
        if (visibility == DeckVisibility.Private)
            deck.PublishedAt = null;
        else if (deck.PublishedAt is null
                 || (visibility == DeckVisibility.Public && deck.Visibility != DeckVisibility.Public))
            deck.PublishedAt = DateTimeOffset.UtcNow;

        deck.Visibility = visibility;
    }

    private async Task<DeckDto> ToDto(Deck deck, string userId)
    {
        var now = DateTimeOffset.UtcNow;
        var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id);
        var dueCount = await db.DeckCards.CountAsync(dc =>
            dc.DeckId == deck.Id && !dc.Card.ReviewStates.Any(r =>
                r.UserId == userId && (r.IsSuspended || r.DueAt > now)));

        return new DeckDto(
            deck.PublicId, deck.Name, deck.Description, cardCount, dueCount,
            deck.CreatedAt, deck.IsSuspended,
            deck.Visibility.ToWire(), deck.PublishedAt, deck.CopyCount,
            deck.CopiedFromDeckPublicId, deck.CopiedFromHandle);
    }
}

/// <summary>
/// Thrown by the single-card paths when adding the card would push a published deck
/// past <see cref="PublishingService.MaxCardsInPublicDeck"/>. The bulk paths report
/// the same condition through their result types instead.
/// </summary>
public class PublishedDeckFullException()
    : Exception($"Published decks are limited to {PublishingService.MaxCardsInPublicDeck} cards. " +
                "Unpublish the deck before adding more.");

public enum SetHandleError { None, Invalid, Taken, UserNotFound }

public record SetHandleResult(SetHandleError Error, HandleResponse? Handle);

public enum SetVisibilityError
{
    None,
    DeckNotFound,
    HandleRequired,
    PublishingDisabled,
    DeckTooLarge,
    PublicDeckLimit,
}

public record SetVisibilityResult(SetVisibilityError Error, DeckDto? Deck);
