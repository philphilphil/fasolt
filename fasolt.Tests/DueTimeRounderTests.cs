using FluentAssertions;
using Fasolt.Server.Application.Services;

namespace Fasolt.Tests;

public class DueTimeRounderTests
{
    [Fact]
    public void RoundDueUtc_LeavesShortIntervalsUnchanged()
    {
        var now = new DateTime(2026, 4, 26, 22, 0, 0, DateTimeKind.Utc);
        var due = now.AddMinutes(10);

        var result = DueTimeRounder.RoundDueUtc(due, now, 4, TimeZoneInfo.Utc);

        result.Should().Be(due);
    }

    [Fact]
    public void RoundDueUtc_LeavesIntervalsUnderOneDayUnchanged()
    {
        var now = new DateTime(2026, 4, 26, 9, 0, 0, DateTimeKind.Utc);
        var due = now.AddHours(23);

        var result = DueTimeRounder.RoundDueUtc(due, now, 4, TimeZoneInfo.Utc);

        result.Should().Be(due);
    }

    [Fact]
    public void RoundDueUtc_RoundsDownToDayStartInUtc()
    {
        var now = new DateTime(2026, 4, 26, 9, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2026, 4, 30, 14, 32, 0, DateTimeKind.Utc);

        var result = DueTimeRounder.RoundDueUtc(due, now, 4, TimeZoneInfo.Utc);

        result.Should().Be(new DateTime(2026, 4, 30, 4, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void RoundDueUtc_DueBeforeDayStart_RoundsToPreviousDay()
    {
        var now = new DateTime(2026, 4, 26, 9, 0, 0, DateTimeKind.Utc);
        // 02:00 UTC on the 30th — the "study day" started at 04:00 on the 29th.
        var due = new DateTime(2026, 4, 30, 2, 0, 0, DateTimeKind.Utc);

        var result = DueTimeRounder.RoundDueUtc(due, now, 4, TimeZoneInfo.Utc);

        result.Should().Be(new DateTime(2026, 4, 29, 4, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void RoundDueUtc_RespectsBerlinTimezone()
    {
        // User in Berlin, day-start hour = 4. CEST is UTC+2 in late April.
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var now = new DateTime(2026, 4, 26, 21, 0, 0, DateTimeKind.Utc); // 23:00 Berlin
        // FSRS due 2 days later at the same UTC moment: 2026-04-28 21:00 UTC = 23:00 Berlin
        var due = new DateTime(2026, 4, 28, 21, 0, 0, DateTimeKind.Utc);

        var result = DueTimeRounder.RoundDueUtc(due, now, 4, berlin);

        // Berlin study-day for that local time started 2026-04-28 04:00 Berlin = 2026-04-28 02:00 UTC.
        result.Should().Be(new DateTime(2026, 4, 28, 2, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void RoundDueUtc_BerlinDayStart6_RoundsToSixAmBerlin()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        var now = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc); // 10:00 Berlin
        var due = new DateTime(2026, 5, 5, 14, 0, 0, DateTimeKind.Utc); // 16:00 Berlin

        var result = DueTimeRounder.RoundDueUtc(due, now, 6, berlin);

        // Berlin 06:00 on 2026-05-05 = 04:00 UTC (CEST = UTC+2).
        result.Should().Be(new DateTime(2026, 5, 5, 4, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void IsValidTimeZoneId_AcceptsKnownIanaZone()
    {
        DueTimeRounder.IsValidTimeZoneId("Europe/Berlin").Should().BeTrue();
        DueTimeRounder.IsValidTimeZoneId("UTC").Should().BeTrue();
        DueTimeRounder.IsValidTimeZoneId("America/New_York").Should().BeTrue();
    }

    [Fact]
    public void IsValidTimeZoneId_RejectsGarbage()
    {
        DueTimeRounder.IsValidTimeZoneId("").Should().BeFalse();
        DueTimeRounder.IsValidTimeZoneId("Not/A/Zone").Should().BeFalse();
    }
}
