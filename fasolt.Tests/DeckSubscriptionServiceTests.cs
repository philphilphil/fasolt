using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
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
    public async Task Subscribe_ToADeckOverTheCardCap_IsRejected()
    {
        // Unlisted decks are uncapped at publish time, so without this a subscriber
        // could link an unbounded deck — and convert-to-copy, which does enforce the
        // cap, would refuse it forever after.
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-huge");
        var deck = await LinkedDeckTestData.AddDeck(
            db, author, DeckVisibility.Unlisted, "Huge",
            cardCount: PublishingService.MaxCardsInPublicDeck + 1);

        var result = await new DeckSubscriptionService(db).Subscribe(SubscriberId, deck.PublicId);

        result.Error.Should().Be(SubscribeError.DeckTooLarge);
        result.Created.Should().BeFalse();

        await using var verify = _db.CreateDbContext();
        (await verify.DeckSubscriptions.CountAsync(s => s.UserId == SubscriberId)).Should().Be(0);
    }

    [Fact]
    public async Task Subscribe_ToADeckExactlyAtTheCardCap_IsAllowed()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-at-cap");
        var deck = await LinkedDeckTestData.AddDeck(
            db, author, DeckVisibility.Public, "At Cap",
            cardCount: PublishingService.MaxCardsInPublicDeck);

        var result = await new DeckSubscriptionService(db).Subscribe(SubscriberId, deck.PublicId);

        result.Error.Should().Be(SubscribeError.None);
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

    /// <summary>
    /// A subscribe that overlaps an unpublish must not leave a live link to a private
    /// deck: nothing re-checks visibility for an existing subscription, so such a row
    /// would grant permanent access with no surface for the owner to revoke it.
    /// </summary>
    [Fact]
    public async Task Subscribe_WaitsForAnInFlightUnpublish_AndThenFindsNothing()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-race");
        var deck = await LinkedDeckTestData.AddDeck(db, author, cardCount: 1);

        // Stands in for an unpublish that has written the visibility but not yet
        // committed — exactly the window the row lock has to cover.
        await using var unpublisher = _db.CreateDbContext();
        await using var unpublish = await unpublisher.Database.BeginTransactionAsync();
        await unpublisher.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Decks" SET "Visibility" = 'Private', "PublishedAt" = NULL WHERE "Id" = {deck.Id}
            """);

        await using var joiner = _db.CreateDbContext();
        var subscribe = new DeckSubscriptionService(joiner).Subscribe(SubscriberId, deck.PublicId);

        await Task.Delay(300);
        subscribe.IsCompleted.Should().BeFalse("the deck row is locked by the in-flight unpublish");

        await unpublish.CommitAsync();

        (await subscribe).Error.Should().Be(SubscribeError.NotFound);

        await using var verify = _db.CreateDbContext();
        (await verify.DeckSubscriptions.AnyAsync(s => s.DeckId == deck.Id)).Should().BeFalse();
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
    public async Task ConvertToCopy_MovesTheReviewHistoryOntoTheCopy()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-logs");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Logged", cardCount: 0);
        var card = LinkedDeckTestData.AddCard(db, deck, "Q", "A");
        await db.SaveChangesAsync();
        await LinkedDeckTestData.Subscribe(db, SubscriberId, deck);

        db.ReviewLogs.Add(new ReviewLog
        {
            UserId = SubscriberId,
            CardId = card.Id,
            Rating = "good",
            ReviewedAt = DateTimeOffset.UtcNow.AddDays(-1),
            ScheduledDueAfter = DateTimeOffset.UtcNow.AddDays(2),
            StateAfter = "review",
        });
        await db.SaveChangesAsync();

        var result = await new DeckSubscriptionService(db).ConvertToCopy(SubscriberId, deck.PublicId);
        result.Error.Should().Be(ConvertToCopyError.None);

        await using var verify = _db.CreateDbContext();
        var copiedCardId = await verify.DeckCards
            .Where(dc => dc.Deck.PublicId == result.Deck!.Id)
            .Select(dc => dc.CardId)
            .SingleAsync();

        var log = await verify.ReviewLogs.SingleAsync(r => r.UserId == SubscriberId);
        log.CardId.Should().Be(copiedCardId,
            "history left on the author's card cascade-deletes with it, silently shrinking "
            + "the converter's totals for a deck they now own outright");

        // The author deleting the original must no longer touch the converter's history.
        await verify.Cards.Where(c => c.Id == card.Id).ExecuteDeleteAsync();
        (await verify.ReviewLogs.CountAsync(r => r.UserId == SubscriberId)).Should().Be(1);
    }

    [Fact]
    public async Task ConvertToCopy_ReportsTheScheduledDueCount_NotEveryCard()
    {
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-due");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Scheduled", cardCount: 0);
        var scheduled = LinkedDeckTestData.AddCard(db, deck, "Later", "A");
        LinkedDeckTestData.AddCard(db, deck, "Now", "B");
        await db.SaveChangesAsync();
        await LinkedDeckTestData.Subscribe(db, SubscriberId, deck);

        var state = await db.ReviewStateFor(SubscriberId, scheduled.Id);
        state.State = "review";
        state.DueAt = DateTimeOffset.UtcNow.AddDays(7);
        await db.SaveChangesAsync();

        var result = await new DeckSubscriptionService(db).ConvertToCopy(SubscriberId, deck.PublicId);

        result.Deck!.CardCount.Should().Be(2);
        result.Deck.DueCount.Should().Be(1, "the copy inherits the caller's schedule");

        // Same number the deck list computes for the copy from its ReviewState rows.
        await using var verify = _db.CreateDbContext();
        var listed = (await new DeckService(verify).ListDecks(SubscriberId))
            .Single(d => d.Id == result.Deck.Id);
        listed.DueCount.Should().Be(result.Deck.DueCount);
    }

    [Fact]
    public async Task ConvertToCopy_DatesEachCardFromWhenItBecameTheSubscribersToStudy()
    {
        // The SRS state and the review history move onto the clones, so their CreatedAt
        // has to keep meaning what it meant while the deck was linked — the later of the
        // author's creation date and the subscription. Stamping the conversion time
        // instead would put every carried-over review before its own card existed and
        // rewrite the streak; keeping the author's date on a years-old deck would
        // repaint days the subscriber never had the card as days they missed.
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-dates");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Dated", cardCount: 0);
        var ancient = LinkedDeckTestData.AddCard(db, deck, "Ancient", "A");
        ancient.CreatedAt = DateTimeOffset.UtcNow.AddDays(-400);
        await db.SaveChangesAsync();

        var subscription = await LinkedDeckTestData.Subscribe(db, SubscriberId, deck);

        // The author adds a card after the link exists — that one keeps its own date.
        var later = LinkedDeckTestData.AddCard(db, deck, "Later", "B");
        later.CreatedAt = subscription.SubscribedAt.AddDays(3);
        await db.SaveChangesAsync();

        var result = await new DeckSubscriptionService(db).ConvertToCopy(SubscriberId, deck.PublicId);
        result.Error.Should().Be(ConvertToCopyError.None);

        await using var verify = _db.CreateDbContext();
        var copied = await verify.DeckCards
            .Where(dc => dc.Deck.PublicId == result.Deck!.Id)
            .Select(dc => dc.Card)
            .ToListAsync();

        copied.Single(c => c.Front == "Ancient").CreatedAt
            .Should().BeCloseTo(subscription.SubscribedAt, TimeSpan.FromMilliseconds(1),
                "the subscription is when this card became the subscriber's to study");
        copied.Single(c => c.Front == "Later").CreatedAt
            .Should().BeCloseTo(later.CreatedAt, TimeSpan.FromMilliseconds(1),
                "a card added while linked was only studyable from its own creation");
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

    [Fact]
    public async Task OwnerDeletingACard_KeepsTheSubscribersReviewHistoryAndStreak()
    {
        // The ReviewLog row belongs to the reviewer, not to the author's card. Under a
        // cascade the author's delete would retroactively shrink every subscriber's
        // streak and totals for reviews they really did.
        await using var db = _db.CreateDbContext();
        var author = await LinkedDeckTestData.AddUser(db, "author-history");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Historic", cardCount: 0);
        var doomed = LinkedDeckTestData.AddCard(db, deck, "Doomed", "A");
        LinkedDeckTestData.AddCard(db, deck, "Kept", "A");
        await db.SaveChangesAsync();
        await LinkedDeckTestData.Subscribe(db, SubscriberId, deck);

        var time = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(
            new DateTimeOffset(2025, 6, 2, 5, 0, 0, TimeSpan.Zero));
        var stats = new StudyStatsService(db, time);
        var review = new ReviewService(db, time, stats);

        (await review.RateCard(SubscriberId, new Server.Application.Dtos.RateCardRequest(doomed.PublicId, "good")))
            .Should().NotBeNull();

        var before = await stats.GetStats(SubscriberId);
        before.TotalAnswered.Should().Be(1);
        before.CurrentStreak.Should().Be(1);

        (await new CardService(db).DeleteCard(author, doomed.PublicId)).Should().BeTrue();

        await using var verify = _db.CreateDbContext();
        var log = await verify.ReviewLogs.SingleAsync(r => r.UserId == SubscriberId);
        log.CardId.Should().BeNull("the card is gone but the review still happened");

        var after = await new StudyStatsService(verify, time).GetStats(SubscriberId);
        after.TotalAnswered.Should().Be(before.TotalAnswered);
        after.CurrentStreak.Should().Be(before.CurrentStreak);
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
