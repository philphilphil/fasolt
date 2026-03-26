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
    public async Task AddCards_Success()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Add Test", null);
        var card = await cardSvc.CreateCard(UserId, "AQ", "AA", null, null);

        var result = await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        result.Should().Be(AddCardsResult.Success);
        var detail = await deckSvc.GetDeck(UserId, deck.Id);
        detail!.Cards.Should().HaveCount(1);
    }

    [Fact]
    public async Task AddCards_DeckNotFound()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);

        var result = await deckSvc.AddCards(UserId, "nonexistent", ["card1"]);

        result.Should().Be(AddCardsResult.DeckNotFound);
    }

    [Fact]
    public async Task AddCards_CardsNotFound()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Add Missing", null);
        var result = await deckSvc.AddCards(UserId, deck.Id, ["nonexistent"]);

        result.Should().Be(AddCardsResult.CardsNotFound);
    }

    [Fact]
    public async Task AddCards_SkipsDuplicates()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Dup Add", null);
        var card = await cardSvc.CreateCard(UserId, "DupQ", "DupA", null, null);
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        // Add same card again
        var result = await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        result.Should().Be(AddCardsResult.Success);
        var detail = await deckSvc.GetDeck(UserId, deck.Id);
        detail!.Cards.Should().HaveCount(1, "duplicate add should be silently skipped");
    }

    [Fact]
    public async Task UpdateDeck_RenamesAndUpdatesDescription()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Old Name", "Old desc");
        var updated = await deckSvc.UpdateDeck(UserId, deck.Id, "New Name", "New desc");

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("New Name");
        updated.Description.Should().Be("New desc");
    }

    [Fact]
    public async Task UpdateDeck_NotFound_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);

        var result = await deckSvc.UpdateDeck(UserId, "nonexistent", "Name", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateDeck_ClearsDescription()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Desc Test", "Has description");
        var updated = await deckSvc.UpdateDeck(UserId, deck.Id, "Desc Test", null);

        updated!.Description.Should().BeNull();
    }

    [Fact]
    public async Task SetActive_DeactivatesDeck()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Active Test", null);
        deck.IsActive.Should().BeTrue("decks are active by default");

        var result = await deckSvc.SetActive(UserId, deck.Id, false);

        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SetActive_NotFound_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);

        var result = await deckSvc.SetActive(UserId, "nonexistent", true);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveCards_BulkRemovesFromDeck()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Bulk Remove Deck", null);
        var a = await cardSvc.CreateCard(UserId, "RA", "RA", null, null);
        var b = await cardSvc.CreateCard(UserId, "RB", "RB", null, null);
        var c = await cardSvc.CreateCard(UserId, "RC", "RC", null, null);
        await deckSvc.AddCards(UserId, deck.Id, [a.Id, b.Id, c.Id]);

        var result = await deckSvc.RemoveCards(UserId, deck.Id, [a.Id, b.Id]);

        result.DeckFound.Should().BeTrue();
        result.RemovedCount.Should().Be(2);

        // Card C should still be in the deck
        var detail = await deckSvc.GetDeck(UserId, deck.Id);
        detail!.Cards.Should().HaveCount(1);
        detail.Cards[0].Front.Should().Be("RC");

        // Removed cards should still exist
        (await cardSvc.GetCard(UserId, a.Id)).Should().NotBeNull();
        (await cardSvc.GetCard(UserId, b.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveCards_DeckNotFound_ReturnsFalse()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);

        var result = await deckSvc.RemoveCards(UserId, "nonexistent", ["card1"]);

        result.DeckFound.Should().BeFalse();
        result.RemovedCount.Should().Be(0);
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
