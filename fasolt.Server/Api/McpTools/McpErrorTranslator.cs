using ModelContextProtocol.Protocol;

namespace Fasolt.Server.Api.McpTools;

public static class McpErrorTranslator
{
    public static CallToolResult ToErrorResult(Exception ex, string toolName)
    {
        var text = IsInputError(ex)
            ? $"Invalid arguments for '{toolName}': {ex.Message}"
            : $"Internal error in '{toolName}' ({ex.GetType().Name}). Check the tool's input schema and retry.";

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = text }],
            IsError = true,
        };
    }

    public static bool IsInputError(Exception ex) =>
        ex is ArgumentException
        || ex is FormatException
        || ex is System.Text.Json.JsonException;
}
