namespace SpacedMd.Server.Domain.Entities;

public class FileHeading
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public MarkdownFile File { get; set; } = default!;
    public int Level { get; set; }
    public string Text { get; set; } = default!;
    public int SortOrder { get; set; }
}
