using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class LibraryServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();

    /// <summary>The importer. The deck authors are created per test.</summary>
    private string ImporterId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static async Task<string> AddAuthor(AppDbContext db, string handle)
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
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<Deck> AddDeck(
        AppDbContext db,
        string userId,
        DeckVisibility visibility,
        string name = "Some Deck",
        string? description = null,
        int cardCount = 0,
        int copyCount = 0,
        DateTimeOffset? publishedAt = null)
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = userId,
            Name = name,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow,
            Visibility = visibility,
            CopyCount = copyCount,
            PublishedAt = visibility == DeckVisibility.Private
                ? null
                : publishedAt ?? DateTimeOffset.UtcNow,
        };
        db.Decks.Add(deck);

        for (var i = 0; i < cardCount; i++)
        {
            var card = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                UserId = userId,
                SourceFile = $"vault/private-notes-{i}.md",
                Front = $"Question {i}",
                Back = $"Answer {i}",
                FrontSvg = i == 0 ? "<svg></svg>" : null,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i),
            };
            db.Cards.Add(card);
            db.DeckCards.Add(new DeckCard { DeckId = deck.Id, CardId = card.Id });
        }

        await db.SaveChangesAsync();
        return deck;
    }

    // ---- anonymous listing -------------------------------------------------

    [Fact]
    public async Task ListPublicDecks_ListsOnlyPublicDecks()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-one");
        var pub = await AddDeck(db, author, DeckVisibility.Public, "Listed Deck", cardCount: 2);
        await AddDeck(db, author, DeckVisibility.Unlisted, "Unlisted Deck");
        await AddDeck(db, author, DeckVisibility.Private, "Private Deck");

        var svc = new LibraryService(db);
        var result = await svc.ListPublicDecks(null, null, 1, 50);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Id.Should().Be(pub.PublicId);
        result.Items[0].Name.Should().Be("Listed Deck");
        result.Items[0].AuthorHandle.Should().Be("author-one");
        result.Items[0].CardCount.Should().Be(2);
    }

    [Fact]
    public async Task ListPublicDecks_FullTextSearchesNameAndDescription()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-two");
        await AddDeck(db, author, DeckVisibility.Public, "Spanish Vocabulary");
        await AddDeck(db, author, DeckVisibility.Public, "Chemistry", description: "Periodic table basics");

        var svc = new LibraryService(db);

        var byName = await svc.ListPublicDecks("spanish", null, 1, 50);
        byName.Items.Should().ContainSingle().Which.Name.Should().Be("Spanish Vocabulary");

        var byDescription = await svc.ListPublicDecks("periodic", null, 1, 50);
        byDescription.Items.Should().ContainSingle().Which.Name.Should().Be("Chemistry");

        var noMatch = await svc.ListPublicDecks("astrophysics", null, 1, 50);
        noMatch.Items.Should().BeEmpty();
        noMatch.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task ListPublicDecks_SortsByCopyCountThenRecency()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-three");
        var now = DateTimeOffset.UtcNow;
        await AddDeck(db, author, DeckVisibility.Public, "Old Popular", copyCount: 9, publishedAt: now.AddDays(-10));
        await AddDeck(db, author, DeckVisibility.Public, "Fresh Quiet", copyCount: 0, publishedAt: now);

        var svc = new LibraryService(db);

        var popular = await svc.ListPublicDecks(null, "popular", 1, 50);
        popular.Items.Select(d => d.Name).Should().ContainInOrder("Old Popular", "Fresh Quiet");

        var recent = await svc.ListPublicDecks(null, "recent", 1, 50);
        recent.Items.Select(d => d.Name).Should().ContainInOrder("Fresh Quiet", "Old Popular");
    }

    [Fact]
    public async Task ListPublicDecks_Paginates()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-four");
        for (var i = 0; i < 5; i++)
            await AddDeck(db, author, DeckVisibility.Public, $"Deck {i}", copyCount: 5 - i);

        var svc = new LibraryService(db);

        var first = await svc.ListPublicDecks(null, "popular", 1, 2);
        first.TotalCount.Should().Be(5);
        first.Page.Should().Be(1);
        first.PageSize.Should().Be(2);
        first.Items.Select(d => d.Name).Should().ContainInOrder("Deck 0", "Deck 1");

        var second = await svc.ListPublicDecks(null, "popular", 2, 2);
        second.Items.Select(d => d.Name).Should().ContainInOrder("Deck 2", "Deck 3");
    }

    // ---- anonymous deck detail --------------------------------------------

    [Fact]
    public async Task GetPublicDeck_ReturnsUnlistedDeckByDirectId()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-five");
        var deck = await AddDeck(db, author, DeckVisibility.Unlisted, "Shared By Link", cardCount: 3);

        var svc = new LibraryService(db);
        var detail = await svc.GetPublicDeck(deck.PublicId);

        detail.Should().NotBeNull();
        detail!.Visibility.Should().Be("unlisted");
        detail.AuthorHandle.Should().Be("author-five");
        detail.CardCount.Should().Be(3);
        detail.SampleCards.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPublicDeck_HidesPrivateDecks()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-six");
        var deck = await AddDeck(db, author, DeckVisibility.Private, "Not Yours", cardCount: 1);

        var svc = new LibraryService(db);

        (await svc.GetPublicDeck(deck.PublicId)).Should().BeNull();
    }

    [Fact]
    public async Task GetPublicDeck_CapsSampleCardsAndExposesNoSourceFile()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-seven");
        var deck = await AddDeck(db, author, DeckVisibility.Public, "Big Deck", cardCount: 25);

        var svc = new LibraryService(db);
        var detail = await svc.GetPublicDeck(deck.PublicId);

        detail!.CardCount.Should().Be(25);
        detail.SampleCards.Should().HaveCount(LibraryService.SampleCardCount);
        detail.SampleCards[0].Front.Should().Be("Question 0");
        detail.SampleCards[0].Back.Should().Be("Answer 0");
        detail.SampleCards[0].FrontSvg.Should().Be("<svg></svg>");

        // The public DTO has no SourceFile member at all — the author's vault paths
        // must never reach an anonymous surface.
        typeof(LibrarySampleCardDto).GetProperty("SourceFile").Should().BeNull();
        typeof(LibraryDeckDto).GetProperty("UserId").Should().BeNull();
        typeof(LibraryDeckDetailDto).GetProperty("UserId").Should().BeNull();
    }

    // ---- copy import -------------------------------------------------------

    [Fact]
    public async Task CopyDeck_ClonesCardsWithAttributionFreshSrsAndNoSourceFile()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-eight");
        var source = await AddDeck(db, author, DeckVisibility.Public, "Copy Me",
            description: "Worth copying", cardCount: 3);

        // Author has studied their own cards; none of that may follow the copy.
        foreach (var cardId in await db.DeckCards.Where(dc => dc.DeckId == source.Id)
                     .Select(dc => dc.CardId).ToListAsync())
        {
            var state = await db.ReviewStateFor(author, cardId);
            state.State = "review";
            state.Stability = 12.5;
            state.DueAt = DateTimeOffset.UtcNow.AddDays(5);
        }
        await db.SaveChangesAsync();

        var svc = new LibraryService(db);
        var result = await svc.CopyDeck(ImporterId, source.PublicId);

        result.Error.Should().Be(CopyDeckError.None);
        var copy = result.Deck!;
        copy.Name.Should().Be("Copy Me");
        copy.Description.Should().Be("Worth copying");
        copy.CardCount.Should().Be(3);
        copy.Visibility.Should().Be("private");
        copy.CopiedFromDeckPublicId.Should().Be(source.PublicId);
        copy.CopiedFromHandle.Should().Be("author-eight");
        copy.Id.Should().NotBe(source.PublicId);

        var copiedCards = await db.DeckCards
            .Where(dc => dc.Deck.PublicId == copy.Id)
            .Select(dc => dc.Card)
            .ToListAsync();

        copiedCards.Should().HaveCount(3);
        copiedCards.Should().OnlyContain(c => c.UserId == ImporterId);
        copiedCards.Should().OnlyContain(c => c.SourceFile == null);
        copiedCards.Select(c => c.Front).Should().BeEquivalentTo("Question 0", "Question 1", "Question 2");

        // Fresh SRS: the importer has no ReviewState rows at all.
        var copiedIds = copiedCards.Select(c => c.Id).ToList();
        (await db.ReviewStates.CountAsync(r => copiedIds.Contains(r.CardId))).Should().Be(0);
        (await db.ReviewStates.CountAsync(r => r.UserId == ImporterId)).Should().Be(0);

        // Originals are untouched.
        (await db.Cards.CountAsync(c => c.UserId == author)).Should().Be(3);
        (await db.Cards.Where(c => c.UserId == author).AllAsync(c => c.SourceFile != null))
            .Should().BeTrue();
    }

    [Fact]
    public async Task CopyDeck_IncrementsSourceCopyCount()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-nine");
        var source = await AddDeck(db, author, DeckVisibility.Public, "Counted", cardCount: 1);

        var svc = new LibraryService(db);
        await svc.CopyDeck(ImporterId, source.PublicId);
        await svc.CopyDeck(ImporterId, source.PublicId);

        (await db.Decks.AsNoTracking().FirstAsync(d => d.Id == source.Id)).CopyCount.Should().Be(2);
    }

    [Fact]
    public async Task CopyDeck_WorksForUnlistedDecks()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-ten");
        var source = await AddDeck(db, author, DeckVisibility.Unlisted, "Link Only", cardCount: 2);

        var svc = new LibraryService(db);
        var result = await svc.CopyDeck(ImporterId, source.PublicId);

        result.Error.Should().Be(CopyDeckError.None);
        result.Deck!.CardCount.Should().Be(2);
    }

    [Fact]
    public async Task CopyDeck_RefusesPrivateDecks()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-eleven");
        var source = await AddDeck(db, author, DeckVisibility.Private, "Mine Only", cardCount: 1);

        var svc = new LibraryService(db);
        var result = await svc.CopyDeck(ImporterId, source.PublicId);

        result.Error.Should().Be(CopyDeckError.NotFound);
        (await db.Decks.CountAsync(d => d.UserId == ImporterId)).Should().Be(0);
        (await db.Cards.CountAsync(c => c.UserId == ImporterId)).Should().Be(0);
    }

    [Fact]
    public async Task CopyDeck_RefusesDecksOverTheCardCap()
    {
        await using var db = _db.CreateDbContext();
        var author = await AddAuthor(db, "author-twelve");
        var source = await AddDeck(db, author, DeckVisibility.Unlisted, "Enormous",
            cardCount: PublishingService.MaxCardsInPublicDeck + 1);

        var svc = new LibraryService(db);
        var result = await svc.CopyDeck(ImporterId, source.PublicId);

        result.Error.Should().Be(CopyDeckError.DeckTooLarge);
        (await db.Decks.CountAsync(d => d.UserId == ImporterId)).Should().Be(0);
    }
}
