namespace Fasolt.Server.Application.Dtos;

public record CreateDeckRequest(string Name, string? Description);

public record UpdateDeckRequest(string Name, string? Description);

public record DeckDto(string Id, string Name, string? Description, int CardCount, int DueCount, DateTimeOffset CreatedAt, bool IsActive);

public record DeckDetailDto(string Id, string Name, string? Description, int CardCount, int DueCount, List<DeckCardDto> Cards, bool IsActive);

public record SetDeckActiveRequest(bool IsActive);

public record DeckCardDto(
    string Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State, DateTimeOffset? DueAt,
    double? Stability = null, double? Difficulty = null,
    int? Step = null, DateTimeOffset? LastReviewedAt = null,
    string? FrontSvg = null, string? BackSvg = null);

public record AddCardsToDeckRequest(List<string> CardIds);
