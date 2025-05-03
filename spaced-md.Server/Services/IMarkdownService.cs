using System.Collections.Generic;
using static SpacedMd.Server.Services.MarkdownService;

namespace SpacedMd.Server.Services
{
    public interface IMarkdownService
    {
        List<MdHeading> GetHeadings(string markdown);

        string GetSection(string markdown, string heading);
    }
}