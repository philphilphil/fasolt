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

public class ReviewService(AppDbContext db, TimeProvider timeProvider, StudyStatsService studyStatsService)
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

    private async Task<(IScheduler Scheduler, int DayStartHour, TimeZoneInfo TimeZone)> CreateSchedulerForUser(string userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var options = new SchedulerOptions
        {
            DesiredRetention = user.DesiredRetention ?? 0.9,
            MaximumInterval = user.MaximumInterval ?? 36500,
            EnableFuzzing = true,
        };
        var scheduler = new SchedulerFactory(options).CreateScheduler();
        var dayStartHour = user.DayStartHour ?? DueTimeRounder.DefaultDayStartHour;
        var tz = DueTimeRounder.ResolveTimeZone(user.TimeZone);
        return (scheduler, dayStartHour, tz);
    }

    public async Task<List<DueCardDto>> GetDueCards(string userId, int limit = 50, string? deckId = null)
    {
        var take = Math.Clamp(limit, 1, 200);
        var now = timeProvider.GetUtcNow();
        var query = db.Cards
            .Where(c => c.UserId == userId)
            .Where(ReviewStateQuery.DueBy(userId, now));

        query = query.Where(ReviewStateQuery.NotSuspendedBy(userId));
        query = query.Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended));

        if (deckId is not null)
        {
            var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == userId);
            if (deck is null) return null!; // endpoint returns NotFound
            if (deck.IsSuspended) return [];
            query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deck.Id));
        }

        // LEFT JOIN the user's review state; no row means the card is still "new".
        var joined =
            from c in query
            join r in db.ReviewStates.Where(r => r.UserId == userId) on c.Id equals r.CardId into g
            from rs in g.DefaultIfEmpty()
            select new { Card = c, State = rs };

        return await joined
            .OrderBy(x => x.State.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.Card.CreatedAt)
            .Take(take)
            .Select(x => new DueCardDto(
                x.Card.PublicId, x.Card.Front, x.Card.Back, x.Card.SourceFile,
                x.State.State ?? "new", x.Card.FrontSvg, x.Card.BackSvg))
            .ToListAsync();
    }

    public async Task<List<DueCardDto>?> GetCustomStudyCards(string userId, string deckPublicId)
    {
        var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);
        if (deck is null) return null;

        var eligible = db.Cards
            .Where(c => c.UserId == userId && c.DeckCards.Any(dc => dc.DeckId == deck.Id))
            .Where(ReviewStateQuery.NotSuspendedBy(userId));

        var cards = await (
            from c in eligible
            join r in db.ReviewStates.Where(r => r.UserId == userId) on c.Id equals r.CardId into g
            from rs in g.DefaultIfEmpty()
            select new DueCardDto(
                c.PublicId, c.Front, c.Back, c.SourceFile,
                rs.State ?? "new", c.FrontSvg, c.BackSvg))
            .ToListAsync();

        for (var i = cards.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }

        return cards;
    }

    public async Task<RateCardResponse?> RateCard(string userId, RateCardRequest request)
    {
        if (!ValidRatings.TryGetValue(request.Rating, out var fsrsRating))
            return null; // endpoint returns ValidationProblem

        var card = await db.Cards.FirstOrDefaultAsync(c => c.PublicId == request.CardId && c.UserId == userId);
        if (card is null) return null;

        // Lazy creation point: the first review of a card materializes its ReviewState row.
        var state = await ReviewStateQuery.GetOrCreateAsync(db, userId, card.Id);

        var fsrsCard = state.State == "new"
            ? new FsrsCard { Due = state.DueAt?.UtcDateTime ?? card.CreatedAt.UtcDateTime }
            : new FsrsCard
            {
                State = ParseState(state.State),
                Stability = state.Stability,
                Difficulty = state.Difficulty,
                Step = state.Step,
                Due = state.DueAt?.UtcDateTime ?? card.CreatedAt.UtcDateTime,
                LastReview = state.LastReviewedAt?.UtcDateTime,
            };

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var (scheduler, dayStartHour, tz) = await CreateSchedulerForUser(userId);
        var (updated, _) = scheduler.ReviewCard(fsrsCard, fsrsRating, now, null);

        var roundedDue = DueTimeRounder.RoundDueUtc(updated.Due, now, dayStartHour, tz);

        state.Stability = updated.Stability;
        state.Difficulty = updated.Difficulty;
        state.Step = updated.Step;
        state.State = MapState(updated.State);
        state.DueAt = new DateTimeOffset(roundedDue, TimeSpan.Zero);
        state.LastReviewedAt = timeProvider.GetUtcNow();

        db.ReviewLogs.Add(new ReviewLog
        {
            UserId = userId,
            CardId = card.Id,
            Rating = request.Rating.ToLowerInvariant(),
            ReviewedAt = timeProvider.GetUtcNow(),
            ScheduledDueAfter = state.DueAt,
            StateAfter = state.State,
        });

        await db.SaveChangesAsync();
        await studyStatsService.UpdateBestStreakIfNeeded(userId);

        return new RateCardResponse(card.PublicId, state.Stability, state.Difficulty, state.DueAt, state.State);
    }

    public async Task<ReviewStatsDto> GetStats(string userId)
    {
        var now = timeProvider.GetUtcNow();
        var activeCards = db.Cards
            .Where(c => c.UserId == userId)
            .Where(ReviewStateQuery.NotSuspendedBy(userId))
            .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended));

        var dueCount = await activeCards.CountAsync(ReviewStateQuery.DueBy(userId, now));
        var totalCards = await activeCards.CountAsync();
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var studiedToday = await activeCards.CountAsync(c =>
            c.ReviewStates.Any(r => r.UserId == userId && r.LastReviewedAt >= todayStart));

        return new ReviewStatsDto(dueCount, totalCards, studiedToday);
    }
}
