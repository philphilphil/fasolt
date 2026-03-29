using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class SnapshotTools(DeckSnapshotService snapshotService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("Create snapshots of all decks as backups. Captures full card state including content and FSRS data. Keeps last 10 snapshots per deck.")]
    public async Task<string> CreateSnapshot()
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var count = await snapshotService.CreateAll(userId);
        return JsonSerializer.Serialize(new { snapshotsCreated = count }, McpJson.Options);
    }

    [McpServerTool, Description("List deck snapshots. Without deckId, lists the 50 most recent across all decks.")]
    public async Task<string> ListSnapshots(
        [Description("Optional deck ID to filter snapshots for a specific deck")] string? deckId = null)
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = deckId is not null
            ? await snapshotService.ListByDeck(userId, deckId)
            : await snapshotService.ListRecent(userId);
        return JsonSerializer.Serialize(result, McpJson.Options);
    }
}
