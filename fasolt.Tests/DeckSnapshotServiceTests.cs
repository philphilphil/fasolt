using System.Text.Json;
using FluentAssertions;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class DeckSnapshotServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    // Helper: create a deck with cards and return the deck public ID
    private async Task<(string DeckPublicId, List<string> CardPublicIds)> SeedDeck(
        string name, int cardCount, string? description = null)
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, name, description);
        var cardIds = new List<string>();
        for (var i = 0; i < cardCount; i++)
        {
            var card = await cardSvc.CreateCard(UserId, $"{name} Q{i}", $"{name} A{i}", $"{name}.md", $"## H{i}");
            cardIds.Add(card.Id);
        }
        if (cardIds.Count > 0)
            await deckSvc.AddCards(UserId, deck.Id, cardIds);

        return (deck.Id, cardIds);
    }

    #region Create

    [Fact]
    public async Task CreateAll_CreatesSnapshotForEachNonEmptyDeck()
    {
        var (deck1Id, _) = await SeedDeck("Deck1", 3);
        var (deck2Id, _) = await SeedDeck("Deck2", 2);

        await using var db = _db.CreateDbContext();
        var svc = new DeckSnapshotService(db);

        var count = await svc.CreateAll(UserId);

        count.Should().Be(2);
    }

    [Fact]
    public async Task CreateAll_SkipsEmptyDecks()
    {
        await SeedDeck("NonEmpty", 2);
        await SeedDeck("Empty", 0);

        await using var db = _db.CreateDbContext();
        var svc = new DeckSnapshotService(db);

        var count = await svc.CreateAll(UserId);

        count.Should().Be(1, "empty deck should be skipped");
    }

    [Fact]
    public async Task CreateAll_ReturnsCorrectCount()
    {
        await SeedDeck("A", 1);
        await SeedDeck("B", 1);
        await SeedDeck("C", 1);

        await using var db = _db.CreateDbContext();
        var svc = new DeckSnapshotService(db);

        var count = await svc.CreateAll(UserId);

        count.Should().Be(3);
    }

    [Fact]
    public async Task CreateAll_CapturesAllCardFields()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var deckSvc = new DeckService(db);

        var deck = await deckSvc.CreateDeck(UserId, "Field Test", "Test desc");
        var card = await cardSvc.CreateCard(UserId, "Front", "Back", "source.md", "## Heading");
        await deckSvc.AddCards(UserId, deck.Id, [card.Id]);

        // Set FSRS and suspension state on the card entity directly
        var theCard = db.Cards.First(c => c.UserId == UserId);
        theCard.Stability = 12.5;
        theCard.Difficulty = 0.3;
        theCard.Step = 2;
        theCard.DueAt = DateTimeOffset.Parse("2026-04-01T00:00:00Z");
        theCard.State = "review";
        theCard.LastReviewedAt = DateTimeOffset.Parse("2026-03-20T00:00:00Z");
        theCard.IsSuspended = true;
        await db.SaveChangesAsync();

        var svc = new DeckSnapshotService(db);
        await svc.CreateAll(UserId);

        var snapshots = await svc.ListByDeck(UserId, deck.Id);
        snapshots.Should().HaveCount(1);

        // Read the raw snapshot data
        var snapshot = db.DeckSnapshots.First(s => s.UserId == UserId);
        var data = JsonSerializer.Deserialize<SnapshotData>(snapshot.Data, JsonOptions)!;

        data.Cards.Should().HaveCount(1);
        var sc = data.Cards[0];
        sc.Front.Should().Be("Front");
        sc.Back.Should().Be("Back");
        sc.SourceFile.Should().Be("source.md");
        sc.SourceHeading.Should().Be("## Heading");
        sc.Stability.Should().Be(12.5);
        sc.Difficulty.Should().Be(0.3);
        sc.Step.Should().Be(2);
        sc.State.Should().Be("review");
        sc.IsSuspended.Should().BeTrue();
        sc.DueAt.Should().NotBeNull();
        sc.LastReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAll_StoresDeckNameAndDescription()
    {
        await SeedDeck("Japanese Vocab", 1, "Core vocabulary");

        await using var db = _db.CreateDbContext();
        var svc = new DeckSnapshotService(db);
        await svc.CreateAll(UserId);

        var snapshot = db.DeckSnapshots.First(s => s.UserId == UserId);
        var data = JsonSerializer.Deserialize<SnapshotData>(snapshot.Data, JsonOptions)!;

        data.DeckName.Should().Be("Japanese Vocab");
        data.DeckDescription.Should().Be("Core vocabulary");
    }

    [Fact]
    public async Task CreateAll_EachDeckGetsOwnSnapshot()
    {
        await SeedDeck("Deck A", 2);
        await SeedDeck("Deck B", 3);

        await using var db = _db.CreateDbContext();
        var svc = new DeckSnapshotService(db);
        await svc.CreateAll(UserId);

        var allSnapshots = db.DeckSnapshots.Where(s => s.UserId == UserId).ToList();
        allSnapshots.Should().HaveCount(2);
        allSnapshots.Select(s => s.DeckId).Distinct().Should().HaveCount(2);
        allSnapshots.Should().Contain(s => s.CardCount == 2);
        allSnapshots.Should().Contain(s => s.CardCount == 3);
    }

    #endregion

    #region Retention

    [Fact]
    public async Task CreateAll_11thSnapshotDeletesOldest()
    {
        await SeedDeck("Retention Test", 1);

        for (var i = 0; i < 11; i++)
        {
            await using var db = _db.CreateDbContext();
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using var finalDb = _db.CreateDbContext();
        var count = finalDb.DeckSnapshots.Count(s => s.UserId == UserId);
        count.Should().Be(10, "retention should keep max 10 per deck");
    }

    [Fact]
    public async Task CreateAll_RetentionIsPerDeck()
    {
        await SeedDeck("Deck X", 1);
        await SeedDeck("Deck Y", 1);

        // Create 11 snapshots
        for (var i = 0; i < 11; i++)
        {
            await using var db = _db.CreateDbContext();
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using var finalDb = _db.CreateDbContext();
        var deckIds = finalDb.DeckSnapshots
            .Where(s => s.UserId == UserId)
            .Select(s => s.DeckId)
            .Distinct()
            .ToList();

        foreach (var deckId in deckIds)
        {
            var deckCount = finalDb.DeckSnapshots.Count(s => s.DeckId == deckId);
            deckCount.Should().Be(10, "each deck should independently have max 10 snapshots");
        }
    }

    [Fact]
    public async Task CreateAll_RetentionIndependentPerDeck()
    {
        await SeedDeck("Heavy", 1);
        await SeedDeck("Light", 1);

        // Create 11 snapshots for both
        for (var i = 0; i < 11; i++)
        {
            await using var db = _db.CreateDbContext();
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using var checkDb = _db.CreateDbContext();
        var total = checkDb.DeckSnapshots.Count(s => s.UserId == UserId);
        total.Should().Be(20, "10 per deck x 2 decks");
    }

    #endregion

    #region List

    [Fact]
    public async Task ListByDeck_ReturnsNewestFirst()
    {
        var (deckId, _) = await SeedDeck("List Test", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }
        await Task.Delay(50);
        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using var readDb = _db.CreateDbContext();
        var readSvc = new DeckSnapshotService(readDb);
        var list = await readSvc.ListByDeck(UserId, deckId);

        list.Should().HaveCount(2);
        list[0].CreatedAt.Should().BeAfter(list[1].CreatedAt);
    }

    [Fact]
    public async Task ListByDeck_ReturnsEmptyForUnknownDeck()
    {
        await using var db = _db.CreateDbContext();
        var svc = new DeckSnapshotService(db);

        var list = await svc.ListByDeck(UserId, "nonexistent123");

        list.Should().BeEmpty();
    }

    [Fact]
    public async Task ListRecent_ReturnsAcrossAllDecks()
    {
        await SeedDeck("Recent A", 1);
        await SeedDeck("Recent B", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using var readDb = _db.CreateDbContext();
        var readSvc = new DeckSnapshotService(readDb);
        var list = await readSvc.ListRecent(UserId);

        list.Should().HaveCount(2);
    }

    #endregion

    #region Diff

    [Fact]
    public async Task Diff_DeletedCard_AppearsInDeletedBucket()
    {
        var (deckId, cardIds) = await SeedDeck("Diff Del", 2);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using (var db = _db.CreateDbContext())
        {
            var deckSvc = new DeckService(db);
            await deckSvc.RemoveCards(UserId, deckId, [cardIds[0]]);
        }

        await using var diffDb = _db.CreateDbContext();
        var diffSvc = new DeckSnapshotService(diffDb);
        var snapshots = await diffSvc.ListByDeck(UserId, deckId);
        var diff = await diffSvc.ComputeDiff(UserId, snapshots[0].Id);

        diff.Should().NotBeNull();
        diff!.Deleted.Should().HaveCount(1);
        diff.Deleted[0].Front.Should().Be("Diff Del Q0");
    }

    [Fact]
    public async Task Diff_DeletedCard_ShowsExpectedFields()
    {
        var (deckId, cardIds) = await SeedDeck("Diff Fields", 1);

        await using (var db = _db.CreateDbContext())
        {
            var card = db.Cards.First(c => c.UserId == UserId);
            card.Stability = 8.5;
            card.DueAt = DateTimeOffset.Parse("2026-04-15T00:00:00Z");
            await db.SaveChangesAsync();

            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using (var db = _db.CreateDbContext())
        {
            var cardSvc = new CardService(db);
            await cardSvc.DeleteCards(UserId, [cardIds[0]]);
        }

        await using var diffDb = _db.CreateDbContext();
        var diffSvc = new DeckSnapshotService(diffDb);
        var snapshots = await diffSvc.ListByDeck(UserId, deckId);
        var diff = await diffSvc.ComputeDiff(UserId, snapshots[0].Id);

        diff!.Deleted.Should().HaveCount(1);
        var del = diff.Deleted[0];
        del.Front.Should().Be("Diff Fields Q0");
        del.Back.Should().Be("Diff Fields A0");
        del.SourceFile.Should().Be("Diff Fields.md");
        del.Stability.Should().Be(8.5);
        del.DueAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Diff_BackOnlyChange_FrontUnchangedInResponse()
    {
        var (deckId, cardIds) = await SeedDeck("Diff BackOnly", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        // Only change the back, leave front untouched
        await using (var db = _db.CreateDbContext())
        {
            var card = db.Cards.First(c => c.UserId == UserId);
            card.Back = "Changed back";
            await db.SaveChangesAsync();
        }

        await using var diffDb = _db.CreateDbContext();
        var diffSvc = new DeckSnapshotService(diffDb);
        var snapshots = await diffSvc.ListByDeck(UserId, deckId);
        var diff = await diffSvc.ComputeDiff(UserId, snapshots[0].Id);

        diff!.Modified.Should().HaveCount(1);
        var mod = diff.Modified[0];
        mod.Front.Should().Be(mod.CurrentFront, "front was not changed — snapshot and current should match");
        mod.Back.Should().NotBe(mod.CurrentBack, "back was changed — snapshot and current should differ");
        mod.Back.Should().Be("Diff BackOnly A0");
        mod.CurrentBack.Should().Be("Changed back");
    }

    [Fact]
    public async Task Diff_FrontOnlyChange_BackUnchangedInResponse()
    {
        var (deckId, cardIds) = await SeedDeck("Diff FrontOnly", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        // Only change the front, leave back untouched
        await using (var db = _db.CreateDbContext())
        {
            var card = db.Cards.First(c => c.UserId == UserId);
            card.Front = "Changed front";
            await db.SaveChangesAsync();
        }

        await using var diffDb = _db.CreateDbContext();
        var diffSvc = new DeckSnapshotService(diffDb);
        var snapshots = await diffSvc.ListByDeck(UserId, deckId);
        var diff = await diffSvc.ComputeDiff(UserId, snapshots[0].Id);

        diff!.Modified.Should().HaveCount(1);
        var mod = diff.Modified[0];
        mod.Front.Should().Be("Diff FrontOnly Q0");
        mod.CurrentFront.Should().Be("Changed front");
        mod.Back.Should().Be(mod.CurrentBack, "back was not changed — snapshot and current should match");
    }

    [Fact]
    public async Task Diff_FsrsOnlyChange_NotInModifiedBucket()
    {
        var (deckId, _) = await SeedDeck("Diff FSRS", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        // Only change FSRS state, no content
        await using (var db = _db.CreateDbContext())
        {
            var card = db.Cards.First(c => c.UserId == UserId);
            card.Stability = 99.9;
            card.Difficulty = 0.8;
            card.State = "review";
            await db.SaveChangesAsync();
        }

        await using var diffDb = _db.CreateDbContext();
        var diffSvc = new DeckSnapshotService(diffDb);
        var snapshots = await diffSvc.ListByDeck(UserId, deckId);
        var diff = await diffSvc.ComputeDiff(UserId, snapshots[0].Id);

        diff!.Modified.Should().BeEmpty("FSRS-only changes should be ignored");
    }

    [Fact]
    public async Task Diff_ContentAndFsrsChanges_OnlyContentShown()
    {
        var (deckId, cardIds) = await SeedDeck("Diff Both", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using (var db = _db.CreateDbContext())
        {
            var card = db.Cards.First(c => c.UserId == UserId);
            card.Front = "Changed front";
            card.Stability = 50.0;
            await db.SaveChangesAsync();
        }

        await using var diffDb = _db.CreateDbContext();
        var diffSvc = new DeckSnapshotService(diffDb);
        var snapshots = await diffSvc.ListByDeck(UserId, deckId);
        var diff = await diffSvc.ComputeDiff(UserId, snapshots[0].Id);

        diff!.Modified.Should().HaveCount(1);
        diff.Modified[0].Front.Should().Be("Diff Both Q0");
        diff.Modified[0].CurrentFront.Should().Be("Changed front");
    }

    [Fact]
    public async Task Diff_UnchangedCard_NotInModifiedBucket()
    {
        var (deckId, _) = await SeedDeck("Diff Unchanged", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using var diffDb = _db.CreateDbContext();
        var diffSvc = new DeckSnapshotService(diffDb);
        var snapshots = await diffSvc.ListByDeck(UserId, deckId);
        var diff = await diffSvc.ComputeDiff(UserId, snapshots[0].Id);

        diff!.Modified.Should().BeEmpty();
    }

    [Fact]
    public async Task Diff_AddedCard_AppearsInAddedBucket()
    {
        var (deckId, _) = await SeedDeck("Diff Add", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using (var db = _db.CreateDbContext())
        {
            var cardSvc = new CardService(db);
            var deckSvc = new DeckService(db);
            var newCard = await cardSvc.CreateCard(UserId, "New Q", "New A", null, null);
            await deckSvc.AddCards(UserId, deckId, [newCard.Id]);
        }

        await using var diffDb = _db.CreateDbContext();
        var diffSvc = new DeckSnapshotService(diffDb);
        var snapshots = await diffSvc.ListByDeck(UserId, deckId);
        var diff = await diffSvc.ComputeDiff(UserId, snapshots[0].Id);

        diff!.Added.Should().HaveCount(1);
        diff.Added[0].Front.Should().Be("New Q");
    }

    [Fact]
    public async Task Diff_NoChanges_AllBucketsEmpty()
    {
        var (deckId, _) = await SeedDeck("Diff NoChange", 2);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using var diffDb = _db.CreateDbContext();
        var diffSvc = new DeckSnapshotService(diffDb);
        var snapshots = await diffSvc.ListByDeck(UserId, deckId);
        var diff = await diffSvc.ComputeDiff(UserId, snapshots[0].Id);

        diff!.Deleted.Should().BeEmpty();
        diff.Modified.Should().BeEmpty();
        diff.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Diff_DeletedDeck_HandlesGracefully()
    {
        var (deckId, _) = await SeedDeck("Diff Deleted Deck", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        string snapshotPublicId;
        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var list = await svc.ListByDeck(UserId, deckId);
            snapshotPublicId = list[0].Id;
        }

        await using (var db = _db.CreateDbContext())
        {
            var deckSvc = new DeckService(db);
            await deckSvc.DeleteDeck(UserId, deckId, deleteCards: false);
        }

        await using var diffDb = _db.CreateDbContext();
        var diffSvc = new DeckSnapshotService(diffDb);
        var diff = await diffSvc.ComputeDiff(UserId, snapshotPublicId);

        diff.Should().NotBeNull();
        diff!.Deleted.Should().HaveCount(1);
    }

    #endregion

    #region Restore — Deleted cards

    [Fact]
    public async Task Restore_CardRemovedFromDeck_ReAddsAndUpdates()
    {
        var (deckId, cardIds) = await SeedDeck("Restore Re-add", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using (var db = _db.CreateDbContext())
        {
            var deckSvc = new DeckService(db);
            await deckSvc.RemoveCards(UserId, deckId, [cardIds[0]]);
        }

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var snapshots = await svc.ListByDeck(UserId, deckId);
            var diff = await svc.ComputeDiff(UserId, snapshots[0].Id);
            var result = await svc.Restore(UserId, snapshots[0].Id,
                new RestoreRequest(diff!.Deleted.Select(d => d.CardId).ToList(), []));
            result.Should().BeTrue();
        }

        await using var checkDb = _db.CreateDbContext();
        var deckSvc2 = new DeckService(checkDb);
        var detail = await deckSvc2.GetDeck(UserId, deckId);
        detail!.Cards.Should().HaveCount(1);
        detail.Cards[0].Front.Should().Be("Restore Re-add Q0");
    }

    [Fact]
    public async Task Restore_TrulyDeletedCard_CreatesNewCard()
    {
        var (deckId, cardIds) = await SeedDeck("Restore New", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        string snapshotPublicId;
        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var snapshots = await svc.ListByDeck(UserId, deckId);
            snapshotPublicId = snapshots[0].Id;
        }

        await using (var db = _db.CreateDbContext())
        {
            var cardSvc = new CardService(db);
            await cardSvc.DeleteCards(UserId, [cardIds[0]]);
        }

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var diff = await svc.ComputeDiff(UserId, snapshotPublicId);
            diff!.Deleted.Should().HaveCount(1);

            var result = await svc.Restore(UserId, snapshotPublicId,
                new RestoreRequest(diff.Deleted.Select(d => d.CardId).ToList(), []));
            result.Should().BeTrue();
        }

        await using var checkDb = _db.CreateDbContext();
        var deckSvc = new DeckService(checkDb);
        var detail = await deckSvc.GetDeck(UserId, deckId);
        detail!.Cards.Should().HaveCount(1);
        detail.Cards[0].Front.Should().Be("Restore New Q0");
    }

    [Fact]
    public async Task Restore_PreservesIsSuspendedFromSnapshot()
    {
        var (deckId, cardIds) = await SeedDeck("Restore Suspended", 1);

        await using (var db = _db.CreateDbContext())
        {
            var card = db.Cards.First(c => c.UserId == UserId);
            card.IsSuspended = true;
            await db.SaveChangesAsync();

            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        string snapshotPublicId;
        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var snapshots = await svc.ListByDeck(UserId, deckId);
            snapshotPublicId = snapshots[0].Id;
        }

        await using (var db = _db.CreateDbContext())
        {
            var cardSvc = new CardService(db);
            await cardSvc.DeleteCards(UserId, [cardIds[0]]);
        }

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var diff = await svc.ComputeDiff(UserId, snapshotPublicId);
            await svc.Restore(UserId, snapshotPublicId,
                new RestoreRequest(diff!.Deleted.Select(d => d.CardId).ToList(), []));
        }

        await using var checkDb = _db.CreateDbContext();
        var restoredCard = checkDb.Cards.First(c => c.UserId == UserId);
        restoredCard.IsSuspended.Should().BeTrue();
    }

    #endregion

    #region Restore — Modified cards

    [Fact]
    public async Task Restore_ModifiedCard_RevertsAllFields()
    {
        var (deckId, _) = await SeedDeck("Restore Revert", 1);

        await using (var db = _db.CreateDbContext())
        {
            var card = db.Cards.First(c => c.UserId == UserId);
            card.Stability = 10.0;
            card.State = "review";
            card.IsSuspended = true;
            await db.SaveChangesAsync();

            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using (var db = _db.CreateDbContext())
        {
            var card = db.Cards.First(c => c.UserId == UserId);
            card.Front = "Modified front";
            card.Back = "Modified back";
            card.Stability = 99.0;
            card.State = "learning";
            card.IsSuspended = false;
            await db.SaveChangesAsync();
        }

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var snapshots = await svc.ListByDeck(UserId, deckId);
            var diff = await svc.ComputeDiff(UserId, snapshots[0].Id);
            diff!.Modified.Should().HaveCount(1);

            await svc.Restore(UserId, snapshots[0].Id,
                new RestoreRequest([], diff.Modified.Select(m => m.CardId).ToList()));
        }

        await using var checkDb = _db.CreateDbContext();
        var restored = checkDb.Cards.First(c => c.UserId == UserId);
        restored.Front.Should().Be("Restore Revert Q0");
        restored.Back.Should().Be("Restore Revert A0");
        // FSRS state should NOT be reverted — only content is restored
        restored.Stability.Should().Be(99.0, "FSRS state should be preserved");
        restored.State.Should().Be("learning", "FSRS state should be preserved");
    }

    #endregion

    #region Restore — Validation

    [Fact]
    public async Task Restore_CardIdNotInSnapshot_SilentlySkipped()
    {
        var (deckId, _) = await SeedDeck("Restore Skip", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var snapshots = await svc.ListByDeck(UserId, deckId);
            var result = await svc.Restore(UserId, snapshots[0].Id,
                new RestoreRequest([Guid.NewGuid()], [Guid.NewGuid()]));
            result.Should().BeTrue("restore should succeed even with unknown card IDs");
        }
    }

    [Fact]
    public async Task Restore_SnapshotNotFound_ReturnsFalse()
    {
        await using var db = _db.CreateDbContext();
        var svc = new DeckSnapshotService(db);

        var result = await svc.Restore(UserId, "nonexistent123",
            new RestoreRequest([], []));

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Restore_DeletedDeck_ReturnsFalse()
    {
        var (deckId, _) = await SeedDeck("Restore Deleted Deck", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        string snapshotPublicId;
        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var snapshots = await svc.ListByDeck(UserId, deckId);
            snapshotPublicId = snapshots[0].Id;
        }

        await using (var db = _db.CreateDbContext())
        {
            var deckSvc = new DeckService(db);
            await deckSvc.DeleteDeck(UserId, deckId, deleteCards: false);
        }

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var result = await svc.Restore(UserId, snapshotPublicId,
                new RestoreRequest([], []));
            result.Should().BeFalse();
        }
    }

    #endregion

    #region Restore — Deduplication

    [Fact]
    public async Task Restore_TrulyDeletedCard_RestoresOriginalId()
    {
        var (deckId, cardIds) = await SeedDeck("Restore OrigId", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        string snapshotPublicId;
        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var snapshots = await svc.ListByDeck(UserId, deckId);
            snapshotPublicId = snapshots[0].Id;
        }

        // Truly delete the card
        await using (var db = _db.CreateDbContext())
        {
            var cardSvc = new CardService(db);
            await cardSvc.DeleteCards(UserId, [cardIds[0]]);
        }

        // Restore
        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var diff = await svc.ComputeDiff(UserId, snapshotPublicId);
            diff!.Deleted.Should().HaveCount(1);
            await svc.Restore(UserId, snapshotPublicId,
                new RestoreRequest(diff.Deleted.Select(d => d.CardId).ToList(), []));
        }

        // Verify card is back with its original PublicId
        await using (var db = _db.CreateDbContext())
        {
            var deckSvc = new DeckService(db);
            var detail = await deckSvc.GetDeck(UserId, deckId);
            detail!.Cards.Should().HaveCount(1);
            detail.Cards[0].Id.Should().Be(cardIds[0], "restored card should have its original PublicId");
        }
    }

    [Fact]
    public async Task Restore_TrulyDeletedCard_SecondRestoreSeesNoDeleted()
    {
        var (deckId, cardIds) = await SeedDeck("Restore NoDup", 1);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        string snapshotPublicId;
        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var snapshots = await svc.ListByDeck(UserId, deckId);
            snapshotPublicId = snapshots[0].Id;
        }

        await using (var db = _db.CreateDbContext())
        {
            var cardSvc = new CardService(db);
            await cardSvc.DeleteCards(UserId, [cardIds[0]]);
        }

        // First restore
        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var diff = await svc.ComputeDiff(UserId, snapshotPublicId);
            await svc.Restore(UserId, snapshotPublicId,
                new RestoreRequest(diff!.Deleted.Select(d => d.CardId).ToList(), []));
        }

        // Second diff — card has its original ID back, so no deleted
        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var diff = await svc.ComputeDiff(UserId, snapshotPublicId);
            diff!.Deleted.Should().BeEmpty("card was restored with original ID");
        }
    }

    #endregion

    #region Restore — Integration

    [Fact]
    public async Task Restore_MixedDeletedAndModified_BothApplied()
    {
        var (deckId, cardIds) = await SeedDeck("Restore Mixed", 2);

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            await svc.CreateAll(UserId);
        }

        // Delete first card, modify second
        await using (var db = _db.CreateDbContext())
        {
            var cardSvc = new CardService(db);
            await cardSvc.DeleteCards(UserId, [cardIds[0]]);

            var remaining = db.Cards.Where(c => c.UserId == UserId).ToList();
            foreach (var c in remaining)
            {
                c.Front = "Modified " + c.Front;
            }
            await db.SaveChangesAsync();
        }

        await using (var db = _db.CreateDbContext())
        {
            var svc = new DeckSnapshotService(db);
            var snapshots = await svc.ListByDeck(UserId, deckId);
            var diff = await svc.ComputeDiff(UserId, snapshots[0].Id);

            diff!.Deleted.Should().NotBeEmpty("first card was deleted");
            diff.Modified.Should().NotBeEmpty("second card was modified");

            await svc.Restore(UserId, snapshots[0].Id,
                new RestoreRequest(
                    diff.Deleted.Select(d => d.CardId).ToList(),
                    diff.Modified.Select(m => m.CardId).ToList()));
        }

        await using var checkDb = _db.CreateDbContext();
        var deckSvc2 = new DeckService(checkDb);
        var detail = await deckSvc2.GetDeck(UserId, deckId);
        detail!.Cards.Should().HaveCount(2);
        detail.Cards.Should().Contain(c => c.Front == "Restore Mixed Q0");
        detail.Cards.Should().Contain(c => c.Front == "Restore Mixed Q1");
    }

    #endregion
}
