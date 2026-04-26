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
}
