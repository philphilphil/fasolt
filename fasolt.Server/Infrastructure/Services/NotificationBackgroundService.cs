using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Infrastructure.Services;

public class NotificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ApnsService apnsService,
    ILogger<NotificationBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NotificationBackgroundService started, running every {Interval}", Interval);

        using var timer = new PeriodicTimer(Interval);

        // Run once immediately on startup, then on each tick
        await ProcessNotifications(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessNotifications(stoppingToken);
        }
    }

    private async Task ProcessNotifications(CancellationToken stoppingToken)
    {
        logger.LogInformation("Checking for users with due cards to notify");

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTimeOffset.UtcNow;

            var eligibleUsers = await db.DeviceTokens
                .Include(d => d.User)
                .Where(d =>
                    d.User.LastNotifiedAt == null ||
                    d.User.LastNotifiedAt.Value.AddHours(d.User.NotificationIntervalHours) <= now)
                .Select(d => new
                {
                    d.UserId,
                    d.Token,
                    d.User.NotificationIntervalHours,
                })
                .ToListAsync(stoppingToken);

            logger.LogInformation("Found {Count} eligible users to check", eligibleUsers.Count);

            foreach (var entry in eligibleUsers)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    await ProcessUserNotification(db, entry.UserId, entry.Token, now, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process notification for user {UserId}", entry.UserId);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during notification processing cycle");
        }
    }

    private async Task ProcessUserNotification(
        AppDbContext db, string userId, string deviceToken,
        DateTimeOffset now, CancellationToken stoppingToken)
    {
        var dueCardsByDeck = await db.Cards
            .Where(c => c.UserId == userId && (c.DueAt == null || c.DueAt <= now))
            .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive))
            .SelectMany(c => c.DeckCards.DefaultIfEmpty(),
                (card, deckCard) => new { DeckName = deckCard != null ? deckCard.Deck.Name : null })
            .GroupBy(x => x.DeckName ?? "Unsorted")
            .Select(g => new { DeckName = g.Key, Count = g.Count() })
            .ToListAsync(stoppingToken);

        var totalDue = dueCardsByDeck.Sum(g => g.Count);

        if (totalDue == 0)
        {
            logger.LogDebug("User {UserId} has no due cards, skipping", userId);
            return;
        }

        var deckBreakdown = string.Join(", ",
            dueCardsByDeck.OrderByDescending(g => g.Count).Select(g => $"{g.Count} in {g.DeckName}"));
        var body = $"You have {totalDue} card{(totalDue == 1 ? "" : "s")} due: {deckBreakdown}";
        var title = "Cards due";

        var tokenValid = await apnsService.SendNotification(deviceToken, title, body, totalDue);

        if (!tokenValid)
        {
            var token = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == userId, stoppingToken);
            if (token is not null)
            {
                db.DeviceTokens.Remove(token);
                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation("Removed invalid device token for user {UserId}", userId);
            }
            return;
        }

        var user = await db.Users.FindAsync([userId], stoppingToken);
        if (user is not null)
        {
            user.LastNotifiedAt = now;
            await db.SaveChangesAsync(stoppingToken);
        }

        logger.LogInformation("Sent notification to user {UserId}: {TotalDue} cards due", userId, totalDue);
    }
}
