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
    DateTimeOffset CreatedAt);

public record ExtractedContentDto(List<string> Fronts, string Back);
