using System.Collections.Generic;

namespace SpacedMd.Server.Services
{
    public class MarkdownService : IMarkdownService
    {
        public record MdHeading(string Heading, int Level, int LineNumber);

        public List<MdHeading> GetHeadings(string markdown)
        {
            var headings = new List<MdHeading>();
            var lines = markdown.Split(new[] { "\r\n", "\n", "\r" }, System.StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("#"))
                {
                    int level = 0;
                    foreach (char c in trimmed)
                    {
                        if (c == '#')
                            level++;
                        else
                            break;
                    }
                    var headingText = trimmed.Trim();
                    headings.Add(new MdHeading(headingText, level, i + 1));
                }
            }
            return headings;
        }

        public string GetSection(string markdown, string heading)
        {
            var lines = markdown.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();
            bool inSection = false;
            int headingLevel = 0;

            foreach (var line in lines)
            {
                if (line.StartsWith("#"))
                {
                    int currentLevel = line.TakeWhile(c => c == '#').Count();
                    if (line.Trim() == heading)
                    {
                        inSection = true;
                        headingLevel = currentLevel;
                    }
                    else if (inSection && currentLevel <= headingLevel)
                    {
                        break;
                    }
                }

                if (inSection)
                {
                    result.Add(line);
                }
            }

            return string.Join("\n", result);
        }
    }
}