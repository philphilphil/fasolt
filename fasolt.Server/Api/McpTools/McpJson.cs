using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fasolt.Server.Api.McpTools;

internal static class McpJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
