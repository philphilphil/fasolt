namespace Fasolt.Server.Application.Dtos;

public record DailyActivityDto(DateOnly Date, int Count, bool HadDue);

public record ProgressDto(
    int CurrentStreak,
    int BestStreak,
    int TotalAnswered,
    int AnsweredToday,
    int AnsweredThisWeek,
    int AnsweredThisMonth,
    List<DailyActivityDto> DailyActivity);
