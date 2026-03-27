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
                db.Cards.Count(c => c.UserId == u.Id),
                db.Decks.Count(d => d.UserId == u.Id),
                u.LockoutEnabled && u.LockoutEnd > DateTimeOffset.UtcNow,
                db.DeviceTokens.Any(d => d.UserId == u.Id)))
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

        var dueCardsByDeck = await db.Cards
            .Where(c => c.UserId == userId && (c.DueAt == null || c.DueAt <= now))
            .Where(c => !c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive))
            .SelectMany(c => c.DeckCards.DefaultIfEmpty(),
                (card, deckCard) => new { DeckName = deckCard != null ? deckCard.Deck.Name : null })
            .GroupBy(x => x.DeckName ?? "Unsorted")
            .Select(g => new { DeckName = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalDue = dueCardsByDeck.Sum(g => g.Count);
        string body;
        if (totalDue == 0)
            body = "Test notification — no cards currently due";
        else
        {
            var breakdown = string.Join(", ",
                dueCardsByDeck.OrderByDescending(g => g.Count).Select(g => $"{g.Count} in {g.DeckName}"));
            body = $"You have {totalDue} card{(totalDue == 1 ? "" : "s")} due: {breakdown}";
        }

        var tokenValid = await apnsService.SendNotification(deviceToken.Token, "Cards due", body, totalDue);

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
