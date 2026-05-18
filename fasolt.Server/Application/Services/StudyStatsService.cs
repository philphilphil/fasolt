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

    public async Task<ProgressDto> GetProgress(string userId, int days = 30)
    {
        var clampedDays = Math.Clamp(days, 7, 366);

        var user = await db.Users.FirstAsync(u => u.Id == userId);
        var tz = DueTimeRounder.ResolveTimeZone(user.TimeZone);
        var dayStartHour = user.DayStartHour ?? DueTimeRounder.DefaultDayStartHour;

        var now = timeProvider.GetUtcNow();
        var (todayStart, todayEnd) = GetDayBoundaries(now, tz, dayStartHour);
        var weekStart = GetWeekStart(todayStart, tz);
        var monthStart = GetMonthStart(todayStart, tz, dayStartHour);
        var windowStart = todayStart.AddDays(-(clampedDays - 1));

        var totalAnswered = await db.ReviewLogs.CountAsync(r => r.UserId == userId);
        var answeredToday = await db.ReviewLogs.CountAsync(r =>
            r.UserId == userId && r.ReviewedAt >= todayStart && r.ReviewedAt < todayEnd);
        var answeredThisWeek = await db.ReviewLogs.CountAsync(r =>
            r.UserId == userId && r.ReviewedAt >= weekStart && r.ReviewedAt < todayEnd);
        var answeredThisMonth = await db.ReviewLogs.CountAsync(r =>
            r.UserId == userId && r.ReviewedAt >= monthStart && r.ReviewedAt < todayEnd);

        var currentStreak = await ComputeCurrentStreak(userId, now, tz, dayStartHour);
        var bestStreak = Math.Max(user.BestStreak, currentStreak);

        var activity = await BuildDailyActivity(userId, todayStart, todayEnd, clampedDays, tz, dayStartHour);
        var ratingMix = await BuildRatingMix(userId, windowStart, todayEnd);

        return new ProgressDto(
            currentStreak, bestStreak,
            totalAnswered, answeredToday, answeredThisWeek, answeredThisMonth,
            activity, ratingMix);
    }

    private async Task<RatingMixDto> BuildRatingMix(
        string userId, DateTimeOffset windowStart, DateTimeOffset windowEnd)
    {
        // Group by rating in SQL, then bucket. Anything outside the known
        // ratings is ignored — we don't want unknown strings inflating totals.
        var counts = await db.ReviewLogs
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.ReviewedAt >= windowStart && r.ReviewedAt < windowEnd)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync();

        var again = 0;
        var hard = 0;
        var good = 0;
        var easy = 0;
        foreach (var c in counts)
        {
            switch (c.Rating)
            {
                case "again": again = c.Count; break;
                case "hard":  hard  = c.Count; break;
                case "good":  good  = c.Count; break;
                case "easy":  easy  = c.Count; break;
            }
        }
        return new RatingMixDto(again, hard, good, easy);
    }

    private async Task<List<DailyActivityDto>> BuildDailyActivity(
        string userId, DateTimeOffset todayStart, DateTimeOffset todayEnd, int days,
        TimeZoneInfo tz, int dayStartHour)
    {
        var windowStart = todayStart.AddDays(-(days - 1));

        var reviewedAts = await db.ReviewLogs
            .Where(r => r.UserId == userId && r.ReviewedAt >= windowStart && r.ReviewedAt < todayEnd)
            .Select(r => r.ReviewedAt)
            .ToListAsync();

        var countsByDay = new Dictionary<DateTimeOffset, int>();
        foreach (var rt in reviewedAts)
        {
            var dayStart = GetDayBoundaries(rt, tz, dayStartHour).Start;
            countsByDay[dayStart] = countsByDay.GetValueOrDefault(dayStart) + 1;
        }

        // Pre-window due day check needs cards that existed before windowStart;
        // ComputeDueDayStarts handles that based on its cutoff.
        var dueDayStarts = await ComputeDueDayStarts(userId, windowStart, todayStart, tz, dayStartHour);

        // Whether today itself has any due cards right now
        var hasDueToday = await db.Cards.AnyAsync(c =>
            c.UserId == userId && !c.IsSuspended &&
            (!c.DeckCards.Any() || c.DeckCards.Any(dc => !dc.Deck.IsSuspended)) &&
            (c.DueAt == null || c.DueAt <= todayEnd));

        var activity = new List<DailyActivityDto>(days);
        for (var d = windowStart; d <= todayStart; d = d.AddDays(1))
        {
            var count = countsByDay.GetValueOrDefault(d, 0);
            var hadDue = d == todayStart ? hasDueToday : dueDayStarts.Contains(d);

            // Date label is the local calendar date the day-window starts on.
            var localDate = TimeZoneInfo.ConvertTime(d, tz).Date;
            activity.Add(new DailyActivityDto(DateOnly.FromDateTime(localDate), count, hadDue));
        }

        return activity;
    }

    private static DateTimeOffset GetWeekStart(DateTimeOffset todayStart, TimeZoneInfo tz)
    {
        // Week starts on Monday in the user's local time.
        var localDay = TimeZoneInfo.ConvertTime(todayStart, tz);
        var dow = (int)localDay.DayOfWeek; // Sun=0..Sat=6
        var daysSinceMonday = (dow + 6) % 7; // Mon=0, Sun=6
        return todayStart.AddDays(-daysSinceMonday);
    }

    private static DateTimeOffset GetMonthStart(DateTimeOffset todayStart, TimeZoneInfo tz, int dayStartHour)
    {
        var local = TimeZoneInfo.ConvertTime(todayStart, tz);
        var firstLocal = DateTime.SpecifyKind(new DateTime(local.Year, local.Month, 1).AddHours(dayStartHour), DateTimeKind.Unspecified);
        while (tz.IsInvalidTime(firstLocal))
            firstLocal = firstLocal.AddHours(1);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(firstLocal, tz));
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

        var (todayStart, _) = GetDayBoundaries(now, tz, dayStartHour);
        var (earliestStart, _) = GetDayBoundaries(earliestCardCreatedAt.Value, tz, dayStartHour);

        // Bound: at most 365 days back, and not before earliest card creation day
        var cutoff = todayStart.AddDays(-365);
        if (earliestStart > cutoff)
            cutoff = earliestStart;

        // Pre-load study-day boundaries from reviews in the walk window only.
        // Bucketing is done client-side so custom per-user day boundaries are respected.
        var windowStart = now.AddDays(-366);
        var windowReviewedAts = await db.ReviewLogs
            .Where(r => r.UserId == userId && r.ReviewedAt >= windowStart)
            .Select(r => r.ReviewedAt)
            .ToListAsync();

        var studyDayStarts = new HashSet<DateTimeOffset>(
            windowReviewedAts.Select(r => GetDayBoundaries(r, tz, dayStartHour).Start));

        // Precompute the set of "due days" in the walk window with a single load,
        // instead of a per-day SQL round-trip (avoids N+1 in the gap walk).
        var dueDayStarts = await ComputeDueDayStarts(userId, cutoff, todayStart, tz, dayStartHour);

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
            else if (dueDayStarts.Contains(cursor))
            {
                break;
            }
            // else: rest day (no due cards), streak preserved

            cursor = cursor.AddDays(-1);
        }

        return streak;
    }

    private async Task<HashSet<DateTimeOffset>> ComputeDueDayStarts(
        string userId, DateTimeOffset cutoff, DateTimeOffset todayStart, TimeZoneInfo tz, int dayStartHour)
    {
        var todayEnd = todayStart.AddDays(1);

        var cards = await db.Cards
            .Where(c => c.UserId == userId && c.CreatedAt <= todayEnd)
            .Select(c => new { c.Id, c.CreatedAt })
            .ToListAsync();

        if (cards.Count == 0)
            return new HashSet<DateTimeOffset>();

        // For each card with logs before the walk window, fetch MAX(ScheduledDueAfter)
        // to initialise effective-due at cutoff (matches original "MAX over all eligible logs" semantics).
        var preWindowMaxByCard = await db.ReviewLogs
            .Where(r => r.UserId == userId && r.ReviewedAt < cutoff && r.ScheduledDueAfter != null)
            .GroupBy(r => r.CardId)
            .Select(g => new { CardId = g.Key, MaxDue = g.Max(x => x.ScheduledDueAfter) })
            .ToDictionaryAsync(x => x.CardId, x => x.MaxDue!.Value);

        var inWindowLogs = await db.ReviewLogs
            .Where(r => r.UserId == userId && r.ReviewedAt >= cutoff && r.ScheduledDueAfter != null)
            .OrderBy(r => r.ReviewedAt)
            .Select(r => new { r.CardId, r.ReviewedAt, ScheduledDueAfter = r.ScheduledDueAfter!.Value })
            .ToListAsync();

        var logsByCard = inWindowLogs
            .GroupBy(l => l.CardId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var dueDayStarts = new HashSet<DateTimeOffset>();

        foreach (var card in cards)
        {
            var cardCreatedDay = GetDayBoundaries(card.CreatedAt, tz, dayStartHour).Start;
            var startCursor = cardCreatedDay < cutoff ? cutoff : cardCreatedDay;
            if (startCursor >= todayStart)
                continue;

            // Initial effective-due at startCursor: MAX of pre-window scheduled-dues, or CreatedAt if none.
            // (Cards created within the window have no pre-window logs, so they start at CreatedAt.)
            var effectiveDue = preWindowMaxByCard.TryGetValue(card.Id, out var preMax)
                ? preMax
                : card.CreatedAt;

            var logs = logsByCard.TryGetValue(card.Id, out var l) ? l : null;
            var logIdx = 0;

            for (var cursor = startCursor; cursor < todayStart; cursor = cursor.AddDays(1))
            {
                var dayEnd = cursor.AddDays(1);

                if (logs is not null)
                {
                    while (logIdx < logs.Count && logs[logIdx].ReviewedAt < dayEnd)
                    {
                        if (logs[logIdx].ScheduledDueAfter > effectiveDue)
                            effectiveDue = logs[logIdx].ScheduledDueAfter;
                        logIdx++;
                    }
                }

                if (effectiveDue <= dayEnd)
                    dueDayStarts.Add(cursor);
            }
        }

        return dueDayStarts;
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
