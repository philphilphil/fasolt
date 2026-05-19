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

        var card = await svc.CreateCard(UserId, "What is X?", "X is Y.", "notes.md");

        card.SourceFile.Should().Be("notes.md");
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
        var card = await cardSvc.CreateCard(UserId, "Q?", "A.", null, deckId: deck.Id);

        card.Decks.Should().ContainSingle(d => d.Id == deck.Id);
    }

    [Fact]
    public async Task CreateCard_WithInvalidDeckId_Throws()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var act = () => svc.CreateCard(UserId, "Q?", "A.", null, deckId: "nonexistent");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreateCard_WithoutSourceFile()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var card = await svc.CreateCard(UserId, "Capital of France?", "Paris", null);

        card.SourceFile.Should().BeNull();
        card.Front.Should().Be("Capital of France?");
    }

    [Fact]
    public async Task ListCards_FilterBySourceFile()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "Front A", "Back A", "file-a.md");
        await svc.CreateCard(UserId, "Front B", "Back B", "file-b.md");

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
        var card = await cardSvc.CreateCard(UserId, "Deck Q?", "Deck A.", null);
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        var result = await cardSvc.ListCards(UserId, sourceFile: null, deckId: deck.Id, limit: null, after: null);

        result.Items.Should().Contain(c => c.Id == card.Id);
    }

    [Fact]
    public async Task ListCards_SlimDefault_OmitsSrsAndSvg()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "Q with svg", "A with svg", "slim.md",
            frontSvg: "<svg><rect/></svg>", backSvg: "<svg><circle/></svg>");

        var result = await svc.ListCards(UserId, sourceFile: "slim.md", deckId: null,
            limit: null, after: null, include: new HashSet<string>());

        var card = result.Items.Should().ContainSingle().Subject;
        card.State.Should().BeNull();
        card.DueAt.Should().BeNull();
        card.Stability.Should().BeNull();
        card.Difficulty.Should().BeNull();
        card.Step.Should().BeNull();
        card.LastReviewedAt.Should().BeNull();
        card.FrontSvg.Should().BeNull();
        card.BackSvg.Should().BeNull();
        card.Front.Should().Be("Q with svg");
        card.Back.Should().Be("A with svg");
    }

    [Fact]
    public async Task ListCards_IncludeSrs_PopulatesSrsFieldsOnly()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "Q srs", "A srs", "srs.md",
            frontSvg: "<svg/>", backSvg: "<svg/>");

        var result = await svc.ListCards(UserId, sourceFile: "srs.md", deckId: null,
            limit: null, after: null, include: new HashSet<string> { "srs" });

        var card = result.Items.Should().ContainSingle().Subject;
        card.State.Should().NotBeNull();
        card.FrontSvg.Should().BeNull();
        card.BackSvg.Should().BeNull();
    }

    [Fact]
    public async Task ListCards_IncludeSvg_PopulatesSvgFieldsOnly()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "Q svg", "A svg", "svg.md",
            frontSvg: "<svg><rect/></svg>", backSvg: "<svg><circle/></svg>");

        var result = await svc.ListCards(UserId, sourceFile: "svg.md", deckId: null,
            limit: null, after: null, include: new HashSet<string> { "svg" });

        var card = result.Items.Should().ContainSingle().Subject;
        card.State.Should().BeNull();
        card.DueAt.Should().BeNull();
        card.FrontSvg.Should().NotBeNull().And.Contain("<rect");
        card.BackSvg.Should().NotBeNull().And.Contain("<circle");
    }

    [Fact]
    public async Task ListCards_NullInclude_ReturnsFullShape_ForRestCompatibility()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "Q rest", "A rest", "rest.md",
            frontSvg: "<svg/>", backSvg: "<svg/>");

        var result = await svc.ListCards(UserId, sourceFile: "rest.md", deckId: null,
            limit: null, after: null, include: null);

        var card = result.Items.Should().ContainSingle().Subject;
        card.State.Should().NotBeNull();
        card.FrontSvg.Should().NotBeNull();
        card.BackSvg.Should().NotBeNull();
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

        var card = await svc.CreateCard(UserId, "To delete", "Gone", null);

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

        var a = await svc.CreateCard(UserId, "A", "A", null);
        var b = await svc.CreateCard(UserId, "B", "B", null);
        var c = await svc.CreateCard(UserId, "C", "C", null);

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

        var card = await svc.CreateCard(UserId, "Old front", "Old back", "notes.md");

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
    public async Task ListCards_Pagination_CursorReturnsNextPage()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        // Create 5 cards
        for (var i = 1; i <= 5; i++)
            await svc.CreateCard(UserId, $"Page {i}", $"Back {i}", "page.md");

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

        var card = await svc.CreateCard(UserId, "Reset Q", "Reset A", null);

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

        var a = await svc.CreateCard(UserId, "BU-A", "Old A", "bulk.md");
        var b = await svc.CreateCard(UserId, "BU-B", "Old B", "bulk.md");

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
    public async Task BulkUpdateCards_MixedResults()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var card = await svc.CreateCard(UserId, "Exists", "Back", "mix.md");

        var results = await svc.BulkUpdateCards(UserId,
        [
            new BulkUpdateCardItem(CardId: card.Id, NewBack: "Updated"),
            new BulkUpdateCardItem(CardId: "nonexistent", NewBack: "Nope"),
        ]);

        results.Should().HaveCount(2);
        results[0].Status.Should().Be(UpdateCardStatus.Success);
        results[1].Status.Should().Be(UpdateCardStatus.NotFound);
    }

    [Fact]
    public async Task DeleteCardsBySource_DeletesMatchingCards()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "Q1", "A1", "target.md");
        await svc.CreateCard(UserId, "Q2", "A2", "target.md");
        await svc.CreateCard(UserId, "Q3", "A3", "other.md");

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

        var act = () => svc.CreateCard(UserId, longFront, "Back", null);

        await act.Should().ThrowAsync<System.ComponentModel.DataAnnotations.ValidationException>()
            .WithMessage("*Front*maximum*");
    }

    [Fact]
    public async Task CreateCard_RejectsOversizedBack()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var longBack = new string('x', CardService.MaxBackLength + 1);

        var act = () => svc.CreateCard(UserId, "Front", longBack, null);

        await act.Should().ThrowAsync<System.ComponentModel.DataAnnotations.ValidationException>()
            .WithMessage("*Back*maximum*");
    }

    [Fact]
    public async Task CreateCard_AcceptsMaxLengthFields()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var front = new string('x', CardService.MaxFrontLength);
        var back = new string('x', CardService.MaxBackLength);

        var card = await svc.CreateCard(UserId, front, back, "file.md");

        card.Front.Should().HaveLength(CardService.MaxFrontLength);
        card.Back.Should().HaveLength(CardService.MaxBackLength);
    }

    [Fact]
    public async Task RenameSource_RenamesMatchingCards()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "A?", "A.", "old/path.md");
        await svc.CreateCard(UserId, "B?", "B.", "old/path.md");
        await svc.CreateCard(UserId, "C?", "C.", "other.md");

        var result = await svc.RenameSource(UserId, "old/path.md", "new/path.md");

        result.Renamed.Should().Be(2);

        var moved = await svc.ListCards(UserId, sourceFile: "new/path.md", deckId: null, limit: null, after: null);
        moved.Items.Should().HaveCount(2);
        var untouched = await svc.ListCards(UserId, sourceFile: "other.md", deckId: null, limit: null, after: null);
        untouched.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task RenameSource_NoMatch_ReturnsZero()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var result = await svc.RenameSource(UserId, "missing.md", "new.md");

        result.Renamed.Should().Be(0);
    }

    [Fact]
    public async Task RenameSource_EmptyOrSameFromTo_NoOp()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        await svc.CreateCard(UserId, "Q?", "A.", "x.md");

        (await svc.RenameSource(UserId, "", "new.md")).Renamed.Should().Be(0);
        (await svc.RenameSource(UserId, "x.md", "")).Renamed.Should().Be(0);
        (await svc.RenameSource(UserId, "x.md", "x.md")).Renamed.Should().Be(0);
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
