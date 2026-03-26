using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Server.Infrastructure.Services;

namespace Fasolt.Server.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization("AdminCookieOnly");

        group.MapGet("/users", ListUsers);
        group.MapPost("/users/{id}/lock", LockUser);
        group.MapPost("/users/{id}/unlock", UnlockUser);
        group.MapGet("/logs", GetLogs);
        group.MapPost("/users/{id}/push", TriggerPushForUser);
    }

    private static async Task<IResult> ListUsers(
        int? page,
        int? pageSize,
        AdminService adminService)
    {
        var p = page ?? 1;
        var ps = Math.Clamp(pageSize ?? 50, 1, 100);
        var result = await adminService.ListUsers(p, ps);
        return Results.Ok(result);
    }

    private static async Task<IResult> LockUser(
        string id,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager)
    {
        var currentUser = await userManager.GetUserAsync(principal);
        if (currentUser is null) return Results.Unauthorized();

        if (currentUser.Id == id)
            return Results.BadRequest(new { error = "Cannot lock your own account." });

        var user = await userManager.FindByIdAsync(id);
        if (user is null) return Results.NotFound();

        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

        return Results.Ok();
    }

    private static async Task<IResult> UnlockUser(
        string id,
        UserManager<AppUser> userManager)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return Results.NotFound();

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        return Results.Ok();
    }

    private static async Task<IResult> GetLogs(
        AppDbContext db,
        int? page,
        int? pageSize,
        string? type)
    {
        var p = page ?? 1;
        var ps = Math.Clamp(pageSize ?? 50, 1, 100);

        var query = db.Logs.AsQueryable();
        if (!string.IsNullOrEmpty(type) && Enum.TryParse<LogType>(type, true, out var logType))
            query = query.Where(l => l.Type == logType);

        var total = await query.CountAsync();
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(l => new
            {
                l.Id,
                Type = l.Type.ToString(),
                l.Message,
                l.Detail,
                l.Success,
                l.CreatedAt,
            })
            .ToListAsync();

        return Results.Ok(new { logs, totalCount = total, page = p, pageSize = ps });
    }

    private static async Task<IResult> TriggerPushForUser(
        string id,
        AppDbContext db,
        [FromServices] ApnsService apnsService)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return Results.NotFound();

        var deviceToken = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == id);
        if (deviceToken is null)
            return Results.BadRequest(new { error = "No device token registered for this user." });

        var now = DateTimeOffset.UtcNow;

        // Count due cards with deck breakdown — same query as the background service
        var dueCardsByDeck = await db.Cards
            .Where(c => c.UserId == id && (c.DueAt == null || c.DueAt <= now))
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
        {
            db.DeviceTokens.Remove(deviceToken);
        }

        await db.SaveChangesAsync();

        return tokenValid
            ? Results.Ok(new { message = $"Push sent: {body}" })
            : Results.Ok(new { message = "Token was invalid and has been removed." });
    }
}
