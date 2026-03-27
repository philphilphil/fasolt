namespace Fasolt.Server.Application.Dtos;

public record UpsertDeviceTokenRequest(string Token);
public record UpdateNotificationSettingsRequest(int IntervalHours);
public record NotificationSettingsResponse(int IntervalHours, bool HasDeviceToken);
