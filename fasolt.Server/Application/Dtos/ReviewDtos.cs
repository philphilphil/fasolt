namespace Fasolt.Server.Application.Dtos;

public record RateCardRequest(string CardId, int Quality);

public record RateCardResponse(string CardId, double? Stability, double? Difficulty, int? Step, DateTimeOffset? DueAt, string State);

public record DueCardDto(
    string Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State, double? Stability, double? Difficulty, int? Step);

public record ReviewStatsDto(int DueCount, int TotalCards, int StudiedToday);
