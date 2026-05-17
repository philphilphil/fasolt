namespace Fasolt.Server.Application.Dtos;

public record OverviewDto(
    int TotalCards,
    int DueCards,
    Dictionary<string, int> CardsByState,
    int TotalDecks,
    int TotalSources);

public record OverviewIdentityDto(
    string Email,
    string? DisplayName,
    string? ExternalProvider);
