using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class OverviewTools(OverviewService overviewService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("Get an overview of the user's account: identity, total cards, due cards, cards by state, deck count, and source file count. Call this first to confirm which Fasolt account is connected.")]
    public async Task<string> GetOverview()
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var identity = await overviewService.GetIdentity(userId);
        var overview = await overviewService.GetOverview(userId);

        var response = new
        {
            identity,
            overview.TotalCards,
            overview.DueCards,
            overview.CardsByState,
            overview.TotalDecks,
            overview.TotalSources,
        };

        return JsonSerializer.Serialize(response, McpJson.Options);
    }
}
