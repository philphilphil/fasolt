namespace Fasolt.Server.Application.Dtos;

public record SearchResponse(
    List<CardSearchResult> Cards,
    List<DeckSearchResult> Decks);

public record CardSearchResult(string Id, string Headline, string State);
public record DeckSearchResult(string Id, string Headline, int CardCount);
