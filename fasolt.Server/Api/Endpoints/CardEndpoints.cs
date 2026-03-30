using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class CardEndpoints
{
    public static void MapCardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/cards").RequireAuthorization("EmailVerified").RequireRateLimiting("api");

        group.MapPost("/", Create);
        group.MapPost("/bulk", BulkCreate);
        group.MapGet("/", List);
        group.MapGet("/{id}", GetById);
        group.MapPut("/{id}", Update);
        group.MapDelete("/{id}", Delete);
        group.MapPost("/{id}/reset", ResetProgress);
        group.MapPut("/{id}/suspended", SetSuspended);
    }

    private static async Task<IResult> Create(
        CreateCardRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CardService cardService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Front) || string.IsNullOrWhiteSpace(request.Back))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Front and back are required."]
            });

        try
        {
            var dto = await cardService.CreateCard(user.Id, request.Front, request.Back, request.SourceFile, request.SourceHeading, request.FrontSvg, request.BackSvg, request.DeckId);
            return Results.Created($"/api/cards/{dto.Id}", dto);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.BadRequest(new { error = "validation_error", message = ex.Message });
        }
    }

    private static async Task<IResult> List(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CardService cardService,
        string? sourceFile = null,
        string? deckId = null,
        int? limit = null,
        string? after = null)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var result = await cardService.ListCards(user.Id, sourceFile, deckId, limit, after);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetById(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CardService cardService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var dto = await cardService.GetCard(user.Id, id);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> Update(
        string id,
        UpdateCardRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CardService cardService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Front) || string.IsNullOrWhiteSpace(request.Back))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [""] = ["Front and back are required."]
            });

        var dto = await cardService.UpdateCard(user.Id, id, request);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> Delete(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CardService cardService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var deleted = await cardService.DeleteCard(user.Id, id);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ResetProgress(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CardService cardService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var dto = await cardService.ResetProgress(user.Id, id);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> SetSuspended(
        string id,
        SetCardSuspendedRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CardService cardService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var dto = await cardService.SetSuspended(user.Id, id, request.IsSuspended);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> BulkCreate(
        BulkCreateCardsRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CardService cardService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (request.Cards is null || request.Cards.Count == 0)
            return Results.BadRequest(new { error = "validation_error", message = "Cards array is required and must not be empty" });

        if (request.Cards.Count > 100)
            return Results.BadRequest(new { error = "validation_error", message = "Maximum 100 cards per request" });

        // Validate all cards first (atomic — if any fail, none are created)
        var validationErrors = new List<object>();
        for (var i = 0; i < request.Cards.Count; i++)
        {
            var c = request.Cards[i];
            if (string.IsNullOrWhiteSpace(c.Front))
                validationErrors.Add(new { field = $"cards[{i}].front", message = "Front is required" });
            if (string.IsNullOrWhiteSpace(c.Back))
                validationErrors.Add(new { field = $"cards[{i}].back", message = "Back is required" });
        }
        if (validationErrors.Count > 0)
            return Results.BadRequest(new { error = "validation_error", message = "Validation failed", details = validationErrors });

        var result = await cardService.BulkCreateCards(user.Id, request.Cards, request.SourceFile, request.DeckId);

        if (result.IsDeckNotFound)
            return Results.BadRequest(new { error = "validation_error", message = "Deck not found or does not belong to you" });

        return Results.Created("/api/cards/bulk", result.Response);
    }
}
