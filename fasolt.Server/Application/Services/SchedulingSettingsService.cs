using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class SchedulingSettingsService(AppDbContext db)
{
    public const double DefaultRetention = 0.9;
    public const int DefaultMaxInterval = 36500;

    public async Task<SchedulingSettingsResponse> GetSettings(string userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        return new SchedulingSettingsResponse(
            user.DesiredRetention ?? DefaultRetention,
            user.MaximumInterval ?? DefaultMaxInterval);
    }

    public async Task<SchedulingSettingsResponse?> UpdateSettings(string userId, double desiredRetention, int maximumInterval)
    {
        if (desiredRetention < 0.70 || desiredRetention > 0.97)
            return null;
        if (maximumInterval < 1 || maximumInterval > 36500)
            return null;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return null;

        user.DesiredRetention = desiredRetention;
        user.MaximumInterval = maximumInterval;
        await db.SaveChangesAsync();

        return new SchedulingSettingsResponse(desiredRetention, maximumInterval);
    }
}
