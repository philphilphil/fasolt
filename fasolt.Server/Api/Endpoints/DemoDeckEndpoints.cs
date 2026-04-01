using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Api.Endpoints;

public static class DemoDeckEndpoints
{
    private static readonly List<BulkCardItem> DemoCards =
    [
        new("What is Fasolt?",
            "Fasolt is **MCP-first spaced repetition** for your markdown notes. Your AI agent reads your notes, extracts key concepts, and creates flashcards — then spaced repetition schedules reviews at optimal intervals so you actually remember what you read."),
        new("How are flashcards created in Fasolt?",
            "Your AI agent (e.g. Claude) reads your local markdown files and pushes flashcards to Fasolt via the **MCP server** or REST API. You don't need to write cards manually — just ask your agent to create cards from your notes."),
        new("What is spaced repetition?",
            "A study technique backed by memory research. Instead of cramming, cards are shown at **increasing intervals** based on how well you recall them. Fasolt uses the **FSRS algorithm** to calculate optimal review timing — so you study less but remember more."),
        new("What are sources and decks?",
            "**Sources** track provenance — each card remembers which file and heading it came from, so you can always trace back to the original material.\n\n**Decks** let you organize cards into focused study groups (e.g. \"Biology 101\", \"Rust Basics\") for targeted review sessions."),
        new("How do you format code on a card?",
            "Cards support full **markdown** including fenced code blocks:\n\n```python\ndef hello(name: str) -> str:\n    return f\"Hello, {name}!\"\n```\n\nThis renders with syntax highlighting during review — great for programming flashcards."),
    ];

    public static void MapDemoDeckEndpoints(this WebApplication app)
    {
        app.MapPost("/api/demo-deck", Create)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("api");
    }

    private static async Task<IResult> Create(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CardService cardService,
        DeckService deckService)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var deck = await deckService.CreateDeck(user.Id, "Welcome to Fasolt", "A quick tour of how Fasolt works — feel free to delete this deck when you're done.");
        await cardService.BulkCreateCards(user.Id, DemoCards, sourceFile: null, deckId: deck.Id);

        return Results.Created($"/api/decks/{deck.Id}", deck);
    }
}
