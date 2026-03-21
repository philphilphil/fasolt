using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using SpacedMd.Mcp;

namespace SpacedMd.Mcp.Tools;

[McpServerToolType]
public class CardTools
{
    [McpServerTool, Description("Search existing cards and decks by query text. Use this to check for duplicates before creating cards.")]
    public static async Task<string> SearchCards(
        ApiClient api,
        [Description("Search query (minimum 2 characters)")] string query)
    {
        var result = await api.GetAsync<JsonElement>($"/api/search?q={Uri.EscapeDataString(query)}");
        return result.GetRawText();
    }

    [McpServerTool, Description("List all cards, optionally filtered by source file or deck. Supports cursor-based pagination.")]
    public static async Task<string> ListCards(
        ApiClient api,
        [Description("Filter by source file name")] string? sourceFile = null,
        [Description("Filter by deck ID")] string? deckId = null,
        [Description("Max results to return (1-200, default 50)")] int? limit = null,
        [Description("Cursor from previous page's nextCursor field")] string? after = null)
    {
        var queryParams = new List<string>();
        if (sourceFile is not null) queryParams.Add($"sourceFile={Uri.EscapeDataString(sourceFile)}");
        if (deckId is not null) queryParams.Add($"deckId={Uri.EscapeDataString(deckId)}");
        if (limit.HasValue) queryParams.Add($"limit={limit.Value}");
        if (after is not null) queryParams.Add($"after={Uri.EscapeDataString(after)}");

        var qs = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
        var result = await api.GetAsync<JsonElement>($"/api/cards{qs}");
        return result.GetRawText();
    }

    [McpServerTool, Description("Create one or more flashcards, optionally linked to a source file and/or deck. Returns created cards and any skipped duplicates.")]
    public static async Task<string> CreateCards(
        ApiClient api,
        [Description("Array of cards to create. Each card needs 'front' and 'back' text, plus optional 'sourceFile' and 'sourceHeading'.")] List<CardInput> cards,
        [Description("Default source file name for all cards (individual cards can override)")] string? sourceFile = null,
        [Description("Add cards to this deck ID")] string? deckId = null)
    {
        var body = new
        {
            sourceFile,
            deckId = deckId is not null ? Guid.Parse(deckId) : (Guid?)null,
            cards
        };
        var result = await api.PostAsync<JsonElement>("/api/cards/bulk", body);
        return result.GetRawText();
    }
}

public record CardInput(string Front, string Back, string? SourceFile = null, string? SourceHeading = null);
