using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Server.Infrastructure.Services;

namespace Fasolt.Server.Application.Services;

public class AdminService(AppDbContext db, ApnsService? apnsService = null)
{
    public async Task<AdminUserListResponse> ListUsers(
        int page,
        int pageSize,
        string? q,
        string? provider,
        bool? lockedOnly,
        bool? hasPushOnly)
    {
        var now = DateTimeOffset.UtcNow;
        var query = db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(u =>
                (u.Email != null && EF.Functions.ILike(u.Email, pattern)) ||
                (u.UserName != null && EF.Functions.ILike(u.UserName, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            // "Email" sentinel means local (no external provider)
            if (provider.Equals("Email", StringComparison.OrdinalIgnoreCase))
                query = query.Where(u => u.ExternalProvider == null);
            else
                query = query.Where(u => u.ExternalProvider == provider);
        }

        if (lockedOnly == true)
            query = query.Where(u => u.LockoutEnabled && u.LockoutEnd > now);

        if (hasPushOnly == true)
            query = query.Where(u => db.DeviceTokens.Any(d => d.UserId == u.Id));

        var totalCount = await query.CountAsync();

        var users = await query
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
                u.LockoutEnabled && u.LockoutEnd > now,
                db.DeviceTokens.Any(d => d.UserId == u.Id),
                u.EmailConfirmed))
            .ToListAsync();

        return new AdminUserListResponse(users, totalCount, page, pageSize);
    }

    public async Task<AdminStatsDto> GetStats()
    {
        var now = DateTimeOffset.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);

        var totalUsers = await db.Users.CountAsync();
        var lockedUsers = await db.Users.CountAsync(u => u.LockoutEnabled && u.LockoutEnd > now);
        var usersWithPush = await db.DeviceTokens.Select(d => d.UserId).Distinct().CountAsync();
        var totalCards = await db.Cards.CountAsync();
        var totalDecks = await db.Decks.CountAsync();
        var dueCards = await db.Cards.CountAsync(c =>
            !c.IsSuspended && c.DueAt != null && c.DueAt <= now);

        var registrationsLast7d = await db.Logs.CountAsync(l =>
            l.Type == LogType.UserRegistered && l.CreatedAt >= sevenDaysAgo);
        var registrationsLast30d = await db.Logs.CountAsync(l =>
            l.Type == LogType.UserRegistered && l.CreatedAt >= thirtyDaysAgo);
        var mcpLoginsLast7d = await db.Logs.CountAsync(l =>
            l.Type == LogType.McpLogin && l.CreatedAt >= sevenDaysAgo);
        var mcpLoginsLast30d = await db.Logs.CountAsync(l =>
            l.Type == LogType.McpLogin && l.CreatedAt >= thirtyDaysAgo);

        return new AdminStatsDto(
            totalUsers,
            lockedUsers,
            usersWithPush,
            totalCards,
            totalDecks,
            dueCards,
            registrationsLast7d,
            registrationsLast30d,
            mcpLoginsLast7d,
            mcpLoginsLast30d);
    }

    public async Task<LogListResponse> GetLogs(int page, int pageSize, string? type, string? q, bool? success)
    {
        var query = db.Logs.AsQueryable();
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<LogType>(type, true, out var logType))
            query = query.Where(l => l.Type == logType);

        if (success.HasValue)
            query = query.Where(l => l.Success == success.Value);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(l =>
                EF.Functions.ILike(l.Message, pattern) ||
                (l.Detail != null && EF.Functions.ILike(l.Detail, pattern)));
        }

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LogEntryDto(l.Id, l.Type.ToString(), l.Message, l.Detail, l.Success, l.CreatedAt))
            .ToListAsync();

        return new LogListResponse(logs, total, page, pageSize);
    }

    public async Task LogAdminAction(string message)
    {
        db.Logs.Add(new AppLog
        {
            Type = LogType.Admin,
            Message = message,
            Success = true,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
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
