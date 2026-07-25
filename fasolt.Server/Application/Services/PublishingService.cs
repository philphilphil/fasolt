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
    /// Moves one of the caller's decks to a new visibility. Only <c>Public</c> is
    /// gated: it needs a handle, publishing rights, and both caps to hold. Going
    /// back to <c>Private</c> always succeeds.
    /// </summary>
    public async Task<SetVisibilityResult> SetVisibility(string userId, string deckPublicId, DeckVisibility visibility)
    {
        var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);
        if (deck is null) return new SetVisibilityResult(SetVisibilityError.DeckNotFound, null);

        if (visibility == DeckVisibility.Public && deck.Visibility != DeckVisibility.Public)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return new SetVisibilityResult(SetVisibilityError.DeckNotFound, null);

            if (!user.CanPublish)
                return new SetVisibilityResult(SetVisibilityError.PublishingDisabled, null);

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

        ApplyVisibility(deck, visibility);
        await db.SaveChangesAsync();

        return new SetVisibilityResult(SetVisibilityError.None, await ToDto(deck, userId));
    }

    /// <summary>Admin action: force a deck back to Private, whoever owns it.</summary>
    public async Task<bool> Unlist(string deckPublicId)
    {
        var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckPublicId);
        if (deck is null) return false;

        ApplyVisibility(deck, DeckVisibility.Private);
        await db.SaveChangesAsync();
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
        if (visibility == DeckVisibility.Private)
            deck.PublishedAt = null;
        else
            deck.PublishedAt ??= DateTimeOffset.UtcNow;

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
