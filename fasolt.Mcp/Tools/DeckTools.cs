using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Fasolt.Mcp;

namespace Fasolt.Mcp.Tools;

[McpServerToolType]
public class DeckTools
{
    [McpServerTool, Description("List all decks with card counts and due counts.")]
    public static async Task<string> ListDecks(ApiClient api)
    {
        var result = await api.GetAsync<JsonElement>("/api/decks");
        return result.GetRawText();
    }

    [McpServerTool, Description("Create a new deck for organizing flashcards.")]
    public static async Task<string> CreateDeck(
        ApiClient api,
        [Description("Deck name (max 100 characters)")] string name,
        [Description("Optional deck description")] string? description = null)
    {
        var body = new { name, description };
        var result = await api.PostAsync<JsonElement>("/api/decks", body);
        return result.GetRawText();
    }
}
