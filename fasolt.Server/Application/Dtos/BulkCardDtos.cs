using System.ComponentModel;

namespace Fasolt.Server.Application.Dtos;

public record BulkCreateCardsRequest(string? SourceFile, string? DeckId, List<BulkCardItem> Cards);
public record BulkCardItem(
    [property: Description("Front of the card (question/prompt). Markdown — see server instructions.")]
    string Front,
    [property: Description("Back of the card (answer/explanation). Markdown — see server instructions.")]
    string Back,
    string? SourceFile = null,
    string? SourceHeading = null,
    [property: Description("Optional inline SVG for the front. See server instructions for sanitization rules and viewBox guidance.")]
    string? FrontSvg = null,
    [property: Description("Optional inline SVG for the back. Same rules as frontSvg.")]
    string? BackSvg = null);
public record BulkCreateCardsResponse(List<CardDto> Created, List<SkippedCardDto> Skipped);
public record SkippedCardDto(string Front, string Reason);
