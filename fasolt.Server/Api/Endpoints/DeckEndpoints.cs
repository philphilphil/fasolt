using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class DeckEndpoints
{
    public static void MapDeckEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/decks").RequireAuthorization().RequireRateLimiting("api");

        group.MapPost("/", Create);
        group.MapGet("/", List);
        group.MapGet("/{id}", GetById);
        group.MapPut("/{id}", Update);
        group.MapDelete("/{id}", Delete);
        group.MapPost("/{id}/cards", AddCards);
        group.MapDelete("/{id}/cards/{cardId}", RemoveCard);
        group.MapPut("/{id}/suspended", SetSuspended);
    }

    private static async Task<IResult> Create(
        CreateDeckRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckService deckService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name is required."]
            });

        var dto = await deckService.CreateDeck(user.Id, request.Name, request.Description);
        return Results.Created($"/api/decks/{dto.Id}", dto);
    }

    private static async Task<IResult> List(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckService deckService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var decks = await deckService.ListDecks(user.Id);
        return Results.Ok(decks);
    }

    private static async Task<IResult> GetById(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckService deckService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var dto = await deckService.GetDeck(user.Id, id);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> Update(
        string id,
        UpdateDeckRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckService deckService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name is required."]
            });

        var dto = await deckService.UpdateDeck(user.Id, id, request.Name, request.Description);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> Delete(
        string id,
        bool? deleteCards,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckService deckService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await deckService.DeleteDeck(user.Id, id, deleteCards == true);
        return result.Deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> AddCards(
        string id,
        AddCardsToDeckRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckService deckService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await deckService.AddCards(user.Id, id, request.CardIds);

        return result switch
        {
            AddCardsResult.DeckNotFound => Results.NotFound(),
            AddCardsResult.CardsNotFound => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["cardIds"] = ["One or more cards not found."]
            }),
            _ => Results.NoContent(),
        };
    }

    private static async Task<IResult> RemoveCard(
        string id,
        string cardId,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckService deckService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await deckService.RemoveCard(user.Id, id, cardId);
        return result == RemoveCardResult.Success ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> SetSuspended(
        string id,
        SetDeckSuspendedRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        DeckService deckService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var dto = await deckService.SetSuspended(user.Id, id, request.IsSuspended);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }
}
