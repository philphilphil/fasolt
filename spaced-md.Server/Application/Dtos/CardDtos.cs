namespace SpacedMd.Server.Application.Dtos;

public record CreateCardRequest(Guid? FileId, string? SourceHeading, string Front, string Back, string CardType);

public record UpdateCardRequest(string Front, string Back);

public record CardDto(
    Guid Id,
    Guid? FileId,
    string? SourceHeading,
    string Front,
    string Back,
    string CardType,
    DateTimeOffset CreatedAt,
    List<CardDeckInfoDto> Decks);

public record CardDeckInfoDto(Guid Id, string Name);

public record ExtractedContentDto(List<string> Fronts, string Back);
