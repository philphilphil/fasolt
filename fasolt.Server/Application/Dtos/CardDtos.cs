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
    [property: Description("Lookup key: card ID. Either provide this OR (sourceFile + front).")]
    string? CardId = null,
    [property: Description("Lookup key part 1: source file. Combine with `front` for case-insensitive natural-key lookup when `cardId` is unknown.")]
    string? SourceFile = null,
    [property: Description("Lookup key part 2: existing front text (case-insensitive). Combine with `sourceFile`.")]
    string? Front = null,
    [property: Description("New front of the card (question/prompt). Rendered as Markdown — supported: headings (#, ##, ###), **bold**, *italic*, ~~strikethrough~~, `inline code`, fenced code blocks, bullet/numbered lists, > blockquotes, tables, [links](url), and auto-linked URLs. Soft newlines are preserved as line breaks. HTML is escaped (not rendered). LaTeX/math (e.g. $...$) is NOT rendered. Example: \"What is the **capital** of France?\"")]
    string? NewFront = null,
    [property: Description("New back of the card (answer/explanation). Same Markdown features as `newFront`. HTML and LaTeX are NOT rendered. Example: \"**Empiricism**: all knowledge stems from sense experience. *Tabula rasa* (Locke). Key figures: Locke, Berkeley, Hume.\"")]
    string? NewBack = null,
    string? NewSourceFile = null,
    string? NewSourceHeading = null,
    [property: Description("New inline SVG for the front. Must start with `<svg`. Sanitized server-side: <style>, <script>, <foreignObject>, all event handlers (on*), the `style` attribute, `font-weight`, `font-style`, and external `href` values are stripped. For emphasis use `fill`, `stroke`, or `font-size` — bold/italic via CSS will not survive. Allowed tags include: svg, g, defs, path, circle, rect, line, polyline, polygon, ellipse, text, tspan, use, marker, symbol, linearGradient, radialGradient, stop, filter, feGaussianBlur, feOffset, feMerge, feMergeNode, clipPath, mask, pattern, title, desc. Use a landscape viewBox like '0 0 400 250'. Max ~1MB.")]
    string? NewFrontSvg = null,
    [property: Description("New inline SVG for the back. Same sanitization rules as `newFrontSvg`.")]
    string? NewBackSvg = null);

public record BulkUpdateCardResult(string? CardId, string? SourceFile, string? Front, UpdateCardStatus Status, CardDto? Card = null);
