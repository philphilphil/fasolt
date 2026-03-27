namespace Fasolt.Server.Application.Dtos;

public record LogEntryDto(int Id, string Type, string Message, string? Detail, bool Success, DateTimeOffset CreatedAt);

public record LogListResponse(List<LogEntryDto> Logs, int TotalCount, int Page, int PageSize);

public record PushResult(string Message, bool TokenValid);
