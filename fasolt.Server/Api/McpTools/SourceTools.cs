using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class SourceTools(SourceService sourceService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("List all source files that cards were created from, with card counts and due counts.")]
    public async Task<string> ListSources()
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await sourceService.ListSources(userId);
        return JsonSerializer.Serialize(result);
    }
}
