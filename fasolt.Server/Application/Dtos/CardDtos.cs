namespace Fasolt.Server.Application.Dtos;

public record CreateCardRequest(string? SourceFile, string? SourceHeading, string Front, string Back);
public record UpdateCardRequest(string Front, string Back);
public record CardDto(
    Guid Id, string? SourceFile, string? SourceHeading,
    string Front, string Back, string State,
    DateTimeOffset CreatedAt, List<CardDeckInfoDto> Decks);
public record CardDeckInfoDto(Guid Id, string Name);
