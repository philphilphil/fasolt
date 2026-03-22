using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Api.Endpoints;

public static class DeckEndpoints
{
    public static void MapDeckEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/decks").RequireAuthorization();

        group.MapPost("/", Create);
        group.MapGet("/", List);
        group.MapGet("/{id:guid}", GetById);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/cards", AddCards);
        group.MapDelete("/{id:guid}/cards/{cardId:guid}", RemoveCard);
    }

    private static async Task<IResult> Create(
        CreateDeckRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name is required."]
            });

        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Decks.Add(deck);
        await db.SaveChangesAsync();

        return Results.Created($"/api/decks/{deck.Id}",
            new DeckDto(deck.Id, deck.Name, deck.Description, 0, 0, deck.CreatedAt));
    }

    private static async Task<IResult> List(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var now = DateTimeOffset.UtcNow;

        var decks = await db.Decks
            .Where(d => d.UserId == user.Id)
            .OrderBy(d => d.Name)
            .Select(d => new DeckDto(
                d.Id,
                d.Name,
                d.Description,
                d.Cards.Count(dc => dc.Card.DeletedAt == null),
                d.Cards.Count(dc => dc.Card.DeletedAt == null && (dc.Card.DueAt == null || dc.Card.DueAt <= now)),
                d.CreatedAt))
            .ToListAsync();

        return Results.Ok(decks);
    }

    private static async Task<IResult> GetById(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == user.Id);

        if (deck is null) return Results.NotFound();

        var now = DateTimeOffset.UtcNow;

        var cards = await db.DeckCards
            .Where(dc => dc.DeckId == id && dc.Card.DeletedAt == null)
            .OrderBy(dc => dc.Card.DueAt)
            .Select(dc => new DeckCardDto(dc.CardId, dc.Card.Front, dc.Card.Back, dc.Card.SourceFile, dc.Card.SourceHeading, dc.Card.State, dc.Card.DueAt))
            .ToListAsync();

        var dueCount = cards.Count(c => c.DueAt == null || c.DueAt <= now);

        return Results.Ok(new DeckDetailDto(deck.Id, deck.Name, deck.Description, cards.Count, dueCount, cards));
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateDeckRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = ["Name is required."]
            });

        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == user.Id);

        if (deck is null) return Results.NotFound();

        deck.Name = request.Name.Trim();
        deck.Description = request.Description?.Trim();
        await db.SaveChangesAsync();

        return Results.Ok(new DeckDto(deck.Id, deck.Name, deck.Description, 0, 0, deck.CreatedAt));
    }

    private static async Task<IResult> Delete(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == user.Id);

        if (deck is null) return Results.NotFound();

        db.Decks.Remove(deck);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    private static async Task<IResult> AddCards(
        Guid id,
        AddCardsToDeckRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == user.Id);

        if (deck is null) return Results.NotFound();

        var userCardIds = await db.Cards
            .Where(c => c.UserId == user.Id && request.CardIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync();

        if (userCardIds.Count != request.CardIds.Count)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["cardIds"] = ["One or more cards not found."]
            });

        var existingCardIds = await db.DeckCards
            .Where(dc => dc.DeckId == id && request.CardIds.Contains(dc.CardId))
            .Select(dc => dc.CardId)
            .ToListAsync();

        var newCardIds = userCardIds.Except(existingCardIds);

        foreach (var cardId in newCardIds)
        {
            db.DeckCards.Add(new DeckCard { DeckId = id, CardId = cardId });
        }

        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> RemoveCard(
        Guid id,
        Guid cardId,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        AppDbContext db)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == user.Id);

        if (deck is null) return Results.NotFound();

        var deckCard = await db.DeckCards
            .FirstOrDefaultAsync(dc => dc.DeckId == id && dc.CardId == cardId);

        if (deckCard is null) return Results.NotFound();

        db.DeckCards.Remove(deckCard);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

}
