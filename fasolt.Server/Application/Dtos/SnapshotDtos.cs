namespace Fasolt.Server.Application.Dtos;

// Data stored inside the jsonb blob
public record SnapshotData(string DeckName, string? DeckDescription, List<SnapshotCardData> Cards);

public record SnapshotCardData(
    Guid CardId,
    string PublicId,
    string Front,
    string Back,
    string? FrontSvg,
    string? BackSvg,
    string? SourceFile,
    string? SourceHeading,
    DateTimeOffset CreatedAt,
    double? Stability,
    double? Difficulty,
    int? Step,
    DateTimeOffset? DueAt,
    string State,
    DateTimeOffset? LastReviewedAt,
    bool IsSuspended);

// API response for listing
public record SnapshotListDto(string Id, string? DeckName, int CardCount, DateTimeOffset CreatedAt);

// API response for diff
public record SnapshotDiffDto(
    List<DiffDeletedCard> Deleted,
    List<DiffModifiedCard> Modified,
    List<DiffAddedCard> Added);

public record DiffDeletedCard(
    Guid CardId, string Front, string Back, string? SourceFile,
    double? Stability, DateTimeOffset? DueAt, bool StillExists);

public record DiffModifiedCard(
    Guid CardId,
    string Front, string CurrentFront,
    string Back, string CurrentBack,
    double? SnapshotStability, double? CurrentStability,
    bool HasContentChanges, bool HasFsrsChanges);

public record DiffAddedCard(Guid CardId, string Front, string Back);

// Restore request
public record RestoreRequest(List<Guid> RestoreDeletedCardIds, List<Guid> RevertModifiedCardIds);

// Create response
public record SnapshotCreateResult(int Count);
