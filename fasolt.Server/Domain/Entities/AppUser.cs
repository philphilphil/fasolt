using Microsoft.AspNetCore.Identity;

namespace Fasolt.Server.Domain.Entities;

public class AppUser : IdentityUser
{
    public int NotificationIntervalHours { get; set; } = 8;
    public DateTimeOffset? LastNotifiedAt { get; set; }
    public double? DesiredRetention { get; set; }
    public int? MaximumInterval { get; set; }
    public int? DayStartHour { get; set; }
    public string? TimeZone { get; set; }
    public string? ExternalProvider { get; set; }
    public string? ExternalProviderId { get; set; }
    public int BestStreak { get; set; } = 0;
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>
    /// Public author handle, shown on library listings. Null until claimed.
    /// 3–30 chars, lowercase alphanumeric plus hyphen; unique across users.
    /// </summary>
    public string? Handle { get; set; }

    /// <summary>False when an admin has banned the user from publishing decks.</summary>
    public bool CanPublish { get; set; } = true;
}
