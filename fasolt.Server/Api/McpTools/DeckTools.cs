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

    [McpServerTool, Description("Add cards to a deck. Cards already in the deck are silently skipped.")]
    public async Task<string> AddCardsToDeck(
        [Description("ID of the deck")] string deckId,
        [Description("List of card IDs to add")] List<string> cardIds)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await deckService.AddCards(userId, deckId, cardIds);
        return result switch
        {
            AddCardsResult.Success => JsonSerializer.Serialize(new { success = true }),
            AddCardsResult.DeckNotFound => JsonSerializer.Serialize(new { error = "Deck not found" }),
            AddCardsResult.CardsNotFound => JsonSerializer.Serialize(new { error = "One or more cards not found" }),
            _ => JsonSerializer.Serialize(new { error = "Unexpected error" }),
        };
    }

    [McpServerTool, Description("Remove cards from a deck. The cards themselves are not deleted, only unlinked from the deck.")]
    public async Task<string> RemoveCardsFromDeck(
        [Description("ID of the deck")] string deckId,
        [Description("List of card IDs to remove from the deck")] List<string> cardIds)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var removedCount = 0;
        foreach (var cardId in cardIds)
        {
            var result = await deckService.RemoveCard(userId, deckId, cardId);
            if (result == RemoveCardResult.DeckNotFound)
                return JsonSerializer.Serialize(new { error = "Deck not found" });
            if (result == RemoveCardResult.Success)
                removedCount++;
        }
        return JsonSerializer.Serialize(new { success = true, removedCount });
    }

    [McpServerTool, Description("Move cards from one deck to another. Removes from source deck and adds to target deck.")]
    public async Task<string> MoveCards(
        [Description("ID of the deck to move cards from")] string fromDeckId,
        [Description("ID of the deck to move cards to")] string toDeckId,
        [Description("List of card IDs to move")] List<string> cardIds)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);

        // Add to target first
        var addResult = await deckService.AddCards(userId, toDeckId, cardIds);
        if (addResult == AddCardsResult.DeckNotFound)
            return JsonSerializer.Serialize(new { error = "Target deck not found" });
        if (addResult == AddCardsResult.CardsNotFound)
            return JsonSerializer.Serialize(new { error = "One or more cards not found" });

        // Remove from source
        foreach (var cardId in cardIds)
        {
            await deckService.RemoveCard(userId, fromDeckId, cardId);
        }

        return JsonSerializer.Serialize(new { success = true, movedCount = cardIds.Count });
    }

    [McpServerTool, Description("Delete a deck. Optionally also delete all cards assigned to that deck (useful when recreating a deck from scratch).")]
    public async Task<string> DeleteDeck(
        [Description("ID of the deck to delete")] string deckId,
        [Description("If true, also permanently delete all cards in the deck. Default false (cards are kept, only the deck is removed).")] bool deleteCards = false)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await deckService.DeleteDeck(userId, deckId, deleteCards);
        if (!result.Deleted)
            return JsonSerializer.Serialize(new { error = "Deck not found" });
        return JsonSerializer.Serialize(new { deleted = true, deletedCardCount = result.DeletedCardCount });
    }
}
