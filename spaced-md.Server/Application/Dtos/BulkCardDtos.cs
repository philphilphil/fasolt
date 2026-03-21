namespace SpacedMd.Server.Application.Dtos;

public record BulkCreateCardsRequest(
    Guid? FileId,
    Guid? DeckId,
    List<BulkCardItem> Cards);

public record BulkCardItem(
    string Front,
    string Back,
    string? SourceHeading);

public record BulkCreateCardsResponse(
    List<CardDto> Created,
    List<SkippedCardDto> Skipped);

public record SkippedCardDto(
    string Front,
    string Reason);
