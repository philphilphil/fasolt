using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using FSRS.Core.Enums;
using FSRS.Core.Interfaces;
using FsrsCard = FSRS.Core.Models.Card;

namespace Fasolt.Server.Api.Endpoints;

public static class ReviewEndpoints
{
    private static readonly Dictionary<string, Rating> ValidRatings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["again"] = Rating.Again,
        ["hard"] = Rating.Hard,
        ["good"] = Rating.Good,
        ["easy"] = Rating.Easy,
    };

    public static void MapReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/review").RequireAuthorization();
        group.MapGet("/due", GetDueCards);
        group.MapPost("/rate", RateCard);
        group.MapGet("/stats", GetStats);
        group.MapGet("/overview", GetOverview);
    }

    private static string MapState(State state) => state switch
    {
        State.Learning => "learning",
        State.Review => "review",
        State.Relearning => "relearning",
        _ => "new",
    };

    private static State ParseState(string state) => state switch
    {
        "learning" => State.Learning,
        "review" => State.Review,
        "relearning" => State.Relearning,
        _ => default,
    };

    private static async Task<IResult> GetDueCards(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, AppDbContext db, int limit = 50, string? deckId = null)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var take = Math.Clamp(limit, 1, 200);
        var now = DateTimeOffset.UtcNow;
        var query = db.Cards
            .Where(c => c.UserId == user.Id && (c.DueAt == null || c.DueAt <= now));

        if (deckId is not null)
        {
            var deck = await db.Decks.FirstOrDefaultAsync(d => d.PublicId == deckId && d.UserId == user.Id);
            if (deck is null) return Results.NotFound();
            query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deck.Id));
        }

        var cards = await query
            .OrderBy(c => c.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(c => c.CreatedAt)
            .Take(take)
            .Select(c => new DueCardDto(c.PublicId, c.Front, c.Back, c.SourceFile, c.SourceHeading, c.State))
            .ToListAsync();

        return Results.Ok(cards);
    }

    private static async Task<IResult> RateCard(
        RateCardRequest request, ClaimsPrincipal principal, UserManager<AppUser> userManager,
        AppDbContext db, IScheduler scheduler)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (!ValidRatings.TryGetValue(request.Rating, out var fsrsRating))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rating"] = ["Rating must be 'again', 'hard', 'good', or 'easy'."]
            });

        var card = await db.Cards.FirstOrDefaultAsync(c => c.PublicId == request.CardId && c.UserId == user.Id);
        if (card is null) return Results.NotFound();

        // Build FSRS.Core Card from our entity
        // For new/unreviewed cards, use a fresh FsrsCard with defaults
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

        var now = DateTime.UtcNow;
        var (updated, _) = scheduler.ReviewCard(fsrsCard, fsrsRating, now, null);

        // Map back to our entity
        card.Stability = updated.Stability;
        card.Difficulty = updated.Difficulty;
        card.Step = updated.Step;
        card.State = MapState(updated.State);
        card.DueAt = new DateTimeOffset(updated.Due, TimeSpan.Zero);
        card.LastReviewedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(new RateCardResponse(card.PublicId, card.Stability, card.Difficulty, card.DueAt, card.State));
    }

    private static async Task<IResult> GetStats(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var now = DateTimeOffset.UtcNow;
        var dueCount = await db.Cards.CountAsync(c => c.UserId == user.Id && (c.DueAt == null || c.DueAt <= now));
        var totalCards = await db.Cards.CountAsync(c => c.UserId == user.Id);
        var todayStart = new DateTimeOffset(now.Date, TimeSpan.Zero);
        var studiedToday = await db.Cards.CountAsync(c =>
            c.UserId == user.Id && c.LastReviewedAt != null && c.LastReviewedAt >= todayStart);

        return Results.Ok(new ReviewStatsDto(dueCount, totalCards, studiedToday));
    }

    private static async Task<IResult> GetOverview(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, OverviewService overviewService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var overview = await overviewService.GetOverview(user.Id);
        return Results.Ok(overview);
    }
}
