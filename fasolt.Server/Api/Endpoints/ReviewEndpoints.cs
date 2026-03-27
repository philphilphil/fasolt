using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/review").RequireAuthorization().RequireRateLimiting("api");
        group.MapGet("/due", GetDueCards);
        group.MapPost("/rate", RateCard);
        group.MapGet("/stats", GetStats);
        group.MapGet("/overview", GetOverview);
    }

    private static async Task<IResult> GetDueCards(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, ReviewService reviewService,
        int limit = 50, string? deckId = null)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var cards = await reviewService.GetDueCards(user.Id, limit, deckId);
        if (cards is null) return Results.NotFound();
        return Results.Ok(cards);
    }

    private static async Task<IResult> RateCard(
        RateCardRequest request, ClaimsPrincipal principal, UserManager<AppUser> userManager,
        ReviewService reviewService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        // Check if rating is valid before calling service
        var validRatings = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "again", "hard", "good", "easy" };
        if (!validRatings.Contains(request.Rating))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["rating"] = ["Rating must be 'again', 'hard', 'good', or 'easy'."]
            });

        var result = await reviewService.RateCard(user.Id, request);
        if (result is null) return Results.NotFound();
        return Results.Ok(result);
    }

    private static async Task<IResult> GetStats(
        ClaimsPrincipal principal, UserManager<AppUser> userManager, ReviewService reviewService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var stats = await reviewService.GetStats(user.Id);
        return Results.Ok(stats);
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
