namespace Fasolt.Server.Application.Dtos;

public record RateCardRequest(string CardId, string Rating);

public record RateCardResponse(string CardId, double? Stability, double? Difficulty, DateTimeOffset? DueAt, string State);

public record DueCardDto(
    string Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State,
    string? FrontSvg = null, string? BackSvg = null);

public record ReviewStatsDto(int DueCount, int TotalCards, int StudiedToday);
