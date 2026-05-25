namespace Fasolt.Server.Application.Dtos;

public record StudyStatsDto(
    int CurrentStreak,
    int BestStreak,
    int TotalAnswered,
    int AnsweredToday,
    IReadOnlyList<DailyActivityDto> Last7Days);
