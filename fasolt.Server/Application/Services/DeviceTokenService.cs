using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class DeviceTokenService(AppDbContext db, UserManager<AppUser> userManager)
{
    private static readonly int[] AllowedIntervals = [4, 6, 8, 10, 12, 24];

    public async Task UpsertDeviceToken(string userId, string token)
    {
        var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == userId);
        var now = DateTimeOffset.UtcNow;

        if (existing is not null)
        {
            existing.Token = token;
            existing.UpdatedAt = now;
        }
        else
        {
            db.DeviceTokens.Add(new DeviceToken
            {
                UserId = userId,
                Token = token,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task DeleteDeviceToken(string userId)
    {
        var existing = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == userId);
        if (existing is not null)
        {
            db.DeviceTokens.Remove(existing);
            await db.SaveChangesAsync();
        }
    }

    public async Task<NotificationSettingsResponse> GetSettings(string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        var hasToken = await db.DeviceTokens.AnyAsync(d => d.UserId == userId);
        return new NotificationSettingsResponse(user!.NotificationIntervalHours, hasToken);
    }

    public async Task<bool> UpdateSettings(string userId, int intervalHours)
    {
        if (!AllowedIntervals.Contains(intervalHours))
            return false;

        var user = await userManager.FindByIdAsync(userId);
        if (user is null) return false;

        user.NotificationIntervalHours = intervalHours;
        await userManager.UpdateAsync(user);
        return true;
    }
}
