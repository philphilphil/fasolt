using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

/// <summary>
/// A subscriber may study linked content but never change it. Every mutation path
/// raises <see cref="LinkedContentException"/>, which the card and deck endpoint
/// groups translate into a 403 — as opposed to the 404 an unrelated deck gets.
/// </summary>
public class LinkedDeckAuthorizationTests : IAsyncLifetime
{
    private readonly TestDb _db = new();

    /// <summary>The subscriber.</summary>
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task<(string AuthorId, Deck Deck, Card Card)> SeedLinkedDeck(AppDbContext db)
    {
        var authorId = await LinkedDeckTestData.AddUser(db, $"a{Guid.NewGuid().ToString("N")[..8]}");
        var deck = await LinkedDeckTestData.AddDeck(db, authorId, name: "Author's Deck", cardCount: 0);
        var card = LinkedDeckTestData.AddCard(db, deck, "Author Q", "Author A", "vault/author.md");
        await db.SaveChangesAsync();
        await LinkedDeckTestData.Subscribe(db, UserId, deck);
        return (authorId, deck, card);
    }

    // ---- card mutations ----------------------------------------------------

    [Fact]
    public async Task EditingALinkedCard_IsForbidden()
    {
        await using var db = _db.CreateDbContext();
        var (_, _, card) = await SeedLinkedDeck(db);
        var svc = new CardService(db);

        var update = async () => await svc.UpdateCard(UserId, card.PublicId,
            new UpdateCardRequest("Hijacked", "Hijacked", null, null, null, null));
        await update.Should().ThrowAsync<LinkedContentException>();

        var fields = async () => await svc.UpdateCardFields(UserId, card.PublicId,
            new UpdateCardFieldsRequest("Hijacked", null, null, null, null));
        await fields.Should().ThrowAsync<LinkedContentException>();

        var bulk = async () => await svc.BulkUpdateCards(UserId,
            [new BulkUpdateCardItem(card.PublicId, "Hijacked", null, null, null, null)]);
        await bulk.Should().ThrowAsync<LinkedContentException>();

        await using var verify = _db.CreateDbContext();
        (await verify.Cards.SingleAsync(c => c.Id == card.Id)).Front.Should().Be("Author Q");
    }

    [Fact]
    public async Task DeletingALinkedCard_IsForbidden_SingleAndBulk()
    {
        await using var db = _db.CreateDbContext();
        var (_, _, card) = await SeedLinkedDeck(db);
        var own = await new CardService(db).CreateCard(UserId, "Own", "A", null);
        var svc = new CardService(db);

        var single = async () => await svc.DeleteCard(UserId, card.PublicId);
        await single.Should().ThrowAsync<LinkedContentException>();

        var bulk = async () => await svc.DeleteCards(UserId, [own.Id, card.PublicId]);
        await bulk.Should().ThrowAsync<LinkedContentException>();

        await using var verify = _db.CreateDbContext();
        (await verify.Cards.CountAsync()).Should().Be(2,
            "a batch that touches linked content must not delete the caller's own cards either");
    }

    [Fact]
    public async Task BulkUpdate_TouchingLinkedContent_LeavesTheCallersOwnCardsUntouched()
    {
        await using var db = _db.CreateDbContext();
        var (_, _, linked) = await SeedLinkedDeck(db);
        var svc = new CardService(db);
        var first = await svc.CreateCard(UserId, "Own 1", "A1", null);
        var last = await svc.CreateCard(UserId, "Own 2", "A2", null);

        // The linked card sits in the middle: without an up-front check the first
        // item is already committed by the time the batch fails.
        var bulk = async () => await svc.BulkUpdateCards(UserId,
        [
            new BulkUpdateCardItem(first.Id, NewBack: "Changed 1"),
            new BulkUpdateCardItem(linked.PublicId, NewBack: "Hijacked"),
            new BulkUpdateCardItem(last.Id, NewBack: "Changed 2"),
        ]);
        await bulk.Should().ThrowAsync<LinkedContentException>();

        await using var verify = _db.CreateDbContext();
        var backs = await verify.Cards.Select(c => c.Back).ToListAsync();
        backs.Should().BeEquivalentTo(["Author A", "A1", "A2"],
            "a batch rejected for linked content must not have applied any of its items");
    }

    [Fact]
    public async Task CreatingCardsIntoALinkedDeck_IsForbidden_NotReportedAsMissing()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _) = await SeedLinkedDeck(db);
        var svc = new CardService(db);

        var single = async () => await svc.CreateCard(UserId, "Own", "A", null, deckId: deck.PublicId);
        await single.Should().ThrowAsync<LinkedContentException>();

        var bulk = async () => await svc.BulkCreateCards(
            UserId, [new BulkCardItem("Own", "A")], null, deck.PublicId);
        await bulk.Should().ThrowAsync<LinkedContentException>();

        await using var verify = _db.CreateDbContext();
        (await verify.DeckCards.CountAsync(dc => dc.DeckId == deck.Id)).Should().Be(1);
    }

    [Fact]
    public async Task AddingAnOwnCardToALinkedDeck_IsForbidden()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _) = await SeedLinkedDeck(db);
        var own = await new CardService(db).CreateCard(UserId, "Own", "A", null);

        var viaDeck = async () => await new DeckService(db).AddCards(UserId, deck.PublicId, [own.Id]);
        await viaDeck.Should().ThrowAsync<LinkedContentException>();

        var viaCard = async () => await new CardService(db).UpdateCard(UserId, own.Id,
            new UpdateCardRequest("Own", "A", DeckIds: [deck.PublicId]));
        await viaCard.Should().ThrowAsync<LinkedContentException>();

        await using var verify = _db.CreateDbContext();
        (await verify.DeckCards.CountAsync(dc => dc.DeckId == deck.Id)).Should().Be(1);
    }

    [Fact]
    public async Task UnknownIdsStillReport404Semantics()
    {
        await using var db = _db.CreateDbContext();
        await SeedLinkedDeck(db);
        var cards = new CardService(db);
        var decks = new DeckService(db);

        (await cards.DeleteCard(UserId, "nope")).Should().BeFalse();
        (await cards.UpdateCard(UserId, "nope", new UpdateCardRequest("a", "b", null, null, null, null)))
            .Should().BeNull();
        (await decks.UpdateDeck(UserId, "nope", "x", null)).Should().BeNull();
        (await decks.SetSuspended(UserId, "nope", true)).Should().BeNull();
    }

    // ---- deck mutations ----------------------------------------------------

    [Fact]
    public async Task RenamingOrDeletingALinkedDeck_IsForbidden()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _) = await SeedLinkedDeck(db);
        var svc = new DeckService(db);

        var rename = async () => await svc.UpdateDeck(UserId, deck.PublicId, "Mine now", null);
        await rename.Should().ThrowAsync<LinkedContentException>();

        var delete = async () => await svc.DeleteDeck(UserId, deck.PublicId);
        await delete.Should().ThrowAsync<LinkedContentException>();

        await using var verify = _db.CreateDbContext();
        var stored = await verify.Decks.SingleAsync(d => d.Id == deck.Id);
        stored.Name.Should().Be("Author's Deck");
    }

    [Fact]
    public async Task RemovingCardsFromALinkedDeck_IsForbidden_SingleAndBulk()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, card) = await SeedLinkedDeck(db);
        var svc = new DeckService(db);

        var single = async () => await svc.RemoveCard(UserId, deck.PublicId, card.PublicId);
        await single.Should().ThrowAsync<LinkedContentException>();

        var bulk = async () => await svc.RemoveCards(UserId, deck.PublicId, [card.PublicId]);
        await bulk.Should().ThrowAsync<LinkedContentException>();

        await using var verify = _db.CreateDbContext();
        (await verify.DeckCards.CountAsync(dc => dc.DeckId == deck.Id)).Should().Be(1);
    }

    [Fact]
    public async Task PublishingALinkedDeck_IsForbidden()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _) = await SeedLinkedDeck(db);

        var publish = async () => await new PublishingService(db)
            .SetVisibility(UserId, deck.PublicId, DeckVisibility.Public);

        await publish.Should().ThrowAsync<LinkedContentException>();
    }

    // ---- snapshots ---------------------------------------------------------

    [Fact]
    public async Task SnapshotsCoverOwnDecksOnly()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _) = await SeedLinkedDeck(db);
        var ownDeck = await new DeckService(db).CreateDeck(UserId, "My Deck", null);
        await new CardService(db).CreateCard(UserId, "Own", "A", null, deckId: ownDeck.Id);

        var svc = new DeckSnapshotService(db);
        var result = await svc.CreateAll(UserId);

        result.Created.Should().Be(1);
        result.CreatedDecks.Should().ContainSingle().Which.Should().Be("My Deck");
        (await svc.ListByDeck(UserId, deck.PublicId)).Should().BeEmpty();

        await using var verify = _db.CreateDbContext();
        (await verify.DeckSnapshots.CountAsync(s => s.DeckId == deck.Id)).Should().Be(0);
    }
}
