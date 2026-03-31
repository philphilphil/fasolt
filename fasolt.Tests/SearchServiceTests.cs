using FluentAssertions;
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
        await cardSvc.CreateCard(UserId, "Unrelated card", "Nothing about plants.", null, null);

        var result = await searchSvc.Search(UserId, "Photosynthesis");

        result.Cards.Should().ContainSingle();
        result.Cards[0].Headline.Should().Contain("Photosynthesis");
    }

    [Fact]
    public async Task Search_FindsDecks_ByName()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var searchSvc = new SearchService(db);

        await deckSvc.CreateDeck(UserId, "Biology Fundamentals", "Core biology concepts");
        await deckSvc.CreateDeck(UserId, "Mathematics", "Numbers and proofs");

        var result = await searchSvc.Search(UserId, "Biology");

        result.Decks.Should().ContainSingle();
        result.Decks[0].Headline.Should().Contain("Biology");
    }

    [Fact]
    public async Task Search_EscapesPercentWildcard()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var searchSvc = new SearchService(db);

        await cardSvc.CreateCard(UserId, "100% correct", "Always right", null, null);
        await cardSvc.CreateCard(UserId, "Unrelated card", "Nothing here", null, null);

        var result = await searchSvc.Search(UserId, "100%");

        result.Cards.Should().ContainSingle(c => c.Headline.Contains("100%"));
    }

    [Fact]
    public async Task Search_EscapesUnderscoreWildcard()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var searchSvc = new SearchService(db);

        await cardSvc.CreateCard(UserId, "snake_case naming", "Use underscores", null, null);
        await cardSvc.CreateCard(UserId, "snakeXcase naming", "Not underscore", null, null);

        var result = await searchSvc.Search(UserId, "snake_case");

        result.Cards.Should().ContainSingle(c => c.Headline.Contains("snake_case"));
    }

    [Fact]
    public async Task Search_MultiTermQuery_RequiresAllTerms()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var searchSvc = new SearchService(db);

        await cardSvc.CreateCard(UserId, "Was ist der Unterschied von Apfel und der Birne", "Äpfel und Birnen sind Obstsorten.", null, null);
        await cardSvc.CreateCard(UserId, "Was ist ein Apfel", "Ein Apfel ist eine Frucht.", null, null);
        await cardSvc.CreateCard(UserId, "Unrelated card", "Nothing here.", null, null);

        var result = await searchSvc.Search(UserId, "apfel birne");

        result.Cards.Should().ContainSingle(c => c.Headline.Contains("Apfel"));
        result.Cards[0].Headline.Should().Contain("Birne");
    }

    [Fact]
    public async Task Search_MultiTermQuery_MatchesAcrossFrontAndBack()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var searchSvc = new SearchService(db);

        await cardSvc.CreateCard(UserId, "Photosynthesis", "Plants convert sunlight into energy.", null, null);
        await cardSvc.CreateCard(UserId, "Respiration", "Cells convert glucose into ATP.", null, null);

        // "photosynthesis" is in front, "sunlight" is in back — both must match
        var result = await searchSvc.Search(UserId, "photosynthesis sunlight");

        result.Cards.Should().ContainSingle(c => c.Headline.Contains("Photosynthesis"));
    }

}
