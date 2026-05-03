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
        return JsonSerializer.Serialize(result, McpJson.Options);
    }

    [McpServerTool, Description("List all cards, optionally filtered by source file or deck. Supports cursor-based pagination.")]
    public async Task<string> ListCards(
        [Description("Filter by source file name")] string? sourceFile = null,
        [Description("Filter by deck ID")] string? deckId = null,
        [Description("Max results to return (1-200, default 50)")] int? limit = null,
        [Description("Cursor from previous page's nextCursor field")] string? after = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await cardService.ListCards(userId, sourceFile, deckId, limit, after);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }

    [McpServerTool, Description("Get a single card by ID. Returns full card details including front, back, source metadata, deck assignments, and SRS state.")]
    public async Task<string> GetCard(
        [Description("Card ID")] string cardId)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await cardService.GetCard(userId, cardId);
        if (result is null)
            return JsonSerializer.Serialize(new { error = "Card not found" }, McpJson.Options);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }

    [McpServerTool, Description("Reset a card's SRS progress. Clears stability, difficulty, and scheduling — returns the card to 'new' state so it appears in the next study session. Use when a user wants to relearn a card from scratch.")]
    public async Task<string> ResetCardProgress(
        [Description("Card ID")] string cardId)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await cardService.ResetProgress(userId, cardId);
        if (result is null)
            return JsonSerializer.Serialize(new { error = "Card not found" }, McpJson.Options);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }

    [McpServerTool, Description("Create one or more flashcards, optionally linked to a source file and/or deck. Card text is Markdown with KaTeX math support — emit \\(...\\) / $...$ for inline math and \\[...\\] / $$...$$ for block math (mhchem chemistry and \\dv/\\pdv/\\abs/\\norm physics shortcuts are preconfigured). Each card can also include SVG images for front and/or back (use a landscape viewBox like '0 0 400 250' for best display). Returns created cards, any skipped duplicates, and a deckUrl deep link if a deck was used.")]
    public async Task<string> CreateCards(
        [Description("Array of cards to create. Each card needs 'front' and 'back' text, plus optional 'sourceFile' and 'sourceHeading'.")] List<BulkCardItem> cards,
        [Description("Default source file name for all cards (individual cards can override)")] string? sourceFile = null,
        [Description("Add cards to this deck ID")] string? deckId = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await cardService.BulkCreateCards(userId, cards, sourceFile, deckId);
        if (result.IsDeckNotFound)
            return JsonSerializer.Serialize(new { error = "Deck not found" }, McpJson.Options);

        var response = result.Response!;
        if (deckId is not null)
        {
            var request = httpContextAccessor.HttpContext!.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            return JsonSerializer.Serialize(new
            {
                response.Created,
                response.Skipped,
                deckUrl = $"{baseUrl}/decks/{deckId}",
            }, McpJson.Options);
        }

        return JsonSerializer.Serialize(response, McpJson.Options);
    }

    [McpServerTool, Description("Delete cards by IDs or by source file. Provide cardIds to delete specific cards, or sourceFile to delete all cards from that source.")]
    public async Task<string> DeleteCards(
        [Description("List of card IDs to delete")] List<string>? cardIds = null,
        [Description("Delete all cards from this source file")] string? sourceFile = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);

        if (cardIds is null && sourceFile is null)
            return JsonSerializer.Serialize(new { error = "Provide cardIds or sourceFile" }, McpJson.Options);

        var count = 0;
        if (cardIds is not null && cardIds.Count > 0)
            count += await cardService.DeleteCards(userId, cardIds);
        if (sourceFile is not null)
            count += await cardService.DeleteCardsBySource(userId, sourceFile);

        return JsonSerializer.Serialize(new { deleted = count > 0, deletedCount = count }, McpJson.Options);
    }

    [McpServerTool, Description("Update one or more existing cards' text or source metadata. Preserves all review/SRS history. Each card can be looked up by cardId, or by sourceFile + front (case-insensitive natural key). Card text fields accept the same Markdown + KaTeX math (mhchem, physics shortcuts) as create_cards.")]
    public async Task<string> UpdateCards(
        [Description("Array of card updates. Each needs a lookup key (cardId, or sourceFile + front) and at least one field to update (newFront, newBack, newSourceFile, newSourceHeading, newFrontSvg, newBackSvg).")] List<BulkUpdateCardItem> cards)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);

        if (cards.Count == 0)
            return JsonSerializer.Serialize(new { error = "Provide at least one card to update" }, McpJson.Options);

        var results = await cardService.BulkUpdateCards(userId, cards);
        return JsonSerializer.Serialize(new
        {
            updated = results.Count(r => r.Status == UpdateCardStatus.Success),
            results
        }, McpJson.Options);
    }

    [McpServerTool, Description("Add an SVG image to a card. LLMs can generate SVG diagrams, charts, chemical structures, math visualizations, etc. The SVG is sanitized server-side for security. Use a landscape viewBox like '0 0 400 250' for best display across web and mobile.")]
    public async Task<string> AddSvgToCard(
        [Description("Card ID")] string cardId,
        [Description("Which side: 'front' or 'back'")] string side,
        [Description("Raw SVG markup (must start with <svg). Use a landscape viewBox (e.g. '0 0 400 250') — avoid square or portrait ratios as they may be clipped on mobile.")] string svg)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);

        if (side is not "front" and not "back")
            return JsonSerializer.Serialize(new { error = "Side must be 'front' or 'back'" }, McpJson.Options);

        var req = side == "front"
            ? new UpdateCardFieldsRequest(NewFrontSvg: svg)
            : new UpdateCardFieldsRequest(NewBackSvg: svg);

        var result = await cardService.UpdateCardFields(userId, cardId, req);

        return result.Status switch
        {
            UpdateCardStatus.Success => JsonSerializer.Serialize(result.Card, McpJson.Options),
            UpdateCardStatus.NotFound => JsonSerializer.Serialize(new { error = "Card not found" }, McpJson.Options),
            _ => JsonSerializer.Serialize(new { error = "Unexpected error" }, McpJson.Options),
        };
    }

    [McpServerTool, Description("Suspend or unsuspend a card. Suspended cards are excluded from study/review but retain their SRS history. Use this when a user wants to temporarily stop seeing a card.")]
    public async Task<string> SuspendCard(
        [Description("Card ID")] string cardId,
        [Description("true to suspend (exclude from study), false to unsuspend")] bool isSuspended)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await cardService.SetSuspended(userId, cardId, isSuspended);
        if (result is null)
            return JsonSerializer.Serialize(new { error = "Card not found" }, McpJson.Options);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }
}
