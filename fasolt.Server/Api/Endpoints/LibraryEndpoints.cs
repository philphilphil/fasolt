using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class LibraryEndpoints
{
    public static void MapLibraryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/library");

        // Anonymous reads — these are the SEO/funnel surface, so they must work
        // logged out. Their own rate-limit policy is keyed by IP.
        group.MapGet("/", ListPublicDecks).AllowAnonymous().RequireRateLimiting("library");
        group.MapGet("/decks/{publicId}", GetPublicDeck).AllowAnonymous().RequireRateLimiting("library");

        group.MapPost("/decks/{publicId}/copy", CopyDeck)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("api");

        group.MapPost("/decks/{publicId}/subscribe", Subscribe)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("api");

        group.MapDelete("/decks/{publicId}/subscribe", Unsubscribe)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("api");
    }

    private static async Task<IResult> ListPublicDecks(
        string? q,
        string? sort,
        int? page,
        int? pageSize,
        LibraryService libraryService)
    {
        var result = await libraryService.ListPublicDecks(
            q, sort, page ?? 1, pageSize ?? LibraryService.DefaultPageSize);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPublicDeck(string publicId, LibraryService libraryService)
    {
        var deck = await libraryService.GetPublicDeck(publicId);
        return deck is null ? Results.NotFound() : Results.Ok(deck);
    }

    private static async Task<IResult> CopyDeck(
        string publicId,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        LibraryService libraryService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await libraryService.CopyDeck(user.Id, publicId);

        return result.Error switch
        {
            CopyDeckError.NotFound => Results.NotFound(),
            CopyDeckError.DeckTooLarge => Results.BadRequest(new
            {
                error = "deck_too_large",
                message = $"Decks with more than {PublishingService.MaxCardsInPublicDeck} cards cannot be imported.",
            }),
            _ => Results.Created($"/api/decks/{result.Deck!.Id}", result.Deck),
        };
    }

    /// <summary>
    /// Links a shared deck into the caller's account. Idempotent: a repeat subscribe
    /// returns 200 with the existing link instead of 201.
    /// </summary>
    private static async Task<IResult> Subscribe(
        string publicId,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSubscriptionService subscriptionService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await subscriptionService.Subscribe(user.Id, publicId);

        return result.Error switch
        {
            SubscribeError.NotFound => Results.NotFound(),
            SubscribeError.OwnDeck => Results.BadRequest(new
            {
                error = "own_deck",
                message = "You already own this deck.",
            }),
            SubscribeError.DeckTooLarge => Results.BadRequest(new
            {
                error = "deck_too_large",
                message = $"Decks with more than {PublishingService.MaxCardsInPublicDeck} cards cannot be imported.",
            }),
            _ => result.Created
                ? Results.Created($"/api/decks/{result.Deck!.Id}", result.Deck)
                : Results.Ok(result.Deck),
        };
    }

    private static async Task<IResult> Unsubscribe(
        string publicId,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckSubscriptionService subscriptionService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var removed = await subscriptionService.Unsubscribe(user.Id, publicId);
        return removed ? Results.NoContent() : Results.NotFound();
    }
}
