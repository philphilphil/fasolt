namespace SpacedMd.Server.Domain.Entities;

public class MarkdownFile
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public AppUser User { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string Content { get; set; } = default!;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public List<FileHeading> Headings { get; set; } = [];
}
