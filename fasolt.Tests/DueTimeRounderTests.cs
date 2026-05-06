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
    public void RoundDueUtc_DstGap_StepsForwardToValidLocalTime()
    {
        // America/New_York spring-forward 2026: 02:00 EST → 03:00 EDT on 2026-03-08.
        // Local 02:00 on that day does not exist.
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var now = new DateTime(2026, 3, 6, 12, 0, 0, DateTimeKind.Utc);
        // Due 2026-03-08 14:00 EDT = 18:00 UTC (after the transition).
        var due = new DateTime(2026, 3, 8, 18, 0, 0, DateTimeKind.Utc);

        var result = DueTimeRounder.RoundDueUtc(due, now, 2, ny);

        // Boundary would be 2026-03-08 02:00 NY which falls in the DST gap;
        // we step forward to 03:00 EDT = 07:00 UTC.
        result.Should().Be(new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void RoundDueUtc_DstAmbiguous_RoundsToValidInstant()
    {
        // America/New_York fall-back 2026: 02:00 EDT → 01:00 EST on 2026-11-01.
        // Local 01:30 happens twice; 04:00 is unambiguous so should just work.
        var ny = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var due = new DateTime(2026, 11, 1, 18, 0, 0, DateTimeKind.Utc); // 13:00 EST

        var result = DueTimeRounder.RoundDueUtc(due, now, 4, ny);

        // 2026-11-01 04:00 EST = 09:00 UTC (after fall-back, EST = UTC-5).
        result.Should().Be(new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ResolveTimeZone_NullOrEmpty_ReturnsUtc()
    {
        DueTimeRounder.ResolveTimeZone(null).Should().Be(TimeZoneInfo.Utc);
        DueTimeRounder.ResolveTimeZone("").Should().Be(TimeZoneInfo.Utc);
        DueTimeRounder.ResolveTimeZone("   ").Should().Be(TimeZoneInfo.Utc);
    }

    [Fact]
    public void ResolveTimeZone_UnknownId_FallsBackToUtc()
    {
        DueTimeRounder.ResolveTimeZone("Not/A/Zone").Should().Be(TimeZoneInfo.Utc);
    }

    [Fact]
    public void ResolveTimeZone_KnownIanaId_ReturnsZone()
    {
        var berlin = DueTimeRounder.ResolveTimeZone("Europe/Berlin");

        berlin.Id.Should().Be("Europe/Berlin");
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
