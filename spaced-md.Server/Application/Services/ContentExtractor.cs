using System.Text;
using System.Text.RegularExpressions;

namespace SpacedMd.Server.Application.Services;

public static partial class ContentExtractor
{
    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingPattern();

    public static string StripFrontmatter(string markdown)
    {
        if (!markdown.StartsWith("---\n") && !markdown.StartsWith("---\r\n"))
            return markdown;

        var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (end == -1) return markdown;

        var afterFrontmatter = markdown.IndexOf('\n', end + 4);
        if (afterFrontmatter == -1) return string.Empty;

        return markdown[(afterFrontmatter + 1)..];
    }

    public static string? ExtractSection(string markdown, string heading)
    {
        var lines = markdown.Split('\n');
        var inCodeFence = false;
        var foundHeading = false;
        var headingLevel = 0;
        var result = new StringBuilder();

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.StartsWith("```"))
            {
                inCodeFence = !inCodeFence;
                if (foundHeading) result.AppendLine(line);
                continue;
            }

            if (inCodeFence)
            {
                if (foundHeading) result.AppendLine(line);
                continue;
            }

            var match = HeadingPattern().Match(trimmed);
            if (match.Success)
            {
                var level = match.Groups[1].Value.Length;
                var text = match.Groups[2].Value.Trim();

                if (!foundHeading && text == heading)
                {
                    foundHeading = true;
                    headingLevel = level;
                    result.AppendLine(line);
                    continue;
                }

                if (foundHeading && level <= headingLevel)
                    break;
            }

            if (foundHeading) result.AppendLine(line);
        }

        return foundHeading ? result.ToString().TrimEnd() : null;
    }

    public static (List<string> Fronts, string CleanedContent) ParseMarkers(string content)
    {
        var fronts = new List<string>();
        var cleaned = new StringBuilder();

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r').Trim();
            if (trimmed.StartsWith("?::"))
            {
                var marker = trimmed[3..].Trim();
                if (!string.IsNullOrEmpty(marker))
                    fronts.Add(marker);
            }
            else
            {
                cleaned.AppendLine(line);
            }
        }

        return (fronts, cleaned.ToString().TrimEnd());
    }

    public static string? GetFirstH1(string markdown)
    {
        var lines = markdown.Split('\n');
        var inCodeFence = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.StartsWith("```"))
            {
                inCodeFence = !inCodeFence;
                continue;
            }

            if (inCodeFence) continue;

            var match = HeadingPattern().Match(trimmed);
            if (match.Success && match.Groups[1].Value.Length == 1)
                return match.Groups[2].Value.Trim();
        }

        return null;
    }
}
