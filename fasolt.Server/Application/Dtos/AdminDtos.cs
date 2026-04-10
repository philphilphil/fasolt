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
    bool EmailConfirmed);

public record AdminUserListResponse(
    List<AdminUserDto> Users,
    int TotalCount,
    int Page,
    int PageSize);
