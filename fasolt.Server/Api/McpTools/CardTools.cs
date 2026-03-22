using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class CardTools(CardService cardService, SearchService searchService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("Search existing cards and decks by query text. Use this to check for duplicates before creating cards.")]
    public async Task<string> SearchCards(
        [Description("Search query (minimum 2 characters)")] string query)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await searchService.Search(userId, query);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("List all cards, optionally filtered by source file or deck. Supports cursor-based pagination.")]
    public async Task<string> ListCards(
        [Description("Filter by source file name")] string? sourceFile = null,
        [Description("Filter by deck ID")] Guid? deckId = null,
        [Description("Max results to return (1-200, default 50)")] int? limit = null,
        [Description("Cursor from previous page's nextCursor field")] string? after = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await cardService.ListCards(userId, sourceFile, deckId, limit, after);
        return JsonSerializer.Serialize(result);
    }

    [McpServerTool, Description("Create one or more flashcards, optionally linked to a source file and/or deck. Returns created cards and any skipped duplicates.")]
    public async Task<string> CreateCards(
        [Description("Array of cards to create. Each card needs 'front' and 'back' text, plus optional 'sourceFile' and 'sourceHeading'.")] List<BulkCardItem> cards,
        [Description("Default source file name for all cards (individual cards can override)")] string? sourceFile = null,
        [Description("Add cards to this deck ID")] Guid? deckId = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await cardService.BulkCreateCards(userId, cards, sourceFile, deckId);
        if (result.IsDeckNotFound)
            return JsonSerializer.Serialize(new { error = "Deck not found" });
        return JsonSerializer.Serialize(result.Response);
    }

    [McpServerTool, Description("Delete a single card by its ID.")]
    public async Task<string> DeleteCard(
        [Description("ID of the card to delete")] Guid cardId)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var deleted = await cardService.DeleteCard(userId, cardId);
        return deleted
            ? JsonSerializer.Serialize(new { deleted = true })
            : JsonSerializer.Serialize(new { error = "Card not found" });
    }
}
