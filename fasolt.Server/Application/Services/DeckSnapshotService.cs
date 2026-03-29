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
