using FluentAssertions;
using FSRS.Core.Configurations;
using FSRS.Core.Enums;
using FSRS.Core.Interfaces;
using FSRS.Core.Services;
using FsrsCard = FSRS.Core.Models.Card;

namespace Fasolt.Tests;

public class FsrsSchedulingTests
{
    private readonly IScheduler _scheduler;

    public FsrsSchedulingTests()
    {
        var options = new SchedulerOptions
        {
            DesiredRetention = 0.9,
            MaximumInterval = 36500,
            EnableFuzzing = false, // deterministic for tests
        };
        var factory = new SchedulerFactory(options);
        _scheduler = factory.CreateScheduler();
    }

    [Fact]
    public void NewCard_RatedGood_EntersLearningState()
    {
        var card = new FsrsCard();
        var now = DateTime.UtcNow;

        var (updated, reviewLog) = _scheduler.ReviewCard(card, Rating.Good, now, null);

        updated.State.Should().Be(State.Learning);
        updated.Stability.Should().NotBeNull();
        updated.Stability.Should().BeGreaterThan(0);
        updated.Difficulty.Should().NotBeNull();
        updated.Difficulty.Should().BeGreaterThan(0);
        updated.Due.Should().BeAfter(now);
    }

    [Fact]
    public void NewCard_RatedAgain_EntersLearningState()
    {
        var card = new FsrsCard();
        var now = DateTime.UtcNow;

        var (updated, _) = _scheduler.ReviewCard(card, Rating.Again, now, null);

        updated.State.Should().Be(State.Learning);
        updated.Stability.Should().NotBeNull();
        updated.Stability.Should().BeGreaterThan(0);
        updated.Due.Should().BeAfter(now);
        // "Again" should have a short interval (within minutes)
        updated.Due.Should().BeBefore(now.AddHours(1));
    }

    [Fact]
    public void NewCard_RatedEasy_EntersReviewState()
    {
        var card = new FsrsCard();
        var now = DateTime.UtcNow;

        var (easy, _) = _scheduler.ReviewCard(card, Rating.Easy, now, null);
        var (good, _) = _scheduler.ReviewCard(new FsrsCard(), Rating.Good, now, null);

        // Easy should go directly to Review state (skipping learning steps)
        easy.State.Should().Be(State.Review);
        easy.Stability.Should().NotBeNull();
        easy.Stability.Should().BeGreaterThan(good.Stability!.Value,
            "Easy rating should produce higher stability than Good");
    }

    [Fact]
    public void ReviewCard_RatedAgain_EntersRelearning()
    {
        // First, get a card into Review state by rating Easy on a new card
        var card = new FsrsCard();
        var now = DateTime.UtcNow;
        var (reviewCard, _) = _scheduler.ReviewCard(card, Rating.Easy, now, null);
        reviewCard.State.Should().Be(State.Review, "precondition: card should be in Review state");

        // Now rate it "Again"
        var laterDate = reviewCard.Due.AddMinutes(1);
        var (updated, _) = _scheduler.ReviewCard(reviewCard, Rating.Again, laterDate, null);

        updated.State.Should().Be(State.Relearning);
        updated.Stability.Should().BeLessThan(reviewCard.Stability!.Value,
            "Stability should decrease after forgetting");
    }

    [Fact]
    public void ReviewCard_RatedGood_StaysInReview()
    {
        // Get card into Review state
        var card = new FsrsCard();
        var now = DateTime.UtcNow;
        var (reviewCard, _) = _scheduler.ReviewCard(card, Rating.Easy, now, null);
        reviewCard.State.Should().Be(State.Review, "precondition: card should be in Review state");

        var previousStability = reviewCard.Stability!.Value;
        var previousDue = reviewCard.Due;

        // Rate Good while in Review
        var laterDate = reviewCard.Due.AddMinutes(1);
        var (updated, _) = _scheduler.ReviewCard(reviewCard, Rating.Good, laterDate, null);

        updated.State.Should().Be(State.Review);
        updated.Stability.Should().BeGreaterThan(previousStability,
            "Stability should increase after successful review");
        updated.Due.Should().BeAfter(previousDue,
            "Due date should be pushed further into the future");
    }

    [Fact]
    public void MultipleReviews_IntervalsGrow()
    {
        var card = new FsrsCard();
        var now = DateTime.UtcNow;
        var intervals = new List<TimeSpan>();

        // Rate "Good" repeatedly and track intervals
        var current = card;
        var currentTime = now;

        for (var i = 0; i < 6; i++)
        {
            var (updated, _) = _scheduler.ReviewCard(current, Rating.Good, currentTime, null);
            var interval = updated.Due - currentTime;
            intervals.Add(interval);
            currentTime = updated.Due.AddMinutes(1); // review shortly after due
            current = updated;
        }

        // After getting through learning steps, intervals should grow
        // The first few may be short (learning steps), but later ones should increase
        var lastThree = intervals.Skip(intervals.Count - 3).ToList();
        for (var i = 1; i < lastThree.Count; i++)
        {
            lastThree[i].Should().BeGreaterThanOrEqualTo(lastThree[i - 1],
                $"interval {i + intervals.Count - 3} should be >= interval {i - 1 + intervals.Count - 3}");
        }
    }

    [Theory]
    [InlineData(Rating.Again)]
    [InlineData(Rating.Hard)]
    [InlineData(Rating.Good)]
    [InlineData(Rating.Easy)]
    public void AllRatings_ProduceValidResults(Rating rating)
    {
        var card = new FsrsCard();
        var now = DateTime.UtcNow;

        var (updated, reviewLog) = _scheduler.ReviewCard(card, rating, now, null);

        updated.Stability.Should().NotBeNull();
        updated.Stability.Should().BeGreaterThan(0);
        updated.Difficulty.Should().NotBeNull();
        updated.Difficulty.Should().BeGreaterThan(0);
        updated.Due.Should().BeAfter(now);
        updated.State.Should().BeOneOf(State.Learning, State.Review, State.Relearning);
    }
}
