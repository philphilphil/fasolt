using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

/// <summary>
/// Golden-data guard for the ReviewState split (issue #133, PR 1/4).
///
/// Seeds one fixed account covering every SRS shape — new / learning / review /
/// relearning, suspended cards, suspended decks, a card in two decks, cards with and
/// without a source file — and asserts the due queue, overview counts, deck counts,
/// source counts and study stats against values written out literally. The numbers
/// below were derived from the pre-refactor behaviour (SRS columns on Card) and must
/// not move when the state lives in ReviewStates instead.
/// </summary>
public class ReviewStateGoldenDataTests : IAsyncLifetime
{
    private readonly TestDb _db = new();

    // OverviewService/DeckService/SourceService read DateTimeOffset.UtcNow directly, so
    // the fixture is anchored to the real clock and the fake clock is set to match.
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private FakeTimeProvider _time = null!;

    private string UserId => _db.UserId;

    private Guid _deckAId;
    private Guid _deckBId;
    private string _deckAPublicId = null!;
    private string _deckBPublicId = null!;

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _time = new FakeTimeProvider(_now);
        await SeedAsync();
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SeedAsync()
    {
        await using var db = _db.CreateDbContext();

        _deckAId = Guid.NewGuid();
        _deckBId = Guid.NewGuid();
        _deckAPublicId = "deckaaaaaaaa";
        _deckBPublicId = "deckbbbbbbbb";

        db.Decks.Add(new Deck
        {
            Id = _deckAId,
            PublicId = _deckAPublicId,
            UserId = UserId,
            Name = "Deck A",
            CreatedAt = _now.AddDays(-20),
        });
        db.Decks.Add(new Deck
        {
            Id = _deckBId,
            PublicId = _deckBPublicId,
            UserId = UserId,
            Name = "Deck B",
            CreatedAt = _now.AddDays(-20),
            IsSuspended = true,
        });

        // (publicId, sourceFile, createdAt, decks)
        var newNoDeck = AddCard(db, "newnodeck", "a.md", _now.AddDays(-10));
        var newDeckA = AddCard(db, "newdecka", "a.md", _now.AddDays(-9), _deckAId);
        var learnDue = AddCard(db, "learndue", "b.md", _now.AddDays(-8), _deckAId);
        var revFuture = AddCard(db, "revfuture", null, _now.AddDays(-7), _deckAId);
        var revDue = AddCard(db, "revdue", "c.md", _now.AddDays(-6));
        var suspDue = AddCard(db, "suspdue", "b.md", _now.AddDays(-5), _deckAId);
        var relDeckB = AddCard(db, "reldeckb", "d.md", _now.AddDays(-4), _deckBId);
        var newDeckB = AddCard(db, "newdeckb", null, _now.AddDays(-3), _deckBId);
        var revBoth = AddCard(db, "revboth", null, _now.AddDays(-2), _deckAId, _deckBId);

        // newNoDeck / newDeckA / newDeckB stay pristine-new — deliberately no rows.
        _ = newNoDeck;
        _ = newDeckA;
        _ = newDeckB;

        db.ReviewStates.AddRange(
            State(learnDue, "learning", stability: 2.1, difficulty: 5.0, step: 1,
                dueAt: _now.AddHours(-1), lastReviewedAt: _now),
            State(revFuture, "review", stability: 20.0, difficulty: 4.0, step: null,
                dueAt: _now.AddDays(3), lastReviewedAt: _now),
            State(revDue, "review", stability: 9.0, difficulty: 6.0, step: null,
                dueAt: _now.AddDays(-1), lastReviewedAt: _now.AddDays(-10)),
            State(suspDue, "review", stability: 7.0, difficulty: 5.5, step: null,
                dueAt: _now.AddDays(-1), lastReviewedAt: _now, isSuspended: true),
            State(relDeckB, "relearning", stability: 1.0, difficulty: 7.0, step: 0,
                dueAt: _now.AddHours(-2), lastReviewedAt: _now),
            State(revBoth, "review", stability: 30.0, difficulty: 3.0, step: null,
                dueAt: _now.AddMinutes(-30), lastReviewedAt: _now));

        // Five reviews total, three of them "today" (logged at exactly `now`).
        db.ReviewLogs.AddRange(
            Log(learnDue, "good", _now),
            Log(revFuture, "easy", _now),
            Log(revBoth, "good", _now),
            Log(revDue, "good", _now.AddDays(-10)),
            Log(revDue, "hard", _now.AddDays(-10)));

        await db.SaveChangesAsync();
    }

    private Guid AddCard(AppDbContext db, string publicId, string? sourceFile,
        DateTimeOffset createdAt, params Guid[] deckIds)
    {
        var id = Guid.NewGuid();
        db.Cards.Add(new Card
        {
            Id = id,
            PublicId = publicId,
            UserId = UserId,
            SourceFile = sourceFile,
            Front = $"{publicId}-front",
            Back = $"{publicId}-back",
            CreatedAt = createdAt,
        });
        foreach (var deckId in deckIds)
            db.DeckCards.Add(new DeckCard { DeckId = deckId, CardId = id });
        return id;
    }

    private ReviewState State(Guid cardId, string state, double? stability, double? difficulty,
        int? step, DateTimeOffset? dueAt, DateTimeOffset? lastReviewedAt, bool isSuspended = false) =>
        new()
        {
            UserId = UserId,
            CardId = cardId,
            State = state,
            Stability = stability,
            Difficulty = difficulty,
            Step = step,
            DueAt = dueAt,
            LastReviewedAt = lastReviewedAt,
            IsSuspended = isSuspended,
        };

    private ReviewLog Log(Guid cardId, string rating, DateTimeOffset reviewedAt) =>
        new()
        {
            UserId = UserId,
            CardId = cardId,
            Rating = rating,
            ReviewedAt = reviewedAt,
            ScheduledDueAfter = reviewedAt.AddDays(1),
            StateAfter = "review",
        };

    private ReviewService CreateReviewService(AppDbContext db)
        => new(db, _time, new StudyStatsService(db, _time));

    [Fact]
    public async Task DueQueue_MatchesGoldenOrderAndStates()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateReviewService(db);

        var due = await svc.GetDueCards(UserId, limit: 50);

        // Ordered by due date (nulls last, then by CreatedAt). Excluded:
        // revfuture (not due), suspdue (suspended), reldeckb + newdeckb (only deck suspended).
        due.Select(c => c.Id).Should().Equal("revdue", "learndue", "revboth", "newnodeck", "newdecka");
        due.Select(c => c.State).Should().Equal("review", "learning", "review", "new", "new");
    }

    [Fact]
    public async Task DueQueue_DeckScoped_MatchesGoldenOrder()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateReviewService(db);

        var due = await svc.GetDueCards(UserId, limit: 50, deckId: _deckAPublicId);

        due.Select(c => c.Id).Should().Equal("learndue", "revboth", "newdecka");
    }

    [Fact]
    public async Task DueQueue_SuspendedDeck_ReturnsNothing()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateReviewService(db);

        var due = await svc.GetDueCards(UserId, limit: 50, deckId: _deckBPublicId);

        due.Should().BeEmpty();
    }

    [Fact]
    public async Task ReviewStats_MatchGoldenCounts()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateReviewService(db);

        var stats = await svc.GetStats(UserId);

        stats.TotalCards.Should().Be(6);   // 9 cards − 1 suspended − 2 in the suspended deck only
        stats.DueCount.Should().Be(5);
        stats.StudiedToday.Should().Be(3); // learndue, revfuture, revboth
    }

    [Fact]
    public async Task Overview_MatchesGoldenCounts()
    {
        await using var db = _db.CreateDbContext();
        var svc = new OverviewService(db);

        var overview = await svc.GetOverview(UserId);

        overview.TotalCards.Should().Be(6);
        overview.DueCards.Should().Be(5);
        overview.CardsByState["new"].Should().Be(2);
        overview.CardsByState["learning"].Should().Be(1);
        overview.CardsByState["review"].Should().Be(3);
        overview.CardsByState["relearning"].Should().Be(0);
        overview.TotalDecks.Should().Be(2);
        overview.TotalSources.Should().Be(3); // a.md, b.md, c.md — d.md is deck-suspended only
    }

    [Fact]
    public async Task DeckCounts_MatchGoldenCounts()
    {
        await using var db = _db.CreateDbContext();
        var svc = new DeckService(db);

        var decks = await svc.ListDecks(UserId);

        var deckA = decks.Single(d => d.Id == _deckAPublicId);
        deckA.CardCount.Should().Be(5);
        deckA.DueCount.Should().Be(3); // newdecka, learndue, revboth

        var deckB = decks.Single(d => d.Id == _deckBPublicId);
        deckB.CardCount.Should().Be(3);
        deckB.DueCount.Should().Be(3); // deck suspension does not affect the per-deck due count

        var detail = await svc.GetDeck(UserId, _deckAPublicId);
        detail!.CardCount.Should().Be(5);
        detail.DueCount.Should().Be(3);
        detail.Cards.Single(c => c.Id == "suspdue").IsSuspended.Should().BeTrue();
        detail.Cards.Single(c => c.Id == "newdecka").State.Should().Be("new");
        detail.Cards.Single(c => c.Id == "newdecka").DueAt.Should().BeNull();
        detail.Cards.Single(c => c.Id == "learndue").Stability.Should().Be(2.1);
    }

    [Fact]
    public async Task Sources_MatchGoldenCounts()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SourceService(db);

        var sources = (await svc.ListSources(UserId)).Items;

        sources.Select(s => s.SourceFile).Should().Equal("a.md", "b.md", "c.md", "d.md");
        sources.Select(s => s.CardCount).Should().Equal(2, 2, 1, 1);
        // b.md holds learndue (due) + suspdue (due but suspended); a.md's cards are new
        // and therefore have no DueAt, which never counted here.
        sources.Select(s => s.DueCount).Should().Equal(0, 1, 1, 1);
    }

    [Fact]
    public async Task StudyStats_MatchGoldenCounts()
    {
        await using var db = _db.CreateDbContext();
        var svc = new StudyStatsService(db, _time);

        var stats = await svc.GetStats(UserId);

        stats.TotalAnswered.Should().Be(5);
        stats.AnsweredToday.Should().Be(3);
        stats.Last7Days.Should().HaveCount(7);
        stats.Last7Days[^1].Count.Should().Be(3);
        stats.Last7Days[^1].HadDue.Should().BeTrue();
    }

    [Fact]
    public async Task ListCards_SrsProjection_MatchesGoldenValues()
    {
        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);

        var page = await svc.ListCards(UserId, sourceFile: null, deckId: null,
            limit: 200, after: null, include: ["srs"]);

        var learnDue = page.Items.Single(c => c.Id == "learndue");
        learnDue.State.Should().Be("learning");
        learnDue.Stability.Should().Be(2.1);
        learnDue.Difficulty.Should().Be(5.0);
        learnDue.Step.Should().Be(1);
        learnDue.IsSuspended.Should().BeFalse();
        learnDue.DueAt.Should().NotBeNull();

        var newNoDeck = page.Items.Single(c => c.Id == "newnodeck");
        newNoDeck.State.Should().Be("new");
        newNoDeck.Stability.Should().BeNull();
        newNoDeck.Difficulty.Should().BeNull();
        newNoDeck.Step.Should().BeNull();
        newNoDeck.DueAt.Should().BeNull();
        newNoDeck.LastReviewedAt.Should().BeNull();
        newNoDeck.IsSuspended.Should().BeFalse();

        page.Items.Single(c => c.Id == "suspdue").IsSuspended.Should().BeTrue();
    }
}
