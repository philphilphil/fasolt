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

        // Fetch current deck cards once for content diff counts
        var currentCards = await db.DeckCards
            .Where(dc => dc.DeckId == deck.Id)
            .Include(dc => dc.Card)
            .Select(dc => dc.Card)
            .ToListAsync();
        var currentById = currentCards.ToDictionary(c => c.Id);

        var snapshots = await db.DeckSnapshots
            .Where(s => s.DeckId == deck.Id && s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return snapshots.Select(s =>
        {
            var data = JsonSerializer.Deserialize<SnapshotData>(s.Data, JsonOptions)!;
            var changes = CountContentChanges(data, currentById);
            return new SnapshotListDto(s.PublicId, deck.Name, s.CardCount, s.CreatedAt, changes);
        }).ToList();
    }

    public async Task<List<SnapshotListDto>> ListRecent(string userId, int limit = 50)
    {
        return await db.DeckSnapshots
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .Select(s => new SnapshotListDto(s.PublicId, s.Deck != null ? s.Deck.Name : null, s.CardCount, s.CreatedAt, null))
            .ToListAsync();
    }

    private static int CountContentChanges(SnapshotData data, Dictionary<Guid, Card> currentById)
    {
        var count = 0;
        foreach (var sc in data.Cards)
        {
            if (!currentById.TryGetValue(sc.CardId, out var cur))
            {
                count++; // deleted or unassigned
                continue;
            }
            if (sc.Front != cur.Front || sc.Back != cur.Back
                || !SvgEqual(sc.FrontSvg, cur.FrontSvg) || !SvgEqual(sc.BackSvg, cur.BackSvg))
                count++; // content changed
        }
        // Cards in deck but not in snapshot = added
        var snapshotIds = new HashSet<Guid>(data.Cards.Select(c => c.CardId));
        count += currentById.Keys.Count(id => !snapshotIds.Contains(id));
        return count;
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

        var missingCardIds = data.Cards
            .Where(sc => !currentById.ContainsKey(sc.CardId))
            .Select(sc => sc.CardId)
            .ToList();
        var existingCardIds = missingCardIds.Count > 0
            ? (await db.Cards.Where(c => missingCardIds.Contains(c.Id)).Select(c => c.Id).ToListAsync()).ToHashSet()
            : new HashSet<Guid>();

        var deleted = data.Cards
            .Where(sc => !currentById.ContainsKey(sc.CardId))
            .Select(sc => new DiffDeletedCard(sc.CardId, sc.Front, sc.Back, sc.SourceFile, sc.Stability, sc.DueAt, existingCardIds.Contains(sc.CardId)))
            .ToList();

        var modified = data.Cards
            .Where(sc => currentById.ContainsKey(sc.CardId))
            .Select(sc =>
            {
                var cur = currentById[sc.CardId];
                var frontSvgChanged = !SvgEqual(sc.FrontSvg, cur.FrontSvg);
                var backSvgChanged = !SvgEqual(sc.BackSvg, cur.BackSvg);
                var contentChanged = sc.Front != cur.Front || sc.Back != cur.Back
                    || frontSvgChanged || backSvgChanged;
                if (!contentChanged) return null;
                return new DiffModifiedCard(
                    sc.CardId, sc.Front, cur.Front, sc.Back, cur.Back,
                    frontSvgChanged ? sc.FrontSvg : null,
                    frontSvgChanged ? cur.FrontSvg : null,
                    backSvgChanged ? sc.BackSvg : null,
                    backSvgChanged ? cur.BackSvg : null);
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
                // Card still exists — just re-add to deck, don't overwrite content
                var alreadyInDeck = await db.DeckCards.AnyAsync(dc => dc.DeckId == deckId && dc.CardId == cardId);
                if (!alreadyInDeck)
                    db.DeckCards.Add(new DeckCard { DeckId = deckId, CardId = cardId });
            }
            else
            {
                // Recreate card with original ID so snapshot references stay valid
                var newCard = new Card
                {
                    Id = sc.CardId,
                    PublicId = sc.PublicId,
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
                db.DeckCards.Add(new DeckCard { DeckId = deckId, CardId = sc.CardId });
            }
        }

        // Revert modified cards
        foreach (var cardId in request.RevertModifiedCardIds)
        {
            if (!snapshotById.TryGetValue(cardId, out var sc)) continue;
            var card = await db.Cards.FirstOrDefaultAsync(c => c.Id == cardId && c.UserId == userId);
            if (card is not null)
                ApplySnapshotContentToCard(card, sc);
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> Delete(string userId, string snapshotPublicId)
    {
        var snapshot = await db.DeckSnapshots
            .FirstOrDefaultAsync(s => s.PublicId == snapshotPublicId && s.UserId == userId);
        if (snapshot is null) return false;

        db.DeckSnapshots.Remove(snapshot);
        await db.SaveChangesAsync();
        return true;
    }

    private static bool SvgEqual(string? a, string? b)
    {
        if (a == b) return true;
        if (a is null || b is null) return false;
        // Normalize whitespace differences from SVG sanitizer reformatting
        return NormalizeSvg(a) == NormalizeSvg(b);
    }

    private static string NormalizeSvg(string svg)
    {
        // Collapse whitespace runs to single space, normalize self-closing tag spacing
        var normalized = System.Text.RegularExpressions.Regex.Replace(svg.Trim(), @"\s+", " ");
        return normalized.Replace(" />", "/>");
    }

    private static void ApplySnapshotContentToCard(Card card, SnapshotCardData sc)
    {
        card.Front = sc.Front;
        card.Back = sc.Back;
        card.FrontSvg = sc.FrontSvg;
        card.BackSvg = sc.BackSvg;
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
