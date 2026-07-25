using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public record McpResourceEntry(
    string Uri,
    string Name,
    string? Description,
    string MimeType);

public class McpResourceService(
    AppDbContext db,
    TimeProvider timeProvider)
{
    private const int SoftCardCap = 100;
    private const int SizeBudgetBytes = 80 * 1024;
    private const int QueryHardCap = 200;
    private const string Mime = "text/markdown";

    public async Task<List<McpResourceEntry>> ListUserResourcesAsync(string userId)
    {
        var decks = await db.Decks
            .Where(d => d.UserId == userId && !d.IsSuspended)
            .OrderBy(d => d.Name)
            .Select(d => new
            {
                d.PublicId,
                d.Name,
                d.Description,
                Count = d.Cards.Count(dc => !dc.Card.ReviewStates.Any(r => r.UserId == userId && r.IsSuspended)),
            })
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

        var cards = await (
            from dc in db.DeckCards.Where(dc => dc.DeckId == deck.Id)
            where !dc.Card.ReviewStates.Any(r => r.UserId == userId && r.IsSuspended)
            join r in db.ReviewStates.Where(r => r.UserId == userId) on dc.CardId equals r.CardId into g
            from rs in g.DefaultIfEmpty()
            orderby dc.Card.CreatedAt
            select new RenderableCard(
                dc.Card.Front,
                dc.Card.Back,
                dc.Card.SourceFile,
                dc.Card.FrontSvg != null || dc.Card.BackSvg != null,
                dc.Card.CreatedAt,
                rs.DueAt,
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

        // Deck loads every non-suspended card (no page cap), so cards.Count is the true total.
        AppendCardsWithTruncation(sb, cards, cards.Count, includeDeckLabel: false, includeCreatedDate: false);
        return sb.ToString();
    }

    private record RenderableCard(
        string Front,
        string Back,
        string? SourceFile,
        bool HasSvg,
        DateTimeOffset CreatedAt,
        DateTimeOffset? DueAt,
        string? DeckName);

    // totalCount is the true count across the whole result set (which may exceed the
    // materialized `cards` page), so the truncation footer reports the real total.
    private void AppendCardsWithTruncation(
        System.Text.StringBuilder sb,
        IReadOnlyList<RenderableCard> cards,
        int totalCount,
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

        if (rendered < totalCount)
            sb.Append($"\n*Showing {rendered} of {totalCount} cards. Use list_cards or search_cards for the full set.*\n");
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
        if (c.HasSvg)
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
        var dueCards = db.Cards
            .Where(c => c.UserId == userId)
            .Where(ReviewStateQuery.NotSuspendedBy(userId))
            .Where(ReviewStateQuery.DueBy(userId, now))
            .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended));

        // Counts come from the full set so the summary/footer stay accurate
        // independent of the rendered-page cap below.
        var totalCards = await dueCards.CountAsync();
        var totalDecks = await db.Decks
            .CountAsync(d => d.UserId == userId && !d.IsSuspended
                && d.Cards.Any(dc => !dc.Card.ReviewStates.Any(r =>
                    r.UserId == userId && (r.IsSuspended || r.DueAt > now))));

        var sb = new System.Text.StringBuilder();
        sb.Append("# Due Today\n\n");

        if (totalCards == 0)
        {
            sb.Append("No cards.\n");
            return sb.ToString();
        }

        // Materialize only a bounded page; rendering caps further at SoftCardCap / size budget.
        var raw = await (
            from c in dueCards
            join r in db.ReviewStates.Where(r => r.UserId == userId) on c.Id equals r.CardId into g
            from rs in g.DefaultIfEmpty()
            orderby rs.DueAt // soonest-due first so the cap keeps the most-due cards
            select new
            {
                c.Front,
                c.Back,
                c.SourceFile,
                HasSvg = c.FrontSvg != null || c.BackSvg != null,
                c.CreatedAt,
                rs.DueAt,
                DeckNames = c.DeckCards.Where(dc => !dc.Deck.IsSuspended).Select(dc => dc.Deck.Name).ToList(),
            })
            .Take(QueryHardCap)
            .ToListAsync();

        // Group by first (alphabetical) active deck name, or "(no deck)".
        var groups = raw
            .Select(c => new
            {
                Card = new RenderableCard(c.Front, c.Back, c.SourceFile, c.HasSvg, c.CreatedAt, c.DueAt, null),
                GroupName = c.DeckNames.Count == 0 ? "(no deck)" : c.DeckNames.OrderBy(n => n).First(),
            })
            .GroupBy(x => x.GroupName)
            .OrderBy(g => g.Key == "(no deck)") // "(no deck)" last
                .ThenBy(g => g.Key)
            .ToList();

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

            var groupHeaderWritten = false;

            foreach (var card in groupCards)
            {
                var block = FormatCardBlock(card, includeDeckLabel: false, includeCreatedDate: false);
                if (rendered >= SoftCardCap || (sb.Length + block.Length > SizeBudgetBytes && rendered > 0))
                {
                    truncatedAtGroup = true;
                    break;
                }
                if (!groupHeaderWritten)
                {
                    sb.Append("## ").Append(group.Key).Append(" (")
                      .Append(groupCards.Count)
                      .Append(groupCards.Count == 1 ? " card)" : " cards)").Append("\n\n");
                    groupHeaderWritten = true;
                }
                sb.Append(block);
                rendered++;
            }
        }

        if (rendered < totalCards)
            sb.Append($"\n*Showing {rendered} of {totalCards} cards. Use list_cards or search_cards for the full set.*\n");

        return sb.ToString();
    }

    public async Task<string> RenderRecentAsync(string userId)
    {
        var now = timeProvider.GetUtcNow();
        var cutoff = now.AddDays(-7);

        var recentCards = db.Cards
            .Where(c => c.UserId == userId && c.CreatedAt >= cutoff)
            .Where(ReviewStateQuery.NotSuspendedBy(userId));

        // Count from the full set so the header stays accurate independent of the page cap.
        var totalCards = await recentCards.CountAsync();

        var sb = new System.Text.StringBuilder();
        sb.Append("# Recently Created\n\n");

        if (totalCards == 0)
        {
            sb.Append("No cards.\n");
            return sb.ToString();
        }

        // Materialize only a bounded page; rendering caps further at SoftCardCap / size budget.
        var raw = await (
            from c in recentCards
            join r in db.ReviewStates.Where(r => r.UserId == userId) on c.Id equals r.CardId into g
            from rs in g.DefaultIfEmpty()
            orderby c.CreatedAt descending
            select new
            {
                c.Front,
                c.Back,
                c.SourceFile,
                HasSvg = c.FrontSvg != null || c.BackSvg != null,
                c.CreatedAt,
                rs.DueAt,
                DeckName = c.DeckCards
                    .Where(dc => !dc.Deck.IsSuspended)
                    .OrderBy(dc => dc.Deck.Name)
                    .Select(dc => dc.Deck.Name)
                    .FirstOrDefault(),
            })
            .Take(QueryHardCap)
            .ToListAsync();

        var since = cutoff.UtcDateTime.ToString("yyyy-MM-dd");
        var wordCard = totalCards == 1 ? "card" : "cards";
        sb.Append(totalCards).Append(' ').Append(wordCard)
          .Append(" created since ").Append(since)
          .Append(" (last 7 days)\n\n---\n\n");

        var renderable = raw
            .Select(c => new RenderableCard(c.Front, c.Back, c.SourceFile, c.HasSvg, c.CreatedAt, c.DueAt, c.DeckName))
            .ToList();

        AppendCardsWithTruncation(sb, renderable, totalCards, includeDeckLabel: true, includeCreatedDate: true);
        return sb.ToString();
    }
}
