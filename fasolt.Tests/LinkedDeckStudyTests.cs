using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

/// <summary>
/// Studying a linked deck: the queue, per-user suspension, the pause precedence
/// rules, and the counts and search results that have to include linked cards
/// without ever exposing the author's <c>SourceFile</c>.
/// </summary>
public class LinkedDeckStudyTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

    /// <summary>The subscriber.</summary>
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private ReviewService CreateReviewService(AppDbContext db)
        => new(db, _time, new StudyStatsService(db, _time));

    /// <summary>An author with one published two-card deck the test user has linked.</summary>
    private async Task<(string AuthorId, Deck Deck, Card First, Card Second)> SeedLinkedDeck(
        AppDbContext db, string handle = "author")
    {
        var authorId = await LinkedDeckTestData.AddUser(db, handle);
        var deck = await LinkedDeckTestData.AddDeck(db, authorId, name: "Linked", cardCount: 0);
        var first = LinkedDeckTestData.AddCard(db, deck, "Linked Q1", "A1", "vault/author.md");
        var second = LinkedDeckTestData.AddCard(db, deck, "Linked Q2", "A2", "vault/author.md");
        await db.SaveChangesAsync();
        await LinkedDeckTestData.Subscribe(db, UserId, deck);
        return (authorId, deck, first, second);
    }

    // ---- due queue ---------------------------------------------------------

    [Fact]
    public async Task DueCards_IncludeLinkedCardsAsNew_WithoutTheAuthorsSourceFile()
    {
        await using var db = _db.CreateDbContext();
        await SeedLinkedDeck(db);
        await new CardService(db).CreateCard(UserId, "Own Q", "A", "vault/mine.md");

        var due = await CreateReviewService(db).GetDueCards(UserId);

        due.Should().HaveCount(3);
        due.Where(c => c.Front.StartsWith("Linked")).Should().OnlyContain(c => c.State == "new");
        due.Where(c => c.Front.StartsWith("Linked")).Should().OnlyContain(c => c.SourceFile == null);
        due.Single(c => c.Front == "Own Q").SourceFile.Should().Be("vault/mine.md");
    }

    [Fact]
    public async Task DueCards_FilteredByLinkedDeck_ReturnThatDecksCards()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _, _) = await SeedLinkedDeck(db);
        await new CardService(db).CreateCard(UserId, "Own Q", "A", null);

        var due = await CreateReviewService(db).GetDueCards(UserId, deckId: deck.PublicId);

        due.Should().HaveCount(2);
        due.Should().OnlyContain(c => c.Front.StartsWith("Linked"));
    }

    [Fact]
    public async Task CustomStudy_WorksOnALinkedDeck()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _, _) = await SeedLinkedDeck(db);

        var cards = await CreateReviewService(db).GetCustomStudyCards(UserId, deck.PublicId);

        cards.Should().NotBeNull();
        cards!.Should().HaveCount(2);
        cards.Should().OnlyContain(c => c.SourceFile == null);
    }

    [Fact]
    public async Task RateCard_OnALinkedCard_WritesOnlyTheSubscribersState()
    {
        await using var db = _db.CreateDbContext();
        var (authorId, _, first, _) = await SeedLinkedDeck(db);

        var result = await CreateReviewService(db).RateCard(UserId, new RateCardRequest(first.PublicId, "good"));

        result.Should().NotBeNull();
        result!.State.Should().Be("learning");

        await using var verify = _db.CreateDbContext();
        var states = await verify.ReviewStates.Where(r => r.CardId == first.Id).ToListAsync();
        states.Should().ContainSingle().Which.UserId.Should().Be(UserId);
        (await verify.ReviewLogs.CountAsync(r => r.UserId == UserId)).Should().Be(1);
        (await verify.ReviewLogs.CountAsync(r => r.UserId == authorId)).Should().Be(0);
    }

    // ---- suspension --------------------------------------------------------

    [Fact]
    public async Task CardSuspension_IsPerUser()
    {
        await using var db = _db.CreateDbContext();
        var (authorId, _, first, _) = await SeedLinkedDeck(db);

        // The subscriber suspends a linked card — their own ReviewState, not the author's.
        var suspended = await new CardService(db).SetSuspended(UserId, first.PublicId, true);
        suspended.Should().NotBeNull();
        suspended!.IsSuspended.Should().BeTrue();
        suspended.SourceFile.Should().BeNull("a linked card never shows the author's vault path");

        var review = CreateReviewService(db);
        (await review.GetDueCards(UserId)).Should().ContainSingle().Which.Front.Should().Be("Linked Q2");
        (await review.GetDueCards(authorId)).Should().HaveCount(2, "the author is unaffected");
    }

    [Fact]
    public async Task OwnerDeckPause_DoesNotReachSubscribers()
    {
        await using var db = _db.CreateDbContext();
        var (authorId, deck, _, _) = await SeedLinkedDeck(db);

        await new DeckService(db).SetSuspended(authorId, deck.PublicId, true);

        var review = CreateReviewService(db);
        (await review.GetDueCards(authorId)).Should().BeEmpty("the owner paused their own deck");
        (await review.GetDueCards(UserId)).Should().HaveCount(2, "the owner's pause is not the subscriber's");
        (await review.GetDueCards(UserId, deckId: deck.PublicId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task SubscriberDeckPause_HidesTheLinkedDeckFromTheirQueueOnly()
    {
        await using var db = _db.CreateDbContext();
        var (authorId, deck, _, _) = await SeedLinkedDeck(db);

        var paused = await new DeckService(db).SetSuspended(UserId, deck.PublicId, true);

        paused.Should().NotBeNull();
        paused!.IsSuspended.Should().BeTrue();
        paused.IsLinked.Should().BeTrue();

        await using var verify = _db.CreateDbContext();
        (await verify.DeckSubscriptions.SingleAsync(s => s.UserId == UserId)).IsSuspended.Should().BeTrue();
        (await verify.Decks.SingleAsync(d => d.Id == deck.Id)).IsSuspended
            .Should().BeFalse("the owner's own flag is untouched");

        var review = CreateReviewService(verify);
        (await review.GetDueCards(UserId)).Should().BeEmpty();
        (await review.GetDueCards(UserId, deckId: deck.PublicId)).Should().BeEmpty();
        (await review.GetDueCards(authorId)).Should().HaveCount(2);
    }

    [Fact]
    public async Task PausingALinkedDeck_LeavesOtherSubscribersAlone()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _, _) = await SeedLinkedDeck(db);
        var otherSubscriber = await LinkedDeckTestData.AddUser(db);
        await LinkedDeckTestData.Subscribe(db, otherSubscriber, deck);

        await new DeckService(db).SetSuspended(UserId, deck.PublicId, true);

        var review = CreateReviewService(db);
        (await review.GetDueCards(UserId)).Should().BeEmpty();
        (await review.GetDueCards(otherSubscriber)).Should().HaveCount(2);
    }

    // ---- deck detail -------------------------------------------------------

    [Fact]
    public async Task DeckDetail_ForALinkedDeck_IsMarkedLinkedAndHidesSourceFiles()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, first, _) = await SeedLinkedDeck(db, "author-detail");

        var state = await db.ReviewStateFor(UserId, first.Id);
        state.State = "review";
        // GetDeck compares against the wall clock, not the fake review clock.
        state.DueAt = DateTimeOffset.UtcNow.AddDays(2);
        await db.SaveChangesAsync();

        var detail = await new DeckService(db).GetDeck(UserId, deck.PublicId);

        detail.Should().NotBeNull();
        detail!.IsLinked.Should().BeTrue();
        detail.AuthorHandle.Should().Be("author-detail");
        detail.CardCount.Should().Be(2);
        detail.DueCount.Should().Be(1, "the reviewed card is scheduled into the future");
        detail.Cards.Should().OnlyContain(c => c.SourceFile == null);
        detail.Cards.Should().Contain(c => c.Front == "Linked Q1" && c.State == "review");
    }

    // ---- counts and search -------------------------------------------------

    [Fact]
    public async Task Overview_CountsLinkedCardsAndDecksButNotTheAuthorsSources()
    {
        await using var db = _db.CreateDbContext();
        await SeedLinkedDeck(db);
        await new CardService(db).CreateCard(UserId, "Own Q", "A", "vault/mine.md");

        var overview = await new OverviewService(db).GetOverview(UserId);

        overview.TotalCards.Should().Be(3);
        overview.DueCards.Should().Be(3);
        overview.CardsByState["new"].Should().Be(3);
        overview.TotalDecks.Should().Be(1, "the linked deck counts as one of the user's decks");
        overview.TotalSources.Should().Be(1, "only the user's own source files are counted");
    }

    [Fact]
    public async Task ReviewStats_IncludeLinkedCards()
    {
        await using var db = _db.CreateDbContext();
        await SeedLinkedDeck(db);

        var stats = await CreateReviewService(db).GetStats(UserId);

        stats.TotalCards.Should().Be(2);
        stats.DueCount.Should().Be(2);
    }

    [Fact]
    public async Task Search_FindsLinkedCardsAndLinkedDecks()
    {
        await using var db = _db.CreateDbContext();
        await SeedLinkedDeck(db);

        var result = await new SearchService(db).Search(UserId, "Linked");

        result.Cards.Should().HaveCount(2);
        result.Decks.Should().ContainSingle().Which.Headline.Should().Be("Linked");
    }

    [Fact]
    public async Task Sources_NeverListTheAuthorsVaultPaths()
    {
        await using var db = _db.CreateDbContext();
        await SeedLinkedDeck(db);
        await new CardService(db).CreateCard(UserId, "Own Q", "A", "vault/mine.md");

        var sources = await new SourceService(db).ListSources(UserId);

        sources.Items.Should().ContainSingle().Which.SourceFile.Should().Be("vault/mine.md");
    }

    [Fact]
    public async Task ListCards_StaysScopedToAuthoredCards()
    {
        await using var db = _db.CreateDbContext();
        await SeedLinkedDeck(db);
        await new CardService(db).CreateCard(UserId, "Own Q", "A", null);

        var cards = await new CardService(db).ListCards(UserId, null, null, null, null);

        cards.Items.Should().ContainSingle().Which.Front.Should().Be("Own Q");
    }

    [Fact]
    public async Task ListCards_FilteredByALinkedDeck_ServesThatDecksCards()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _, _) = await SeedLinkedDeck(db);
        await new CardService(db).CreateCard(UserId, "Own Q", "A", null);

        var cards = await new CardService(db).ListCards(UserId, null, deck.PublicId, null, null);

        cards.Items.Should().HaveCount(2, "list_decks advertises the linked deck and its card "
            + "count, so listing it must return its cards rather than an empty success");
        cards.Items.Should().OnlyContain(c => c.Front.StartsWith("Linked"));
        cards.Items.Should().OnlyContain(c => c.IsLinked);
        // The author's vault path stays with the author.
        cards.Items.Should().OnlyContain(c => c.SourceFile == null);
    }

    [Fact]
    public async Task ListCards_FilteredByALinkedDeckAndASourceFile_ReturnsNothing()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _, _) = await SeedLinkedDeck(db);

        var cards = await new CardService(db).ListCards(UserId, "vault/author.md", deck.PublicId, null, null);

        cards.Items.Should().BeEmpty(
            "a linked card reports no sourceFile, so nothing can match one — and the author's "
            + "vault paths must not be probeable through the filter");
    }

    [Fact]
    public async Task ListCards_FilteredByAnUnknownDeck_ReturnsNothing()
    {
        await using var db = _db.CreateDbContext();
        await new CardService(db).CreateCard(UserId, "Own Q", "A", null);

        var cards = await new CardService(db).ListCards(UserId, null, "does-not-exist", null, null);

        cards.Items.Should().BeEmpty("an unresolvable deck must not widen to the whole collection");
    }

    [Fact]
    public async Task GetCard_MarksLinkedCardsSoTheUiCanHideMutations()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, linked, _) = await SeedLinkedDeck(db);
        var ownDeck = await new DeckService(db).CreateDeck(UserId, "Mine", null);
        var own = await new CardService(db).CreateCard(UserId, "Own Q", "A", null, deckId: ownDeck.Id);
        var svc = new CardService(db);

        var linkedDto = await svc.GetCard(UserId, linked.PublicId);
        linkedDto!.IsLinked.Should().BeTrue();
        linkedDto.Decks.Should().ContainSingle().Which.Id.Should().Be(deck.PublicId);

        var ownDto = await svc.GetCard(UserId, own.Id);
        ownDto!.IsLinked.Should().BeFalse();
        ownDto.Decks.Should().ContainSingle().Which.Id.Should().Be(ownDeck.Id);
    }

    // ---- notification counts -----------------------------------------------

    [Fact]
    public async Task DueCardSummary_CountsLinkedCardsLikeTheStudyQueue()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _, _) = await SeedLinkedDeck(db);
        await new CardService(db).CreateCard(UserId, "Own Q", "A", null);

        var summary = await DueCardQuery.GetDueCardSummary(db, UserId, _time.GetUtcNow());

        var queue = await CreateReviewService(db).GetDueCards(UserId);
        summary.TotalDue.Should().Be(queue.Count).And.Be(3);
        summary.Breakdown.Should().Contain(deck.Name).And.Contain("Unsorted");
    }

    [Fact]
    public async Task DueCardSummary_RespectsTheSubscribersOwnPause()
    {
        await using var db = _db.CreateDbContext();
        var (_, deck, _, _) = await SeedLinkedDeck(db);
        await new DeckService(db).SetSuspended(UserId, deck.PublicId, true);

        var summary = await DueCardQuery.GetDueCardSummary(db, UserId, _time.GetUtcNow());

        summary.TotalDue.Should().Be(0);
        summary.Breakdown.Should().BeEmpty();
    }
}
