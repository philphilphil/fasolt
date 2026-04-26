namespace Fasolt.Server.Application.Services;

public static class DueTimeRounder
{
    public const int DefaultDayStartHour = 4;
    public const string DefaultTimeZoneId = "UTC";

    public static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static bool IsValidTimeZoneId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Rounds a UTC due time down to the most recent day-start boundary in the user's timezone.
    /// Cards whose interval (due - now) is less than one day are returned unchanged so that
    /// FSRS sub-day learning steps still fire at their scheduled times.
    /// </summary>
    public static DateTime RoundDueUtc(DateTime dueUtc, DateTime nowUtc, int dayStartHour, TimeZoneInfo tz)
    {
        if (dueUtc - nowUtc < TimeSpan.FromDays(1)) return dueUtc;

        var dueLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dueUtc, DateTimeKind.Utc), tz);
        var shifted = dueLocal.AddHours(-dayStartHour);
        var localBoundary = shifted.Date.AddHours(dayStartHour);
        // Use unspecified kind so ConvertTimeToUtc treats it as wall-clock in the given tz.
        var unspecified = DateTime.SpecifyKind(localBoundary, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
    }
}
