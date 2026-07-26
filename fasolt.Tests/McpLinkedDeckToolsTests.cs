using System.Text.Json;
using FluentAssertions;
using Fasolt.Server.Api.McpTools;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;
using ModelContextProtocol.Protocol;

namespace Fasolt.Tests;

/// <summary>
/// How linked decks look and behave through MCP: the deck list and overview say
/// which decks are links to another account, and every tool that would write to one
/// refuses with the structured <c>linked_content</c> error the agent can explain.
/// </summary>
public class McpLinkedDeckToolsTests : IAsyncLifetime
{
    private readonly TestDb _db = new();

    /// <summary>The subscriber.</summary>
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task<(Deck Deck, Card Card)> SeedLinkedDeck(AppDbContext db, string handle = "the-author")
    {
        var authorId = await LinkedDeckTestData.AddUser(db, handle);
        var deck = await LinkedDeckTestData.AddDeck(db, authorId, name: "Author's Deck", cardCount: 0);
        var card = LinkedDeckTestData.AddCard(db, deck, "Author Q", "Author A", "vault/author.md");
        await db.SaveChangesAsync();
        await LinkedDeckTestData.Subscribe(db, UserId, deck);
        return (deck, card);
    }

    private CardTools Cards(AppDbContext db) =>
        new(new CardService(db), new SearchService(db), McpTestContext.For(UserId));

    private DeckTools Decks(AppDbContext db) =>
        new(new DeckService(db), McpTestContext.For(UserId));

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    // ---- additive fields on the read tools ---------------------------------

    [Fact]
    public async Task ListDecks_MarksLinkedDecksAndNamesTheirAuthor()
    {
        await using var db = _db.CreateDbContext();
        var (linked, _) = await SeedLinkedDeck(db);
        await new DeckService(db).CreateDeck(UserId, "My Own Deck", null);

        var decks = Json(await Decks(db).ListDecks()).EnumerateArray().ToList();

        var linkedJson = decks.Single(d => d.GetProperty("id").GetString() == linked.PublicId);
        linkedJson.GetProperty("isLinked").GetBoolean().Should().BeTrue();
        linkedJson.GetProperty("authorHandle").GetString().Should().Be("the-author");

        var ownJson = decks.Single(d => d.GetProperty("name").GetString() == "My Own Deck");
        ownJson.GetProperty("isLinked").GetBoolean().Should().BeFalse();
        // Nulls are dropped by the MCP serializer, so an own deck simply has no author.
        ownJson.TryGetProperty("authorHandle", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetOverview_CountsHowManyDecksAreLinks()
    {
        await using var db = _db.CreateDbContext();
        await SeedLinkedDeck(db);
        await new DeckService(db).CreateDeck(UserId, "My Own Deck", null);

        var tools = new OverviewTools(new OverviewService(db), McpTestContext.For(UserId));
        var root = Json(await tools.GetOverview());

        root.GetProperty("totalDecks").GetInt32().Should().Be(2);
        root.GetProperty("linkedDecks").GetInt32().Should().Be(1);
    }

    // ---- write refusals ----------------------------------------------------

    [Fact]
    public async Task UpdateCards_OnALinkedCardIsRefused()
    {
        await using var db = _db.CreateDbContext();
        var (_, card) = await SeedLinkedDeck(db);

        var act = async () => await Cards(db).UpdateCards(
            [new BulkUpdateCardItem(card.PublicId, NewFront: "Hijacked")]);

        await AssertLinkedContent(act, "update_cards");
    }

    [Fact]
    public async Task DeleteCards_OnALinkedCardIsRefused()
    {
        await using var db = _db.CreateDbContext();
        var (_, card) = await SeedLinkedDeck(db);

        var act = async () => await Cards(db).DeleteCards([card.PublicId]);

        await AssertLinkedContent(act, "delete_cards");
    }

    [Fact]
    public async Task AddSvgToCard_OnALinkedCardIsRefused()
    {
        await using var db = _db.CreateDbContext();
        var (_, card) = await SeedLinkedDeck(db);

        var act = async () => await Cards(db).AddSvgToCard(
            card.PublicId, "front", "<svg viewBox=\"0 0 400 250\"></svg>");

        await AssertLinkedContent(act, "add_svg_to_card");
    }

    [Fact]
    public async Task CreateCards_IntoALinkedDeckIsRefused()
    {
        await using var db = _db.CreateDbContext();
        var (deck, _) = await SeedLinkedDeck(db);

        var act = async () => await Cards(db).CreateCards(
            [new BulkCardItem("Mine", "A")], deckId: deck.PublicId);

        await AssertLinkedContent(act, "create_cards");
    }

    [Fact]
    public async Task UpdateDeck_OnALinkedDeckIsRefused()
    {
        await using var db = _db.CreateDbContext();
        var (deck, _) = await SeedLinkedDeck(db);

        var act = async () => await Decks(db).UpdateDeck(deck.PublicId, "Mine now");

        await AssertLinkedContent(act, "update_deck");
    }

    [Fact]
    public async Task DeleteDeck_OnALinkedDeckIsRefused()
    {
        await using var db = _db.CreateDbContext();
        var (deck, _) = await SeedLinkedDeck(db);

        var act = async () => await Decks(db).DeleteDeck(deck.PublicId);

        await AssertLinkedContent(act, "delete_deck");
    }

    [Fact]
    public async Task AssignCardsToDeck_TargetingALinkedDeckIsRefused()
    {
        await using var db = _db.CreateDbContext();
        var (deck, _) = await SeedLinkedDeck(db);
        var own = await new CardService(db).CreateCard(UserId, "Own", "A", null);

        var act = async () => await Decks(db).AssignCardsToDeck(deck.PublicId, [own.Id]);

        await AssertLinkedContent(act, "assign_cards_to_deck");
    }

    [Fact]
    public async Task AssignCardsToDeck_RemovingFromALinkedDeckIsRefused()
    {
        await using var db = _db.CreateDbContext();
        var (deck, card) = await SeedLinkedDeck(db);

        var act = async () => await Decks(db).AssignCardsToDeck(null, [card.PublicId], fromDeckId: deck.PublicId);

        await AssertLinkedContent(act, "assign_cards_to_deck");
    }

    [Fact]
    public async Task SetDeckSuspended_OnALinkedDeckIsAllowed_ItIsTheCallersOwnPause()
    {
        await using var db = _db.CreateDbContext();
        var (deck, _) = await SeedLinkedDeck(db);

        var root = Json(await Decks(db).SetDeckSuspended(deck.PublicId, true));

        root.GetProperty("isSuspended").GetBoolean().Should().BeTrue();
        root.GetProperty("isLinked").GetBoolean().Should().BeTrue();
    }

    /// <summary>
    /// Asserts the tool refuses, and that the refusal reaches the agent as the
    /// structured <c>linked_content</c> error rather than a generic internal error —
    /// the translation the MCP call filter applies to everything a tool throws.
    /// </summary>
    private static async Task AssertLinkedContent(Func<Task<string>> act, string toolName)
    {
        var thrown = await act.Should().ThrowAsync<LinkedContentException>();

        var result = McpErrorTranslator.ToErrorResult(thrown.Which, toolName);
        result.IsError.Should().BeTrue();

        var payload = Json(result.Content.OfType<TextContentBlock>().Single().Text);
        payload.GetProperty("error").GetString().Should().Be(LinkedContentException.ErrorCode);
        payload.GetProperty("message").GetString().Should().Contain("linked");
        payload.GetProperty("hint").GetString().Should().Contain("convert");
    }
}
