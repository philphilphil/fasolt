using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class DeckTools(DeckService deckService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("List all decks with card counts and due counts.")]
    public async Task<string> ListDecks()
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await deckService.ListDecks(userId);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Create a new deck for organizing flashcards.")]
    public async Task<string> CreateDeck(
        [Description("Deck name (max 100 characters)")] string name,
        [Description("Optional deck description")] string? description = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await deckService.CreateDeck(userId, name, description);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Delete a deck. Optionally also delete all cards assigned to that deck (useful when recreating a deck from scratch).")]
    public async Task<string> DeleteDeck(
        [Description("ID of the deck to delete")] Guid deckId,
        [Description("If true, also permanently delete all cards in the deck. Default false (cards are kept, only the deck is removed).")] bool deleteCards = false)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await deckService.DeleteDeck(userId, deckId, deleteCards);
        if (!result.Deleted)
            return JsonSerializer.Serialize(new { error = "Deck not found" });
        return JsonSerializer.Serialize(new { deleted = true, deletedCardCount = result.DeletedCardCount });
    }
}
