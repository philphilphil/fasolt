namespace SpacedMd.Server.Application.Dtos;

public record FileHeadingDto(int Level, string Text);

public record FileListItemDto(
    Guid Id,
    string FileName,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    int CardCount,
    List<FileHeadingDto> Headings);

public record FileDetailDto(
    Guid Id,
    string FileName,
    long SizeBytes,
    DateTimeOffset UploadedAt,
    int CardCount,
    string Content,
    List<FileHeadingDto> Headings);

public record BulkUploadResultDto(string FileName, bool Success, Guid? Id, string? Error);
