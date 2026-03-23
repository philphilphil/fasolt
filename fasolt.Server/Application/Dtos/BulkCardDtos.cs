namespace Fasolt.Server.Application.Dtos;

public record BulkCreateCardsRequest(string? SourceFile, string? DeckId, List<BulkCardItem> Cards);
public record BulkCardItem(string Front, string Back, string? SourceFile = null, string? SourceHeading = null);
public record BulkCreateCardsResponse(List<CardDto> Created, List<SkippedCardDto> Skipped);
public record SkippedCardDto(string Front, string Reason);
