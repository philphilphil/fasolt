namespace Fasolt.Server.Application.Dtos;

public record SchedulingSettingsResponse(double DesiredRetention, int MaximumInterval);
public record UpdateSchedulingSettingsRequest(double DesiredRetention, int MaximumInterval);
