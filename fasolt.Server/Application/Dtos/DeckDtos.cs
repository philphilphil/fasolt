namespace Fasolt.Server.Application.Dtos;

public record CreateDeckRequest(string Name, string? Description);

public record UpdateDeckRequest(string Name, string? Description);

public record DeckDto(string Id, string Name, string? Description, int CardCount, int DueCount, DateTimeOffset CreatedAt, bool IsSuspended);

public record DeckDetailDto(string Id, string Name, string? Description, int CardCount, int DueCount, List<DeckCardDto> Cards, bool IsSuspended);

public record SetDeckSuspendedRequest(bool IsSuspended);

public record DeckCardDto(
    string Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State, DateTimeOffset? DueAt,
    bool IsSuspended = false,
    double? Stability = null, double? Difficulty = null,
    int? Step = null, DateTimeOffset? LastReviewedAt = null,
    string? FrontSvg = null, string? BackSvg = null);

public record AddCardsToDeckRequest(List<string> CardIds);
