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
        return JsonSerializer.Serialize(result, McpJson.Options);
    }

    [McpServerTool, Description("Create a new deck for organizing flashcards.")]
    public async Task<string> CreateDeck(
        [Description("Deck name (max 100 characters)")] string name,
        [Description("Optional deck description")] string? description = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await deckService.CreateDeck(userId, name, description);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }

    [McpServerTool, Description("Update a deck's name or description.")]
    public async Task<string> UpdateDeck(
        [Description("ID of the deck to update")] string deckId,
        [Description("New deck name (max 100 characters)")] string name,
        [Description("New deck description (null to clear)")] string? description = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await deckService.UpdateDeck(userId, deckId, name, description);
        if (result is null)
            return JsonSerializer.Serialize(new { error = "Deck not found" }, McpJson.Options);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }

    [McpServerTool, Description("Assign cards to a deck, or remove them. Pass a deckId to assign cards to that deck. Pass null as deckId to remove cards from a specific deck (requires fromDeckId).")]
    public async Task<string> AssignCardsToDeck(
        [Description("Target deck ID to assign cards to (null to remove cards from fromDeckId)")] string? deckId,
        [Description("List of card IDs")] List<string> cardIds,
        [Description("Deck ID to remove cards from (required when deckId is null, optional when moving cards between decks)")] string? fromDeckId = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);

        // Remove from source deck if specified
        if (fromDeckId is not null)
        {
            var removeResult = await deckService.RemoveCards(userId, fromDeckId, cardIds);
            if (!removeResult.DeckFound)
                return JsonSerializer.Serialize(new { error = "Source deck not found" }, McpJson.Options);
        }

        // Add to target deck if specified
        if (deckId is not null)
        {
            var addResult = await deckService.AddCards(userId, deckId, cardIds);
            return addResult switch
            {
                AddCardsResult.Success => JsonSerializer.Serialize(new { success = true }, McpJson.Options),
                AddCardsResult.DeckNotFound => JsonSerializer.Serialize(new { error = "Target deck not found" }, McpJson.Options),
                AddCardsResult.CardsNotFound => JsonSerializer.Serialize(new { error = "One or more cards not found" }, McpJson.Options),
                _ => JsonSerializer.Serialize(new { error = "Unexpected error" }, McpJson.Options),
            };
        }

        // deckId is null — this was a remove-only operation
        if (fromDeckId is null)
            return JsonSerializer.Serialize(new { error = "Provide deckId to assign, or fromDeckId to remove" }, McpJson.Options);

        return JsonSerializer.Serialize(new { success = true }, McpJson.Options);
    }

    [McpServerTool, Description("Delete a deck. Optionally also delete all cards assigned to that deck (useful when recreating a deck from scratch).")]
    public async Task<string> DeleteDeck(
        [Description("ID of the deck to delete")] string deckId,
        [Description("If true, also permanently delete all cards in the deck. Default false (cards are kept, only the deck is removed).")] bool deleteCards = false)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await deckService.DeleteDeck(userId, deckId, deleteCards);
        if (!result.Deleted)
            return JsonSerializer.Serialize(new { error = "Deck not found" }, McpJson.Options);
        return JsonSerializer.Serialize(new { deleted = true, deletedCardCount = result.DeletedCardCount }, McpJson.Options);
    }

    [McpServerTool, Description("Set a deck's active state. Inactive decks and their cards are excluded from study/review. Cards in multiple decks remain active if at least one deck is active.")]
    public async Task<string> SetDeckActive(
        [Description("ID of the deck")] string deckId,
        [Description("true to activate, false to deactivate")] bool isActive)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await deckService.SetActive(userId, deckId, isActive);
        if (result is null)
            return JsonSerializer.Serialize(new { error = "Deck not found" }, McpJson.Options);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }
}
