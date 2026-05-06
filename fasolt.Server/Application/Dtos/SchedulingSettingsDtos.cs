namespace Fasolt.Server.Application.Dtos;

public record SchedulingSettingsResponse(
    double DesiredRetention,
    int MaximumInterval,
    int DayStartHour,
    string? TimeZone);

public record UpdateSchedulingSettingsRequest(
    double DesiredRetention,
    int MaximumInterval,
    int DayStartHour,
    string TimeZone);
