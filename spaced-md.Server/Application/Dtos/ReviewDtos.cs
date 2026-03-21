namespace SpacedMd.Server.Application.Dtos;

public record RateCardRequest(Guid CardId, int Quality);

public record RateCardResponse(Guid CardId, double EaseFactor, int Interval, int Repetitions, DateTimeOffset? DueAt, string State);

public record DueCardDto(Guid Id, string Front, string Back, string CardType, string? SourceHeading, Guid? FileId, string State, DateTimeOffset? DueAt);

public record ReviewStatsDto(int DueCount, int TotalCards, int StudiedToday);
