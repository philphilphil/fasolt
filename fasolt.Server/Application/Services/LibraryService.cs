using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

/// <summary>
/// The anonymous-facing public deck library plus the authenticated copy import.
/// Everything returned from here is world-readable: no internal user ids and no
/// card <c>SourceFile</c> ever appear in these DTOs.
/// </summary>
public class LibraryService(AppDbContext db)
{
    /// <summary>Sample card previews returned on a public deck page.</summary>
    public const int SampleCardCount = 10;

    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 50;

    /// <summary>
    /// Ceiling on <c>?page=</c>. This is an anonymous endpoint, so an absurd page
    /// number must not overflow the <c>Skip()</c> arithmetic into a negative OFFSET
    /// (Postgres rejects those) or make us pay for a pointless deep scan.
    /// </summary>
    public const int MaxPage = 1000;

    public async Task<LibraryListResponse> ListPublicDecks(string? query, string? sort, int page, int pageSize)
    {
        page = Math.Clamp(page, 1, MaxPage);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var decks = db.Decks.Where(d => d.Visibility == DeckVisibility.Public);

        var trimmed = query?.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            decks = decks.Where(d => d.SearchVector.Matches(
                EF.Functions.WebSearchToTsQuery("english", trimmed)));
        }

        var totalCount = await decks.CountAsync();

        // "popular" is copy count for now; subscriber counts join in with link mode.
        decks = sort?.Trim().ToLowerInvariant() == "recent"
            ? decks.OrderByDescending(d => d.PublishedAt).ThenBy(d => d.Id)
            : decks.OrderByDescending(d => d.CopyCount).ThenByDescending(d => d.PublishedAt).ThenBy(d => d.Id);

        var items = await decks
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new LibraryDeckDto(
                d.PublicId,
                d.Name,
                d.Description,
                d.User.Handle,
                d.Cards.Count,
                d.CopyCount,
                d.PublishedAt))
            .ToListAsync();

        return new LibraryListResponse(items, totalCount, page, pageSize);
    }

    /// <summary>
    /// Detail for a shared deck. Unlisted decks resolve here too — that is what makes
    /// the share link work — but they are never returned by <see cref="ListPublicDecks"/>.
    /// </summary>
    public async Task<LibraryDeckDetailDto?> GetPublicDeck(string publicId)
    {
        var deck = await db.Decks
            .Where(d => d.PublicId == publicId && d.Visibility != DeckVisibility.Private)
            .Select(d => new
            {
                d.Id,
                d.PublicId,
                d.Name,
                d.Description,
                AuthorHandle = d.User.Handle,
                CardCount = d.Cards.Count,
                d.CopyCount,
                d.Visibility,
                d.PublishedAt,
            })
            .FirstOrDefaultAsync();

        if (deck is null) return null;

        var sampleCards = await db.DeckCards
            .Where(dc => dc.DeckId == deck.Id)
            .OrderBy(dc => dc.Card.CreatedAt)
            .ThenBy(dc => dc.CardId)
            .Take(SampleCardCount)
            .Select(dc => new LibrarySampleCardDto(
                dc.Card.Front, dc.Card.Back, dc.Card.FrontSvg, dc.Card.BackSvg))
            .ToListAsync();

        return new LibraryDeckDetailDto(
            deck.PublicId,
            deck.Name,
            deck.Description,
            deck.AuthorHandle,
            deck.CardCount,
            deck.CopyCount,
            deck.Visibility.ToWire(),
            deck.PublishedAt,
            sampleCards);
    }

    /// <summary>
    /// Clones a shared deck into the caller's account: new Card rows owned by the
    /// importer, no <c>SourceFile</c> (that is the author's vault path), and no
    /// ReviewState rows so every card starts new.
    /// </summary>
    public async Task<CopyDeckResult> CopyDeck(string userId, string publicId)
    {
        var source = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == publicId && d.Visibility != DeckVisibility.Private);

        if (source is null) return new CopyDeckResult(CopyDeckError.NotFound, null);

        // Same ceiling as publishing. Unlisted decks are not capped at publish time, so
        // this is the only guard against importing an unbounded deck — and it has to
        // run before the cards are materialised, or an oversized deck is pulled into
        // memory with its SVG blobs just to be refused.
        var cardCount = await db.DeckCards.CountAsync(dc => dc.DeckId == source.Id);
        if (cardCount > PublishingService.MaxCardsInPublicDeck)
            return new CopyDeckResult(CopyDeckError.DeckTooLarge, null);

        var sourceCards = await db.DeckCards
            .Where(dc => dc.DeckId == source.Id)
            .OrderBy(dc => dc.Card.CreatedAt)
            .Select(dc => new { dc.Card.Front, dc.Card.Back, dc.Card.FrontSvg, dc.Card.BackSvg })
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
            db.Cards.Add(card);
            db.DeckCards.Add(new DeckCard { DeckId = copy.Id, CardId = card.Id });
        }

        await db.SaveChangesAsync();

        // Copying your own deck must not inflate the count that drives the library's
        // "popular" sort.
        if (source.UserId != userId)
        {
            // Atomic increment rather than a tracked read-modify-write: EF would write
            // the absolute value read at the top of this method, so simultaneous copies
            // of the same deck would overwrite each other's increments.
            await db.Decks
                .Where(d => d.Id == source.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.CopyCount, d => d.CopyCount + 1));
        }

        await transaction.CommitAsync();

        return new CopyDeckResult(
            CopyDeckError.None,
            new DeckDto(
                copy.PublicId, copy.Name, copy.Description, sourceCards.Count, sourceCards.Count,
                copy.CreatedAt, copy.IsSuspended,
                copy.Visibility.ToWire(), copy.PublishedAt, copy.CopyCount,
                copy.CopiedFromDeckPublicId, copy.CopiedFromHandle));
    }
}

public enum CopyDeckError { None, NotFound, DeckTooLarge }

public record CopyDeckResult(CopyDeckError Error, DeckDto? Deck);
