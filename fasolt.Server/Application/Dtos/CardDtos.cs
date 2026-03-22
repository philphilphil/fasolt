namespace Fasolt.Server.Application.Dtos;

public record CreateCardRequest(string? SourceFile, string? SourceHeading, string Front, string Back);
public record UpdateCardRequest(string Front, string Back);
public record CardDto(
    Guid Id, string? SourceFile, string? SourceHeading,
    string Front, string Back, string State,
    DateTimeOffset CreatedAt, List<CardDeckInfoDto> Decks);
public record CardDeckInfoDto(Guid Id, string Name);

public record UpdateCardFieldsRequest(
    string? NewFront = null,
    string? NewBack = null,
    string? NewSourceFile = null,
    string? NewSourceHeading = null);

public enum UpdateCardStatus { Success, NotFound, Collision }

public record UpdateCardResult(UpdateCardStatus Status, CardDto? Card = null)
{
    public static UpdateCardResult Success(CardDto card) => new(UpdateCardStatus.Success, card);
    public static UpdateCardResult NotFound() => new(UpdateCardStatus.NotFound);
    public static UpdateCardResult Collision() => new(UpdateCardStatus.Collision);
}
