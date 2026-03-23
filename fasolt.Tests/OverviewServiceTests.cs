using FluentAssertions;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class OverviewServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GetOverview_ReturnsCorrectCounts()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        await cardSvc.CreateCard(UserId, "Q1", "A1", "file-a.md", null);
        await cardSvc.CreateCard(UserId, "Q2", "A2", "file-b.md", null);
        await deckSvc.CreateDeck(UserId, "Deck 1", null);

        var svc = new OverviewService(db);
        var overview = await svc.GetOverview(UserId);

        overview.TotalCards.Should().Be(2);
        overview.DueCards.Should().Be(2); // new cards have DueAt = null, which counts as due
        overview.CardsByState["new"].Should().Be(2);
        overview.CardsByState["learning"].Should().Be(0);
        overview.CardsByState["review"].Should().Be(0);
        overview.CardsByState["relearning"].Should().Be(0);
        overview.TotalDecks.Should().Be(1);
        overview.TotalSources.Should().Be(2);
    }

    [Fact]
    public async Task GetOverview_EmptyAccount()
    {
        await using var db = _db.CreateDbContext();
        var svc = new OverviewService(db);

        var overview = await svc.GetOverview(UserId);

        overview.TotalCards.Should().Be(0);
        overview.DueCards.Should().Be(0);
        overview.CardsByState["new"].Should().Be(0);
        overview.CardsByState["learning"].Should().Be(0);
        overview.CardsByState["review"].Should().Be(0);
        overview.CardsByState["relearning"].Should().Be(0);
        overview.TotalDecks.Should().Be(0);
        overview.TotalSources.Should().Be(0);
    }
}
