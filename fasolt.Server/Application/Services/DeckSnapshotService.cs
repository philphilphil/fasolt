using System.Text.Json;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Fasolt.Server.Application.Services;

public class DeckSnapshotService(AppDbContext db)
{
    private const int MaxSnapshotsPerDeck = 10;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<int> CreateAll(string userId)
    {
        var decks = await db.Decks
            .Where(d => d.UserId == userId)
            .Include(d => d.Cards).ThenInclude(dc => dc.Card)
            .ToListAsync();

        var count = 0;
        foreach (var deck in decks)
        {
            var cards = deck.Cards.Select(dc => dc.Card).ToList();
            if (cards.Count == 0) continue; // skip empty decks

            var data = new SnapshotData(
                deck.Name,
                deck.Description,
                cards.Select(c => new SnapshotCardData(
                    c.Id, c.PublicId, c.Front, c.Back, c.FrontSvg, c.BackSvg,
                    c.SourceFile, c.SourceHeading, c.CreatedAt,
                    c.Stability, c.Difficulty, c.Step, c.DueAt, c.State, c.LastReviewedAt,
                    c.IsSuspended
                )).ToList());

            var snapshot = new DeckSnapshot
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                DeckId = deck.Id,
                UserId = userId,
                Version = 1,
                CardCount = cards.Count,
                Data = JsonSerializer.Serialize(data, JsonOptions),
                CreatedAt = DateTimeOffset.UtcNow,
            };

            db.DeckSnapshots.Add(snapshot);
            count++;
        }

        await db.SaveChangesAsync();

        // Enforce retention
        await EnforceRetention(userId);

        return count;
    }

    public async Task<List<SnapshotListDto>> ListByDeck(string userId, string deckPublicId)
    {
        var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);
        if (deck is null) return [];

        return await db.DeckSnapshots
            .Where(s => s.DeckId == deck.Id && s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SnapshotListDto(s.PublicId, s.Deck != null ? s.Deck.Name : null, s.CardCount, s.CreatedAt))
            .ToListAsync();
    }

    public async Task<List<SnapshotListDto>> ListRecent(string userId, int limit = 50)
    {
        return await db.DeckSnapshots
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .Select(s => new SnapshotListDto(s.PublicId, s.Deck != null ? s.Deck.Name : null, s.CardCount, s.CreatedAt))
            .ToListAsync();
    }

    public async Task<object?> GetById(string userId, string snapshotPublicId)
    {
        var snapshot = await db.DeckSnapshots
            .FirstOrDefaultAsync(s => s.PublicId == snapshotPublicId && s.UserId == userId);
        if (snapshot is null) return null;

        var data = JsonSerializer.Deserialize<SnapshotData>(snapshot.Data, JsonOptions)!;
        return new
        {
            snapshot.PublicId,
            DeckName = data.DeckName,
            DeckDescription = data.DeckDescription,
            snapshot.CardCount,
            snapshot.Version,
            snapshot.CreatedAt,
            Cards = data.Cards,
        };
    }

    public async Task<SnapshotDiffDto?> ComputeDiff(string userId, string snapshotPublicId)
    {
        var snapshot = await db.DeckSnapshots
            .FirstOrDefaultAsync(s => s.PublicId == snapshotPublicId && s.UserId == userId);
        if (snapshot is null) return null;

        var data = JsonSerializer.Deserialize<SnapshotData>(snapshot.Data, JsonOptions)!;

        // Get current deck cards
        var currentCards = snapshot.DeckId.HasValue
            ? await db.DeckCards
                .Where(dc => dc.DeckId == snapshot.DeckId.Value)
                .Include(dc => dc.Card)
                .Select(dc => dc.Card)
                .ToListAsync()
            : [];

        var currentById = currentCards.ToDictionary(c => c.Id);
        var snapshotById = data.Cards.ToDictionary(c => c.CardId);

        var deleted = data.Cards
            .Where(sc => !currentById.ContainsKey(sc.CardId))
            .Select(sc => new DiffDeletedCard(sc.CardId, sc.Front, sc.Back, sc.SourceFile, sc.Stability, sc.DueAt))
            .ToList();

        var modified = data.Cards
            .Where(sc => currentById.ContainsKey(sc.CardId))
            .Select(sc =>
            {
                var cur = currentById[sc.CardId];
                var contentChanged = sc.Front != cur.Front || sc.Back != cur.Back
                    || sc.FrontSvg != cur.FrontSvg || sc.BackSvg != cur.BackSvg
                    || sc.SourceFile != cur.SourceFile || sc.SourceHeading != cur.SourceHeading;
                var fsrsChanged = sc.Stability != cur.Stability || sc.Difficulty != cur.Difficulty
                    || sc.Step != cur.Step || sc.DueAt != cur.DueAt || sc.State != cur.State;
                if (!contentChanged && !fsrsChanged) return null;
                return new DiffModifiedCard(
                    sc.CardId, sc.Front, cur.Front, sc.Back, cur.Back,
                    sc.Stability, cur.Stability, contentChanged, fsrsChanged);
            })
            .Where(m => m is not null)
            .Cast<DiffModifiedCard>()
            .ToList();

        var added = currentCards
            .Where(c => !snapshotById.ContainsKey(c.Id))
            .Select(c => new DiffAddedCard(c.Id, c.Front, c.Back))
            .ToList();

        return new SnapshotDiffDto(deleted, modified, added);
    }

    public async Task<bool> Restore(string userId, string snapshotPublicId, RestoreRequest request)
    {
        var snapshot = await db.DeckSnapshots
            .FirstOrDefaultAsync(s => s.PublicId == snapshotPublicId && s.UserId == userId);
        if (snapshot?.DeckId is null) return false;

        var data = JsonSerializer.Deserialize<SnapshotData>(snapshot.Data, JsonOptions)!;
        var snapshotById = data.Cards.ToDictionary(c => c.CardId);
        var deckId = snapshot.DeckId.Value;

        // Restore deleted cards
        foreach (var cardId in request.RestoreDeletedCardIds)
        {
            if (!snapshotById.TryGetValue(cardId, out var sc)) continue;

            var existingCard = await db.Cards.FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId);
            if (existingCard is not null)
            {
                ApplySnapshotToCard(existingCard, sc);
                var alreadyInDeck = await db.DeckCards.AnyAsync(dc => dc.DeckId == deckId && dc.CardId == cardId);
                if (!alreadyInDeck)
                    db.DeckCards.Add(new DeckCard { DeckId = deckId, CardId = cardId });
            }
            else
            {
                var newCard = new Card
                {
                    Id = Guid.NewGuid(),
                    PublicId = NanoIdGenerator.New(),
                    UserId = userId,
                    Front = sc.Front,
                    Back = sc.Back,
                    FrontSvg = sc.FrontSvg,
                    BackSvg = sc.BackSvg,
                    SourceFile = sc.SourceFile,
                    SourceHeading = sc.SourceHeading,
                    CreatedAt = sc.CreatedAt,
                    Stability = sc.Stability,
                    Difficulty = sc.Difficulty,
                    Step = sc.Step,
                    DueAt = sc.DueAt,
                    State = sc.State,
                    LastReviewedAt = sc.LastReviewedAt,
                    IsSuspended = sc.IsSuspended,
                };
                db.Cards.Add(newCard);
                db.DeckCards.Add(new DeckCard { DeckId = deckId, CardId = newCard.Id });
            }
        }

        // Revert modified cards
        foreach (var cardId in request.RevertModifiedCardIds)
        {
            if (!snapshotById.TryGetValue(cardId, out var sc)) continue;
            var card = await db.Cards.FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId);
            if (card is not null)
                ApplySnapshotToCard(card, sc);
        }

        await db.SaveChangesAsync();
        return true;
    }

    private static void ApplySnapshotToCard(Card card, SnapshotCardData sc)
    {
        card.Front = sc.Front;
        card.Back = sc.Back;
        card.FrontSvg = sc.FrontSvg;
        card.BackSvg = sc.BackSvg;
        card.SourceFile = sc.SourceFile;
        card.SourceHeading = sc.SourceHeading;
        card.CreatedAt = sc.CreatedAt;
        card.Stability = sc.Stability;
        card.Difficulty = sc.Difficulty;
        card.Step = sc.Step;
        card.DueAt = sc.DueAt;
        card.State = sc.State;
        card.LastReviewedAt = sc.LastReviewedAt;
        card.IsSuspended = sc.IsSuspended;
    }

    private async Task EnforceRetention(string userId)
    {
        var deckIds = await db.DeckSnapshots
            .Where(s => s.UserId == userId)
            .Select(s => s.DeckId)
            .Distinct()
            .ToListAsync();

        foreach (var deckId in deckIds)
        {
            var excess = await db.DeckSnapshots
                .Where(s => s.DeckId == deckId && s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .Skip(MaxSnapshotsPerDeck)
                .ToListAsync();

            if (excess.Count > 0)
                db.DeckSnapshots.RemoveRange(excess);
        }

        await db.SaveChangesAsync();
    }
}
