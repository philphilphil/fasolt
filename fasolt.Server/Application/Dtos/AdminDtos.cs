namespace Fasolt.Server.Application.Dtos;

public record AdminUserDto(
    string Id,
    string Email,
    string? DisplayName,
    int CardCount,
    int DeckCount,
    bool IsLockedOut);

public record AdminUserListResponse(
    List<AdminUserDto> Users,
    int TotalCount,
    int Page,
    int PageSize);
