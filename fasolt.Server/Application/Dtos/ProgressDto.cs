namespace Fasolt.Server.Application.Dtos;

public record DailyActivityDto(DateOnly Date, int Count, bool HadDue);

public record RatingMixDto(int Again, int Hard, int Good, int Easy);

public record ProgressDto(
    int CurrentStreak,
    int BestStreak,
    int TotalAnswered,
    int AnsweredToday,
    int AnsweredThisWeek,
    int AnsweredThisMonth,
    List<DailyActivityDto> DailyActivity,
    RatingMixDto RatingMix);
