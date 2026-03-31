namespace Fasolt.Server.Application.Dtos;

public record AccountExport(
    DateTimeOffset ExportedAt,
    AccountExportProfile Account,
    List<AccountExportDeck> Decks,
    List<AccountExportCard> Cards,
    List<string> Sources,
    List<AccountExportSnapshot> Snapshots,
    List<AccountExportConsentGrant> ConsentGrants,
    AccountExportDeviceToken? DeviceToken
);

public record AccountExportProfile(
    string Email,
    bool EmailConfirmed,
    string? ExternalProvider,
    double? DesiredRetention,
    int? MaximumInterval,
    int NotificationIntervalHours
);

public record AccountExportDeck(
    string Name,
    string? Description,
    bool IsSuspended,
    DateTimeOffset CreatedAt,
    List<string> Cards
);

public record AccountExportCard(
    string PublicId,
    string Front,
    string Back,
    string? FrontSvg,
    string? BackSvg,
    string? SourceFile,
    string? SourceHeading,
    string State,
    double? Stability,
    double? Difficulty,
    int? Step,
    DateTimeOffset? DueAt,
    DateTimeOffset? LastReviewedAt,
    bool IsSuspended,
    DateTimeOffset CreatedAt
);

public record AccountExportSnapshot(
    string? DeckName,
    int Version,
    int CardCount,
    string Data,
    DateTimeOffset CreatedAt
);

public record AccountExportConsentGrant(string ClientId, DateTimeOffset GrantedAt);

public record AccountExportDeviceToken(string Token, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public record DeleteAccountRequest(string? Password, string? ConfirmEmail);
