using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class OverviewTools(OverviewService overviewService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("Get an overview of the user's account: total cards, due cards, cards by state, deck count, and source file count. Call this first to orient yourself.")]
    public async Task<string> GetOverview()
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await overviewService.GetOverview(userId);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }
}
