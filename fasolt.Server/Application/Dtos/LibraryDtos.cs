namespace Fasolt.Server.Application.Dtos;

/// <summary>
/// A public deck as seen by anonymous visitors. Deliberately carries no
/// internal user id and no card <c>SourceFile</c> — nothing here may leak
/// anything about the author's vault.
/// </summary>
public record LibraryDeckDto(
    string Id,
    string Name,
    string? Description,
    string? AuthorHandle,
    int CardCount,
    int CopyCount,
    DateTimeOffset? PublishedAt);

public record LibraryListResponse(
    List<LibraryDeckDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public record LibrarySampleCardDto(string Front, string Back, string? FrontSvg, string? BackSvg);

public record LibraryDeckDetailDto(
    string Id,
    string Name,
    string? Description,
    string? AuthorHandle,
    int CardCount,
    int CopyCount,
    string Visibility,
    DateTimeOffset? PublishedAt,
    List<LibrarySampleCardDto> SampleCards);

public record SetDeckVisibilityRequest(string Visibility);

public record SetHandleRequest(string Handle);

public record HandleResponse(string? Handle, bool CanPublish);

public record SetUserCanPublishRequest(bool CanPublish);
