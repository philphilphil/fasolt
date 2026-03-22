using FluentAssertions;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class CardServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task CreateCard_WithSourceFile()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var card = await svc.CreateCard(UserId, "What is X?", "X is Y.", "notes.md", "Introduction");

        card.SourceFile.Should().Be("notes.md");
        card.SourceHeading.Should().Be("Introduction");
        card.Front.Should().Be("What is X?");
        card.Back.Should().Be("X is Y.");
    }

    [Fact]
    public async Task CreateCard_WithoutSourceFile()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var card = await svc.CreateCard(UserId, "Capital of France?", "Paris", null, null);

        card.SourceFile.Should().BeNull();
        card.Front.Should().Be("Capital of France?");
    }

    [Fact]
    public async Task ListCards_FilterBySourceFile()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "Front A", "Back A", "file-a.md", null);
        await svc.CreateCard(UserId, "Front B", "Back B", "file-b.md", null);

        var result = await svc.ListCards(UserId, sourceFile: "file-a.md", deckId: null, limit: null, after: null);

        result.Items.Should().AllSatisfy(c => c.SourceFile.Should().Be("file-a.md"));
        result.Items.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ListCards_FilterByDeckId()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Test Deck", null);
        var card = await cardSvc.CreateCard(UserId, "Deck Q?", "Deck A.", null, null);
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        var result = await cardSvc.ListCards(UserId, sourceFile: null, deckId: deck.Id, limit: null, after: null);

        result.Items.Should().Contain(c => c.Id == card.Id);
    }

    [Fact]
    public async Task BulkCreate_WithSourceFileOverride()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var result = await svc.BulkCreateCards(UserId,
            [
                new BulkCardItem("Q from default", "A from default"),
                new BulkCardItem("Q from override", "A from override", SourceFile: "override-source.md"),
            ],
            sourceFile: "default-source.md",
            deckId: null);

        result.IsSuccess.Should().BeTrue();
        var resp = result.Response!;
        resp.Created.Should().HaveCount(2);
        resp.Skipped.Should().BeEmpty();

        resp.Created.Single(c => c.Front == "Q from default").SourceFile.Should().Be("default-source.md");
        resp.Created.Single(c => c.Front == "Q from override").SourceFile.Should().Be("override-source.md");
    }

    [Fact]
    public async Task BulkCreate_SkipsDuplicates_SameSourceFile()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.BulkCreateCards(UserId,
            [new BulkCardItem("Duplicate Q", "Duplicate A")],
            sourceFile: "dup-test.md", deckId: null);

        var result = await svc.BulkCreateCards(UserId,
            [new BulkCardItem("Duplicate Q", "Different A")],
            sourceFile: "dup-test.md", deckId: null);

        result.IsSuccess.Should().BeTrue();
        var resp = result.Response!;
        resp.Created.Should().BeEmpty();
        resp.Skipped.Should().HaveCount(1);
        resp.Skipped[0].Front.Should().Be("Duplicate Q");
    }

    [Fact]
    public async Task BulkCreate_AllowsSameFront_DifferentSourceFile()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var result = await svc.BulkCreateCards(UserId,
            [
                new BulkCardItem("Shared Front", "Back A", SourceFile: "source-one.md"),
                new BulkCardItem("Shared Front", "Back B", SourceFile: "source-two.md"),
            ],
            sourceFile: null, deckId: null);

        result.IsSuccess.Should().BeTrue();
        result.Response!.Created.Should().HaveCount(2);
        result.Response!.Skipped.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteCard_RemovesFromDatabase()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var card = await svc.CreateCard(UserId, "To delete", "Gone", null, null);

        var deleted = await svc.DeleteCard(UserId, card.Id);
        deleted.Should().BeTrue();

        var fetched = await svc.GetCard(UserId, card.Id);
        fetched.Should().BeNull();
    }
}
