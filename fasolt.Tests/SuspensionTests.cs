using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class SuspensionTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private ReviewService CreateReviewService(Server.Infrastructure.Data.AppDbContext db)
        => new(db, _time);

    // --- GetDueCards excludes suspended cards ---

    [Fact]
    public async Task GetDueCards_ExcludesSuspendedCards()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var reviewSvc = CreateReviewService(db);

        var card1 = await cardSvc.CreateCard(UserId, "Q1", "A1", null, null);
        var card2 = await cardSvc.CreateCard(UserId, "Q2", "A2", null, null);

        // Both should be due (new cards)
        var due = await reviewSvc.GetDueCards(UserId);
        due.Select(c => c.Id).Should().Contain(card1.Id).And.Contain(card2.Id);

        // Suspend card1
        await cardSvc.SetSuspended(UserId, card1.Id, true);

        due = await reviewSvc.GetDueCards(UserId);
        due.Select(c => c.Id).Should().NotContain(card1.Id, "suspended card should not appear in due cards");
        due.Select(c => c.Id).Should().Contain(card2.Id, "unsuspended card should still appear");
    }

    [Fact]
    public async Task GetDueCards_UnsuspendedCardReappears()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var reviewSvc = CreateReviewService(db);

        var card = await cardSvc.CreateCard(UserId, "Q", "A", null, null);

        await cardSvc.SetSuspended(UserId, card.Id, true);
        var due = await reviewSvc.GetDueCards(UserId);
        due.Should().NotContain(c => c.Id == card.Id);

        await cardSvc.SetSuspended(UserId, card.Id, false);
        due = await reviewSvc.GetDueCards(UserId);
        due.Should().Contain(c => c.Id == card.Id, "unsuspended card should be due again");
    }

    // --- GetStats excludes suspended cards ---

    [Fact]
    public async Task GetStats_ExcludesSuspendedCardsFromDueCount()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var reviewSvc = CreateReviewService(db);

        await cardSvc.CreateCard(UserId, "Q1", "A1", null, null);
        var card2 = await cardSvc.CreateCard(UserId, "Q2", "A2", null, null);

        var stats = await reviewSvc.GetStats(UserId);
        stats.DueCount.Should().Be(2);
        stats.TotalCards.Should().Be(2);

        // Suspend one card — due count should drop but it's still counted
        // in total since GetStats uses OverviewService-style active filter
        await cardSvc.SetSuspended(UserId, card2.Id, true);

        stats = await reviewSvc.GetStats(UserId);
        stats.DueCount.Should().Be(1, "suspended card should not count as due");
    }

    // --- Deck due counts exclude suspended cards ---

    [Fact]
    public async Task ListDecks_DueCountExcludesSuspendedCards()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Test Deck", null);
        var card1 = await cardSvc.CreateCard(UserId, "Q1", "A1", null, null);
        var card2 = await cardSvc.CreateCard(UserId, "Q2", "A2", null, null);
        await deckSvc.AddCards(UserId, deck.Id, [card1.Id, card2.Id]);

        // Both new cards are due
        var decks = await deckSvc.ListDecks(UserId);
        var testDeck = decks.Single(d => d.Id == deck.Id);
        testDeck.DueCount.Should().Be(2);
        testDeck.CardCount.Should().Be(2);

        // Suspend one card
        await cardSvc.SetSuspended(UserId, card1.Id, true);

        decks = await deckSvc.ListDecks(UserId);
        testDeck = decks.Single(d => d.Id == deck.Id);
        testDeck.DueCount.Should().Be(1, "suspended card should not count as due in deck listing");
        testDeck.CardCount.Should().Be(2, "card count includes all cards regardless of suspension");
    }

    [Fact]
    public async Task GetDeckDetail_DueCountExcludesSuspendedCards()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Detail Deck", null);
        var card1 = await cardSvc.CreateCard(UserId, "Q1", "A1", null, null);
        var card2 = await cardSvc.CreateCard(UserId, "Q2", "A2", null, null);
        await deckSvc.AddCards(UserId, deck.Id, [card1.Id, card2.Id]);

        await cardSvc.SetSuspended(UserId, card1.Id, true);

        var detail = await deckSvc.GetDeck(UserId, deck.Id);
        detail!.DueCount.Should().Be(1, "suspended card should not count as due in deck detail");
        detail.Cards.Should().HaveCount(2, "all cards should still appear in the card list");
    }

    // --- Deck-scoped review excludes suspended cards ---

    [Fact]
    public async Task GetDueCards_DeckScoped_ExcludesSuspendedCards()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);
        var reviewSvc = CreateReviewService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Study Deck", null);
        var card1 = await cardSvc.CreateCard(UserId, "Q1", "A1", null, null);
        var card2 = await cardSvc.CreateCard(UserId, "Q2", "A2", null, null);
        await deckSvc.AddCards(UserId, deck.Id, [card1.Id, card2.Id]);

        await cardSvc.SetSuspended(UserId, card1.Id, true);

        var due = await reviewSvc.GetDueCards(UserId, deckId: deck.Id);
        due!.Select(c => c.Id).Should().NotContain(card1.Id, "suspended card excluded from deck study");
        due.Select(c => c.Id).Should().Contain(card2.Id);
    }

    // --- Suspended deck excludes all its cards from review ---

    [Fact]
    public async Task GetDueCards_SuspendedDeckExcludesAllCards()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);
        var reviewSvc = CreateReviewService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Suspend Deck", null);
        var card = await cardSvc.CreateCard(UserId, "Q", "A", null, null);
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        // Card in active deck is due
        var due = await reviewSvc.GetDueCards(UserId);
        due.Should().Contain(c => c.Id == card.Id);

        // Suspend the deck
        await deckSvc.SetSuspended(UserId, deck.Id, true);

        due = await reviewSvc.GetDueCards(UserId);
        due.Should().NotContain(c => c.Id == card.Id, "cards in suspended deck should not be due");
    }
}
