namespace SpacedMd.Server.Application.Dtos;

public record FileUpdatePreviewDto(
    Guid FileId,
    string FileName,
    List<UpdatedCardPreviewDto> UpdatedCards,
    List<OrphanedCardPreviewDto> OrphanedCards,
    int UnchangedCount,
    List<NewSectionPreviewDto> NewSections);

public record UpdatedCardPreviewDto(Guid CardId, string Front, string OldBack, string NewBack);
public record OrphanedCardPreviewDto(Guid CardId, string Front, string? SourceHeading);
public record NewSectionPreviewDto(string Heading, bool HasMarkers);

public record FileUpdateResultDto(int UpdatedCount, int DeletedCount, int OrphanedCount);
