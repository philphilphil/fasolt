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
}
