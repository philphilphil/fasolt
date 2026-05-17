namespace Fasolt.Server.Application.Dtos;

public record AdminUserDto(
    string Id,
    string Email,
    string? DisplayName,
    string? ExternalProvider,
    int CardCount,
    int DeckCount,
    bool IsLockedOut,
    bool HasPush,
    bool EmailConfirmed,
    DateTimeOffset? LastActivityAt);

public record AdminUserListResponse(
    List<AdminUserDto> Users,
    int TotalCount,
    int Page,
    int PageSize);

public record AdminStatsDto(
    int TotalUsers,
    int LockedUsers,
    int UsersWithPush,
    int TotalCards,
    int TotalDecks,
    int DueCards,
    int RegistrationsLast7d,
    int RegistrationsLast30d);
