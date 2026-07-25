using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

/// <summary>
/// Subscribing, unlinking, converting to a copy, and what happens to subscribers
/// when the author takes the deck away.
/// </summary>
public class DeckSubscriptionServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();

    /// <summary>The subscriber. Authors are created per test.</summary>
    private string SubscriberId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    // ---- subscribe ---------------------------------------------------------

    [Fact]
    public async Task Subscribe_LinksTheDeckIntoTheSubscribersDeckList()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-link");
        var deck = await LinkedDeckTestData.AddDeck(db, author, cardCount: 3, name: "Linked Deck");

        var result = await new DeckSubscriptionService(db).Subscribe(SubscriberId, deck.PublicId);

        result.Error.Should().Be(SubscribeError.None);
        result.Created.Should().BeTrue();
        result.Deck!.IsLinked.Should().BeTrue();
        result.Deck.AuthorHandle.Should().Be("author-link");
        result.Deck.CardCount.Should().Be(3);
        result.Deck.DueCount.Should().Be(3, "cards with no ReviewState row are new, and new cards are due");

        var decks = await new DeckService(db).ListDecks(SubscriberId);
        decks.Should().ContainSingle();
        decks[0].Id.Should().Be(deck.PublicId);
        decks[0].IsLinked.Should().BeTrue();
        decks[0].AuthorHandle.Should().Be("author-link");
    }

    [Fact]
    public async Task Subscribe_ResolvesUnlistedDecksButNotPrivateOnes()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-vis");
        var unlisted = await LinkedDeckTestData.AddDeck(db, author, DeckVisibility.Unlisted, "Unlisted");
        var priv = await LinkedDeckTestData.AddDeck(db, author, DeckVisibility.Private, "Private");

        var svc = new DeckSubscriptionService(db);

        (await svc.Subscribe(SubscriberId, unlisted.PublicId)).Error.Should().Be(SubscribeError.None);
        (await svc.Subscribe(SubscriberId, priv.PublicId)).Error.Should().Be(SubscribeError.NotFound);
    }

    [Fact]
    public async Task Subscribe_ToYourOwnDeck_IsRejected()
    {
        await using var db = _db.CreateDbContext();
        var deck = await LinkedDeckTestData.AddDeck(db, SubscriberId, name: "Mine");

        var result = await new DeckSubscriptionService(db).Subscribe(SubscriberId, deck.PublicId);

        result.Error.Should().Be(SubscribeError.OwnDeck);
        (await db.DeckSubscriptions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Subscribe_Twice_IsIdempotentAndKeepsTheExistingPause()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-idem");
        var deck = await LinkedDeckTestData.AddDeck(db, author, cardCount: 1);
        var svc = new DeckSubscriptionService(db);

        await svc.Subscribe(SubscriberId, deck.PublicId);
        await new DeckService(db).SetSuspended(SubscriberId, deck.PublicId, true);

        var second = await svc.Subscribe(SubscriberId, deck.PublicId);

        second.Error.Should().Be(SubscribeError.None);
        second.Created.Should().BeFalse();
        second.Deck!.IsSuspended.Should().BeTrue("a repeat subscribe must not resume a paused link");
        (await db.DeckSubscriptions.CountAsync(s => s.UserId == SubscriberId)).Should().Be(1);
    }

    // ---- unlink ------------------------------------------------------------

    [Fact]
    public async Task Unsubscribe_DropsTheLinkAndOnlyTheReviewStatesItLeavesOrphaned()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-unlink");

        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Unlinked", cardCount: 0);
        var soloCard = LinkedDeckTestData.AddCard(db, deck, "Solo", "A");
        var sharedCard = LinkedDeckTestData.AddCard(db, deck, "Shared", "A");
        await db.SaveChangesAsync();

        // The same card also sits in a second deck the subscriber links.
        var otherDeck = await LinkedDeckTestData.AddDeck(db, author, name: "Other");
        db.DeckCards.Add(new DeckCard { DeckId = otherDeck.Id, CardId = sharedCard.Id });
        await db.SaveChangesAsync();

        await LinkedDeckTestData.Subscribe(db, SubscriberId, deck);
        await LinkedDeckTestData.Subscribe(db, SubscriberId, otherDeck);

        var ownCard = await new CardService(db).CreateCard(SubscriberId, "Own", "A", null);
        var ownCardId = await db.Cards.Where(c => c.PublicId == ownCard.Id).Select(c => c.Id).FirstAsync();

        foreach (var cardId in new[] { soloCard.Id, sharedCard.Id, ownCardId })
        {
            var state = await db.ReviewStateFor(SubscriberId, cardId);
            state.State = "review";
            state.DueAt = DateTimeOffset.UtcNow.AddDays(1);
        }
        await db.SaveChangesAsync();

        var removed = await new DeckSubscriptionService(db).Unsubscribe(SubscriberId, deck.PublicId);

        removed.Should().BeTrue();

        await using var verify = _db.CreateDbContext();
        (await verify.DeckSubscriptions.AnyAsync(s => s.DeckId == deck.Id)).Should().BeFalse();
        var remaining = await verify.ReviewStates
            .Where(r => r.UserId == SubscriberId)
            .Select(r => r.CardId)
            .ToListAsync();

        remaining.Should().NotContain(soloCard.Id, "the unlinked deck was the only route to it");
        remaining.Should().Contain(sharedCard.Id, "another linked deck still contains it");
        remaining.Should().Contain(ownCardId, "authored cards are never touched by unlinking");
    }

    [Fact]
    public async Task Unsubscribe_WhenNotSubscribed_ReportsNothingRemoved()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-none");
        var deck = await LinkedDeckTestData.AddDeck(db, author);

        (await new DeckSubscriptionService(db).Unsubscribe(SubscriberId, deck.PublicId)).Should().BeFalse();
    }

    // ---- convert to copy ---------------------------------------------------

    [Fact]
    public async Task ConvertToCopy_ClonesTheDeckAndPreservesSrsExactly()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-convert");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Convert Me", cardCount: 0);
        var studied = LinkedDeckTestData.AddCard(db, deck, "Studied", "A", "vault/secret.md");
        var untouched = LinkedDeckTestData.AddCard(db, deck, "Untouched", "B", "vault/secret.md");
        await db.SaveChangesAsync();
        await LinkedDeckTestData.Subscribe(db, SubscriberId, deck);

        var due = DateTimeOffset.UtcNow.AddDays(4);
        var reviewed = DateTimeOffset.UtcNow.AddHours(-2);
        var state = await db.ReviewStateFor(SubscriberId, studied.Id);
        state.State = "review";
        state.Stability = 12.5;
        state.Difficulty = 4.25;
        state.Step = 2;
        state.DueAt = due;
        state.LastReviewedAt = reviewed;
        state.IsSuspended = true;
        await db.SaveChangesAsync();

        var result = await new DeckSubscriptionService(db).ConvertToCopy(SubscriberId, deck.PublicId);

        result.Error.Should().Be(ConvertToCopyError.None);
        result.Deck!.IsLinked.Should().BeFalse();
        result.Deck.CardCount.Should().Be(2);
        result.Deck.CopiedFromDeckPublicId.Should().Be(deck.PublicId);
        result.Deck.CopiedFromHandle.Should().Be("author-convert");

        await using var verify = _db.CreateDbContext();

        var copy = await verify.Decks.FirstAsync(d => d.PublicId == result.Deck.Id);
        copy.UserId.Should().Be(SubscriberId);

        var copiedCards = await verify.DeckCards
            .Where(dc => dc.DeckId == copy.Id)
            .Select(dc => dc.Card)
            .ToListAsync();

        copiedCards.Should().HaveCount(2);
        copiedCards.Should().OnlyContain(c => c.UserId == SubscriberId);
        copiedCards.Should().OnlyContain(c => c.SourceFile == null, "the author's vault path is not copied");
        copiedCards.Select(c => c.Id).Should().NotContain([studied.Id, untouched.Id]);

        var copiedStudied = copiedCards.Single(c => c.Front == "Studied");
        var copiedState = await verify.ReviewStates
            .SingleAsync(r => r.UserId == SubscriberId && r.CardId == copiedStudied.Id);

        copiedState.State.Should().Be("review");
        copiedState.Stability.Should().Be(12.5);
        copiedState.Difficulty.Should().Be(4.25);
        copiedState.Step.Should().Be(2);
        copiedState.DueAt.Should().BeCloseTo(due, TimeSpan.FromMilliseconds(1));
        copiedState.LastReviewedAt.Should().BeCloseTo(reviewed, TimeSpan.FromMilliseconds(1));
        copiedState.IsSuspended.Should().BeTrue();

        // The card that was never studied stays new — no row is invented for it.
        var copiedUntouched = copiedCards.Single(c => c.Front == "Untouched");
        (await verify.ReviewStates.AnyAsync(r => r.CardId == copiedUntouched.Id)).Should().BeFalse();

        // The link is gone, and so is the state that belonged to it.
        (await verify.DeckSubscriptions.AnyAsync(s => s.UserId == SubscriberId)).Should().BeFalse();
        (await verify.ReviewStates.AnyAsync(r => r.UserId == SubscriberId && r.CardId == studied.Id))
            .Should().BeFalse();

        // A conversion counts as an import.
        (await verify.Decks.Where(d => d.Id == deck.Id).Select(d => d.CopyCount).FirstAsync())
            .Should().Be(1);
    }

    [Fact]
    public async Task ConvertToCopy_KeepsStateForCardsStillReachableThroughAnotherLink()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-both");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "First", cardCount: 0);
        var card = LinkedDeckTestData.AddCard(db, deck, "Shared", "A");
        await db.SaveChangesAsync();

        var otherDeck = await LinkedDeckTestData.AddDeck(db, author, name: "Second");
        db.DeckCards.Add(new DeckCard { DeckId = otherDeck.Id, CardId = card.Id });
        await db.SaveChangesAsync();

        await LinkedDeckTestData.Subscribe(db, SubscriberId, deck);
        await LinkedDeckTestData.Subscribe(db, SubscriberId, otherDeck);

        var state = await db.ReviewStateFor(SubscriberId, card.Id);
        state.State = "review";
        state.Stability = 3.0;
        await db.SaveChangesAsync();

        await new DeckSubscriptionService(db).ConvertToCopy(SubscriberId, deck.PublicId);

        await using var verify = _db.CreateDbContext();
        var states = await verify.ReviewStates.Where(r => r.UserId == SubscriberId).ToListAsync();

        states.Should().HaveCount(2, "the copy gets its own row and the still-linked original keeps its own");
        states.Should().OnlyContain(r => r.State == "review" && r.Stability == 3.0);
    }

    [Fact]
    public async Task ConvertToCopy_WithoutASubscription_IsNotFound()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-nosub");
        var deck = await LinkedDeckTestData.AddDeck(db, author, cardCount: 1);

        var result = await new DeckSubscriptionService(db).ConvertToCopy(SubscriberId, deck.PublicId);

        result.Error.Should().Be(ConvertToCopyError.NotFound);
    }

    // ---- owner-side lifecycle ---------------------------------------------

    [Fact]
    public async Task OwnerGoingPrivate_RemovesEverySubscriptionAndCleansUpState()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-unpub");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Going Private", cardCount: 0);
        var card = LinkedDeckTestData.AddCard(db, deck, "Q", "A");
        await db.SaveChangesAsync();
        await LinkedDeckTestData.Subscribe(db, SubscriberId, deck);

        var state = await db.ReviewStateFor(SubscriberId, card.Id);
        state.State = "review";
        await db.SaveChangesAsync();

        var result = await new PublishingService(db).SetVisibility(author, deck.PublicId, DeckVisibility.Private);
        result.Error.Should().Be(SetVisibilityError.None);

        await using var verify = _db.CreateDbContext();
        (await verify.DeckSubscriptions.AnyAsync(s => s.DeckId == deck.Id)).Should().BeFalse();
        (await verify.ReviewStates.AnyAsync(r => r.UserId == SubscriberId)).Should().BeFalse();
        (await new DeckService(verify).ListDecks(SubscriberId)).Should().BeEmpty();
    }

    [Fact]
    public async Task AdminUnlist_RemovesEverySubscription()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-unlist");
        var deck = await LinkedDeckTestData.AddDeck(db, author, cardCount: 1);
        await LinkedDeckTestData.Subscribe(db, SubscriberId, deck);

        (await new PublishingService(db).Unlist(deck.PublicId)).Should().BeTrue();

        await using var verify = _db.CreateDbContext();
        (await verify.DeckSubscriptions.AnyAsync(s => s.DeckId == deck.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task OwnerDeletingTheDeck_RemovesSubscriptionsAndOrphanedState()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-delete");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Doomed", cardCount: 0);
        var card = LinkedDeckTestData.AddCard(db, deck, "Q", "A");
        await db.SaveChangesAsync();
        await LinkedDeckTestData.Subscribe(db, SubscriberId, deck);

        var state = await db.ReviewStateFor(SubscriberId, card.Id);
        state.State = "review";
        await db.SaveChangesAsync();

        var result = await new DeckService(db).DeleteDeck(author, deck.PublicId);
        result.Deleted.Should().BeTrue();

        await using var verify = _db.CreateDbContext();
        (await verify.DeckSubscriptions.AnyAsync()).Should().BeFalse();
        (await verify.ReviewStates.AnyAsync(r => r.UserId == SubscriberId)).Should().BeFalse();
        (await verify.Cards.AnyAsync(c => c.Id == card.Id)).Should().BeTrue("deleting a deck keeps its cards");
    }

    [Fact]
    public async Task OwnerDeletingASingleCard_CascadesTheSubscribersReviewState()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-cardgone");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Shrinking", cardCount: 0);
        var card = LinkedDeckTestData.AddCard(db, deck, "Q", "A");
        var kept = LinkedDeckTestData.AddCard(db, deck, "Kept", "A");
        await db.SaveChangesAsync();
        await LinkedDeckTestData.Subscribe(db, SubscriberId, deck);

        foreach (var id in new[] { card.Id, kept.Id })
        {
            var state = await db.ReviewStateFor(SubscriberId, id);
            state.State = "review";
        }
        await db.SaveChangesAsync();

        var deleted = await new CardService(db).DeleteCard(author, card.PublicId);
        deleted.Should().BeTrue();

        await using var verify = _db.CreateDbContext();
        var states = await verify.ReviewStates.Where(r => r.UserId == SubscriberId).ToListAsync();
        states.Should().ContainSingle().Which.CardId.Should().Be(kept.Id);
    }

    /// <summary>
    /// The whole loop from the design's testing section: link, study, the owner edits
    /// and deletes content, then unpublishes.
    /// </summary>
    [Fact]
    public async Task LinkedDeckLifecycle_SubscribeStudyOwnerEditsThenUnpublish()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-lifecycle");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Lifecycle", cardCount: 0);
        var edited = LinkedDeckTestData.AddCard(db, deck, "Before", "Old answer");
        var doomed = LinkedDeckTestData.AddCard(db, deck, "Doomed", "A");
        await db.SaveChangesAsync();

        var subscriptions = new DeckSubscriptionService(db);
        await subscriptions.Subscribe(SubscriberId, deck.PublicId);

        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
            new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var review = new ReviewService(db, time, new StudyStatsService(db, time));

        var dueBefore = await review.GetDueCards(SubscriberId);
        dueBefore.Should().HaveCount(2);

        (await review.RateCard(SubscriberId, new Server.Application.Dtos.RateCardRequest(edited.PublicId, "good")))
            .Should().NotBeNull();

        // The owner edits a card: the subscriber sees the new text on the same row,
        // and their SRS state survives.
        await new CardService(db).UpdateCardFields(author, edited.PublicId,
            new Server.Application.Dtos.UpdateCardFieldsRequest("After", "New answer", null, null, null));

        var detail = await new DeckService(db).GetDeck(SubscriberId, deck.PublicId);
        detail!.IsLinked.Should().BeTrue();
        detail.Cards.Should().Contain(c => c.Front == "After" && c.State == "learning");

        // The owner deletes the other card: it leaves the subscriber's deck too.
        await new CardService(db).DeleteCard(author, doomed.PublicId);
        (await new DeckService(db).GetDeck(SubscriberId, deck.PublicId))!.Cards.Should().ContainSingle();

        // Unpublishing takes the whole deck away.
        await new PublishingService(db).SetVisibility(author, deck.PublicId, DeckVisibility.Private);

        await using var verify = _db.CreateDbContext();
        (await new DeckService(verify).ListDecks(SubscriberId)).Should().BeEmpty();
        (await verify.ReviewStates.AnyAsync(r => r.UserId == SubscriberId)).Should().BeFalse();
    }
}
