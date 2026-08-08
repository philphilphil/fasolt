using System.Text.Json;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Protocol;

namespace Fasolt.Server.Api.McpTools;

public static class McpErrorTranslator
{
    public static CallToolResult ToErrorResult(Exception ex, string toolName)
    {
        var text = ex switch
        {
            LinkedContentException linked => LinkedContentText(linked),
            _ when IsInputError(ex) => $"Invalid arguments for '{toolName}': {ex.Message}",
            _ => $"Internal error in '{toolName}' ({ex.GetType().Name}). Check the tool's input schema and retry.",
        };

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = text }],
            IsError = true,
        };
    }

    /// <summary>
    /// Every writing tool — update_cards, delete_cards, update_deck, delete_deck,
    /// assign_cards_to_deck, add_svg_to_card, publish_deck and the rest — can be
    /// pointed at content the caller only reaches through a subscription. That is a
    /// refusal the agent has to explain to the user, not a server fault, so it gets
    /// the same structured shape and error code as the REST 403 instead of the
    /// generic "internal error" text.
    /// </summary>
    private static string LinkedContentText(LinkedContentException ex) =>
        JsonSerializer.Serialize(new
        {
            error = LinkedContentException.ErrorCode,
            message = ex.Message,
            hint = "Linked decks belong to another account and are read-only here: their cards stay in "
                + "sync with the author's. To make changes, convert the deck to a copy on its page in "
                + "the Fasolt web app — that clones the cards into the user's own account and carries "
                + "their review progress across.",
        }, McpJson.Options);

    /// <summary>
    /// Whether the call failed because of what the caller asked for rather than a
    /// server fault. Decides the log level: these are expected and self-correctable,
    /// so they must not page anyone.
    /// </summary>
    public static bool IsCallerError(Exception ex) =>
        IsInputError(ex) || ex is LinkedContentException;

    public static bool IsInputError(Exception ex) =>
        ex is ArgumentException
        || ex is FormatException
        || ex is System.Text.Json.JsonException;
}
