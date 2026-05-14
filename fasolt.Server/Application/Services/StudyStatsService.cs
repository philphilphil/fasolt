using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Application.Services;

public class StudyStatsService(AppDbContext db, TimeProvider timeProvider)
{
    public async Task<StudyStatsDto> GetStats(string userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var tz = DueTimeRounder.ResolveTimeZone(user.TimeZone);
        var dayStartHour = user.DayStartHour ?? DueTimeRounder.DefaultDayStartHour;

        var now = timeProvider.GetUtcNow();
        var (todayStart, todayEnd) = GetDayBoundaries(now, tz, dayStartHour);

        var totalAnswered = await db.ReviewLogs.CountAsync(r => r.UserId == userId);
        var answeredToday = await db.ReviewLogs.CountAsync(r =>
            r.UserId == userId && r.ReviewedAt >= todayStart && r.ReviewedAt < todayEnd);

        var currentStreak = await ComputeCurrentStreak(userId, now, tz, dayStartHour);
        var bestStreak = Math.Max(user.BestStreak, currentStreak);

        return new StudyStatsDto(currentStreak, bestStreak, totalAnswered, answeredToday);
    }

    public async Task UpdateBestStreakIfNeeded(string userId)
    {
        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var tz = DueTimeRounder.ResolveTimeZone(user.TimeZone);
        var dayStartHour = user.DayStartHour ?? DueTimeRounder.DefaultDayStartHour;
        var now = timeProvider.GetUtcNow();

        var currentStreak = await ComputeCurrentStreak(userId, now, tz, dayStartHour);
        if (currentStreak > user.BestStreak)
        {
            user.BestStreak = currentStreak;
            await db.SaveChangesAsync();
        }
    }

    private async Task<int> ComputeCurrentStreak(string userId, DateTimeOffset now, TimeZoneInfo tz, int dayStartHour)
    {
        // Short-circuit: if user has no cards at all, return 0
        var earliestCardCreatedAt = await db.Cards
            .Where(c => c.UserId == userId)
            .Select(c => (DateTimeOffset?)c.CreatedAt)
            .MinAsync();

        if (earliestCardCreatedAt is null)
            return 0;

        // Pre-load the set of distinct study-day start boundaries (UTC) the user has reviewed on
        // We fetch all ReviewedAt timestamps and bucket them client-side for correctness with custom day boundaries
        var allReviewedAts = await db.ReviewLogs
            .Where(r => r.UserId == userId)
            .Select(r => r.ReviewedAt)
            .ToListAsync();

        var studyDayStarts = new HashSet<DateTimeOffset>(
            allReviewedAts.Select(r => GetDayBoundaries(r, tz, dayStartHour).Start));

        var (todayStart, _) = GetDayBoundaries(now, tz, dayStartHour);
        var (earliestStart, _) = GetDayBoundaries(earliestCardCreatedAt.Value, tz, dayStartHour);

        // Bound: at most 365 days back, and not before earliest card creation day
        var cutoff = todayStart.AddDays(-365);
        if (earliestStart > cutoff)
            cutoff = earliestStart;

        var streak = 0;

        // Today is special: if reviewed today, count it; then start walking from yesterday.
        // If not reviewed today, we don't break the streak — just start walking from yesterday.
        if (studyDayStarts.Contains(todayStart))
            streak = 1;

        var cursor = todayStart.AddDays(-1);

        while (cursor >= cutoff)
        {
            if (studyDayStarts.Contains(cursor))
            {
                streak++;
            }
            else
            {
                // No review on this day — check if it was a due day (missed = streak breaks)
                var isDue = await IsDueDay(userId, cursor, tz, dayStartHour);
                if (isDue)
                    break;
                // else: rest day (no due cards), streak preserved
            }

            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    private async Task<bool> IsDueDay(string userId, DateTimeOffset dayStart, TimeZoneInfo tz, int dayStartHour)
    {
        var (_, dayEnd) = (dayStart, dayStart.AddDays(1));
        // dayEnd is the exclusive end; for the "due as of end of day" check we use dayStart.AddDays(1)
        // which is the start of next day = end of this day
        var dayEndInclusive = dayStart.AddDays(1);

        // A day is a "due day" if there exists a card created on/before day-end
        // whose latest-known scheduled-due as of day-end <= day-end.
        // "latest scheduled due as of T" = MAX(ScheduledDueAfter) from ReviewLogs where ReviewedAt <= T,
        // or Card.CreatedAt if no such log row.

        // We check: does any card exist where:
        //   card.CreatedAt <= dayEnd AND
        //   (the max ScheduledDueAfter from logs with ReviewedAt <= dayEnd, or card.CreatedAt if none) <= dayEnd

        // Implemented as: EXISTS a card c where:
        //   c.UserId == userId
        //   c.CreatedAt <= dayEndInclusive
        //   effective_due(c, dayEndInclusive) <= dayEndInclusive
        //
        // effective_due = MAX log.ScheduledDueAfter (where log.CardId = c.Id AND log.ReviewedAt <= dayEndInclusive)
        //                 OR c.CreatedAt if no such log

        return await db.Cards
            .Where(c => c.UserId == userId && c.CreatedAt <= dayEndInclusive)
            .AnyAsync(c =>
                (db.ReviewLogs
                    .Where(l => l.CardId == c.Id && l.ReviewedAt <= dayEndInclusive && l.ScheduledDueAfter != null)
                    .Max(l => (DateTimeOffset?)l.ScheduledDueAfter) ?? c.CreatedAt) <= dayEndInclusive);
    }

    private static (DateTimeOffset Start, DateTimeOffset End) GetDayBoundaries(
        DateTimeOffset utcTime, TimeZoneInfo tz, int dayStartHour)
    {
        var localTime = TimeZoneInfo.ConvertTime(utcTime, tz);
        var localDate = localTime.TimeOfDay < TimeSpan.FromHours(dayStartHour)
            ? localTime.Date.AddDays(-1)
            : localTime.Date;

        var localStart = DateTime.SpecifyKind(localDate.AddHours(dayStartHour), DateTimeKind.Unspecified);

        // Handle DST gaps
        while (tz.IsInvalidTime(localStart))
            localStart = localStart.AddHours(1);

        var startUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localStart, tz));
        var endUtc = startUtc.AddDays(1);

        return (startUtc, endUtc);
    }
}
