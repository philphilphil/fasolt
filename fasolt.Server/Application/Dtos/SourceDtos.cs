namespace Fasolt.Server.Application.Dtos;

public record SourceListResponse(List<SourceItemDto> Items);
public record SourceItemDto(string SourceFile, int CardCount, int DueCount);
