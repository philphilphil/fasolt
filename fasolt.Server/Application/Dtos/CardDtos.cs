using System.ComponentModel;

namespace Fasolt.Server.Application.Dtos;

public record CreateCardRequest(string? SourceFile, string? SourceHeading, string Front, string Back, string? FrontSvg = null, string? BackSvg = null, string? DeckId = null);
public record UpdateCardRequest(
    string Front,
    string Back,
    string? FrontSvg = null,
    string? BackSvg = null,
    string? SourceFile = null,
    string? SourceHeading = null,
    List<string>? DeckIds = null);
public record CardDto(
    string Id, string? SourceFile, string? SourceHeading,
    string Front, string Back, string State,
    DateTimeOffset CreatedAt, List<CardDeckInfoDto> Decks,
    bool IsSuspended = false,
    DateTimeOffset? DueAt = null, double? Stability = null,
    double? Difficulty = null, int? Step = null,
    DateTimeOffset? LastReviewedAt = null,
    string? FrontSvg = null, string? BackSvg = null);
public record CardDeckInfoDto(string Id, string Name, bool IsSuspended);

public record SetCardSuspendedRequest(bool IsSuspended);

public record UpdateCardFieldsRequest(
    string? NewFront = null,
    string? NewBack = null,
    string? NewSourceFile = null,
    string? NewSourceHeading = null,
    string? NewFrontSvg = null,
    string? NewBackSvg = null);

public enum UpdateCardStatus { Success, NotFound, Collision }

public record UpdateCardResult(UpdateCardStatus Status, CardDto? Card = null)
{
    public static UpdateCardResult Success(CardDto card) => new(UpdateCardStatus.Success, card);
    public static UpdateCardResult NotFound() => new(UpdateCardStatus.NotFound);
    public static UpdateCardResult Collision() => new(UpdateCardStatus.Collision);
}

public record BulkUpdateCardItem(
    [property: Description("Card ID to update. Required. Use list_cards or search_cards to discover IDs.")]
    string CardId,
    [property: Description("New front of the card. Markdown — see server instructions.")]
    string? NewFront = null,
    [property: Description("New back of the card. Markdown — see server instructions.")]
    string? NewBack = null,
    string? NewSourceFile = null,
    string? NewSourceHeading = null,
    [property: Description("New inline SVG for the front. See server instructions.")]
    string? NewFrontSvg = null,
    [property: Description("New inline SVG for the back. Same rules as newFrontSvg.")]
    string? NewBackSvg = null);

public record BulkUpdateCardResult(string CardId, UpdateCardStatus Status, CardDto? Card = null);
