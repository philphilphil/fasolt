using System.Text.RegularExpressions;

namespace SpacedMd.Server.Application.Services;

public static partial class HeadingExtractor
{
    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingPattern();

    public static List<(int Level, string Text, int SortOrder)> Extract(string markdown)
    {
        var headings = new List<(int Level, string Text, int SortOrder)>();
        var inCodeFence = false;
        var sortOrder = 0;

        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');

            if (trimmed.StartsWith("```"))
            {
                inCodeFence = !inCodeFence;
                continue;
            }

            if (inCodeFence) continue;

            var match = HeadingPattern().Match(trimmed);
            if (match.Success)
            {
                headings.Add((match.Groups[1].Value.Length, match.Groups[2].Value.Trim(), sortOrder++));
            }
        }

        return headings;
    }
}
