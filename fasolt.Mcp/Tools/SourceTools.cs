using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Fasolt.Mcp;

namespace Fasolt.Mcp.Tools;

[McpServerToolType]
public class SourceTools
{
    [McpServerTool, Description("List all source files that cards were created from, with card counts and due counts.")]
    public static async Task<string> ListSources(ApiClient api)
    {
        var result = await api.GetAsync<JsonElement>("/api/sources");
        return result.GetRawText();
    }
}
