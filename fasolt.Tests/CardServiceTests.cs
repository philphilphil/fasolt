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

    [Fact]
    public async Task DeleteCards_BulkDelete()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var a = await svc.CreateCard(UserId, "A", "A", null, null);
        var b = await svc.CreateCard(UserId, "B", "B", null, null);
        var c = await svc.CreateCard(UserId, "C", "C", null, null);

        var count = await svc.DeleteCards(UserId, [a.Id, b.Id]);

        count.Should().Be(2);
        (await svc.GetCard(UserId, a.Id)).Should().BeNull();
        (await svc.GetCard(UserId, b.Id)).Should().BeNull();
        (await svc.GetCard(UserId, c.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateCardFields_ById_PreservesSrsState()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var card = await svc.CreateCard(UserId, "Old front", "Old back", "notes.md", "Heading");

        // Simulate some SRS state by updating directly
        var entity = await db.Cards.FindAsync(card.Id);
        entity!.Stability = 5.0;
        entity.Difficulty = 4.2;
        entity.Step = 2;
        entity.State = "review";
        await db.SaveChangesAsync();

        var result = await svc.UpdateCardFields(UserId, card.Id,
            new UpdateCardFieldsRequest(NewFront: "New front", NewBack: "New back"));

        result.Status.Should().Be(UpdateCardStatus.Success);
        result.Card!.Front.Should().Be("New front");
        result.Card.Back.Should().Be("New back");

        // Verify SRS state preserved
        await using var db2 = _db.CreateDbContext();
        var reloaded = await db2.Cards.FindAsync(card.Id);
        reloaded!.Stability.Should().Be(5.0);
        reloaded.Difficulty.Should().Be(4.2);
        reloaded.Step.Should().Be(2);
        reloaded.State.Should().Be("review");
    }

    [Fact]
    public async Task UpdateCardByNaturalKey_CaseInsensitive()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "What is DNA?", "Deoxyribonucleic acid", "biology.md", "Basics");

        // Look up with different casing
        var result = await svc.UpdateCardByNaturalKey(UserId, "Biology.MD", "what is dna?",
            new UpdateCardFieldsRequest(NewBack: "Updated answer"));

        result.Status.Should().Be(UpdateCardStatus.Success);
        result.Card!.Back.Should().Be("Updated answer");
        result.Card.Front.Should().Be("What is DNA?"); // original casing preserved
    }

    [Fact]
    public async Task UpdateCardFields_RejectsCollision()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "Existing front", "Back A", "notes.md", null);
        var cardB = await svc.CreateCard(UserId, "Other front", "Back B", "notes.md", null);

        // Try to rename cardB's front to collide with existing card
        var result = await svc.UpdateCardFields(UserId, cardB.Id,
            new UpdateCardFieldsRequest(NewFront: "Existing front"));

        result.Status.Should().Be(UpdateCardStatus.Collision);
        result.Card.Should().BeNull();
    }

    [Fact]
    public async Task UpdateCardFields_SourceFileChangeCollision()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "Same front", "Back A", "notes.md", null);
        var cardB = await svc.CreateCard(UserId, "Same front", "Back B", "other.md", null);

        // Move cardB to notes.md — collides with existing card
        var result = await svc.UpdateCardFields(UserId, cardB.Id,
            new UpdateCardFieldsRequest(NewSourceFile: "notes.md"));

        result.Status.Should().Be(UpdateCardStatus.Collision);
    }

    [Fact]
    public async Task DeleteCardsBySource_DeletesMatchingCards()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "Q1", "A1", "target.md", null);
        await svc.CreateCard(UserId, "Q2", "A2", "target.md", null);
        await svc.CreateCard(UserId, "Q3", "A3", "other.md", null);

        var count = await svc.DeleteCardsBySource(UserId, "target.md");

        count.Should().Be(2);

        var remaining = await svc.ListCards(UserId, sourceFile: null, deckId: null, limit: null, after: null);
        remaining.Items.Should().HaveCount(1);
        remaining.Items[0].SourceFile.Should().Be("other.md");
    }

    [Fact]
    public async Task DeleteCardsBySource_NoMatch_ReturnsZero()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var count = await svc.DeleteCardsBySource(UserId, "nonexistent.md");

        count.Should().Be(0);
    }
}
