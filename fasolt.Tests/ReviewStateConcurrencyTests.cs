using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

/// <summary>
/// The lazy ReviewState row is materialized with INSERT ... ON CONFLICT DO NOTHING, so
/// two requests touching the same card for the first time both succeed instead of one
/// failing on the (UserId, CardId) primary key. Rows are also never deleted once
/// created — a row carrying only the "new" defaults is equivalent to having none, and
/// deleting it would race with a concurrent review of the same card.
/// </summary>
public class ReviewStateConcurrencyTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private ReviewService CreateReviewService(AppDbContext db)
        => new(db, _time, new StudyStatsService(db, _time));

    private async Task<(string PublicId, Guid Id)> CreateCard(string front = "Q?", string back = "A.")
    {
        await using var db = _db.CreateDbContext();
        var card = await new CardService(db).CreateCard(UserId, front, back, null);
        var id = await db.Cards.Where(c => c.PublicId == card.Id).Select(c => c.Id).FirstAsync();
        return (card.Id, id);
    }

    [Fact]
    public async Task EnsureExist_WhenRowWasCreatedConcurrently_DoesNotThrowOrOverwrite()
    {
        var card = await CreateCard();

        // The other request won the race and already recorded a review.
        await using (var other = _db.CreateDbContext())
        {
            var state = await other.ReviewStateFor(UserId, card.Id);
            state.State = "review";
            state.Stability = 12.5;
            state.Difficulty = 4.25;
            state.DueAt = _time.GetUtcNow().AddDays(3);
            state.LastReviewedAt = _time.GetUtcNow();
            await other.SaveChangesAsync();
        }

        await using var db = _db.CreateDbContext();
        var act = async () => await ReviewStateQuery.EnsureExistAsync(db, UserId, [card.Id]);
        await act.Should().NotThrowAsync();

        await using var verify = _db.CreateDbContext();
        var rows = await verify.ReviewStates.Where(r => r.CardId == card.Id).ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].State.Should().Be("review");
        rows[0].Stability.Should().Be(12.5);
        rows[0].Difficulty.Should().Be(4.25);
    }

    [Fact]
    public async Task RateCard_FirstReviewFromTwoConcurrentRequests_BothSucceed()
    {
        var card = await CreateCard();

        await using var dbA = _db.CreateDbContext();
        await using var dbB = _db.CreateDbContext();

        var results = await Task.WhenAll(
            CreateReviewService(dbA).RateCard(UserId, new RateCardRequest(card.PublicId, "good")),
            CreateReviewService(dbB).RateCard(UserId, new RateCardRequest(card.PublicId, "good")));

        results.Should().NotContainNulls();

        await using var verify = _db.CreateDbContext();
        var rows = await verify.ReviewStates.Where(r => r.CardId == card.Id).ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].State.Should().Be("learning");
        (await verify.ReviewLogs.CountAsync(r => r.CardId == card.Id)).Should().Be(2);
    }

    [Fact]
    public async Task SetSuspended_FirstSuspendFromTwoConcurrentRequests_BothSucceed()
    {
        var card = await CreateCard();

        await using var dbA = _db.CreateDbContext();
        await using var dbB = _db.CreateDbContext();

        var results = await Task.WhenAll(
            new CardService(dbA).SetSuspended(UserId, card.PublicId, true),
            new CardService(dbB).SetSuspended(UserId, card.PublicId, true));

        results.Should().NotContainNulls();
        results.Should().OnlyContain(r => r!.IsSuspended);

        await using var verify = _db.CreateDbContext();
        (await verify.ReviewStates.CountAsync(r => r.CardId == card.Id)).Should().Be(1);
    }

    [Fact]
    public async Task SetSuspendedBulk_WhenRowsAppearConcurrently_BothSucceed()
    {
        var first = await CreateCard("Bulk 1", "A");
        var second = await CreateCard("Bulk 2", "A");
        var publicIds = new List<string> { first.PublicId, second.PublicId };

        await using var dbA = _db.CreateDbContext();
        await using var dbB = _db.CreateDbContext();

        var counts = await Task.WhenAll(
            new CardService(dbA).SetSuspendedBulk(UserId, publicIds, true),
            new CardService(dbB).SetSuspendedBulk(UserId, publicIds, true));

        counts.Should().AllBeEquivalentTo(2);

        await using var verify = _db.CreateDbContext();
        var rows = await verify.ReviewStates.Where(r => r.UserId == UserId).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(r => r.IsSuspended);
    }

    [Fact]
    public async Task SetSuspended_Unsuspending_CardWithoutState_CreatesNoRow()
    {
        var card = await CreateCard();

        await using var db = _db.CreateDbContext();
        var result = await new CardService(db).SetSuspended(UserId, card.PublicId, false);

        result.Should().NotBeNull();
        result!.IsSuspended.Should().BeFalse();
        result.State.Should().Be("new");

        await using var verify = _db.CreateDbContext();
        (await verify.ReviewStates.CountAsync(r => r.CardId == card.Id)).Should().Be(0);
    }

    [Fact]
    public async Task SetSuspended_UnsuspendedRowSurvives_AndStillReadsAsNew()
    {
        var card = await CreateCard();

        await using var db = _db.CreateDbContext();
        var svc = new CardService(db);
        await svc.SetSuspended(UserId, card.PublicId, true);
        var result = await svc.SetSuspended(UserId, card.PublicId, false);

        result!.IsSuspended.Should().BeFalse();
        result.State.Should().Be("new");
        result.DueAt.Should().BeNull();

        // The row stays behind, but it is indistinguishable from having no row: the
        // card is due and unsuspended again.
        await using var verify = _db.CreateDbContext();
        (await verify.ReviewStates.CountAsync(r => r.CardId == card.Id)).Should().Be(1);

        var due = await CreateReviewService(verify).GetDueCards(UserId);
        due.Should().ContainSingle(c => c.Id == card.PublicId);
    }
}
