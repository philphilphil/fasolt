using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class CardService(AppDbContext db)
{
    public async Task<CardDto> CreateCard(string userId, string front, string back, string? sourceFile, string? sourceHeading, string? frontSvg = null, string? backSvg = null)
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = userId,
            SourceFile = sourceFile?.Trim(),
            SourceHeading = sourceHeading,
            Front = front,
            Back = back,
            CreatedAt = DateTimeOffset.UtcNow,
            FrontSvg = SvgSanitizer.Sanitize(frontSvg),
            BackSvg = SvgSanitizer.Sanitize(backSvg),
        };

        db.Cards.Add(card);
        await db.SaveChangesAsync();

        return ToDto(card);
    }

    public async Task<BulkCreateResult> BulkCreateCards(string userId, List<BulkCardItem> cards, string? sourceFile, string? deckId)
    {
        // Validate deckId if provided
        Guid? deckGuid = null;
        if (deckId is not null)
        {
            var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == userId);
            if (deck is null) return BulkCreateResult.DeckNotFound();
            deckGuid = deck.Id;
        }

        // Check for duplicates
        var fronts = cards.Select(c => c.Front.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestSourceFile = sourceFile?.Trim();

        var cardsWithSource = cards
            .Where(c => (c.SourceFile?.Trim() ?? requestSourceFile) is not null)
            .Select(c => new { SourceFile = (c.SourceFile?.Trim() ?? requestSourceFile)!, Front = c.Front.Trim() })
            .ToList();

        var cardsWithoutSource = cards
            .Where(c => (c.SourceFile?.Trim() ?? requestSourceFile) is null)
            .Select(c => c.Front.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingWithSource = cardsWithSource.Count > 0
            ? await db.Cards
                .Where(c => c.UserId == userId && c.SourceFile != null && fronts.Contains(c.Front))
                .Select(c => new { c.SourceFile, c.Front })
                .ToListAsync()
            : [];

        var existingWithoutSource = cardsWithoutSource.Count > 0
            ? await db.Cards
                .Where(c => c.UserId == userId && c.SourceFile == null && fronts.Contains(c.Front))
                .Select(c => c.Front)
                .ToListAsync()
            : [];

        var sourceKeyComparer = new SourceFrontComparer();
        var existingWithSourceSet = existingWithSource
            .Select(x => (x.SourceFile!, x.Front))
            .ToHashSet(sourceKeyComparer);
        var existingWithoutSourceSet = existingWithoutSource.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = new List<Card>();
        var createdKeys = new HashSet<(string, string)>(sourceKeyComparer);
        var skipped = new List<SkippedCardDto>();

        foreach (var item in cards)
        {
            var trimmedFront = item.Front.Trim();
            var effectiveSourceFile = item.SourceFile?.Trim() ?? requestSourceFile;

            bool isDuplicate = effectiveSourceFile is not null
                ? existingWithSourceSet.Contains((effectiveSourceFile, trimmedFront))
                : existingWithoutSourceSet.Contains(trimmedFront);

            if (isDuplicate)
            {
                skipped.Add(new SkippedCardDto(trimmedFront, "Card with same front text already exists"));
                continue;
            }

            if (!createdKeys.Add((effectiveSourceFile ?? "", trimmedFront)))
            {
                skipped.Add(new SkippedCardDto(trimmedFront, "Duplicate within batch"));
                continue;
            }

            var card = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                UserId = userId,
                SourceFile = effectiveSourceFile,
                SourceHeading = item.SourceHeading,
                Front = trimmedFront,
                Back = item.Back.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                State = "new",
                FrontSvg = SvgSanitizer.Sanitize(item.FrontSvg),
                BackSvg = SvgSanitizer.Sanitize(item.BackSvg),
            };
            db.Cards.Add(card);
            created.Add(card);
        }

        // Add to deck if specified
        if (deckGuid.HasValue)
        {
            foreach (var card in created)
            {
                db.DeckCards.Add(new DeckCard
                {
                    DeckId = deckGuid.Value,
                    CardId = card.Id,
                });
            }
        }

        await db.SaveChangesAsync();

        var createdDtos = created.Select(c => new CardDto(
            c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back,
            c.State, c.CreatedAt,
            deckId is not null
                ? [new CardDeckInfoDto(deckId, "", true)]
                : [],
            c.DueAt, c.Stability, c.Difficulty, c.Step, c.LastReviewedAt,
            c.FrontSvg, c.BackSvg)).ToList();

        return BulkCreateResult.Success(new BulkCreateCardsResponse(createdDtos, skipped));
    }

    public async Task<PaginatedResponse<CardDto>> ListCards(string userId, string? sourceFile, string? deckId, int? limit, string? after)
    {
        var take = Math.Clamp(limit ?? 50, 1, 200);

        IQueryable<Card> query = db.Cards
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id);

        if (sourceFile is not null)
            query = query.Where(c => c.SourceFile == sourceFile);

        if (deckId is not null)
        {
            var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == userId);
            if (deck is not null)
                query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deck.Id));
        }

        if (after is not null)
        {
            var cursor = await db.Cards.Where(c => c.PublicId == after && c.UserId == userId)
                .Select(c => new { c.CreatedAt, c.Id }).FirstOrDefaultAsync();
            if (cursor is not null)
                query = query.Where(c => c.CreatedAt < cursor.CreatedAt ||
                    (c.CreatedAt == cursor.CreatedAt && c.Id.CompareTo(cursor.Id) > 0));
        }

        var cards = await query
            .Take(take + 1)
            .Select(c => new CardDto(c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back, c.State, c.CreatedAt,
                c.DeckCards.Select(dc => new CardDeckInfoDto(dc.Deck.PublicId, dc.Deck.Name, dc.Deck.IsActive)).ToList(),
                c.DueAt, c.Stability, c.Difficulty, c.Step, c.LastReviewedAt,
                c.FrontSvg, c.BackSvg))
            .ToListAsync();

        var hasMore = cards.Count > take;
        if (hasMore) cards = cards[..take];
        var nextCursor = hasMore ? cards[^1].Id : null;

        return new PaginatedResponse<CardDto>(cards, hasMore, nextCursor);
    }

    public async Task<CardDto?> GetCard(string userId, string publicId)
    {
        var card = await db.Cards
            .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
            .FirstOrDefaultAsync(c => c.PublicId == publicId && c.UserId == userId);

        return card is null ? null : ToDto(card);
    }

    public async Task<CardDto?> UpdateCard(string userId, string publicId, UpdateCardRequest request)
    {
        var card = await db.Cards
            .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
            .FirstOrDefaultAsync(c => c.PublicId == publicId && c.UserId == userId);

        if (card is null) return null;

        card.Front = request.Front;
        card.Back = request.Back;
        if (request.FrontSvg is not null)
            card.FrontSvg = request.FrontSvg == "" ? null : SvgSanitizer.Sanitize(request.FrontSvg);
        if (request.BackSvg is not null)
            card.BackSvg = request.BackSvg == "" ? null : SvgSanitizer.Sanitize(request.BackSvg);
        if (request.SourceFile is not null)
            card.SourceFile = request.SourceFile == "" ? null : request.SourceFile;
        if (request.SourceHeading is not null)
            card.SourceHeading = request.SourceHeading == "" ? null : request.SourceHeading;

        if (request.DeckIds is not null)
        {
            db.DeckCards.RemoveRange(card.DeckCards);
            foreach (var deckPublicId in request.DeckIds)
            {
                var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);
                if (deck is not null)
                {
                    db.DeckCards.Add(new DeckCard { DeckId = deck.Id, CardId = card.Id });
                }
            }
        }

        await db.SaveChangesAsync();

        // Reload DeckCards after potential changes
        await db.Entry(card).Collection(c => c.DeckCards).Query().Include(dc => dc.Deck).LoadAsync();

        return ToDto(card);
    }

    public async Task<UpdateCardResult> UpdateCardFields(string userId, string publicId, UpdateCardFieldsRequest req)
    {
        var card = await db.Cards
            .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
            .FirstOrDefaultAsync(c => c.PublicId == publicId && c.UserId == userId);

        if (card is null) return UpdateCardResult.NotFound();

        return await ApplyCardFieldUpdates(userId, card, req);
    }

    public async Task<UpdateCardResult> UpdateCardByNaturalKey(string userId, string sourceFile, string front, UpdateCardFieldsRequest req)
    {
        var card = await db.Cards
            .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
            .FirstOrDefaultAsync(c => c.UserId == userId
                && c.SourceFile != null
                && c.SourceFile.ToLower() == sourceFile.ToLower()
                && c.Front.ToLower() == front.ToLower());

        if (card is null) return UpdateCardResult.NotFound();

        return await ApplyCardFieldUpdates(userId, card, req);
    }

    public async Task<List<BulkUpdateCardResult>> BulkUpdateCards(string userId, List<BulkUpdateCardItem> items)
    {
        var results = new List<BulkUpdateCardResult>();

        foreach (var item in items)
        {
            var req = new UpdateCardFieldsRequest(item.NewFront, item.NewBack, item.NewSourceFile, item.NewSourceHeading, item.NewFrontSvg, item.NewBackSvg);

            UpdateCardResult result;
            if (item.CardId is not null)
            {
                result = await UpdateCardFields(userId, item.CardId, req);
            }
            else if (item.SourceFile is not null && item.Front is not null)
            {
                result = await UpdateCardByNaturalKey(userId, item.SourceFile, item.Front, req);
            }
            else
            {
                results.Add(new BulkUpdateCardResult(item.CardId, item.SourceFile, item.Front, UpdateCardStatus.NotFound));
                continue;
            }

            results.Add(new BulkUpdateCardResult(item.CardId, item.SourceFile, item.Front, result.Status, result.Card));
        }

        return results;
    }

    private async Task<UpdateCardResult> ApplyCardFieldUpdates(string userId, Card card, UpdateCardFieldsRequest req)
    {
        var effectiveFront = req.NewFront?.Trim() ?? card.Front;
        var effectiveSourceFile = req.NewSourceFile?.Trim() ?? card.SourceFile;

        // Check for natural key collision if front or sourceFile is changing
        if (effectiveFront != card.Front || effectiveSourceFile != card.SourceFile)
        {
            if (effectiveSourceFile is not null)
            {
                var collision = await db.Cards.AnyAsync(c =>
                    c.UserId == userId
                    && c.Id != card.Id
                    && c.SourceFile != null
                    && c.SourceFile.ToLower() == effectiveSourceFile.ToLower()
                    && c.Front.ToLower() == effectiveFront.ToLower());

                if (collision) return UpdateCardResult.Collision();
            }
        }

        if (req.NewFront is not null) card.Front = req.NewFront.Trim();
        if (req.NewBack is not null) card.Back = req.NewBack.Trim();
        if (req.NewSourceFile is not null) card.SourceFile = req.NewSourceFile.Trim();
        if (req.NewSourceHeading is not null) card.SourceHeading = req.NewSourceHeading.Trim();
        if (req.NewFrontSvg is not null)
            card.FrontSvg = req.NewFrontSvg == "" ? null : SvgSanitizer.Sanitize(req.NewFrontSvg);
        if (req.NewBackSvg is not null)
            card.BackSvg = req.NewBackSvg == "" ? null : SvgSanitizer.Sanitize(req.NewBackSvg);

        await db.SaveChangesAsync();

        return UpdateCardResult.Success(ToDto(card));
    }

    public async Task<int> DeleteCardsBySource(string userId, string sourceFile)
    {
        return await db.Cards
            .Where(c => c.UserId == userId && c.SourceFile == sourceFile)
            .ExecuteDeleteAsync();
    }

    /// <returns>true if deleted, false if not found</returns>
    public async Task<bool> DeleteCard(string userId, string publicId)
    {
        var deleted = await db.Cards
            .Where(c => c.PublicId == publicId && c.UserId == userId)
            .ExecuteDeleteAsync();

        return deleted > 0;
    }

    public async Task<int> DeleteCards(string userId, List<string> publicIds)
    {
        return await db.Cards
            .Where(c => c.UserId == userId && publicIds.Contains(c.PublicId))
            .ExecuteDeleteAsync();
    }

    public async Task<CardDto?> ResetProgress(string userId, string publicId)
    {
        var card = await db.Cards
            .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
            .FirstOrDefaultAsync(c => c.PublicId == publicId && c.UserId == userId);

        if (card is null) return null;

        card.Stability = null;
        card.Difficulty = null;
        card.Step = null;
        card.DueAt = null;
        card.State = "new";
        card.LastReviewedAt = null;

        await db.SaveChangesAsync();

        return ToDto(card);
    }

    private static CardDto ToDto(Card c) =>
        new(c.PublicId, c.SourceFile, c.SourceHeading, c.Front, c.Back, c.State, c.CreatedAt,
            c.DeckCards.Select(dc => new CardDeckInfoDto(dc.Deck.PublicId, dc.Deck.Name, dc.Deck.IsActive)).ToList(),
            c.DueAt, c.Stability, c.Difficulty, c.Step, c.LastReviewedAt,
            c.FrontSvg, c.BackSvg);

    private sealed class SourceFrontComparer : IEqualityComparer<(string, string)>
    {
        public bool Equals((string, string) x, (string, string) y) =>
            string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string, string) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2));
    }
}

public class BulkCreateResult
{
    public bool IsSuccess { get; private init; }
    public bool IsDeckNotFound { get; private init; }
    public BulkCreateCardsResponse? Response { get; private init; }

    public static BulkCreateResult Success(BulkCreateCardsResponse response) =>
        new() { IsSuccess = true, Response = response };

    public static BulkCreateResult DeckNotFound() =>
        new() { IsDeckNotFound = true };
}
