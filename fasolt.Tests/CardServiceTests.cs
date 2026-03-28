using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
    public async Task CreateCard_WithDeckId_AssignsCardToDeck()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Target Deck", null);
        var card = await cardSvc.CreateCard(UserId, "Q?", "A.", null, null, deckId: deck.Id);

        card.Decks.Should().ContainSingle(d => d.Id == deck.Id);
    }

    [Fact]
    public async Task CreateCard_WithInvalidDeckId_Throws()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var act = () => svc.CreateCard(UserId, "Q?", "A.", null, null, deckId: "nonexistent");

        await act.Should().ThrowAsync<KeyNotFoundException>();
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
        var entity = await db.Cards.FirstOrDefaultAsync(c => c.PublicId == card.Id);
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
        var reloaded = await db2.Cards.FirstOrDefaultAsync(c => c.PublicId == card.Id);
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
    public async Task ListCards_Pagination_CursorReturnsNextPage()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        // Create 5 cards
        for (var i = 1; i <= 5; i++)
            await svc.CreateCard(UserId, $"Page {i}", $"Back {i}", "page.md", null);

        var page1 = await svc.ListCards(UserId, sourceFile: null, deckId: null, limit: 2, after: null);

        page1.Items.Should().HaveCount(2);
        page1.HasMore.Should().BeTrue();
        page1.NextCursor.Should().NotBeNull();

        var page2 = await svc.ListCards(UserId, sourceFile: null, deckId: null, limit: 2, after: page1.NextCursor);

        page2.Items.Should().HaveCount(2);
        page2.HasMore.Should().BeTrue();

        // No overlap between pages
        page2.Items.Should().NotContain(c => page1.Items.Any(p1 => p1.Id == c.Id));

        var page3 = await svc.ListCards(UserId, sourceFile: null, deckId: null, limit: 2, after: page2.NextCursor);

        page3.Items.Should().HaveCount(1);
        page3.HasMore.Should().BeFalse();
        page3.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task ResetProgress_ClearsSrsState()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var card = await svc.CreateCard(UserId, "Reset Q", "Reset A", null, null);

        // Simulate SRS state
        var entity = await db.Cards.FindAsync(db.Cards.First(c => c.PublicId == card.Id).Id);
        entity!.Stability = 5.0;
        entity.Difficulty = 4.2;
        entity.Step = 3;
        entity.State = "review";
        entity.DueAt = DateTimeOffset.UtcNow;
        entity.LastReviewedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var result = await svc.ResetProgress(UserId, card.Id);

        result.Should().NotBeNull();
        result!.State.Should().Be("new");
        result.Stability.Should().BeNull();
        result.Difficulty.Should().BeNull();
        result.Step.Should().BeNull();
        result.DueAt.Should().BeNull();
        result.LastReviewedAt.Should().BeNull();
    }

    [Fact]
    public async Task ResetProgress_NotFound_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var result = await svc.ResetProgress(UserId, "nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task BulkUpdateCards_ById_UpdatesMultiple()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var a = await svc.CreateCard(UserId, "BU-A", "Old A", "bulk.md", null);
        var b = await svc.CreateCard(UserId, "BU-B", "Old B", "bulk.md", null);

        var results = await svc.BulkUpdateCards(UserId,
        [
            new BulkUpdateCardItem(CardId: a.Id, NewBack: "New A"),
            new BulkUpdateCardItem(CardId: b.Id, NewBack: "New B"),
        ]);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Status.Should().Be(UpdateCardStatus.Success));
        results[0].Card!.Back.Should().Be("New A");
        results[1].Card!.Back.Should().Be("New B");
    }

    [Fact]
    public async Task BulkUpdateCards_ByNaturalKey_UpdatesMultiple()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "NK-A", "Old A", "nk.md", null);
        await svc.CreateCard(UserId, "NK-B", "Old B", "nk.md", null);

        var results = await svc.BulkUpdateCards(UserId,
        [
            new BulkUpdateCardItem(SourceFile: "nk.md", Front: "NK-A", NewBack: "Updated A"),
            new BulkUpdateCardItem(SourceFile: "nk.md", Front: "NK-B", NewBack: "Updated B"),
        ]);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(r => r.Status.Should().Be(UpdateCardStatus.Success));
        results[0].Card!.Back.Should().Be("Updated A");
        results[1].Card!.Back.Should().Be("Updated B");
    }

    [Fact]
    public async Task BulkUpdateCards_MixedResults()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var card = await svc.CreateCard(UserId, "Exists", "Back", "mix.md", null);

        var results = await svc.BulkUpdateCards(UserId,
        [
            new BulkUpdateCardItem(CardId: card.Id, NewBack: "Updated"),
            new BulkUpdateCardItem(CardId: "nonexistent", NewBack: "Nope"),
            new BulkUpdateCardItem(SourceFile: null, Front: null, NewBack: "Invalid"),
        ]);

        results.Should().HaveCount(3);
        results[0].Status.Should().Be(UpdateCardStatus.Success);
        results[1].Status.Should().Be(UpdateCardStatus.NotFound);
        results[2].Status.Should().Be(UpdateCardStatus.NotFound);
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

    [Fact]
    public async Task CreateCard_RejectsOversizedFront()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var longFront = new string('x', CardService.MaxFrontLength + 1);

        var act = () => svc.CreateCard(UserId, longFront, "Back", null, null);

        await act.Should().ThrowAsync<System.ComponentModel.DataAnnotations.ValidationException>()
            .WithMessage("*Front*maximum*");
    }

    [Fact]
    public async Task CreateCard_RejectsOversizedBack()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var longBack = new string('x', CardService.MaxBackLength + 1);

        var act = () => svc.CreateCard(UserId, "Front", longBack, null, null);

        await act.Should().ThrowAsync<System.ComponentModel.DataAnnotations.ValidationException>()
            .WithMessage("*Back*maximum*");
    }

    [Fact]
    public async Task CreateCard_RejectsOversizedSourceHeading()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var longHeading = new string('x', CardService.MaxSourceHeadingLength + 1);

        var act = () => svc.CreateCard(UserId, "Front", "Back", "file.md", longHeading);

        await act.Should().ThrowAsync<System.ComponentModel.DataAnnotations.ValidationException>()
            .WithMessage("*Source heading*maximum*");
    }

    [Fact]
    public async Task CreateCard_AcceptsMaxLengthFields()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var front = new string('x', CardService.MaxFrontLength);
        var back = new string('x', CardService.MaxBackLength);
        var heading = new string('x', CardService.MaxSourceHeadingLength);

        var card = await svc.CreateCard(UserId, front, back, "file.md", heading);

        card.Front.Should().HaveLength(CardService.MaxFrontLength);
        card.Back.Should().HaveLength(CardService.MaxBackLength);
        card.SourceHeading.Should().HaveLength(CardService.MaxSourceHeadingLength);
    }

    [Fact]
    public async Task BulkCreateCards_SkipsOversizedCards()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var cards = new List<BulkCardItem>
        {
            new("Normal front", "Normal back"),
            new(new string('x', CardService.MaxFrontLength + 1), "Back"),
        };

        var result = await svc.BulkCreateCards(UserId, cards, null, null);

        result.Response!.Created.Should().HaveCount(1);
        result.Response.Skipped.Should().HaveCount(1);
        result.Response.Skipped[0].Reason.Should().Contain("Front");
    }
}
