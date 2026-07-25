using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

/// <summary>
/// Runs the real migrations (the rest of the suite builds its schema with
/// EnsureCreated) to pin the backfill in SplitReviewStateFromCard: exactly one
/// ReviewState row per card that was reviewed or suspended, none for pristine-new
/// cards, and every SRS value copied across untouched.
/// </summary>
public class ReviewStateMigrationTests : IAsyncLifetime
{
    /// <summary>Last migration before the ReviewState split.</summary>
    private const string PreSplitMigration = "20260519053021_RemoveSourceHeading";

    private readonly TestDb _db = new();

    private readonly DateTimeOffset _now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    // The schema is built by the test itself, migration by migration.
    public Task InitializeAsync() => Task.CompletedTask;
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task Backfill_CreatesOneReviewState_PerReviewedOrSuspendedCard()
    {
        await _db.MigrateAsync(PreSplitMigration);

        var userId = Guid.NewGuid().ToString();
        var pristine = Guid.NewGuid();
        var reviewed = Guid.NewGuid();
        var learning = Guid.NewGuid();
        var suspendedOnly = Guid.NewGuid();
        var dueOnly = Guid.NewGuid();

        await using (var legacy = _db.CreateDbContext())
        {
            legacy.Users.Add(new AppUser
            {
                Id = userId,
                UserName = "migration@fasolt.test",
                NormalizedUserName = "MIGRATION@FASOLT.TEST",
                Email = "migration@fasolt.test",
                NormalizedEmail = "MIGRATION@FASOLT.TEST",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            });

            // The Card entity no longer maps the SRS columns, so cards go in as
            // pristine-new rows (the pre-split defaults) and the SRS state is set
            // afterwards with raw SQL against the old columns.
            foreach (var (id, publicId) in new[]
                     {
                         (pristine, "pristine0000"),
                         (reviewed, "reviewed0000"),
                         (learning, "learning0000"),
                         (suspendedOnly, "suspended000"),
                         (dueOnly, "dueonly00000"),
                     })
            {
                legacy.Cards.Add(new Card
                {
                    Id = id,
                    PublicId = publicId,
                    UserId = userId,
                    Front = $"Q {publicId}",
                    Back = $"A {publicId}",
                    CreatedAt = _now.AddDays(-30),
                });
            }

            await legacy.SaveChangesAsync();

            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Cards"
                SET "State" = 'review', "Stability" = 12.5, "Difficulty" = 6.25,
                    "DueAt" = {_now.AddDays(-2)}, "LastReviewedAt" = {_now.AddDays(-12)}
                WHERE "Id" = {reviewed}
                """);

            // Mid-learning: a step and a due date, and suspended on top of it.
            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Cards"
                SET "State" = 'learning', "Step" = 1, "DueAt" = {_now.AddMinutes(10)},
                    "IsSuspended" = true
                WHERE "Id" = {learning}
                """);

            // Suspended but never reviewed — still needs a row.
            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Cards" SET "IsSuspended" = true WHERE "Id" = {suspendedOnly}
                """);

            // Defensive branch of the backfill predicate: a due date on an otherwise
            // untouched card.
            await legacy.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Cards" SET "DueAt" = {_now.AddDays(1)} WHERE "Id" = {dueOnly}
                """);
        }

        await _db.MigrateAsync();

        await using var db = _db.CreateDbContext();
        var states = await db.ReviewStates.ToDictionaryAsync(r => r.CardId);

        states.Should().HaveCount(4);
        states.Keys.Should().NotContain(pristine);
        states.Values.Should().OnlyContain(r => r.UserId == userId);

        var reviewedState = states[reviewed];
        reviewedState.State.Should().Be("review");
        reviewedState.Stability.Should().Be(12.5);
        reviewedState.Difficulty.Should().Be(6.25);
        reviewedState.Step.Should().BeNull();
        reviewedState.DueAt.Should().Be(_now.AddDays(-2));
        reviewedState.LastReviewedAt.Should().Be(_now.AddDays(-12));
        reviewedState.IsSuspended.Should().BeFalse();

        var learningState = states[learning];
        learningState.State.Should().Be("learning");
        learningState.Step.Should().Be(1);
        learningState.DueAt.Should().Be(_now.AddMinutes(10));
        learningState.LastReviewedAt.Should().BeNull();
        learningState.Stability.Should().BeNull();
        learningState.IsSuspended.Should().BeTrue();

        var suspendedState = states[suspendedOnly];
        suspendedState.State.Should().Be("new");
        suspendedState.IsSuspended.Should().BeTrue();
        suspendedState.DueAt.Should().BeNull();
        suspendedState.LastReviewedAt.Should().BeNull();

        var dueOnlyState = states[dueOnly];
        dueOnlyState.State.Should().Be("new");
        dueOnlyState.IsSuspended.Should().BeFalse();
        dueOnlyState.DueAt.Should().Be(_now.AddDays(1));
    }
}
