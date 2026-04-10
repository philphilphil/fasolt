using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Server.Infrastructure.Services;

namespace Fasolt.Server.Application.Services;

public class AdminService(AppDbContext db, ApnsService? apnsService = null)
{
    public async Task<AdminUserListResponse> ListUsers(int page, int pageSize)
    {
        var totalCount = await db.Users.CountAsync();

        var users = await db.Users
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto(
                u.Id,
                u.Email!,
                u.ExternalProvider != null ? u.UserName : null,
                u.ExternalProvider,
                db.Cards.Count(c => c.UserId == u.Id),
                db.Decks.Count(d => d.UserId == u.Id),
                u.LockoutEnabled && u.LockoutEnd > DateTimeOffset.UtcNow,
                db.DeviceTokens.Any(d => d.UserId == u.Id),
                u.EmailConfirmed))
            .ToListAsync();

        return new AdminUserListResponse(users, totalCount, page, pageSize);
    }

    public async Task<LogListResponse> GetLogs(int page, int pageSize, string? type)
    {
        var query = db.Logs.AsQueryable();
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<LogType>(type, true, out var logType))
            query = query.Where(l => l.Type == logType);

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LogEntryDto(l.Id, l.Type.ToString(), l.Message, l.Detail, l.Success, l.CreatedAt))
            .ToListAsync();

        return new LogListResponse(logs, total, page, pageSize);
    }

    public async Task<PushResult?> TriggerPushForUser(string userId)
    {
        if (apnsService is null) return null;

        var user = await db.Users.FindAsync(userId);
        if (user is null) return null;

        var deviceToken = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == userId);
        if (deviceToken is null) return null;

        var now = DateTimeOffset.UtcNow;

        var summary = await DueCardQuery.GetDueCardSummary(db, userId, now);
        string body;
        if (summary.TotalDue == 0)
            body = "Test notification — no cards currently due";
        else
            body = $"You have {summary.TotalDue} card{(summary.TotalDue == 1 ? "" : "s")} due: {summary.Breakdown}";

        var tokenValid = await apnsService.SendNotification(deviceToken.Token, "Cards due", body, summary.TotalDue);

        db.Logs.Add(new AppLog
        {
            Type = LogType.Notification,
            Message = tokenValid
                ? $"Admin push to {user.Email}: {body}"
                : $"Invalid token for {user.Email}, removed",
            Detail = tokenValid ? null : "Token returned 410 Gone",
            Success = tokenValid,
            CreatedAt = now,
        });

        if (!tokenValid)
            db.DeviceTokens.Remove(deviceToken);

        await db.SaveChangesAsync();

        return new PushResult(
            tokenValid ? $"Push sent: {body}" : "Token was invalid and has been removed.",
            tokenValid);
    }
}
