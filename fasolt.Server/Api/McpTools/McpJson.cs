using System.Text.Json;

namespace Fasolt.Server.Api.McpTools;

internal static class McpJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
