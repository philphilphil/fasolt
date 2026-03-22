using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Api.Endpoints;

public static class ReviewEndpoints
{
    private static readonly int[] ValidQualities = [0, 2, 4, 5];

    public static void MapReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/review").RequireAuthorization();
        group.MapGet("/due", GetDueCards);
        group.MapPost("/rate", RateCard);
        group.MapGet("/stats", GetStats);
    }

    private static async Task<IResult> GetDueCards(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, AppDbContext db, int limit = 50, Guid? deckId = null)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var take = Math.Clamp(limit, 1, 200);
        var now = DateTimeOffset.UtcNow;
        var query = db.Cards
            .Where(c => c.UserId == user.Id && (c.DueAt == null || c.DueAt <= now));

        if (deckId.HasValue)
            query = query.Where(c => c.DeckCards.Any(dc => dc.DeckId == deckId.Value));

        var cards = await query
            .OrderBy(c => c.DueAt ?? DateTimeOffset.MaxValue)
            .ThenBy(c => c.CreatedAt)
            .Take(take)
            .Select(c => new DueCardDto(c.Id, c.Front, c.Back, c.SourceFile, c.SourceHeading, c.State, c.EaseFactor, c.Interval, c.Repetitions))
            .ToListAsync();

        return Results.Ok(cards);
    }

    private static async Task<IResult> RateCard(
        RateCardRequest request, ClaimsPrincipal principal, UserManager<AppUser> userManager, AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (!ValidQualities.Contains(request.Quality))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["quality"] = ["Quality must be 0 (Again), 2 (Hard), 4 (Good), or 5 (Easy)."]
            });

        var card = await db.Cards.FirstOrDefaultAsync(c => c.Id == request.CardId && c.UserId == user.Id);
        if (card is null) return Results.NotFound();

        var result = Sm2Algorithm.Calculate(card.EaseFactor, card.Interval, card.Repetitions, request.Quality);

        card.EaseFactor = result.EaseFactor;
        card.Interval = result.Interval;
        card.Repetitions = result.Repetitions;
        card.State = result.State;
        card.DueAt = result.Interval == 0 ? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow.AddDays(result.Interval);
        card.LastReviewedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(new RateCardResponse(card.Id, card.EaseFactor, card.Interval, card.Repetitions, card.DueAt, card.State));
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
}
