using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class PublishingServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static async Task<string> AddUser(AppDbContext db, string? handle = null, bool canPublish = true)
    {
        var id = Guid.NewGuid().ToString();
        var email = $"{id}@fasolt.test";
        db.Users.Add(new AppUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            Handle = handle,
            CanPublish = canPublish,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Deck> AddDeck(AppDbContext db, string userId, int cardCount = 0,
        DeckVisibility visibility = DeckVisibility.Private)
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = userId,
            Name = "Deck " + Guid.NewGuid().ToString("N")[..6],
            CreatedAt = DateTimeOffset.UtcNow,
            Visibility = visibility,
            PublishedAt = visibility == DeckVisibility.Private ? null : DateTimeOffset.UtcNow,
        };
        db.Decks.Add(deck);

        for (var i = 0; i < cardCount; i++)
        {
            var card = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                UserId = userId,
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

    // ---- handle validation -------------------------------------------------

    [Theory]
    [InlineData("phil")]
    [InlineData("abc")]
    [InlineData("a-b-c")]
    [InlineData("user123")]
    [InlineData("012345678901234567890123456789")] // exactly 30
    public async Task SetHandle_AcceptsValidHandles(string handle)
    {
        await using var db = _db.CreateDbContext();
        var svc = new PublishingService(db);

        var result = await svc.SetHandle(UserId, handle);

        result.Error.Should().Be(SetHandleError.None);
        result.Handle!.Handle.Should().Be(handle);
    }

    [Theory]
    [InlineData("ab")]                                 // too short
    [InlineData("0123456789012345678901234567890")]    // 31 chars
    [InlineData("has space")]
    [InlineData("under_score")]
    [InlineData("dots.here")]
    [InlineData("emoji🙂")]
    [InlineData("")]
    [InlineData(null)]
    public async Task SetHandle_RejectsInvalidHandles(string? handle)
    {
        await using var db = _db.CreateDbContext();
        var svc = new PublishingService(db);

        var result = await svc.SetHandle(UserId, handle);

        result.Error.Should().Be(SetHandleError.Invalid);
        (await db.Users.FirstAsync(u => u.Id == UserId)).Handle.Should().BeNull();
    }

    [Fact]
    public async Task SetHandle_NormalizesCaseAndWhitespace()
    {
        await using var db = _db.CreateDbContext();
        var svc = new PublishingService(db);

        var result = await svc.SetHandle(UserId, "  PhilBaum  ");

        result.Error.Should().Be(SetHandleError.None);
        result.Handle!.Handle.Should().Be("philbaum");
    }

    [Fact]
    public async Task SetHandle_RejectsHandleTakenByAnotherUser()
    {
        await using var db = _db.CreateDbContext();
        var other = await AddUser(db, handle: "taken-one");
        var svc = new PublishingService(db);

        var result = await svc.SetHandle(UserId, "Taken-One");

        result.Error.Should().Be(SetHandleError.Taken);
        (await db.Users.FirstAsync(u => u.Id == other)).Handle.Should().Be("taken-one");
        (await db.Users.FirstAsync(u => u.Id == UserId)).Handle.Should().BeNull();
    }

    [Fact]
    public async Task SetHandle_ReclaimingOwnHandleIsANoOp()
    {
        await using var db = _db.CreateDbContext();
        var svc = new PublishingService(db);

        await svc.SetHandle(UserId, "mine");
        var result = await svc.SetHandle(UserId, "mine");

        result.Error.Should().Be(SetHandleError.None);
        result.Handle!.Handle.Should().Be("mine");
    }

    [Fact]
    public async Task SetHandle_AllowsChangingToAFreeHandle()
    {
        await using var db = _db.CreateDbContext();
        var svc = new PublishingService(db);

        await svc.SetHandle(UserId, "first-handle");
        var result = await svc.SetHandle(UserId, "second-handle");

        result.Error.Should().Be(SetHandleError.None);
        (await db.Users.FirstAsync(u => u.Id == UserId)).Handle.Should().Be("second-handle");
    }

    [Fact]
    public async Task GetHandle_ReturnsHandleAndPublishFlag()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "reader", canPublish: false);
        var svc = new PublishingService(db);

        var result = await svc.GetHandle(userId);

        result.Should().NotBeNull();
        result!.Handle.Should().Be("reader");
        result.CanPublish.Should().BeFalse();
    }

    // ---- visibility rules --------------------------------------------------

    [Fact]
    public async Task SetVisibility_PublicRequiresHandle()
    {
        await using var db = _db.CreateDbContext();
        var deck = await AddDeck(db, UserId, cardCount: 1);
        var svc = new PublishingService(db);

        var result = await svc.SetVisibility(UserId, deck.PublicId, DeckVisibility.Public);

        result.Error.Should().Be(SetVisibilityError.HandleRequired);
        (await db.Decks.AsNoTracking().FirstAsync(d => d.Id == deck.Id))
            .Visibility.Should().Be(DeckVisibility.Private);
    }

    [Fact]
    public async Task SetVisibility_PublicRequiresCanPublish()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "banned-user", canPublish: false);
        var deck = await AddDeck(db, userId, cardCount: 1);
        var svc = new PublishingService(db);

        var result = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Public);

        result.Error.Should().Be(SetVisibilityError.PublishingDisabled);
    }

    [Fact]
    public async Task SetVisibility_PublicRejectsDeckOverCardCap()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "big-deck-owner");
        var deck = await AddDeck(db, userId, cardCount: PublishingService.MaxCardsInPublicDeck + 1);
        var svc = new PublishingService(db);

        var result = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Public);

        result.Error.Should().Be(SetVisibilityError.DeckTooLarge);
    }

    [Fact]
    public async Task SetVisibility_PublicAllowsDeckExactlyAtCardCap()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "cap-deck-owner");
        var deck = await AddDeck(db, userId, cardCount: PublishingService.MaxCardsInPublicDeck);
        var svc = new PublishingService(db);

        var result = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Public);

        result.Error.Should().Be(SetVisibilityError.None);
    }

    [Fact]
    public async Task SetVisibility_PublicRejectsOverPublicDeckCap()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "prolific");
        for (var i = 0; i < PublishingService.MaxPublicDecksPerUser; i++)
            await AddDeck(db, userId, visibility: DeckVisibility.Public);

        var deck = await AddDeck(db, userId, cardCount: 1);
        var svc = new PublishingService(db);

        var result = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Public);

        result.Error.Should().Be(SetVisibilityError.PublicDeckLimit);
    }

    [Fact]
    public async Task SetVisibility_RepublishingAnAlreadyPublicDeckIgnoresTheCap()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "at-the-cap");
        Deck? last = null;
        for (var i = 0; i < PublishingService.MaxPublicDecksPerUser; i++)
            last = await AddDeck(db, userId, visibility: DeckVisibility.Public);

        var svc = new PublishingService(db);
        var result = await svc.SetVisibility(userId, last!.PublicId, DeckVisibility.Public);

        result.Error.Should().Be(SetVisibilityError.None);
    }

    [Fact]
    public async Task SetVisibility_UnlistedDoesNotRequireHandle()
    {
        await using var db = _db.CreateDbContext();
        var deck = await AddDeck(db, UserId, cardCount: 1);
        var svc = new PublishingService(db);

        var result = await svc.SetVisibility(UserId, deck.PublicId, DeckVisibility.Unlisted);

        result.Error.Should().Be(SetVisibilityError.None);
        result.Deck!.Visibility.Should().Be("unlisted");
        result.Deck.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SetVisibility_PublishThenPrivateClearsPublishedAt()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "toggler");
        var deck = await AddDeck(db, userId, cardCount: 2);
        var svc = new PublishingService(db);

        var published = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Public);
        published.Error.Should().Be(SetVisibilityError.None);
        published.Deck!.Visibility.Should().Be("public");
        published.Deck.PublishedAt.Should().NotBeNull();

        var unpublished = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Private);
        unpublished.Error.Should().Be(SetVisibilityError.None);
        unpublished.Deck!.Visibility.Should().Be("private");
        unpublished.Deck.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task SetVisibility_OtherUsersDeckIsNotFound()
    {
        await using var db = _db.CreateDbContext();
        var otherUser = await AddUser(db, handle: "someone-else");
        var deck = await AddDeck(db, otherUser, cardCount: 1);
        var svc = new PublishingService(db);

        var result = await svc.SetVisibility(UserId, deck.PublicId, DeckVisibility.Unlisted);

        result.Error.Should().Be(SetVisibilityError.DeckNotFound);
    }

    // ---- admin actions -----------------------------------------------------

    [Fact]
    public async Task Unlist_ForcesAnyDeckBackToPrivate()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "unlist-me");
        var deck = await AddDeck(db, userId, cardCount: 1, visibility: DeckVisibility.Public);
        var svc = new PublishingService(db);

        (await svc.Unlist(deck.PublicId)).Should().BeTrue();

        var reloaded = await db.Decks.AsNoTracking().FirstAsync(d => d.Id == deck.Id);
        reloaded.Visibility.Should().Be(DeckVisibility.Private);
        reloaded.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task Unlist_UnknownDeckReturnsFalse()
    {
        await using var db = _db.CreateDbContext();
        var svc = new PublishingService(db);

        (await svc.Unlist("does-not-fit")).Should().BeFalse();
    }

    [Fact]
    public async Task SetCanPublish_TogglesTheFlagAndBlocksFuturePublishing()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "to-ban");
        var deck = await AddDeck(db, userId, cardCount: 1);
        var svc = new PublishingService(db);

        (await svc.SetCanPublish(userId, false)).Should().BeTrue();
        (await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId)).CanPublish.Should().BeFalse();

        var blocked = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Public);
        blocked.Error.Should().Be(SetVisibilityError.PublishingDisabled);

        (await svc.SetCanPublish(userId, true)).Should().BeTrue();
        var allowed = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Public);
        allowed.Error.Should().Be(SetVisibilityError.None);
    }
}
