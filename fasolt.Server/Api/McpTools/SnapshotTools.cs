using System.ComponentModel;
using System.Text.Json;
using Fasolt.Server.Application.Services;
using ModelContextProtocol.Server;

namespace Fasolt.Server.Api.McpTools;

[McpServerToolType]
public class SnapshotTools(DeckSnapshotService snapshotService, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool, Description("Create snapshots of all decks as backups. Only creates a snapshot if card content changed since the last one. Keeps last 10 snapshots per deck. Returns how many were created vs skipped. Users can restore snapshots at https://fasolt.app.")]
    public async Task<string> CreateSnapshot()
    {
        var userId = McpUserResolver.GetUserId(httpContextAccessor);
        var result = await snapshotService.CreateAll(userId);
        return JsonSerializer.Serialize(new { result.Created, result.Skipped }, McpJson.Options);
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
