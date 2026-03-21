using SpacedMd.Server.Domain.Entities;

namespace SpacedMd.Server.Application.Services;

public record UpdatedCardInfo(Guid CardId, string Front, string OldBack, string NewBack);
public record OrphanedCardInfo(Guid CardId, string Front, string? SourceHeading);
public record NewSectionInfo(string Heading, bool HasMarkers);

public record FileComparisonResult(
    List<UpdatedCardInfo> UpdatedCards,
    List<OrphanedCardInfo> OrphanedCards,
    List<Guid> UnchangedCardIds,
    List<NewSectionInfo> NewSections);

public static class FileComparer
{
    public static FileComparisonResult Compare(
        string newContent, List<Card> existingCards)
    {
        var updated = new List<UpdatedCardInfo>();
        var orphaned = new List<OrphanedCardInfo>();
        var unchanged = new List<Guid>();

        var newStripped = ContentExtractor.StripFrontmatter(newContent);

        foreach (var card in existingCards)
        {
            if (card.CardType == "file")
            {
                var (_, cleanedNew) = ContentExtractor.ParseMarkers(newStripped);
                if (card.Back == cleanedNew)
                    unchanged.Add(card.Id);
                else
                    updated.Add(new UpdatedCardInfo(card.Id, card.Front, card.Back, cleanedNew));
            }
            else if (card.CardType == "section" && card.SourceHeading is not null)
            {
                var section = ContentExtractor.ExtractSection(newContent, card.SourceHeading);
                if (section is null)
                {
                    orphaned.Add(new OrphanedCardInfo(card.Id, card.Front, card.SourceHeading));
                }
                else
                {
                    var (_, cleanedSection) = ContentExtractor.ParseMarkers(section);
                    if (card.Back == cleanedSection)
                        unchanged.Add(card.Id);
                    else
                        updated.Add(new UpdatedCardInfo(card.Id, card.Front, card.Back, cleanedSection));
                }
            }
            else
            {
                unchanged.Add(card.Id);
            }
        }

        var existingHeadings = existingCards
            .Where(c => c.CardType == "section" && c.SourceHeading is not null)
            .Select(c => c.SourceHeading!)
            .ToHashSet();

        var newSections = new List<NewSectionInfo>();
        var allNewHeadings = HeadingExtractor.Extract(newContent);
        foreach (var (_, text, _) in allNewHeadings)
        {
            if (!existingHeadings.Contains(text))
            {
                var section = ContentExtractor.ExtractSection(newContent, text);
                var hasMarkers = false;
                if (section is not null)
                {
                    var (markers, _) = ContentExtractor.ParseMarkers(section);
                    hasMarkers = markers.Count > 0;
                }
                newSections.Add(new NewSectionInfo(text, hasMarkers));
            }
        }

        return new FileComparisonResult(updated, orphaned, unchanged, newSections);
    }
}
