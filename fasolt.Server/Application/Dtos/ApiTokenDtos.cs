namespace Fasolt.Server.Application.Dtos;

public record CreateTokenRequest(string Name, DateTimeOffset? ExpiresAt);

public record CreateTokenResponse(Guid Id, string Name, string Token, DateTimeOffset CreatedAt, DateTimeOffset? ExpiresAt);

public record TokenListItemDto(
    Guid Id,
    string Name,
    string TokenPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? ExpiresAt,
    bool IsExpired,
    bool IsRevoked);
