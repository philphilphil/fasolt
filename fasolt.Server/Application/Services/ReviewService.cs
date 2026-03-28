using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using FSRS.Core.Configurations;
using FSRS.Core.Enums;
using FSRS.Core.Interfaces;
using FSRS.Core.Services;
using FsrsCard = FSRS.Core.Models.Card;

namespace Fasolt.Server.Application.Services;

public class ReviewService(AppDbContext db, TimeProvider timeProvider)
{
    private static readonly Dictionary<string, Rating> ValidRatings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["again"] = Rating.Again,
        ["hard"] = Rating.Hard,
        ["good"] = Rating.Good,
        ["easy"] = Rating.Easy,
    };

    internal static string MapState(State state) => state switch
    {
        State.Learning => "learning",
        State.Review => "review",
        State.Relearning => "relearning",
        _ => "new",
    };

    internal static State ParseState(string state) => state switch
    {
        "learning" => State.Learning,
        "review" => State.Review,
        "relearning" => State.Relearning,
        _ => default,
    };

    private async Task<IScheduler> CreateSchedulerForUser(string userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var options = new SchedulerOptions
        {
            DesiredRetention = user.DesiredRetention ?? 0.9,
            MaximumInterval = user.MaximumInterval ?? 36500,
            EnableFuzzing = true,
        };
        return new SchedulerFactory(options).CreateScheduler();
    }

    public async Task<List<DueCardDto>> GetDueCards(string userId, int limit = 50, string? deckId = null)
    {
        var take = Math.Clamp(limit, 1, 200);
        var now = timeProvider.GetUtcNow();
        var query = db.Cards
            .Where(c => c.UserId == userId && (c.DueAt == null || c.DueAt <= now));

        query = query.Where(c => !c.IsSuspended);
        query = query.Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended));

        if (deckId is not null)
        {
            var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == userId);
            if (deck is null) return null!; // endpoint returns NotFound
            if (deck.IsSuspended) return [];
            query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deck.Id));
        }

        return await query
            .OrderBy(c => c.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(c => c.CreatedAt)
            .Take(take)
            .Select(c => new DueCardDto(c.PublicId, c.Front, c.Back, c.SourceFile, c.SourceHeading, c.State, c.FrontSvg, c.BackSvg))
            .ToListAsync();
    }

    public async Task<RateCardResponse?> RateCard(string userId, RateCardRequest request)
    {
        if (!ValidRatings.TryGetValue(request.Rating, out var fsrsRating))
            return null; // endpoint returns ValidationProblem

        var card = await db.Cards.FirstOrDefaultAsync(c => c.PublicId == request.CardId && c.UserId == userId);
        if (card is null) return null;

        var fsrsCard = card.State == "new"
            ? new FsrsCard { Due = card.DueAt?.UtcDateTime ?? card.CreatedAt.UtcDateTime }
            : new FsrsCard
            {
                State = ParseState(card.State),
                Stability = card.Stability,
                Difficulty = card.Difficulty,
                Step = card.Step,
                Due = card.DueAt?.UtcDateTime ?? card.CreatedAt.UtcDateTime,
                LastReview = card.LastReviewedAt?.UtcDateTime,
            };

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var scheduler = await CreateSchedulerForUser(userId);
        var (updated, _) = scheduler.ReviewCard(fsrsCard, fsrsRating, now, null);

        card.Stability = updated.Stability;
        card.Difficulty = updated.Difficulty;
        card.Step = updated.Step;
        card.State = MapState(updated.State);
        card.DueAt = new DateTimeOffset(updated.Due, TimeSpan.Zero);
        card.LastReviewedAt = timeProvider.GetUtcNow();

        await db.SaveChangesAsync();
        return new RateCardResponse(card.PublicId, card.Stability, card.Difficulty, card.DueAt, card.State);
    }

    public async Task<ReviewStatsDto> GetStats(string userId)
    {
        var now = timeProvider.GetUtcNow();
        var activeCards = db.Cards
            .Where(c => c.UserId == userId)
            .Where(c => !c.IsSuspended)
            .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended));

        var dueCount = await activeCards.CountAsync(c => c.DueAt == null || c.DueAt <= now);
        var totalCards = await activeCards.CountAsync();
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var studiedToday = await activeCards.CountAsync(c =>
            c.LastReviewedAt != null && c.LastReviewedAt >= todayStart);

        return new ReviewStatsDto(dueCount, totalCards, studiedToday);
    }
}
