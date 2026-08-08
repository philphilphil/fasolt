using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
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
    public async Task SetVisibility_UnlistedRequiresCanPublish()
    {
        // An unlisted deck's share link resolves for anyone holding it, and copy and
        // subscribe accept every non-private deck — so a ban that only covered the
        // public transition would be one "unlisted" away from meaningless.
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "banned-unlister", canPublish: false);
        var deck = await AddDeck(db, userId, cardCount: 1);
        var svc = new PublishingService(db);

        var result = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Unlisted);

        result.Error.Should().Be(SetVisibilityError.PublishingDisabled);
        var stored = await db.Decks.AsNoTracking().FirstAsync(d => d.Id == deck.Id);
        stored.Visibility.Should().Be(DeckVisibility.Private);
        stored.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task SetVisibility_BannedAuthorCannotRelistAnAlreadyUnlistedDeck()
    {
        // Going private and back is the loop the ban has to survive.
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "relister");
        var deck = await AddDeck(db, userId, cardCount: 1, visibility: DeckVisibility.Unlisted);
        var svc = new PublishingService(db);

        await svc.SetCanPublish(userId, false);

        // Taking it down is always allowed; putting it back is not.
        (await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Private))
            .Error.Should().Be(SetVisibilityError.None);
        (await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Unlisted))
            .Error.Should().Be(SetVisibilityError.PublishingDisabled);
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
    public async Task SetVisibility_UnlistedThenPublicRestampsPublishedAt()
    {
        // PublishedAt is the "shared since" date: the library's recent sort, the public
        // page's Shared date and the sitemap's lastmod all read it. A deck unlisted in
        // January and made public in August would otherwise enter the library backdated
        // to January and sort as if it had been there all along.
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "relister");
        var deck = await AddDeck(db, userId, cardCount: 1, visibility: DeckVisibility.Unlisted);
        var unlistedAt = DateTimeOffset.UtcNow.AddDays(-200);
        deck.PublishedAt = unlistedAt;
        await db.SaveChangesAsync();
        var svc = new PublishingService(db);

        var published = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Public);

        published.Error.Should().Be(SetVisibilityError.None);
        published.Deck!.PublishedAt.Should().BeAfter(unlistedAt.AddDays(1));

        // Re-applying Public is not a fresh share, so the date stands.
        var again = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Public);
        again.Deck!.PublishedAt.Should().BeCloseTo(published.Deck.PublishedAt!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task SetVisibility_PublicThenUnlistedKeepsPublishedAt()
    {
        // Hiding a deck and re-listing it must not reset its age in the library.
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "hider");
        var deck = await AddDeck(db, userId, cardCount: 1, visibility: DeckVisibility.Public);
        var publishedAt = DateTimeOffset.UtcNow.AddDays(-30);
        deck.PublishedAt = publishedAt;
        await db.SaveChangesAsync();
        var svc = new PublishingService(db);

        var result = await svc.SetVisibility(userId, deck.PublicId, DeckVisibility.Unlisted);

        result.Error.Should().Be(SetVisibilityError.None);
        result.Deck!.PublishedAt.Should().BeCloseTo(publishedAt, TimeSpan.FromMilliseconds(1));
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

    // ---- card cap after publishing -----------------------------------------
    // Publishing only checks the deck as it stands at that moment, so every path
    // that adds cards has to re-check — otherwise a deck published at 999 cards
    // could grow without limit while listed in the library.

    [Fact]
    public async Task AddCards_CannotPushAPublishedDeckOverTheCardCap()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "grower");
        var deck = await AddDeck(db, userId, cardCount: PublishingService.MaxCardsInPublicDeck,
            visibility: DeckVisibility.Public);

        var extra = await new CardService(db).CreateCard(userId, "One more", "Nope", null);
        var result = await new DeckService(db).AddCards(userId, deck.PublicId, [extra.Id]);

        result.Should().Be(AddCardsResult.PublishedDeckFull);
        (await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id))
            .Should().Be(PublishingService.MaxCardsInPublicDeck);
    }

    [Fact]
    public async Task AddCards_IsUncappedForDecksThatAreNotPublic()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "private-grower");
        var deck = await AddDeck(db, userId, cardCount: PublishingService.MaxCardsInPublicDeck,
            visibility: DeckVisibility.Unlisted);

        var extra = await new CardService(db).CreateCard(userId, "One more", "Fine", null);
        var result = await new DeckService(db).AddCards(userId, deck.PublicId, [extra.Id]);

        result.Should().Be(AddCardsResult.Success);
        (await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id))
            .Should().Be(PublishingService.MaxCardsInPublicDeck + 1);
    }

    [Fact]
    public async Task CreateCard_IntoAFullPublishedDeckIsRejected()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "single-adder");
        var deck = await AddDeck(db, userId, cardCount: PublishingService.MaxCardsInPublicDeck,
            visibility: DeckVisibility.Public);

        var act = () => new CardService(db).CreateCard(userId, "Q", "A", null, deckId: deck.PublicId);

        await act.Should().ThrowAsync<PublishedDeckFullException>();
        (await db.Cards.CountAsync(c => c.UserId == userId && c.Front == "Q")).Should().Be(0);
    }

    [Fact]
    public async Task BulkCreateCards_IntoAFullPublishedDeckCreatesNothing()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "bulk-adder");
        var deck = await AddDeck(db, userId, cardCount: PublishingService.MaxCardsInPublicDeck,
            visibility: DeckVisibility.Public);

        var result = await new CardService(db).BulkCreateCards(
            userId,
            [new BulkCardItem("Bulk Q", "Bulk A")],
            sourceFile: null,
            deckId: deck.PublicId);

        result.IsPublishedDeckFull.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        (await db.Cards.CountAsync(c => c.UserId == userId && c.Front == "Bulk Q")).Should().Be(0);
        (await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id))
            .Should().Be(PublishingService.MaxCardsInPublicDeck);
    }

    [Fact]
    public async Task UpdateCard_CannotAssignAnExtraCardToAFullPublishedDeck()
    {
        await using var db = _db.CreateDbContext();
        var userId = await AddUser(db, handle: "reassigner");
        var deck = await AddDeck(db, userId, cardCount: PublishingService.MaxCardsInPublicDeck,
            visibility: DeckVisibility.Public);

        var svc = new CardService(db);
        var loose = await svc.CreateCard(userId, "Loose Q", "Loose A", null);

        var act = () => svc.UpdateCard(userId, loose.Id,
            new UpdateCardRequest("Loose Q", "Loose A", null, null, null, [deck.PublicId]));

        await act.Should().ThrowAsync<PublishedDeckFullException>();
        (await db.DeckCards.CountAsync(dc => dc.DeckId == deck.Id))
            .Should().Be(PublishingService.MaxCardsInPublicDeck);
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
