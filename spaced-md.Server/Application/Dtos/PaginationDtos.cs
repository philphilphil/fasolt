namespace SpacedMd.Server.Application.Dtos;

public record PaginatedResponse<T>(
    List<T> Items,
    bool HasMore,
    string? NextCursor);
