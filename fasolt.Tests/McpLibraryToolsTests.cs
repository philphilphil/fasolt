using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Api.McpTools;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

/// <summary>
/// The MCP sharing tools. They are a thin shell over the library, subscription and
/// publishing services, so what is under test here is the agent-facing contract:
/// which conditions come back as structured errors, which are idempotent successes,
/// and what the returned links point at.
/// </summary>
public class McpLibraryToolsTests : IAsyncLifetime
{
    private readonly TestDb _db = new();

    /// <summary>The caller. Deck authors are created per test.</summary>
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private LibraryTools Tools(AppDbContext db, string? userId = null) => new(
        new LibraryService(db),
        new DeckSubscriptionService(db),
        new PublishingService(db),
        McpTestContext.For(userId ?? UserId));

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    private static string? ErrorCode(string raw)
    {
        var root = Json(raw);
        return root.TryGetProperty("error", out var error) ? error.GetString() : null;
    }

    // ---- list_public_decks -------------------------------------------------

    [Fact]
    public async Task ListPublicDecks_ReturnsPublishedDecksWithTheirAuthorHandle()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "author-one");
        await LinkedDeckTestData.AddDeck(db, author, name: "Spanish Verbs", cardCount: 3);

        var root = Json(await Tools(db).ListPublicDecks());

        root.GetProperty("totalCount").GetInt32().Should().Be(1);
        var item = root.GetProperty("items")[0];
        item.GetProperty("name").GetString().Should().Be("Spanish Verbs");
        item.GetProperty("authorHandle").GetString().Should().Be("author-one");
        item.GetProperty("cardCount").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task ListPublicDecks_HidesPrivateAndUnlistedDecks()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "author-two");
        await LinkedDeckTestData.AddDeck(db, author, DeckVisibility.Private, name: "Private Deck");
        await LinkedDeckTestData.AddDeck(db, author, DeckVisibility.Unlisted, name: "Unlisted Deck");
        await LinkedDeckTestData.AddDeck(db, author, DeckVisibility.Public, name: "Public Deck");

        var root = Json(await Tools(db).ListPublicDecks());

        root.GetProperty("totalCount").GetInt32().Should().Be(1);
        root.GetProperty("items")[0].GetProperty("name").GetString().Should().Be("Public Deck");
    }

    [Fact]
    public async Task ListPublicDecks_FiltersByQueryAndPages()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "author-three");
        await LinkedDeckTestData.AddDeck(db, author, name: "Chemistry Basics");
        await LinkedDeckTestData.AddDeck(db, author, name: "Chemistry Advanced");
        await LinkedDeckTestData.AddDeck(db, author, name: "Latin Grammar");

        var filtered = Json(await Tools(db).ListPublicDecks(query: "chemistry"));
        filtered.GetProperty("totalCount").GetInt32().Should().Be(2);

        var paged = Json(await Tools(db).ListPublicDecks(query: "chemistry", pageSize: 1, page: 2));
        paged.GetProperty("items").GetArrayLength().Should().Be(1);
        paged.GetProperty("page").GetInt32().Should().Be(2);
        paged.GetProperty("pageSize").GetInt32().Should().Be(1);
        paged.GetProperty("totalCount").GetInt32().Should().Be(2);
    }

    // ---- import_deck: copy -------------------------------------------------

    [Fact]
    public async Task ImportDeck_Copy_ClonesTheCardsIntoTheCallersAccount()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "copy-author");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Copy Me", cardCount: 2);

        var root = Json(await Tools(db).ImportDeck(deck.PublicId, "copy"));

        root.GetProperty("mode").GetString().Should().Be("copy");
        var copy = root.GetProperty("deck");
        copy.GetProperty("name").GetString().Should().Be("Copy Me");
        copy.GetProperty("cardCount").GetInt32().Should().Be(2);
        copy.GetProperty("copiedFromHandle").GetString().Should().Be("copy-author");
        // A copy is owned outright, so it is never reported as linked.
        copy.GetProperty("isLinked").GetBoolean().Should().BeFalse();
        root.GetProperty("deckUrl").GetString()
            .Should().Be($"https://fasolt.app/decks/{copy.GetProperty("id").GetString()}");

        await using var verify = _db.CreateDbContext();
        (await verify.Cards.CountAsync(c => c.UserId == UserId)).Should().Be(2);
    }

    [Fact]
    public async Task ImportDeck_Copy_WorksForAnUnlistedDeckReachedByItsShareId()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "unlisted-author");
        var deck = await LinkedDeckTestData.AddDeck(db, author, DeckVisibility.Unlisted, cardCount: 1);

        var root = Json(await Tools(db).ImportDeck(deck.PublicId, "copy"));

        root.GetProperty("deck").GetProperty("cardCount").GetInt32().Should().Be(1);
    }

    // ---- import_deck: link -------------------------------------------------

    [Fact]
    public async Task ImportDeck_Link_SubscribesAndReportsTheAuthor()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "link-author");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Link Me", cardCount: 2);

        var root = Json(await Tools(db).ImportDeck(deck.PublicId, "link"));

        root.GetProperty("mode").GetString().Should().Be("link");
        root.GetProperty("alreadyLinked").GetBoolean().Should().BeFalse();
        var linked = root.GetProperty("deck");
        linked.GetProperty("id").GetString().Should().Be(deck.PublicId, "a link points at the author's deck");
        linked.GetProperty("isLinked").GetBoolean().Should().BeTrue();
        linked.GetProperty("authorHandle").GetString().Should().Be("link-author");

        await using var verify = _db.CreateDbContext();
        (await verify.DeckSubscriptions.CountAsync(s => s.UserId == UserId)).Should().Be(1);
        (await verify.Cards.CountAsync(c => c.UserId == UserId)).Should().Be(0, "linking clones nothing");
    }

    [Fact]
    public async Task ImportDeck_Link_IsIdempotentAndSaysSo()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "twice-author");
        var deck = await LinkedDeckTestData.AddDeck(db, author, cardCount: 1);

        await Tools(db).ImportDeck(deck.PublicId, "link");
        var second = Json(await Tools(db).ImportDeck(deck.PublicId, "link"));

        second.TryGetProperty("error", out _).Should().BeFalse("a repeat link is a success, not an error");
        second.GetProperty("alreadyLinked").GetBoolean().Should().BeTrue();

        await using var verify = _db.CreateDbContext();
        (await verify.DeckSubscriptions.CountAsync(s => s.UserId == UserId)).Should().Be(1);
    }

    [Fact]
    public async Task ImportDeck_Link_KeepsTheCallersOwnPauseOnARepeatImport()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "paused-author");
        var deck = await LinkedDeckTestData.AddDeck(db, author, cardCount: 1);

        await Tools(db).ImportDeck(deck.PublicId, "link");
        await new DeckService(db).SetSuspended(UserId, deck.PublicId, true);

        var second = Json(await Tools(db).ImportDeck(deck.PublicId, "link"));

        second.GetProperty("deck").GetProperty("isSuspended").GetBoolean().Should().BeTrue();
    }

    // ---- import_deck: errors -----------------------------------------------

    [Theory]
    [InlineData("copy")]
    [InlineData("link")]
    public async Task ImportDeck_UnknownDeckIsAStructuredNotFound(string mode)
    {
        await using var db = _db.CreateDbContext();

        var raw = await Tools(db).ImportDeck("no-such-deck", mode);

        ErrorCode(raw).Should().Be("deck_not_found");
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("link")]
    public async Task ImportDeck_PrivateDeckIsIndistinguishableFromMissing(string mode)
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "secretive");
        var deck = await LinkedDeckTestData.AddDeck(db, author, DeckVisibility.Private, cardCount: 1);

        var raw = await Tools(db).ImportDeck(deck.PublicId, mode);

        ErrorCode(raw).Should().Be("deck_not_found");
    }

    [Fact]
    public async Task ImportDeck_LinkingYourOwnDeckIsRejected()
    {
        await using var db = _db.CreateDbContext();
        var deck = await LinkedDeckTestData.AddDeck(db, UserId, cardCount: 1);

        var raw = await Tools(db).ImportDeck(deck.PublicId, "link");

        ErrorCode(raw).Should().Be("own_deck");
    }

    [Fact]
    public async Task ImportDeck_DeckOverTheCardCapIsRejected()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "oversized");
        var deck = await LinkedDeckTestData.AddDeck(
            db, author, cardCount: PublishingService.MaxCardsInPublicDeck + 1);

        var raw = await Tools(db).ImportDeck(deck.PublicId, "copy");

        ErrorCode(raw).Should().Be("deck_too_large");
    }

    [Theory]
    [InlineData("subscribe")]
    [InlineData("")]
    public async Task ImportDeck_UnknownModeIsRejectedBeforeAnythingIsTouched(string mode)
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "mode-author");
        var deck = await LinkedDeckTestData.AddDeck(db, author, cardCount: 1);

        var raw = await Tools(db).ImportDeck(deck.PublicId, mode);

        ErrorCode(raw).Should().Be("invalid_mode");
        await using var verify = _db.CreateDbContext();
        (await verify.DeckSubscriptions.CountAsync()).Should().Be(0);
        (await verify.Cards.CountAsync(c => c.UserId == UserId)).Should().Be(0);
    }

    [Theory]
    [InlineData("COPY")]
    [InlineData(" link ")]
    public async Task ImportDeck_ModeIsCaseAndWhitespaceInsensitive(string mode)
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "lenient");
        var deck = await LinkedDeckTestData.AddDeck(db, author, cardCount: 1);

        var raw = await Tools(db).ImportDeck(deck.PublicId, mode);

        ErrorCode(raw).Should().BeNull();
    }

    // ---- publish_deck ------------------------------------------------------

    [Fact]
    public async Task PublishDeck_PublicReturnsTheLibraryShareUrl()
    {
        await using var db = _db.CreateDbContext();
        var deck = await LinkedDeckTestData.AddDeck(db, UserId, DeckVisibility.Private, cardCount: 2);
        await new PublishingService(db).SetHandle(UserId, "publisher");

        var root = Json(await Tools(db).PublishDeck(deck.PublicId, "public"));

        root.GetProperty("deck").GetProperty("visibility").GetString().Should().Be("public");
        root.GetProperty("shareUrl").GetString().Should().Be($"https://fasolt.app/library/{deck.PublicId}");
    }

    [Fact]
    public async Task PublishDeck_UnlistedNeedsNoHandleAndStillReturnsAShareUrl()
    {
        await using var db = _db.CreateDbContext();
        var deck = await LinkedDeckTestData.AddDeck(db, UserId, DeckVisibility.Private, cardCount: 1);

        var root = Json(await Tools(db).PublishDeck(deck.PublicId, "unlisted"));

        root.GetProperty("deck").GetProperty("visibility").GetString().Should().Be("unlisted");
        root.GetProperty("shareUrl").GetString().Should().Be($"https://fasolt.app/library/{deck.PublicId}");
    }

    [Fact]
    public async Task PublishDeck_PrivateUnpublishesAndReturnsNoShareUrl()
    {
        await using var db = _db.CreateDbContext();
        var deck = await LinkedDeckTestData.AddDeck(db, UserId, DeckVisibility.Unlisted, cardCount: 1);

        var root = Json(await Tools(db).PublishDeck(deck.PublicId, "private"));

        root.GetProperty("deck").GetProperty("visibility").GetString().Should().Be("private");
        root.TryGetProperty("shareUrl", out _).Should().BeFalse();
    }

    [Fact]
    public async Task PublishDeck_WithoutAHandlePointsTheUserAtTheWebApp()
    {
        await using var db = _db.CreateDbContext();
        var deck = await LinkedDeckTestData.AddDeck(db, UserId, DeckVisibility.Private, cardCount: 1);

        var raw = await Tools(db).PublishDeck(deck.PublicId, "public");

        ErrorCode(raw).Should().Be("handle_required");
        var message = Json(raw).GetProperty("message").GetString()!;
        message.Should().Contain("handle");
        message.Should().Contain("web app");

        await using var verify = _db.CreateDbContext();
        (await verify.Decks.AsNoTracking().FirstAsync(d => d.Id == deck.Id))
            .Visibility.Should().Be(DeckVisibility.Private);
    }

    [Fact]
    public async Task PublishDeck_BannedAccountIsToldPublishingIsDisabled()
    {
        await using var db = _db.CreateDbContext();
        var deck = await LinkedDeckTestData.AddDeck(db, UserId, DeckVisibility.Private, cardCount: 1);
        await new PublishingService(db).SetHandle(UserId, "banned-publisher");
        await new PublishingService(db).SetCanPublish(UserId, false);

        var raw = await Tools(db).PublishDeck(deck.PublicId, "public");

        ErrorCode(raw).Should().Be("publishing_disabled");
    }

    [Fact]
    public async Task PublishDeck_OverThePublicDeckCapIsRejected()
    {
        await using var db = _db.CreateDbContext();
        await new PublishingService(db).SetHandle(UserId, "prolific-publisher");
        for (var i = 0; i < PublishingService.MaxPublicDecksPerUser; i++)
            await LinkedDeckTestData.AddDeck(db, UserId, DeckVisibility.Public, name: $"Public {i}");

        var deck = await LinkedDeckTestData.AddDeck(db, UserId, DeckVisibility.Private, cardCount: 1);
        var raw = await Tools(db).PublishDeck(deck.PublicId, "public");

        ErrorCode(raw).Should().Be("public_deck_limit");
    }

    [Fact]
    public async Task PublishDeck_OverTheCardCapIsRejected()
    {
        await using var db = _db.CreateDbContext();
        await new PublishingService(db).SetHandle(UserId, "big-publisher");
        var deck = await LinkedDeckTestData.AddDeck(
            db, UserId, DeckVisibility.Private, cardCount: PublishingService.MaxCardsInPublicDeck + 1);

        var raw = await Tools(db).PublishDeck(deck.PublicId, "public");

        ErrorCode(raw).Should().Be("deck_too_large");
    }

    [Fact]
    public async Task PublishDeck_UnknownDeckIsAStructuredNotFound()
    {
        await using var db = _db.CreateDbContext();

        ErrorCode(await Tools(db).PublishDeck("no-such-deck", "public")).Should().Be("deck_not_found");
    }

    [Fact]
    public async Task PublishDeck_AnotherUsersDeckIsReportedAsMissing()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "not-me");
        var deck = await LinkedDeckTestData.AddDeck(db, author, DeckVisibility.Private, cardCount: 1);

        ErrorCode(await Tools(db).PublishDeck(deck.PublicId, "unlisted")).Should().Be("deck_not_found");
    }

    [Theory]
    [InlineData("listed")]
    [InlineData("")]
    public async Task PublishDeck_UnknownVisibilityIsRejected(string visibility)
    {
        await using var db = _db.CreateDbContext();
        var deck = await LinkedDeckTestData.AddDeck(db, UserId, DeckVisibility.Private, cardCount: 1);

        var raw = await Tools(db).PublishDeck(deck.PublicId, visibility);

        ErrorCode(raw).Should().Be("invalid_visibility");
        await using var verify = _db.CreateDbContext();
        (await verify.Decks.AsNoTracking().FirstAsync(d => d.Id == deck.Id))
            .Visibility.Should().Be(DeckVisibility.Private);
    }

    /// <summary>
    /// Publishing a deck the caller only links is a linked-content refusal, which the
    /// filter in Program.cs renders through <see cref="McpErrorTranslator"/>.
    /// </summary>
    [Fact]
    public async Task PublishDeck_ALinkedDeckIsRefusedAsLinkedContent()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, handle: "real-owner");
        var deck = await LinkedDeckTestData.AddDeck(db, author, cardCount: 1);
        await LinkedDeckTestData.Subscribe(db, UserId, deck);

        var publish = async () => await Tools(db).PublishDeck(deck.PublicId, "public");

        await publish.Should().ThrowAsync<LinkedContentException>();
    }
}
