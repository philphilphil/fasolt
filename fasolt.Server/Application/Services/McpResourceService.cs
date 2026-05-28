using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public record McpResourceEntry(
    string Uri,
    string Name,
    string? Description,
    string MimeType);

public class McpResourceService(
    AppDbContext db,
    ReviewService reviewService,
    TimeProvider timeProvider)
{
    private const int SoftCardCap = 100;
    private const int SizeBudgetBytes = 80 * 1024;
    private const string Mime = "text/markdown";

    public async Task<List<McpResourceEntry>> ListUserResourcesAsync(string userId)
    {
        var decks = await db.Decks
            .Where(d => d.UserId == userId && !d.IsSuspended)
            .OrderBy(d => d.Name)
            .Select(d => new { d.PublicId, d.Name, d.Description, Count = d.Cards.Count })
            .ToListAsync();

        var entries = new List<McpResourceEntry>();

        foreach (var d in decks)
        {
            var desc = string.IsNullOrWhiteSpace(d.Description)
                ? $"{d.Count} cards"
                : $"{d.Count} cards · {d.Description}";
            entries.Add(new McpResourceEntry(
                Uri: $"fasolt://deck/{d.PublicId}",
                Name: d.Name,
                Description: desc,
                MimeType: Mime));
        }

        entries.Add(new McpResourceEntry(
            Uri: "fasolt://due-today",
            Name: "Due Today",
            Description: "Cards due for review today, grouped by deck",
            MimeType: Mime));

        entries.Add(new McpResourceEntry(
            Uri: "fasolt://recent",
            Name: "Recently Created",
            Description: "Cards created in the last 7 days, newest first",
            MimeType: Mime));

        return entries;
    }

    public Task<string?> RenderDeckAsync(string userId, string deckPublicId) =>
        throw new NotImplementedException();

    public Task<string> RenderDueTodayAsync(string userId) =>
        throw new NotImplementedException();

    public Task<string> RenderRecentAsync(string userId) =>
        throw new NotImplementedException();
}
