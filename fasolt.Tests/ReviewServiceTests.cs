using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class ReviewServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private ReviewService CreateService(Server.Infrastructure.Data.AppDbContext db)
        => new(db, _time);

    private async Task<string> CreateCard(Server.Infrastructure.Data.AppDbContext db, string front, string back)
    {
        var cardSvc = new CardService(db);
        var card = await cardSvc.CreateCard(UserId, front, back, null, null);
        return card.Id;
    }

    // --- RateCard tests ---

    [Fact]
    public async Task RateCard_InvalidRating_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Q?", "A.");

        var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "invalid"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task RateCard_CardNotFound_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.RateCard(UserId, new RateCardRequest("nonexistent", "good"));

        result.Should().BeNull();
    }

    [Fact]
    public async Task RateCard_Good_MovesToLearning()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Q?", "A.");

        var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "good"));

        result.Should().NotBeNull();
        result!.State.Should().Be("learning");
        result.Stability.Should().BeGreaterThan(0);
        result.DueAt.Should().BeAfter(_time.GetUtcNow());
    }

    [Fact]
    public async Task RateCard_Easy_MovesToReview()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Q?", "A.");

        var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "easy"));

        result.Should().NotBeNull();
        result!.State.Should().Be("review");
    }

    // --- Lapse and recovery ---

    [Fact]
    public async Task Lapse_CardEntersRelearningAndRecovers()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Lapse Q?", "Lapse A.");

        // Rate easy to get into review
        var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "easy"));
        result!.State.Should().Be("review");
        var stabilityAfterEasy = result.Stability!.Value;

        // Build stability with good reviews
        for (var i = 0; i < 3; i++)
        {
            _time.SetUtcNow(result!.DueAt!.Value.AddMinutes(1));
            result = await svc.RateCard(UserId, new RateCardRequest(cardId, "good"));
            result!.State.Should().Be("review");
        }
        var stabilityBeforeLapse = result!.Stability!.Value;
        stabilityBeforeLapse.Should().BeGreaterThan(stabilityAfterEasy);

        // Lapse: rate again
        _time.SetUtcNow(result.DueAt!.Value.AddMinutes(1));
        result = await svc.RateCard(UserId, new RateCardRequest(cardId, "again"));
        result!.State.Should().Be("relearning");
        result.Stability!.Value.Should().BeLessThan(stabilityBeforeLapse);

        // Recover
        for (var i = 0; i < 5 && result!.State != "review"; i++)
        {
            _time.SetUtcNow(result.DueAt!.Value.AddMinutes(1));
            result = await svc.RateCard(UserId, new RateCardRequest(cardId, "good"));
        }
        result!.State.Should().Be("review");
    }

    // --- GetDueCards tests ---

    [Fact]
    public async Task GetDueCards_ReturnsNewCards()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Due Q?", "Due A.");

        var due = await svc.GetDueCards(UserId);

        due.Should().ContainSingle(c => c.Id == cardId);
    }

    [Fact]
    public async Task GetDueCards_ExcludesFutureCards()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Q?", "A.");

        // Rate easy to push due date into the future
        var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "easy"));

        var due = await svc.GetDueCards(UserId);
        due.Should().NotContain(c => c.Id == cardId);
    }

    [Fact]
    public async Task GetDueCards_RespectsTimeProgression()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var start = _time.GetUtcNow();

        var card1Id = await CreateCard(db, "Due 1d Q?", "Due 1d A.");
        var card2Id = await CreateCard(db, "Due 7d Q?", "Due 7d A.");

        // Rate both easy to get into review
        await svc.RateCard(UserId, new RateCardRequest(card1Id, "easy"));
        await svc.RateCard(UserId, new RateCardRequest(card2Id, "easy"));

        // Manually set due dates for precise control
        var card1 = await db.Cards.FindAsync(db.Cards.First(c => c.PublicId == card1Id).Id);
        var card2 = await db.Cards.FindAsync(db.Cards.First(c => c.PublicId == card2Id).Id);
        card1!.DueAt = start.AddDays(1);
        card2!.DueAt = start.AddDays(7);
        await db.SaveChangesAsync();

        // Day 2: only card1 due
        _time.SetUtcNow(start.AddDays(2));
        var due = await svc.GetDueCards(UserId);
        due.Should().ContainSingle(c => c.Id == card1Id);

        // Day 8: both due
        _time.SetUtcNow(start.AddDays(8));
        due = await svc.GetDueCards(UserId);
        due.Select(c => c.Id).Should().Contain(card1Id).And.Contain(card2Id);
    }

    [Fact]
    public async Task GetDueCards_NonexistentDeck_ReturnsNull()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.GetDueCards(UserId, deckId: "nonexistent");

        result.Should().BeNull();
    }

    // --- GetStats tests ---

    [Fact]
    public async Task GetStats_CountsDueAndTotal()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        await CreateCard(db, "Q1?", "A1.");
        await CreateCard(db, "Q2?", "A2.");

        var stats = await svc.GetStats(UserId);

        stats.TotalCards.Should().Be(2);
        stats.DueCount.Should().Be(2); // new cards are immediately due
        stats.StudiedToday.Should().Be(0);
    }

    [Fact]
    public async Task GetStats_TracksStudiedToday()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Q?", "A.");

        await svc.RateCard(UserId, new RateCardRequest(cardId, "good"));

        var stats = await svc.GetStats(UserId);
        stats.StudiedToday.Should().Be(1);
    }

    // --- 6-month simulation (ported from FsrsFullFlowTests) ---

    [Fact]
    public async Task SixMonthSimulation_CardsScheduledCorrectly()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);
        var start = _time.GetUtcNow();

        // Create 5 cards
        var cardIds = new List<string>();
        for (var i = 1; i <= 5; i++)
            cardIds.Add(await CreateCard(db, $"Q{i}?", $"A{i}."));

        var statesReached = cardIds.ToDictionary(id => id, _ => new HashSet<string>());
        var lapsedCards = new HashSet<string>();
        var reviewCounts = cardIds.ToDictionary(id => id, _ => 0);
        var consecutiveGoodIntervals = cardIds.ToDictionary(id => id, _ => new List<TimeSpan>());

        for (var day = 0; day < 180; day++)
        {
            _time.SetUtcNow(start.AddDays(day));
            var dueCards = await svc.GetDueCards(UserId, limit: 200);

            foreach (var dueCard in dueCards)
            {
                var totalReviews = reviewCounts[dueCard.Id]++;
                var previousState = dueCard.State;

                string rating;
                if (previousState == "review" && totalReviews == 3)
                    rating = "again";
                else if (previousState == "review" && totalReviews % 7 == 0)
                    rating = "easy";
                else
                    rating = "good";

                var result = await svc.RateCard(UserId, new RateCardRequest(dueCard.Id, rating));
                result.Should().NotBeNull();

                statesReached[dueCard.Id].Add(result!.State);

                if (previousState == "review" && result.State == "relearning")
                {
                    lapsedCards.Add(dueCard.Id);
                    consecutiveGoodIntervals[dueCard.Id].Clear();
                }

                if (rating == "good" && result.State == "review")
                {
                    var interval = result.DueAt!.Value - _time.GetUtcNow();
                    consecutiveGoodIntervals[dueCard.Id].Add(interval);
                }

                result.Stability.Should().BeGreaterThan(0);
                result.DueAt.Should().BeAfter(_time.GetUtcNow());
                result.State.Should().BeOneOf("learning", "review", "relearning");
            }
        }

        foreach (var (id, states) in statesReached)
            states.Should().Contain("review", $"card {id} should reach review state");

        lapsedCards.Should().NotBeEmpty();

        foreach (var (id, intervals) in consecutiveGoodIntervals)
        {
            if (intervals.Count < 3) continue;
            var lastThree = intervals.Skip(intervals.Count - 3).ToList();
            for (var i = 1; i < lastThree.Count; i++)
                lastThree[i].Should().BeGreaterThanOrEqualTo(lastThree[i - 1]);
        }
    }

    [Fact]
    public async Task RateCard_UsesCustomRetention_WhenSet()
    {
        await using var db = _db.CreateDbContext();

        // Set custom retention on user
        var user = await db.Users.FirstAsync(u => u.Id == UserId);
        user.DesiredRetention = 0.80;
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var cardId = await CreateCard(db, "Custom Q?", "Custom A.");

        // Rate easy to get into review state with a scheduled interval
        var result = await svc.RateCard(UserId, new RateCardRequest(cardId, "easy"));
        result.Should().NotBeNull();
        result!.DueAt.Should().BeAfter(_time.GetUtcNow());

        // Now rate again with default retention for comparison
        await using var db2 = _db.CreateDbContext();
        var user2 = await db2.Users.FirstAsync(u => u.Id == UserId);
        user2.DesiredRetention = null; // reset to default
        await db2.SaveChangesAsync();

        await using var db3 = _db.CreateDbContext();
        var svc2 = CreateService(db3);
        var cardId2 = await CreateCard(db3, "Default Q?", "Default A.");
        var result2 = await svc2.RateCard(UserId, new RateCardRequest(cardId2, "easy"));

        // Lower retention (0.80) should produce longer intervals than default (0.9)
        var interval1 = result.DueAt!.Value - _time.GetUtcNow();
        var interval2 = result2!.DueAt!.Value - _time.GetUtcNow();
        interval1.Should().BeGreaterThan(interval2);
    }
}
