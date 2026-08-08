using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Api.McpTools;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

/// <summary>
/// The published-deck card cap seen through MCP. The REST endpoints report it as
/// <c>{ error: "deck_full", … }</c>; an agent hitting the same wall has to get the
/// same code, or it can only guess at the condition from prose.
/// </summary>
public class McpDeckCapToolsTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private CardTools Cards(AppDbContext db) =>
        new(new CardService(db), new SearchService(db), McpTestContext.For(UserId));

    private DeckTools Decks(AppDbContext db) =>
        new(new DeckService(db), McpTestContext.For(UserId));

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    /// <summary>A published deck holding exactly as many cards as the cap allows.</summary>
    private async Task<Deck> SeedFullPublishedDeck(AppDbContext db)
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = UserId,
            Name = "Full Deck",
            CreatedAt = DateTimeOffset.UtcNow,
            Visibility = DeckVisibility.Public,
            PublishedAt = DateTimeOffset.UtcNow,
        };
        db.Decks.Add(deck);

        for (var i = 0; i < PublishingService.MaxCardsInPublicDeck; i++)
        {
            var card = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                UserId = UserId,
                Front = $"Q{i}",
                Back = $"A{i}",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Cards.Add(card);
            db.DeckCards.Add(new DeckCard { DeckId = deck.Id, CardId = card.Id });
        }

        await db.SaveChangesAsync();
        return deck;
    }

    [Fact]
    public async Task CreateCards_IntoAFullPublishedDeck_ReportsStructuredDeckFull()
    {
        await using var db = _db.CreateDbContext();
        var deck = await SeedFullPublishedDeck(db);

        var root = Json(await Cards(db).CreateCards(
            [new BulkCardItem("One more", "Nope")], deckId: deck.PublicId));

        root.GetProperty("error").GetString().Should().Be("deck_full");
        root.GetProperty("message").GetString()
            .Should().Contain(PublishingService.MaxCardsInPublicDeck.ToString());

        await using var verify = _db.CreateDbContext();
        (await verify.DeckCards.CountAsync(dc => dc.DeckId == deck.Id))
            .Should().Be(PublishingService.MaxCardsInPublicDeck);
    }

    [Fact]
    public async Task AssignCardsToDeck_IntoAFullPublishedDeck_ReportsStructuredDeckFull()
    {
        await using var db = _db.CreateDbContext();
        var deck = await SeedFullPublishedDeck(db);
        var loose = await new CardService(db).CreateCard(UserId, "Loose", "A", null);

        var root = Json(await Decks(db).AssignCardsToDeck(deck.PublicId, [loose.Id]));

        root.GetProperty("error").GetString().Should().Be("deck_full");
        root.GetProperty("message").GetString()
            .Should().Contain(PublishingService.MaxCardsInPublicDeck.ToString());

        await using var verify = _db.CreateDbContext();
        (await verify.DeckCards.CountAsync(dc => dc.DeckId == deck.Id))
            .Should().Be(PublishingService.MaxCardsInPublicDeck);
    }
}
