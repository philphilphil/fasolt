using System.Text.Json;

namespace Fasolt.Server.Api.McpTools;

/// <summary>
/// Structured tool errors: a stable machine-readable <c>error</c> code plus a
/// message written for the agent to relay. Same shape the REST endpoints return,
/// so a condition an agent hits through MCP and a user hits through the web app
/// is named identically in both places.
/// </summary>
internal static class McpErrors
{
    public static string Structured(string code, string message) =>
        JsonSerializer.Serialize(new { error = code, message }, McpJson.Options);
}
