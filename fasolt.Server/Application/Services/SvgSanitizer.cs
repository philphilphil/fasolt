using System.Xml.Linq;

namespace Fasolt.Server.Application.Services;

public static class SvgSanitizer
{
    private static readonly int MaxSvgLength = 1_048_576; // ~1MB (char count, not bytes)

    private static readonly HashSet<string> AllowedElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "svg", "path", "circle", "rect", "line", "polyline", "polygon", "ellipse",
        "g", "defs", "use", "text", "tspan", "clipPath", "mask", "pattern",
        "linearGradient", "radialGradient", "stop", "filter",
        "feGaussianBlur", "feOffset", "feMerge", "feMergeNode",
        "title", "desc", "marker", "symbol",
    };

    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "viewBox", "width", "height", "fill", "stroke", "stroke-width", "d",
        "cx", "cy", "r", "rx", "ry", "x", "y", "x1", "y1", "x2", "y2",
        "points", "transform", "opacity", "font-size", "font-family",
        "text-anchor", "dominant-baseline", "class", "id", "xmlns",
        "preserveAspectRatio", "gradientUnits", "offset",
        "stop-color", "stop-opacity", "stroke-dasharray",
        "stroke-linecap", "stroke-linejoin", "fill-opacity", "stroke-opacity",
        "marker-start", "marker-mid", "marker-end", "fill-rule", "clip-rule",
        "dx", "dy", "textLength", "lengthAdjust",
    };

    /// <summary>
    /// Validates and sanitizes SVG content. Returns null if invalid.
    /// </summary>
    public static string? Sanitize(string? svg)
    {
        if (string.IsNullOrWhiteSpace(svg)) return null;

        var trimmed = svg.Trim();
        if (trimmed.Length > MaxSvgLength) return null;
        if (!trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)) return null;

        try
        {
            var doc = XDocument.Parse(trimmed);
            if (doc.Root is null) return null;

            SanitizeElement(doc.Root);
            return doc.Root.ToString();
        }
        catch
        {
            return null; // Invalid XML
        }
    }

    private static void SanitizeElement(XElement element)
    {
        var localName = element.Name.LocalName;

        // Remove disallowed elements
        var childrenToRemove = element.Elements()
            .Where(e => !AllowedElements.Contains(e.Name.LocalName))
            .ToList();
        foreach (var child in childrenToRemove)
            child.Remove();

        // Remove disallowed attributes (including all on* event handlers)
        var attrsToRemove = element.Attributes()
            .Where(a =>
            {
                var name = a.Name.LocalName;
                // Always strip event handlers
                if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase)) return true;
                // Always strip style attribute
                if (name.Equals("style", StringComparison.OrdinalIgnoreCase)) return true;
                // Strip href/xlink:href unless it's a fragment reference
                if (name.Equals("href", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.ToString().Contains("href", StringComparison.OrdinalIgnoreCase))
                {
                    return !a.Value.StartsWith('#');
                }
                // Allow namespace declarations
                if (a.IsNamespaceDeclaration) return false;
                // Check allowlist
                return !AllowedAttributes.Contains(name);
            })
            .ToList();
        foreach (var attr in attrsToRemove)
            attr.Remove();

        // Recurse into remaining children
        foreach (var child in element.Elements())
            SanitizeElement(child);
    }
}
