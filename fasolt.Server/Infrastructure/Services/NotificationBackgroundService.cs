using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
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

        await ProcessNotifications(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessNotifications(stoppingToken);
        }
    }

    public async Task ProcessNotifications(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var sent = 0;
        var failed = 0;

        try
        {
            var eligibleUsers = await db.DeviceTokens
                .Include(d => d.User)
                .Where(d =>
                    d.User.LastNotifiedAt == null ||
                    d.User.LastNotifiedAt.Value.AddHours(d.User.NotificationIntervalHours) <= now)
                .Select(d => new
                {
                    d.UserId,
                    d.Token,
                    UserEmail = d.User.Email ?? "",
                })
                .ToListAsync(stoppingToken);

            if (eligibleUsers.Count == 0)
            {
                var totalTokens = await db.DeviceTokens.CountAsync(stoppingToken);
                db.Logs.Add(new AppLog
                {
                    Type = LogType.Notification,
                    Message = totalTokens == 0
                        ? "No devices registered for push"
                        : $"{totalTokens} device(s) registered, none due for notification yet",
                    Success = true,
                    CreatedAt = now,
                });
                await db.SaveChangesAsync(stoppingToken);
                return;
            }

            foreach (var entry in eligibleUsers)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    var didSend = await ProcessUserNotification(db, entry.UserId, entry.UserEmail, entry.Token, now, stoppingToken);
                    if (didSend) sent++;
                }
                catch (Exception ex)
                {
                    failed++;
                    logger.LogError(ex, "Failed to process notification for user {UserId}", entry.UserId);
                    db.Logs.Add(new AppLog
                    {
                        Type = LogType.Notification,
                        Message = $"Error for {entry.UserEmail}",
                        Detail = ex.Message,
                        Success = false,
                        CreatedAt = now,
                    });
                    await db.SaveChangesAsync(stoppingToken);
                }
            }

            if (sent == 0 && failed == 0)
            {
                db.Logs.Add(new AppLog
                {
                    Type = LogType.Notification,
                    Message = $"Checked {eligibleUsers.Count} user(s), no cards due",
                    Success = true,
                    CreatedAt = now,
                });
                await db.SaveChangesAsync(stoppingToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notification cycle failed");
            db.Logs.Add(new AppLog
            {
                Type = LogType.Notification,
                Message = "Notification cycle failed",
                Detail = ex.Message,
                Success = false,
                CreatedAt = now,
            });
            await db.SaveChangesAsync(stoppingToken);
        }
    }

    /// <summary>Returns true if a notification was actually sent.</summary>
    private async Task<bool> ProcessUserNotification(
        AppDbContext db, string userId, string userEmail, string deviceToken,
        DateTimeOffset now, CancellationToken stoppingToken)
    {
        var summary = await DueCardQuery.GetDueCardSummary(db, userId, now, stoppingToken);

        if (summary.TotalDue == 0)
            return false;

        var body = $"You have {summary.TotalDue} card{(summary.TotalDue == 1 ? "" : "s")} due: {summary.Breakdown}";

        var tokenValid = await apnsService.SendNotification(deviceToken, "Cards due", body, summary.TotalDue);

        if (!tokenValid)
        {
            db.Logs.Add(new AppLog
            {
                Type = LogType.Notification,
                Message = $"Invalid token for {userEmail}, removed",
                Success = false,
                CreatedAt = now,
            });
            var token = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == userId, stoppingToken);
            if (token is not null)
                db.DeviceTokens.Remove(token);
            await db.SaveChangesAsync(stoppingToken);
            return false;
        }

        var user = await db.Users.FindAsync([userId], stoppingToken);
        if (user is not null)
            user.LastNotifiedAt = now;

        db.Logs.Add(new AppLog
        {
            Type = LogType.Notification,
            Message = $"Sent to {userEmail}: {summary.TotalDue} cards due",
            Detail = summary.Breakdown,
            Success = true,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(stoppingToken);
        return true;
    }
}
