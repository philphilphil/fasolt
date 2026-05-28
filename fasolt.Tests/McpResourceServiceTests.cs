using FluentAssertions;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class McpResourceServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private McpResourceService CreateService(AppDbContext db) =>
        new McpResourceService(
            db,
            new ReviewService(db, TimeProvider.System, new StudyStatsService(db, TimeProvider.System)),
            TimeProvider.System);

    [Fact]
    public async Task ListUserResourcesAsync_NoDecks_ReturnsTwoStatics()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var entries = await svc.ListUserResourcesAsync(UserId);

        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.Uri == "fasolt://due-today");
        entries.Should().Contain(e => e.Uri == "fasolt://recent");
    }

    [Fact]
    public async Task RenderDeckAsync_BasicCard_FormatsFrontBackAndHeader()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "German Verbs", "Common verbs");
        var card = await cardSvc.CreateCard(UserId, "essen", "to eat", null);
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().NotBeNull();
        md.Should().Contain("# Deck: German Verbs");
        md.Should().Contain("1 card");
        md.Should().Contain("Common verbs");
        md.Should().Contain("**Front:** essen");
        md.Should().Contain("**Back:** to eat");
    }

    [Fact]
    public async Task RenderDeckAsync_EmptyDeck_RendersHeaderAndNoCardsLine()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var deck = await deckSvc.CreateDeck(UserId, "Empty Deck", null);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().Contain("# Deck: Empty Deck");
        md.Should().Contain("0 cards");
        md.Should().Contain("No cards.");
    }

    [Fact]
    public async Task RenderDeckAsync_UnknownDeck_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var md = await svc.RenderDeckAsync(UserId, "does-not-exist");

        md.Should().BeNull();
    }

    [Fact]
    public async Task RenderDeckAsync_OtherUsersDeck_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var deck = await deckSvc.CreateDeck(UserId, "Mine", null);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync("different-user-id", deck.Id);

        md.Should().BeNull();
    }

    [Fact]
    public async Task RenderDeckAsync_NoDescription_OmitsDescriptionLine()
    {
        await using var db = _db.CreateDbContext();
        var deckSvc = new DeckService(db);
        var deck = await deckSvc.CreateDeck(UserId, "NoDesc", null);

        var svc = CreateService(db);
        var md = await svc.RenderDeckAsync(UserId, deck.Id);

        md.Should().NotBeNull();
        md.Should().Contain("# Deck: NoDesc");
        // Header line followed directly by a card section / no cards, not by an empty description
        md.Should().NotContain("\n\n\n\n");
    }
}
