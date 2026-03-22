namespace Fasolt.Server.Application.Dtos;

public record CreateDeckRequest(string Name, string? Description);

public record UpdateDeckRequest(string Name, string? Description);

public record DeckDto(Guid Id, string Name, string? Description, int CardCount, int DueCount, DateTimeOffset CreatedAt);

public record DeckDetailDto(Guid Id, string Name, string? Description, int CardCount, int DueCount, List<DeckCardDto> Cards);

public record DeckCardDto(
    Guid Id, string Front, string Back,
    string? SourceFile, string? SourceHeading,
    string State, DateTimeOffset? DueAt);

public record AddCardsToDeckRequest(List<Guid> CardIds);
