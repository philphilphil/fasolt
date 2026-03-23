namespace Fasolt.Server.Application.Dtos;

public record RateCardRequest(string CardId, int Quality);

public record RateCardResponse(string CardId, double EaseFactor, int Interval, int Repetitions, DateTimeOffset? DueAt, string State);

public record DueCardDto(
    string Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State, double EaseFactor, int Interval, int Repetitions);

public record ReviewStatsDto(int DueCount, int TotalCards, int StudiedToday);
