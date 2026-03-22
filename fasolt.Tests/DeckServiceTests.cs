using FluentAssertions;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class DeckServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task CreateDeck_ReturnsDeck()
    {
        await using var db = _db.CreateDbContext();
        var svc = new DeckService(db);

        var deck = await svc.CreateDeck(UserId, "My Test Deck", "A deck for testing");

        deck.Name.Should().Be("My Test Deck");
        deck.Description.Should().Be("A deck for testing");
        deck.CardCount.Should().Be(0);
    }

    [Fact]
    public async Task ListDecks_ReturnsDecks()
    {
        await using var db = _db.CreateDbContext();
        var svc = new DeckService(db);

        await svc.CreateDeck(UserId, "Deck Alpha", null);
        await svc.CreateDeck(UserId, "Deck Beta", null);

        var decks = await svc.ListDecks(UserId);

        decks.Count.Should().BeGreaterThanOrEqualTo(2);
        decks.Should().Contain(d => d.Name == "Deck Alpha");
        decks.Should().Contain(d => d.Name == "Deck Beta");
    }

    [Fact]
    public async Task GetDeckDetail_IncludesCardSourceFile()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Detail Deck", null);
        var card = await cardSvc.CreateCard(UserId, "Card Q?", "Card A.", "source.md", "## Section");
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        var detail = await deckSvc.GetDeck(UserId, deck.Id);

        detail.Should().NotBeNull();
        detail!.Cards.Should().HaveCount(1);
        detail.Cards[0].SourceFile.Should().Be("source.md");
        detail.Cards[0].SourceHeading.Should().Be("## Section");
    }

    [Fact]
    public async Task DeleteDeck_WithDeleteCards_RemovesCards()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Delete Me", null);
        var card = await cardSvc.CreateCard(UserId, "Gone Q?", "Gone A.", null, null);
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        var result = await deckSvc.DeleteDeck(UserId, deck.Id, deleteCards: true);

        result.Deleted.Should().BeTrue();
        result.DeletedCardCount.Should().Be(1);

        var fetched = await cardSvc.GetCard(UserId, card.Id);
        fetched.Should().BeNull("card should be hard-deleted with the deck");
    }

    [Fact]
    public async Task DeleteDeck_WithoutDeleteCards_KeepsCards()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Delete Deck Only", null);
        var card = await cardSvc.CreateCard(UserId, "Keep Q?", "Keep A.", null, null);
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        var result = await deckSvc.DeleteDeck(UserId, deck.Id, deleteCards: false);

        result.Deleted.Should().BeTrue();
        result.DeletedCardCount.Should().Be(0);

        var fetched = await cardSvc.GetCard(UserId, card.Id);
        fetched.Should().NotBeNull("card should still exist when deck is deleted without deleteCards");
    }
}
