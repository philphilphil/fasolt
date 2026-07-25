namespace Fasolt.Server.Application.Dtos;

public record CreateDeckRequest(string Name, string? Description);

public record UpdateDeckRequest(string Name, string? Description);

/// <summary>
/// A deck in the caller's deck list. For a linked deck (<paramref name="IsLinked"/>)
/// the deck itself belongs to <paramref name="AuthorHandle"/>: the counts are the
/// caller's own, <paramref name="IsSuspended"/> is the caller's pause of the link,
/// and the content is read-only.
/// </summary>
public record DeckDto(
    string Id, string Name, string? Description, int CardCount, int DueCount,
    DateTimeOffset CreatedAt, bool IsSuspended,
    string Visibility = "private", DateTimeOffset? PublishedAt = null, int CopyCount = 0,
    string? CopiedFromDeckPublicId = null, string? CopiedFromHandle = null,
    bool IsLinked = false, string? AuthorHandle = null);

public record DeckDetailDto(
    string Id, string Name, string? Description, int CardCount, int DueCount,
    List<DeckCardDto> Cards, bool IsSuspended,
    string Visibility = "private", DateTimeOffset? PublishedAt = null, int CopyCount = 0,
    string? CopiedFromDeckPublicId = null, string? CopiedFromHandle = null,
    bool IsLinked = false, string? AuthorHandle = null);

public record SetDeckSuspendedRequest(bool IsSuspended);

public record DeckCardDto(
    string Id, string Front, string Back,
    string? SourceFile,
    string State, DateTimeOffset? DueAt,
    bool IsSuspended = false,
    double? Stability = null, double? Difficulty = null,
    int? Step = null, DateTimeOffset? LastReviewedAt = null,
    string? FrontSvg = null, string? BackSvg = null);

public record AddCardsToDeckRequest(List<string> CardIds);
public record RemoveCardsFromDeckRequest(List<string> CardIds);
public record RemoveCardsFromDeckResponse(int Removed);
