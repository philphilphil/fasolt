using FluentAssertions;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class SearchServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Search_RequiresMinLength()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SearchService(db);

        var result = await svc.Search(UserId, "a");

        result.Cards.Should().BeEmpty();
        result.Decks.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_FindsCards_ByFrontText()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var searchSvc = new SearchService(db);

        await cardSvc.CreateCard(UserId, "Photosynthesis question", "It makes food from light.", null, null);

        var result = await searchSvc.Search(UserId, "Photosynthesis");

        result.Cards.Should().Contain(c => c.Headline.Contains("Photosynthesis"));
    }

    [Fact]
    public async Task Search_FindsDecks_ByName()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var searchSvc = new SearchService(db);

        await deckSvc.CreateDeck(UserId, "Biology Fundamentals", "Core biology concepts");

        var result = await searchSvc.Search(UserId, "Biology");

        result.Decks.Should().Contain(d => d.Headline.Contains("Biology"));
    }

    [Fact]
    public async Task Search_ResponseHasNoFilesProperty()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SearchService(db);

        var result = await svc.Search(UserId, "x");

        // SearchResponse only has Cards and Decks — verify via reflection
        var props = result.GetType().GetProperties().Select(p => p.Name).ToList();
        props.Should().NotContain("Files");
    }
}
