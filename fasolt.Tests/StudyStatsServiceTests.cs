using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class StudyStatsServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    // Start on a Monday at 05:00 UTC so day-start (04:00 UTC) has already passed
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 6, 2, 5, 0, 0, TimeSpan.Zero));

    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private StudyStatsService CreateStatsService(Server.Infrastructure.Data.AppDbContext db)
        => new(db, _time);

    private ReviewService CreateReviewService(Server.Infrastructure.Data.AppDbContext db)
        => new(db, _time, new StudyStatsService(db, _time));

    // Creates a card with CreatedAt set to the current fake-time clock so the
    // card exists "as of now" in tests that manipulate _time.
    private async Task<string> CreateCardAt(Server.Infrastructure.Data.AppDbContext db, DateTimeOffset createdAt,
        string front = "Q?", string back = "A.")
    {
        var card = new Card
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = UserId,
            Front = front,
            Back = back,
            CreatedAt = createdAt,
        };
        db.Cards.Add(card);
        await db.SaveChangesAsync();
        return card.PublicId;
    }

    // --- Empty account ---

    [Fact]
    public async Task Empty_ReturnsAllZeros()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateStatsService(db);

        var stats = await svc.GetStats(UserId);

        stats.CurrentStreak.Should().Be(0);
        stats.BestStreak.Should().Be(0);
        stats.TotalAnswered.Should().Be(0);
        stats.AnsweredToday.Should().Be(0);
        stats.Last7Days.Should().HaveCount(7);
        stats.Last7Days.Should().OnlyContain(d => d.Count == 0);
    }

    // --- Last7Days reflects real activity, today is the last entry ---

    [Fact]
    public async Task Last7Days_ReflectsActivity_TodayIsLastEntry()
    {
        await using var db = _db.CreateDbContext();
        var reviewSvc = CreateReviewService(db);

        var now = _time.GetUtcNow();
        var c1 = await CreateCardAt(db, now.AddHours(-1), "Q1", "A1");
        var c2 = await CreateCardAt(db, now.AddHours(-1), "Q2", "A2");
        await reviewSvc.RateCard(UserId, new RateCardRequest(c1, "good"));
        await reviewSvc.RateCard(UserId, new RateCardRequest(c2, "good"));

        var statsSvc = CreateStatsService(db);
        var stats = await statsSvc.GetStats(UserId);

        stats.Last7Days.Should().HaveCount(7);
        stats.Last7Days[^1].Count.Should().Be(2); // today
        stats.Last7Days.Take(6).Should().OnlyContain(d => d.Count == 0); // prior 6 days idle
    }

    // --- Single review today ---

    [Fact]
    public async Task SingleReviewToday_ReturnsCorrectStats()
    {
        await using var db = _db.CreateDbContext();
        var cardId = await CreateCardAt(db, _time.GetUtcNow().AddHours(-1));
        var reviewSvc = CreateReviewService(db);
        await reviewSvc.RateCard(UserId, new RateCardRequest(cardId, "good"));

        var statsSvc = CreateStatsService(db);
        var stats = await statsSvc.GetStats(UserId);

        stats.CurrentStreak.Should().Be(1);
        stats.BestStreak.Should().Be(1);
        stats.TotalAnswered.Should().Be(1);
        stats.AnsweredToday.Should().Be(1);
    }

    // --- Two consecutive days ---

    [Fact]
    public async Task TwoConsecutiveDays_Streak2()
    {
        await using var db = _db.CreateDbContext();

        // Day 1: create card and review
        var day1 = _time.GetUtcNow(); // 2025-06-02 05:00 UTC
        var cardId = await CreateCardAt(db, day1.AddHours(-1));
        var reviewSvc = CreateReviewService(db);
        await reviewSvc.RateCard(UserId, new RateCardRequest(cardId, "good"));

        // Day 2: advance time by 1 day, create another card, review it
        _time.SetUtcNow(day1.AddDays(1));
        var card2Id = await CreateCardAt(db, _time.GetUtcNow().AddMinutes(-10));
        await reviewSvc.RateCard(UserId, new RateCardRequest(card2Id, "good"));

        var statsSvc = CreateStatsService(db);
        var stats = await statsSvc.GetStats(UserId);

        stats.CurrentStreak.Should().Be(2);
        stats.TotalAnswered.Should().Be(2);
        stats.AnsweredToday.Should().Be(1);
    }

    // --- Gap day with no due cards (rest day) ---
    // Rate card1 "easy" on day 1 so it's due far in the future.
    // Skip day 2 entirely (no due cards).
    // Create card2 on day 3 and review it.
    // Streak should be 2 (day 1 + day 3; day 2 is a rest day because no cards were due).

    [Fact]
    public async Task GapDayWithNoDueCards_StreakPreserved()
    {
        await using var db = _db.CreateDbContext();
        var reviewSvc = CreateReviewService(db);

        // Day 1: review card1 "easy" -> scheduled far ahead
        var day1 = _time.GetUtcNow();
        var card1Id = await CreateCardAt(db, day1.AddHours(-1));
        var result1 = await reviewSvc.RateCard(UserId, new RateCardRequest(card1Id, "easy"));
        result1.Should().NotBeNull();
        // Card1 is now due far in the future (easy rating), well past day 2

        // Day 2: no reviews (skip)
        // Day 3: create card2 and review it
        _time.SetUtcNow(day1.AddDays(2));
        var card2Id = await CreateCardAt(db, _time.GetUtcNow().AddMinutes(-5));
        await reviewSvc.RateCard(UserId, new RateCardRequest(card2Id, "good"));

        var statsSvc = CreateStatsService(db);
        var stats = await statsSvc.GetStats(UserId);

        // Day 2 had no due cards (card1 was scheduled far ahead, card2 didn't exist yet)
        // so it's a rest day and streak is preserved: day1 + day3 = 2
        stats.CurrentStreak.Should().Be(2);
        stats.TotalAnswered.Should().Be(2);
    }

    // --- Gap day WITH due cards but no review (streak breaks) ---
    // card1 created day 1, rated good (short interval).
    // card2 created day 1, never reviewed through day 2.
    // On day 3, rate card2. Since day 2 had a due card (card2), streak resets to 1.

    [Fact]
    public async Task GapDayWithDueCards_StreakBreaks()
    {
        await using var db = _db.CreateDbContext();
        var reviewSvc = CreateReviewService(db);

        var day1 = _time.GetUtcNow();

        // Create card1 and rate it on day 1 (good → scheduled in future)
        var card1Id = await CreateCardAt(db, day1.AddHours(-1), "Card1?", "Card1.");
        await reviewSvc.RateCard(UserId, new RateCardRequest(card1Id, "good"));

        // Create card2 on day 1 but do NOT review it
        var card2Id = await CreateCardAt(db, day1.AddMinutes(-30), "Card2?", "Card2.");

        // Jump to day 3 and review card2 (it was due since day 1, so day 2 is a "due day")
        _time.SetUtcNow(day1.AddDays(2));
        await reviewSvc.RateCard(UserId, new RateCardRequest(card2Id, "good"));

        var statsSvc = CreateStatsService(db);
        var stats = await statsSvc.GetStats(UserId);

        // card2 was due on day 2 and was not reviewed → streak breaks → current streak = 1
        stats.CurrentStreak.Should().Be(1);
    }

    // --- BestStreak persists after current streak resets ---

    [Fact]
    public async Task BestStreak_PersistsAfterCurrentStreakResets()
    {
        await using var db = _db.CreateDbContext();
        var reviewSvc = CreateReviewService(db);
        var day1 = _time.GetUtcNow();

        // Build a 3-day streak: day1, day2, day3
        for (var i = 0; i < 3; i++)
        {
            _time.SetUtcNow(day1.AddDays(i));
            var cardId = await CreateCardAt(db, _time.GetUtcNow().AddMinutes(-5));
            await reviewSvc.RateCard(UserId, new RateCardRequest(cardId, "easy"));
        }

        // Verify streak is 3 after day 3
        {
            var statsSvc = CreateStatsService(db);
            var stats = await statsSvc.GetStats(UserId);
            stats.CurrentStreak.Should().Be(3);
            stats.BestStreak.Should().Be(3);
        }

        // Day 5 (break day 4) — create a new card on day 4 without reviewing, then review on day 5
        // On day 4 a new card was due (created on day 4 = immediately due); day 4 had no review → streak breaks
        _time.SetUtcNow(day1.AddDays(3));
        var missedCard = await CreateCardAt(db, _time.GetUtcNow().AddMinutes(-5));
        // skip reviewing missedCard on day 4

        _time.SetUtcNow(day1.AddDays(4));
        await reviewSvc.RateCard(UserId, new RateCardRequest(missedCard, "good"));

        // Current streak should be 1 (only today), but BestStreak should still be 3
        {
            await using var db2 = _db.CreateDbContext();
            var statsSvc = CreateStatsService(db2);
            var stats = await statsSvc.GetStats(UserId);
            stats.CurrentStreak.Should().Be(1);
            stats.BestStreak.Should().Be(3);
        }
    }

    // --- Progress: empty account ---

    [Fact]
    public async Task Progress_Empty_ReturnsZeros()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateStatsService(db);

        var progress = await svc.GetProgress(UserId);

        progress.CurrentStreak.Should().Be(0);
        progress.BestStreak.Should().Be(0);
        progress.TotalAnswered.Should().Be(0);
        progress.AnsweredToday.Should().Be(0);
        progress.AnsweredThisWeek.Should().Be(0);
        progress.AnsweredThisMonth.Should().Be(0);
        progress.DailyActivity.Should().HaveCount(30);
        progress.DailyActivity.Should().OnlyContain(d => d.Count == 0);
        progress.RatingMix.Again.Should().Be(0);
        progress.RatingMix.Hard.Should().Be(0);
        progress.RatingMix.Good.Should().Be(0);
        progress.RatingMix.Easy.Should().Be(0);
    }

    // --- Progress: rating mix matches actual reviews ---

    [Fact]
    public async Task Progress_RatingMix_ReflectsActualReviews()
    {
        await using var db = _db.CreateDbContext();
        var reviewSvc = CreateReviewService(db);

        var now = _time.GetUtcNow();
        var cards = new List<string>();
        for (var i = 0; i < 6; i++)
            cards.Add(await CreateCardAt(db, now.AddHours(-1), $"Q{i}", $"A{i}"));

        // 1 again, 2 hard, 2 good, 1 easy
        await reviewSvc.RateCard(UserId, new RateCardRequest(cards[0], "again"));
        await reviewSvc.RateCard(UserId, new RateCardRequest(cards[1], "hard"));
        await reviewSvc.RateCard(UserId, new RateCardRequest(cards[2], "hard"));
        await reviewSvc.RateCard(UserId, new RateCardRequest(cards[3], "good"));
        await reviewSvc.RateCard(UserId, new RateCardRequest(cards[4], "good"));
        await reviewSvc.RateCard(UserId, new RateCardRequest(cards[5], "easy"));

        var statsSvc = CreateStatsService(db);
        var progress = await statsSvc.GetProgress(UserId, 30);

        progress.RatingMix.Again.Should().Be(1);
        progress.RatingMix.Hard.Should().Be(2);
        progress.RatingMix.Good.Should().Be(2);
        progress.RatingMix.Easy.Should().Be(1);
    }

    // --- Progress: clamps days param ---

    [Fact]
    public async Task Progress_DaysParam_IsClamped()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateStatsService(db);

        (await svc.GetProgress(UserId, 1)).DailyActivity.Should().HaveCount(7);
        (await svc.GetProgress(UserId, 1000)).DailyActivity.Should().HaveCount(366);
        (await svc.GetProgress(UserId, 14)).DailyActivity.Should().HaveCount(14);
    }

    // --- Progress: today is last entry, counts match ---

    [Fact]
    public async Task Progress_TodayCount_AndPositionInActivity()
    {
        await using var db = _db.CreateDbContext();
        var reviewSvc = CreateReviewService(db);

        var day1 = _time.GetUtcNow();
        var c1 = await CreateCardAt(db, day1.AddHours(-1), "Q1", "A1");
        var c2 = await CreateCardAt(db, day1.AddHours(-1), "Q2", "A2");
        await reviewSvc.RateCard(UserId, new RateCardRequest(c1, "good"));
        await reviewSvc.RateCard(UserId, new RateCardRequest(c2, "good"));

        var statsSvc = CreateStatsService(db);
        var progress = await statsSvc.GetProgress(UserId, 14);

        progress.AnsweredToday.Should().Be(2);
        progress.TotalAnswered.Should().Be(2);
        progress.AnsweredThisWeek.Should().BeGreaterThanOrEqualTo(2);
        progress.AnsweredThisMonth.Should().BeGreaterThanOrEqualTo(2);
        progress.DailyActivity.Should().HaveCount(14);
        progress.DailyActivity.Last().Count.Should().Be(2);
    }

    // --- Progress: rest day (no due) marked hadDue=false ---

    [Fact]
    public async Task Progress_RestDay_HadDueFalse()
    {
        await using var db = _db.CreateDbContext();
        var reviewSvc = CreateReviewService(db);

        // Day 1: rate "easy" so card scheduled far in the future
        var day1 = _time.GetUtcNow();
        var card1 = await CreateCardAt(db, day1.AddHours(-1));
        await reviewSvc.RateCard(UserId, new RateCardRequest(card1, "easy"));

        // Day 3: create + review a new card so today != day1
        _time.SetUtcNow(day1.AddDays(2));
        var card2 = await CreateCardAt(db, _time.GetUtcNow().AddMinutes(-5));
        await reviewSvc.RateCard(UserId, new RateCardRequest(card2, "good"));

        var statsSvc = CreateStatsService(db);
        var progress = await statsSvc.GetProgress(UserId, 14);

        // Day 2 (one day before today) should be a rest day: no count, no due
        var dayBeforeToday = progress.DailyActivity[^2];
        dayBeforeToday.Count.Should().Be(0);
        dayBeforeToday.HadDue.Should().BeFalse();
    }

    // --- Progress: missed day (had due, no review) marked hadDue=true ---

    [Fact]
    public async Task Progress_MissedDay_HadDueTrue()
    {
        await using var db = _db.CreateDbContext();
        var reviewSvc = CreateReviewService(db);

        var day1 = _time.GetUtcNow();
        // Create a card on day1 but don't review it; it stays due
        var unreviewed = await CreateCardAt(db, day1.AddMinutes(-10));

        // Day 3: review something
        _time.SetUtcNow(day1.AddDays(2));
        await reviewSvc.RateCard(UserId, new RateCardRequest(unreviewed, "good"));

        var statsSvc = CreateStatsService(db);
        var progress = await statsSvc.GetProgress(UserId, 14);

        // Day 2 had a due card and no review → hadDue true, count zero
        var dayBeforeToday = progress.DailyActivity[^2];
        dayBeforeToday.Count.Should().Be(0);
        dayBeforeToday.HadDue.Should().BeTrue();
    }

    // --- Linked decks only count from the day they were linked ---

    /// <summary>
    /// A linked card carries the author's <c>CreatedAt</c>, which may predate the
    /// subscription by years. Days before the link existed were never the user's to
    /// miss, so they must not be repainted as due days — that would retroactively
    /// break a streak built entirely on the user's own cards.
    /// </summary>
    [Fact]
    public async Task LinkingAnOldDeck_DoesNotRetroactivelyBreakTheStreakOrRepaintRestDays()
    {
        await using var db = _db.CreateDbContext();
        var reviewSvc = CreateReviewService(db);

        // Day 1: study an own card "easy" so it is scheduled well past day 2.
        var day1 = _time.GetUtcNow();
        var own = await CreateCardAt(db, day1.AddHours(-1));
        await reviewSvc.RateCard(UserId, new RateCardRequest(own, "easy"));

        // Day 2 is a rest day. Day 3: study again — streak 2 over a preserved rest day.
        _time.SetUtcNow(day1.AddDays(2));
        var second = await CreateCardAt(db, _time.GetUtcNow().AddMinutes(-5));
        await reviewSvc.RateCard(UserId, new RateCardRequest(second, "good"));

        var before = await CreateStatsService(db).GetStats(UserId);
        before.CurrentStreak.Should().Be(2);

        // Today: link a deck whose cards the author wrote a year ago.
        var author = await LinkedDeckTestData.AddUser(db, "author-old");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Ancient", cardCount: 0);
        var old = LinkedDeckTestData.AddCard(db, deck, "Old Q", "A");
        old.CreatedAt = day1.AddDays(-365);
        await db.SaveChangesAsync();
        db.DeckSubscriptions.Add(new DeckSubscription
        {
            UserId = UserId,
            DeckId = deck.Id,
            SubscribedAt = _time.GetUtcNow(),
        });
        await db.SaveChangesAsync();

        var after = await CreateStatsService(db).GetStats(UserId);
        after.CurrentStreak.Should().Be(2, "the linked deck's history is the author's, not the subscriber's");

        var progress = await CreateStatsService(db).GetProgress(UserId, 14);
        progress.DailyActivity[^2].HadDue.Should().BeFalse("day 2 was a rest day before the link and stays one");
    }

    // --- Converting a linked deck to a copy is invisible to the streak ---

    /// <summary>
    /// Convert-to-copy carries the user's SRS state and review history onto the cloned
    /// cards, so the clones have to keep the date the originals became studyable. Dating
    /// them from the conversion makes every missed day since look like a rest day and
    /// resurrects a streak the user actually broke.
    /// </summary>
    [Fact]
    public async Task ConvertingALinkedDeck_DoesNotResurrectABrokenStreak()
    {
        await using var db = _db.CreateDbContext();
        var reviewSvc = CreateReviewService(db);

        // Day 1: an own card, studied "easy" so it is scheduled well past day 2. It also
        // keeps the streak walk from stopping at the converted cards' creation day.
        var day1 = _time.GetUtcNow();
        var own = await CreateCardAt(db, day1.AddHours(-1), "Own Q", "A");
        await reviewSvc.RateCard(UserId, new RateCardRequest(own, "easy"));

        // A deck written a year ago, linked on day 1, with one card never studied — so
        // day 2 is a day with a due card.
        var author = await LinkedDeckTestData.AddUser(db, "author-convert-streak");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Linked", cardCount: 0);
        var missed = LinkedDeckTestData.AddCard(db, deck, "Missed Q", "B");
        missed.CreatedAt = day1.AddDays(-365);
        await db.SaveChangesAsync();
        db.DeckSubscriptions.Add(new DeckSubscription
        {
            UserId = UserId,
            DeckId = deck.Id,
            SubscribedAt = day1.AddHours(-1),
        });
        await db.SaveChangesAsync();

        // Day 2 is missed. Day 3: study the card that was due all along.
        _time.SetUtcNow(day1.AddDays(2));
        await reviewSvc.RateCard(UserId, new RateCardRequest(missed.PublicId, "good"));

        var before = await CreateStatsService(db).GetStats(UserId);
        before.CurrentStreak.Should().Be(1, "day 2 had a due card and no review");

        await new DeckSubscriptionService(db).ConvertToCopy(UserId, deck.PublicId);

        await using var verify = _db.CreateDbContext();
        var after = await CreateStatsService(verify).GetStats(UserId);
        after.CurrentStreak.Should().Be(1, "owning the cards outright does not undo a missed day");
    }

    /// <summary>
    /// The other direction of the same rule: converting a deck the author wrote years
    /// ago must not backdate the clones to the author's dates either, or every rest day
    /// since would turn into a day the user missed a card they did not yet have.
    /// </summary>
    [Fact]
    public async Task ConvertingAnOldLinkedDeck_DoesNotRetroactivelyBreakTheStreak()
    {
        await using var db = _db.CreateDbContext();
        var reviewSvc = CreateReviewService(db);

        // Day 1: own card studied "easy", so day 2 has nothing due — a rest day.
        var day1 = _time.GetUtcNow();
        var own = await CreateCardAt(db, day1.AddHours(-1));
        await reviewSvc.RateCard(UserId, new RateCardRequest(own, "easy"));

        // Day 3: study again. Streak 2 across a preserved rest day.
        _time.SetUtcNow(day1.AddDays(2));
        var second = await CreateCardAt(db, _time.GetUtcNow().AddMinutes(-5));
        await reviewSvc.RateCard(UserId, new RateCardRequest(second, "good"));

        (await CreateStatsService(db).GetStats(UserId)).CurrentStreak.Should().Be(2);

        // Today: link a year-old deck and immediately convert it to a copy.
        var author = await LinkedDeckTestData.AddUser(db, "author-old-convert");
        var deck = await LinkedDeckTestData.AddDeck(db, author, name: "Ancient", cardCount: 0);
        var old = LinkedDeckTestData.AddCard(db, deck, "Old Q", "A");
        old.CreatedAt = day1.AddDays(-365);
        await db.SaveChangesAsync();
        db.DeckSubscriptions.Add(new DeckSubscription
        {
            UserId = UserId,
            DeckId = deck.Id,
            SubscribedAt = _time.GetUtcNow(),
        });
        await db.SaveChangesAsync();

        await new DeckSubscriptionService(db).ConvertToCopy(UserId, deck.PublicId);

        await using var verify = _db.CreateDbContext();
        var after = await CreateStatsService(verify).GetStats(UserId);
        after.CurrentStreak.Should().Be(2, "the deck's age is the author's history, not the copier's");

        var progress = await CreateStatsService(verify).GetProgress(UserId, 14);
        progress.DailyActivity[^2].HadDue.Should().BeFalse("day 2 was a rest day before the copy and stays one");
    }
}
