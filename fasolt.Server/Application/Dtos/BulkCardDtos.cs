using System.ComponentModel;

namespace Fasolt.Server.Application.Dtos;

public record BulkCreateCardsRequest(string? SourceFile, string? DeckId, List<BulkCardItem> Cards);
public record BulkCardItem(
    [property: Description("Front of the card (question/prompt). Rendered as Markdown — supported: headings (#, ##, ###), **bold**, *italic*, ~~strikethrough~~, `inline code`, fenced code blocks, bullet/numbered lists, > blockquotes, tables, [links](url), and auto-linked URLs. Soft newlines are preserved as line breaks. HTML is escaped (not rendered). LaTeX/math (e.g. $...$) is NOT rendered. Example: \"What is the **capital** of France?\"")]
    string Front,
    [property: Description("Back of the card (answer/explanation). Same Markdown features as `front`. Use line breaks and lists for readability. HTML and LaTeX are NOT rendered. Example: \"**Empiricism**: all knowledge stems from sense experience. *Tabula rasa* (Locke). Key figures: Locke, Berkeley, Hume.\"")]
    string Back,
    string? SourceFile = null,
    string? SourceHeading = null,
    [property: Description("Optional inline SVG for the front. Must start with `<svg`. Sanitized server-side: <style>, <script>, <foreignObject>, all event handlers (on*), the `style` attribute, `font-weight`, `font-style`, and external `href` values are stripped. For emphasis use `fill`, `stroke`, or `font-size` — bold/italic via CSS will not survive. Allowed tags include: svg, g, defs, path, circle, rect, line, polyline, polygon, ellipse, text, tspan, use, marker, symbol, linearGradient, radialGradient, stop, filter, feGaussianBlur, feOffset, feMerge, feMergeNode, clipPath, mask, pattern, title, desc. Use a landscape viewBox like '0 0 400 250'. Max ~1MB.")]
    string? FrontSvg = null,
    [property: Description("Optional inline SVG for the back. Same sanitization rules as `frontSvg`.")]
    string? BackSvg = null);
public record BulkCreateCardsResponse(List<CardDto> Created, List<SkippedCardDto> Skipped);
public record SkippedCardDto(string Front, string Reason);
