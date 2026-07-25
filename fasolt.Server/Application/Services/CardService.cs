using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class CardService(AppDbContext db)
{
    public const int MaxFrontLength = 10_000;
    public const int MaxBackLength = 50_000;

    public static List<string> ValidateCardFields(string? front, string? back)
    {
        var errors = new List<string>();
        if (front is not null && front.Length > MaxFrontLength)
            errors.Add($"Front text exceeds maximum length of {MaxFrontLength} characters.");
        if (back is not null && back.Length > MaxBackLength)
            errors.Add($"Back text exceeds maximum length of {MaxBackLength} characters.");
        return errors;
    }

    public async Task<CardDto> CreateCard(string userId, string front, string back, string? sourceFile, string? frontSvg = null, string? backSvg = null, string? deckId = null)
    {
        var errors = ValidateCardFields(front, back);
        if (errors.Count > 0)
            throw new ValidationException(string.Join(" ", errors));

        // Validate deckId if provided
        Guid? deckGuid = null;
        if (deckId is not null)
        {
            var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == userId);
            if (deck is null)
                throw new KeyNotFoundException("Deck not found or does not belong to you");
            if (await PublishingService.WouldExceedPublicCardCap(db, deck.Id, 1))
                throw new PublishedDeckFullException();
            deckGuid = deck.Id;
        }

        var card = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = userId,
            SourceFile = sourceFile?.Trim(),
            Front = front,
            Back = back,
            CreatedAt = DateTimeOffset.UtcNow,
            FrontSvg = SvgSanitizer.Sanitize(frontSvg),
            BackSvg = SvgSanitizer.Sanitize(backSvg),
        };

        db.Cards.Add(card);

        if (deckGuid.HasValue)
        {
            db.DeckCards.Add(new DeckCard
            {
                DeckId = deckGuid.Value,
                CardId = card.Id,
            });
        }

        await db.SaveChangesAsync();

        // Reload DeckCards so ToDto reflects the assigned deck
        await db.Entry(card).Collection(c => c.DeckCards).Query().Include(dc => dc.Deck).LoadAsync();

        // A brand-new card has no ReviewState row yet.
        return ToDto(card, null);
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

            var itemErrors = ValidateCardFields(item.Front, item.Back);
            if (itemErrors.Count > 0)
            {
                skipped.Add(new SkippedCardDto(trimmedFront, string.Join(" ", itemErrors)));
                continue;
            }

            var card = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                UserId = userId,
                SourceFile = effectiveSourceFile,
                Front = trimmedFront,
                Back = item.Back.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                FrontSvg = SvgSanitizer.Sanitize(item.FrontSvg),
                BackSvg = SvgSanitizer.Sanitize(item.BackSvg),
            };
            db.Cards.Add(card);
            created.Add(card);
        }

        // Add to deck if specified
        if (deckGuid.HasValue)
        {
            // Only the cards that survived duplicate/validation filtering count towards
            // the published-deck cap.
            if (await PublishingService.WouldExceedPublicCardCap(db, deckGuid.Value, created.Count))
            {
                foreach (var card in created) db.Entry(card).State = EntityState.Detached;
                return BulkCreateResult.PublishedDeckFull();
            }

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

        // Freshly created cards have no ReviewState row yet — they are "new".
        var createdDtos = created.Select(c => new CardDto(
            c.PublicId, c.SourceFile, c.Front, c.Back,
            "new", c.CreatedAt,
            deckId is not null
                ? [new CardDeckInfoDto(deckId, "", false)]
                : [],
            false,
            null, null, null, null, null,
            c.FrontSvg, c.BackSvg)).ToList();

        return BulkCreateResult.Success(new BulkCreateCardsResponse(createdDtos, skipped));
    }

    public async Task<PaginatedResponse<CardDto>> ListCards(
        string userId,
        string? sourceFile,
        string? deckId,
        int? limit,
        string? after,
        HashSet<string>? include = null)
    {
        var take = Math.Clamp(limit ?? 50, 1, 200);
        var includeSrs = include is null || include.Contains("srs");
        var includeSvg = include is null || include.Contains("svg");

        IQueryable<Card> query = db.Cards.Where(c => c.UserId == userId);

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

        var cards = await (
            from c in query
            join r in db.ReviewStates.Where(r => r.UserId == userId) on c.Id equals r.CardId into g
            from rs in g.DefaultIfEmpty()
            orderby c.CreatedAt descending, c.Id
            select new CardDto(
                c.PublicId,
                c.SourceFile,
                c.Front,
                c.Back,
                includeSrs ? rs.State ?? "new" : null,
                c.CreatedAt,
                c.DeckCards.Select(dc => new CardDeckInfoDto(dc.Deck.PublicId, dc.Deck.Name, dc.Deck.IsSuspended)).ToList(),
                rs != null && rs.IsSuspended,
                includeSrs ? rs.DueAt : null,
                includeSrs ? rs.Stability : null,
                includeSrs ? rs.Difficulty : null,
                includeSrs ? rs.Step : null,
                includeSrs ? rs.LastReviewedAt : null,
                includeSvg ? c.FrontSvg : null,
                includeSvg ? c.BackSvg : null))
            .Take(take + 1)
            .ToListAsync();

        var hasMore = cards.Count > take;
        if (hasMore) cards = cards[..take];
        var nextCursor = hasMore ? cards[^1].Id : null;

        return new PaginatedResponse<CardDto>(cards, hasMore, nextCursor);
    }

    public async Task<CardDto?> GetCard(string userId, string publicId)
    {
        var card = await StudyableCardsWithVisibleDecks(userId)
            .FirstOrDefaultAsync(c => c.PublicId == publicId);

        return card is null ? null : await ToViewerDto(card, await LoadState(userId, card.Id), userId);
    }

    public async Task<CardDto?> UpdateCard(string userId, string publicId, UpdateCardRequest request)
    {
        var card = await db.Cards
            .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
            .FirstOrDefaultAsync(c => c.PublicId == publicId && c.UserId == userId);

        if (card is null)
        {
            await ThrowIfLinkedCard(userId, publicId);
            return null;
        }

        var errors = ValidateCardFields(request.Front, request.Back);
        if (errors.Count > 0)
            throw new ValidationException(string.Join(" ", errors));

        card.Front = request.Front;
        card.Back = request.Back;
        if (request.FrontSvg is not null)
            card.FrontSvg = request.FrontSvg == "" ? null : SvgSanitizer.Sanitize(request.FrontSvg);
        if (request.BackSvg is not null)
            card.BackSvg = request.BackSvg == "" ? null : SvgSanitizer.Sanitize(request.BackSvg);
        if (request.SourceFile is not null)
            card.SourceFile = request.SourceFile == "" ? null : request.SourceFile;

        if (request.DeckIds is not null)
        {
            var currentDeckIds = card.DeckCards.Select(dc => dc.DeckId).ToHashSet();

            var targetDecks = new List<Deck>();
            foreach (var deckPublicId in request.DeckIds)
            {
                var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);
                if (deck is not null)
                {
                    targetDecks.Add(deck);
                    continue;
                }

                // Cards cannot be pushed into a deck linked from another account.
                if (await db.DeckSubscriptions.AnyAsync(s => s.UserId == userId && s.Deck.PublicId == deckPublicId))
                    throw LinkedContentException.Deck();
            }

            // Check before mutating anything: decks the card is already in are a no-op,
            // the rest have to fit under the published-deck cap.
            foreach (var deck in targetDecks.Where(d => !currentDeckIds.Contains(d.Id)))
            {
                if (await PublishingService.WouldExceedPublicCardCap(db, deck.Id, 1))
                    throw new PublishedDeckFullException();
            }

            db.DeckCards.RemoveRange(card.DeckCards);
            foreach (var deck in targetDecks)
            {
                db.DeckCards.Add(new DeckCard { DeckId = deck.Id, CardId = card.Id });
            }
        }

        await db.SaveChangesAsync();

        // Reload DeckCards after potential changes
        await db.Entry(card).Collection(c => c.DeckCards).Query().Include(dc => dc.Deck).LoadAsync();

        return ToDto(card, await LoadState(userId, card.Id));
    }

    public async Task<UpdateCardResult> UpdateCardFields(string userId, string publicId, UpdateCardFieldsRequest req)
    {
        var card = await db.Cards
            .Include(c => c.DeckCards).ThenInclude(dc => dc.Deck)
            .FirstOrDefaultAsync(c => c.PublicId == publicId && c.UserId == userId);

        if (card is null)
        {
            await ThrowIfLinkedCard(userId, publicId);
            return UpdateCardResult.NotFound();
        }

        return await ApplyCardFieldUpdates(userId, card, req);
    }

    public async Task<List<BulkUpdateCardResult>> BulkUpdateCards(string userId, List<BulkUpdateCardItem> items)
    {
        var results = new List<BulkUpdateCardResult>();

        foreach (var item in items)
        {
            var req = new UpdateCardFieldsRequest(item.NewFront, item.NewBack, item.NewSourceFile, item.NewFrontSvg, item.NewBackSvg);
            var result = await UpdateCardFields(userId, item.CardId, req);
            results.Add(new BulkUpdateCardResult(item.CardId, result.Status, result.Card));
        }

        return results;
    }

    private async Task<UpdateCardResult> ApplyCardFieldUpdates(string userId, Card card, UpdateCardFieldsRequest req)
    {
        var errors = ValidateCardFields(req.NewFront, req.NewBack);
        if (errors.Count > 0)
            throw new ValidationException(string.Join(" ", errors));

        if (req.NewFront is not null) card.Front = req.NewFront.Trim();
        if (req.NewBack is not null) card.Back = req.NewBack.Trim();
        if (req.NewSourceFile is not null) card.SourceFile = req.NewSourceFile.Trim();
        if (req.NewFrontSvg is not null)
            card.FrontSvg = req.NewFrontSvg == "" ? null : SvgSanitizer.Sanitize(req.NewFrontSvg);
        if (req.NewBackSvg is not null)
            card.BackSvg = req.NewBackSvg == "" ? null : SvgSanitizer.Sanitize(req.NewBackSvg);

        await db.SaveChangesAsync();

        return UpdateCardResult.Success(ToDto(card, await LoadState(userId, card.Id)));
    }

    public async Task<RenameSourceResult> RenameSource(string userId, string from, string to)
    {
        var trimmedFrom = from?.Trim();
        var trimmedTo = to?.Trim();

        if (string.IsNullOrEmpty(trimmedFrom) || string.IsNullOrEmpty(trimmedTo) || trimmedFrom == trimmedTo)
            return new RenameSourceResult(0);

        var renamed = await db.Cards
            .Where(c => c.UserId == userId && c.SourceFile == trimmedFrom)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.SourceFile, trimmedTo));

        return new RenameSourceResult(renamed);
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

        if (deleted > 0) return true;

        await ThrowIfLinkedCard(userId, publicId);
        return false;
    }

    public async Task<int> DeleteCards(string userId, List<string> publicIds)
    {
        // Checked up front so a batch that touches linked content is rejected whole
        // instead of deleting the caller's own cards and then failing.
        await ThrowIfAnyLinkedCard(userId, publicIds);

        return await db.Cards
            .Where(c => c.UserId == userId && publicIds.Contains(c.PublicId))
            .ExecuteDeleteAsync();
    }

    /// <returns>Number of cards matched (not the number of ReviewState rows touched).</returns>
    public async Task<int> SetSuspendedBulk(string userId, List<string> publicIds, bool isSuspended)
    {
        // Suspension is the caller's own ReviewState, so linked cards are fair game.
        var cardIds = await LinkedDeckQuery.StudyableCards(db, userId)
            .Where(c => publicIds.Contains(c.PublicId))
            .Select(c => c.Id)
            .ToListAsync();

        if (cardIds.Count == 0) return 0;

        // Lazy creation: only suspending needs a row; unsuspending a card without one
        // is already a no-op. The rows are created with ON CONFLICT DO NOTHING, so a
        // concurrent request materializing the same row cannot fail this one.
        if (isSuspended) await ReviewStateQuery.EnsureExistAsync(db, userId, cardIds);

        var states = await ReviewStateQuery.LoadForCardsAsync(db, userId, cardIds);

        foreach (var state in states.Values)
            state.IsSuspended = isSuspended;

        await db.SaveChangesAsync();
        return cardIds.Count;
    }

    public async Task<CardDto?> ResetProgress(string userId, string publicId)
    {
        var card = await StudyableCardsWithVisibleDecks(userId)
            .FirstOrDefaultAsync(c => c.PublicId == publicId);

        if (card is null) return null;

        var state = await LoadState(userId, card.Id);
        if (state is not null)
        {
            state.Stability = null;
            state.Difficulty = null;
            state.Step = null;
            state.DueAt = null;
            state.State = "new";
            state.LastReviewedAt = null;
            // Suspension survives a reset. A row left carrying nothing but the "new"
            // default is kept rather than deleted: it is equivalent to having no row,
            // and deleting it would race with a concurrent review of the same card.

            await db.SaveChangesAsync();
        }

        return await ToViewerDto(card, state, userId);
    }

    public async Task<CardDto?> SetSuspended(string userId, string publicId, bool isSuspended)
    {
        var card = await StudyableCardsWithVisibleDecks(userId)
            .FirstOrDefaultAsync(c => c.PublicId == publicId);

        if (card is null) return null;

        // Only suspending needs a row; unsuspending a card that has none is a no-op.
        var state = isSuspended
            ? await ReviewStateQuery.GetOrCreateAsync(db, userId, card.Id)
            : await LoadState(userId, card.Id);

        if (state is not null)
        {
            state.IsSuspended = isSuspended;
            await db.SaveChangesAsync();
        }

        return await ToViewerDto(card, state, userId);
    }

    private Task<ReviewState?> LoadState(string userId, Guid cardId) =>
        db.ReviewStates.FirstOrDefaultAsync(r => r.UserId == userId && r.CardId == cardId);

    /// <summary>
    /// Cards the caller may act on for their own SRS state — authored or linked —
    /// with only the decks they can actually see attached.
    /// </summary>
    private IQueryable<Card> StudyableCardsWithVisibleDecks(string userId) =>
        LinkedDeckQuery.StudyableCards(db, userId)
            .Include(c => c.DeckCards.Where(dc =>
                dc.Deck.UserId == userId || dc.Deck.Subscriptions.Any(s => s.UserId == userId)))
            .ThenInclude(dc => dc.Deck);

    /// <summary>
    /// Turns the "card is not yours" case into a 403 when the card is one the caller
    /// reaches through a linked deck.
    /// </summary>
    private async Task ThrowIfLinkedCard(string userId, string cardPublicId)
    {
        var linked = await LinkedDeckQuery.StudyableCards(db, userId)
            .AnyAsync(c => c.PublicId == cardPublicId && c.UserId != userId);

        if (linked) throw LinkedContentException.Card();
    }

    private async Task ThrowIfAnyLinkedCard(string userId, List<string> cardPublicIds)
    {
        var linked = await LinkedDeckQuery.StudyableCards(db, userId)
            .AnyAsync(c => cardPublicIds.Contains(c.PublicId) && c.UserId != userId);

        if (linked) throw LinkedContentException.Card();
    }

    /// <summary>
    /// Renders a card as the caller sees it. For a linked card the author's
    /// <c>SourceFile</c> is withheld and the deck pause shown is the caller's own.
    /// </summary>
    private async Task<CardDto> ToViewerDto(Card c, ReviewState? rs, string userId)
    {
        if (c.UserId == userId) return ToDto(c, rs);

        var pauses = await db.DeckSubscriptions
            .Where(s => s.UserId == userId)
            .ToDictionaryAsync(s => s.DeckId, s => s.IsSuspended);

        return new CardDto(
            c.PublicId, null, c.Front, c.Back, rs?.State ?? "new", c.CreatedAt,
            c.DeckCards
                .Select(dc => new CardDeckInfoDto(
                    dc.Deck.PublicId, dc.Deck.Name, pauses.GetValueOrDefault(dc.DeckId, dc.Deck.IsSuspended)))
                .ToList(),
            rs?.IsSuspended ?? false,
            rs?.DueAt, rs?.Stability, rs?.Difficulty, rs?.Step, rs?.LastReviewedAt,
            c.FrontSvg, c.BackSvg);
    }

    private static CardDto ToDto(Card c, ReviewState? rs) =>
        new(c.PublicId, c.SourceFile, c.Front, c.Back, rs?.State ?? "new", c.CreatedAt,
            c.DeckCards.Select(dc => new CardDeckInfoDto(dc.Deck.PublicId, dc.Deck.Name, dc.Deck.IsSuspended)).ToList(),
            rs?.IsSuspended ?? false,
            rs?.DueAt, rs?.Stability, rs?.Difficulty, rs?.Step, rs?.LastReviewedAt,
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
    public bool IsPublishedDeckFull { get; private init; }
    public BulkCreateCardsResponse? Response { get; private init; }

    public static BulkCreateResult Success(BulkCreateCardsResponse response) =>
        new() { IsSuccess = true, Response = response };

    public static BulkCreateResult DeckNotFound() =>
        new() { IsDeckNotFound = true };

    public static BulkCreateResult PublishedDeckFull() =>
        new() { IsPublishedDeckFull = true };
}
