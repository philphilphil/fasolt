namespace Fasolt.Server.Domain.Entities;

public enum LogType
{
    Notification,
}

public class AppLog
{
    public int Id { get; set; }
    public LogType Type { get; set; }
    public string Message { get; set; } = default!;
    public string? Detail { get; set; }
    public bool Success { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
