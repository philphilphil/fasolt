namespace Fasolt.Server.Application.Dtos;

/// <param name="TotalDecks">Own decks plus linked ones.</param>
/// <param name="LinkedDecks">
/// How many of <paramref name="TotalDecks"/> are linked from another account, and
/// therefore read-only. Per-deck detail (isLinked, authorHandle) is in the deck list.
/// </param>
public record OverviewDto(
    int TotalCards,
    int DueCards,
    Dictionary<string, int> CardsByState,
    int TotalDecks,
    int LinkedDecks,
    int TotalSources);

public record OverviewIdentityDto(
    string Email,
    string? DisplayName,
    string? ExternalProvider);
