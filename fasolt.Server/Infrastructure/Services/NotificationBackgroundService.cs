using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Infrastructure.Services;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationBackgroundService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    public NotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification background service started.");

        using var timer = new PeriodicTimer(CheckInterval);

        do
        {
            try
            {
                await SendPendingNotificationsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in notification background service.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SendPendingNotificationsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var apns = scope.ServiceProvider.GetRequiredService<ApnsService>();

        var now = DateTimeOffset.UtcNow;

        // Find all device tokens where the user has due cards and is past their notification interval
        var candidates = await db.DeviceTokens
            .Include(d => d.User)
            .Where(d =>
                (d.User.LastNotifiedAt == null ||
                 d.User.LastNotifiedAt.Value.AddHours(d.User.NotificationIntervalHours) <= now))
            .ToListAsync(stoppingToken);

        foreach (var deviceToken in candidates)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var dueCount = await db.Cards
                .CountAsync(c =>
                    c.UserId == deviceToken.UserId &&
                    (c.DueAt == null || c.DueAt <= now) &&
                    (!c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive)),
                    stoppingToken);

            if (dueCount == 0) continue;

            var title = "Cards due for review";
            var body = dueCount == 1
                ? "You have 1 card due for review."
                : $"You have {dueCount} cards due for review.";

            _logger.LogInformation(
                "Sending notification to user {UserId}: {DueCount} due cards.", deviceToken.UserId, dueCount);

            var success = await apns.SendNotification(deviceToken.Token, title, body, dueCount);

            if (!success)
            {
                // Token is invalid (410 Gone) — remove it
                _logger.LogInformation("Removing invalid device token for user {UserId}.", deviceToken.UserId);
                db.DeviceTokens.Remove(deviceToken);
                await db.SaveChangesAsync(stoppingToken);
            }
            else
            {
                // Update LastNotifiedAt
                deviceToken.User.LastNotifiedAt = now;
                await db.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
