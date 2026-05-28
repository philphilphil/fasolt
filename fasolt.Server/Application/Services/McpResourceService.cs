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

    public async Task<string?> RenderDeckAsync(string userId, string deckPublicId)
    {
        var deck = await db.Decks
            .FirstOrDefaultAsync(d => d.PublicId == deckPublicId && d.UserId == userId);
        if (deck is null) return null;

        var now = timeProvider.GetUtcNow();

        var cards = await db.DeckCards
            .Where(dc => dc.DeckId == deck.Id && !dc.Card.IsSuspended)
            .OrderBy(dc => dc.Card.CreatedAt)
            .Select(dc => new RenderableCard(
                dc.Card.Front,
                dc.Card.Back,
                dc.Card.SourceFile,
                dc.Card.FrontSvg,
                dc.Card.BackSvg,
                dc.Card.CreatedAt,
                dc.Card.DueAt,
                null))
            .ToListAsync();

        var totalCount = cards.Count;
        var dueCount = cards.Count(c => c.DueAt == null || c.DueAt <= now);

        var sb = new System.Text.StringBuilder();
        sb.Append("# Deck: ").Append(deck.Name).Append("\n\n");

        var countLine = totalCount == 1 ? "1 card" : $"{totalCount} cards";
        if (dueCount > 0) countLine += $" · {dueCount} due today";
        sb.Append(countLine).Append('\n');

        if (!string.IsNullOrWhiteSpace(deck.Description))
            sb.Append('\n').Append(deck.Description).Append('\n');

        sb.Append("\n---\n\n");

        if (cards.Count == 0)
        {
            sb.Append("No cards.\n");
            return sb.ToString();
        }

        AppendCardsWithTruncation(sb, cards, includeDeckLabel: false, includeCreatedDate: false);
        return sb.ToString();
    }

    private record RenderableCard(
        string Front,
        string Back,
        string? SourceFile,
        string? FrontSvg,
        string? BackSvg,
        DateTimeOffset CreatedAt,
        DateTimeOffset? DueAt,
        string? DeckName);

    private void AppendCardsWithTruncation(
        System.Text.StringBuilder sb,
        IReadOnlyList<RenderableCard> cards,
        bool includeDeckLabel,
        bool includeCreatedDate)
    {
        var rendered = 0;
        var totalToShow = Math.Min(cards.Count, SoftCardCap);

        for (var i = 0; i < totalToShow; i++)
        {
            var block = FormatCardBlock(cards[i], includeDeckLabel, includeCreatedDate);

            // Bail out if appending this block would exceed the size budget.
            if (sb.Length + block.Length > SizeBudgetBytes && rendered > 0)
                break;

            sb.Append(block);
            rendered++;
        }

        if (rendered < cards.Count)
            sb.Append($"\n*Showing {rendered} of {cards.Count} cards. Use list_cards or search_cards for the full set.*\n");
    }

    private static string FormatCardBlock(RenderableCard c, bool includeDeckLabel, bool includeCreatedDate)
    {
        var sb = new System.Text.StringBuilder();

        if (includeCreatedDate || includeDeckLabel)
        {
            var meta = new List<string>();
            if (includeCreatedDate) meta.Add($"**Created:** {c.CreatedAt.UtcDateTime:yyyy-MM-dd}");
            if (includeDeckLabel && c.DeckName is not null) meta.Add($"**Deck:** {c.DeckName}");
            sb.Append(string.Join(" · ", meta)).Append("\n\n");
        }

        sb.Append("**Front:** ").Append(c.Front).Append("\n\n");
        if (!string.IsNullOrEmpty(c.Back))
            sb.Append("**Back:** ").Append(c.Back).Append("\n\n");
        if (c.FrontSvg is not null || c.BackSvg is not null)
            sb.Append("[has SVG image — use get_card for full content]\n\n");
        if (!string.IsNullOrWhiteSpace(c.SourceFile))
            sb.Append("*Source: ").Append(c.SourceFile).Append("*\n\n");

        sb.Append("---\n\n");
        return sb.ToString();
    }

    public async Task<string> RenderDueTodayAsync(string userId)
    {
        var now = timeProvider.GetUtcNow();

        // Cards that are due (DueAt null = new, or DueAt <= now), not suspended,
        // and whose decks (if any) are not all suspended.
        var raw = await db.Cards
            .Where(c => c.UserId == userId && !c.IsSuspended)
            .Where(c => c.DueAt == null || c.DueAt <= now)
            .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended))
            .Select(c => new
            {
                c.Front,
                c.Back,
                c.SourceFile,
                c.FrontSvg,
                c.BackSvg,
                c.CreatedAt,
                c.DueAt,
                DeckNames = c.DeckCards.Where(dc => !dc.Deck.IsSuspended).Select(dc => dc.Deck.Name).ToList(),
            })
            .ToListAsync();

        // Group by first (alphabetical) active deck name, or "(no deck)".
        var groups = raw
            .Select(c => new
            {
                Card = new RenderableCard(c.Front, c.Back, c.SourceFile, c.FrontSvg, c.BackSvg, c.CreatedAt, c.DueAt, null),
                GroupName = c.DeckNames.Count == 0 ? "(no deck)" : c.DeckNames.OrderBy(n => n).First(),
            })
            .GroupBy(x => x.GroupName)
            .OrderBy(g => g.Key == "(no deck)") // "(no deck)" last
                .ThenBy(g => g.Key)
            .ToList();

        var totalCards = raw.Count;
        var totalDecks = groups.Count(g => g.Key != "(no deck)");

        var sb = new System.Text.StringBuilder();
        sb.Append("# Due Today\n\n");

        if (totalCards == 0)
        {
            sb.Append("No cards.\n");
            return sb.ToString();
        }

        var summary = totalCards == 1 ? "1 card" : $"{totalCards} cards";
        if (totalDecks > 0)
        {
            var deckWord = totalDecks == 1 ? "deck" : "decks";
            summary += $" across {totalDecks} {deckWord}";
        }
        sb.Append(summary).Append("\n\n");

        var rendered = 0;
        var truncatedAtGroup = false;

        foreach (var group in groups)
        {
            if (truncatedAtGroup) break;

            var groupCards = group.Select(x => x.Card)
                .OrderBy(c => c.DueAt ?? DateTimeOffset.MaxValue)
                .ToList();

            sb.Append("## ").Append(group.Key).Append(" (")
              .Append(groupCards.Count)
              .Append(groupCards.Count == 1 ? " card)" : " cards)").Append("\n\n");

            foreach (var card in groupCards)
            {
                var block = FormatCardBlock(card, includeDeckLabel: false, includeCreatedDate: false);
                if (rendered >= SoftCardCap || sb.Length + block.Length > SizeBudgetBytes)
                {
                    truncatedAtGroup = true;
                    break;
                }
                sb.Append(block);
                rendered++;
            }
        }

        if (rendered < totalCards)
            sb.Append($"\n*Showing {rendered} of {totalCards} cards. Use list_cards or search_cards for the full set.*\n");

        return sb.ToString();
    }

    public Task<string> RenderRecentAsync(string userId) =>
        throw new NotImplementedException();
}
