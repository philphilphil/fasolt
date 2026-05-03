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
        new("What is Fasolt and how does it work?",
            "Fasolt is **MCP-first spaced repetition** for your markdown notes. Your AI agent (e.g. Claude) reads your local files, extracts key concepts, and pushes flashcards via the **MCP server** — no manual card writing needed. Study your cards on the **web** or the **iOS app**. Spaced repetition schedules reviews at optimal intervals so you actually remember what you read."),
        new("What is spaced repetition?",
            "A study technique backed by memory research. Instead of cramming, cards are shown at **increasing intervals** based on how well you recall them. Fasolt uses the **FSRS algorithm** to calculate optimal review timing — so you study less but remember more."),
        new("What can you do with decks and cards?",
            "**Decks** organize cards into focused study groups. You can **suspend** a deck to temporarily hide all its cards from review, or **suspend individual cards** you don't want to see right now. Suspended items keep their progress — unsuspend them anytime to pick up where you left off."),
        new("What formatting do cards support?",
            "Cards fully support **markdown** — bold, italic, lists, headings, links, and more. You can also include fenced code blocks with syntax highlighting:\n\n```python\ndef hello(name: str) -> str:\n    return f\"Hello, {name}!\"\n```\n\n**LaTeX math** is rendered with KaTeX — inline like \\(e^{i\\pi} + 1 = 0\\) and block like:\n\n$$\\int_{-\\infty}^{\\infty} e^{-x^2}\\,dx = \\sqrt{\\pi}$$\n\nChemistry via mhchem (\\(\\ce{2H2 + O2 -> 2H2O}\\)) and physics shortcuts (\\(\\pdv{\\psi}{t}\\)) are preconfigured."),
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
