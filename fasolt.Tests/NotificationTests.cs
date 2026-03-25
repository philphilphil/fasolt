using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

/// <summary>
/// Tests for notification device token persistence and background service eligibility logic.
/// </summary>
public class NotificationTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    // ── Device token persistence ─────────────────────────────────────────────

    [Fact]
    public async Task UpsertDeviceToken_CreatesNewToken()
    {
        await using var db = _db.CreateDbContext();

        var now = DateTimeOffset.UtcNow;
        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = UserId,
            Token = "token-abc",
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var stored = await db.DeviceTokens.SingleAsync(t => t.UserId == UserId);
        stored.Token.Should().Be("token-abc");
    }

    [Fact]
    public async Task UpsertDeviceToken_UpdatesExistingToken()
    {
        await using var db = _db.CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = UserId,
            Token = "old-token",
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var existing = await db.DeviceTokens.SingleAsync(t => t.UserId == UserId);
        existing.Token = "new-token";
        existing.UpdatedAt = now.AddMinutes(5);
        await db.SaveChangesAsync();

        var stored = await db.DeviceTokens.SingleAsync(t => t.UserId == UserId);
        stored.Token.Should().Be("new-token");
    }

    [Fact]
    public async Task DeleteDeviceToken_RemovesToken()
    {
        await using var db = _db.CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = UserId,
            Token = "to-delete",
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var token = await db.DeviceTokens.SingleAsync(t => t.UserId == UserId);
        db.DeviceTokens.Remove(token);
        await db.SaveChangesAsync();

        var count = await db.DeviceTokens.CountAsync(t => t.UserId == UserId);
        count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteDeviceToken_WhenNoneExists_IsIdempotent()
    {
        await using var db = _db.CreateDbContext();

        // No token exists — just verify no exception and count stays zero
        var existing = await db.DeviceTokens.FirstOrDefaultAsync(t => t.UserId == UserId);
        if (existing is not null)
        {
            db.DeviceTokens.Remove(existing);
            await db.SaveChangesAsync();
        }

        var count = await db.DeviceTokens.CountAsync(t => t.UserId == UserId);
        count.Should().Be(0);
    }

    // ── Notification settings ────────────────────────────────────────────────

    [Fact]
    public async Task NotificationIntervalHours_DefaultIs8()
    {
        await using var db = _db.CreateDbContext();

        var user = await db.Users.FindAsync(UserId);
        user!.NotificationIntervalHours.Should().Be(8);
    }

    [Fact]
    public async Task UpdateNotificationInterval_Persists()
    {
        await using var db = _db.CreateDbContext();

        var user = await db.Users.FindAsync(UserId);
        user!.NotificationIntervalHours = 12;
        await db.SaveChangesAsync();

        await using var db2 = _db.CreateDbContext();
        var reloaded = await db2.Users.FindAsync(UserId);
        reloaded!.NotificationIntervalHours.Should().Be(12);
    }

    // ── Background service eligibility logic ────────────────────────────────

    [Fact]
    public async Task EligibilityQuery_IncludesUser_WhenNeverNotified()
    {
        await using var db = _db.CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        // User has never been notified (LastNotifiedAt is null)
        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = UserId,
            Token = "test-token",
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var eligible = await db.DeviceTokens
            .Include(d => d.User)
            .Where(d =>
                d.User.LastNotifiedAt == null ||
                d.User.LastNotifiedAt.Value.AddHours(d.User.NotificationIntervalHours) <= now)
            .AnyAsync(d => d.UserId == UserId);

        eligible.Should().BeTrue();
    }

    [Fact]
    public async Task EligibilityQuery_ExcludesUser_WhenNotifiedRecently()
    {
        await using var db = _db.CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        // User was notified 1 hour ago, interval is 8 hours — not yet eligible
        var user = await db.Users.FindAsync(UserId);
        user!.LastNotifiedAt = now.AddHours(-1);
        await db.SaveChangesAsync();

        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = UserId,
            Token = "test-token",
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var eligible = await db.DeviceTokens
            .Include(d => d.User)
            .Where(d =>
                d.User.LastNotifiedAt == null ||
                d.User.LastNotifiedAt.Value.AddHours(d.User.NotificationIntervalHours) <= now)
            .AnyAsync(d => d.UserId == UserId);

        eligible.Should().BeFalse();
    }

    [Fact]
    public async Task EligibilityQuery_IncludesUser_WhenIntervalHasElapsed()
    {
        await using var db = _db.CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        // User was notified 9 hours ago, interval is 8 hours — eligible
        var user = await db.Users.FindAsync(UserId);
        user!.NotificationIntervalHours = 8;
        user.LastNotifiedAt = now.AddHours(-9);
        await db.SaveChangesAsync();

        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = UserId,
            Token = "test-token",
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        var eligible = await db.DeviceTokens
            .Include(d => d.User)
            .Where(d =>
                d.User.LastNotifiedAt == null ||
                d.User.LastNotifiedAt.Value.AddHours(d.User.NotificationIntervalHours) <= now)
            .AnyAsync(d => d.UserId == UserId);

        eligible.Should().BeTrue();
    }

    [Fact]
    public async Task DueCardQuery_CountsOnlyDueCards()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var now = DateTimeOffset.UtcNow;

        // Create two cards: one due, one not
        var dueCard = await cardSvc.CreateCard(UserId, "Due Q", "Due A", null, null);
        var futureCard = await cardSvc.CreateCard(UserId, "Future Q", "Future A", null, null);

        // Push the future card's due date into the future
        var futureEntity = await db.Cards.FirstAsync(c => c.PublicId == futureCard.Id);
        futureEntity.DueAt = now.AddDays(7);
        await db.SaveChangesAsync();

        var dueCount = await db.Cards
            .CountAsync(c =>
                c.UserId == UserId &&
                (c.DueAt == null || c.DueAt <= now) &&
                (!c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive)));

        dueCount.Should().Be(1);
    }

    [Fact]
    public async Task DueCardQuery_ReturnsZero_WhenNoneAreDue()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var now = DateTimeOffset.UtcNow;

        var card = await cardSvc.CreateCard(UserId, "Future Q", "Future A", null, null);
        var entity = await db.Cards.FirstAsync(c => c.PublicId == card.Id);
        entity.DueAt = now.AddDays(3);
        await db.SaveChangesAsync();

        var dueCount = await db.Cards
            .CountAsync(c =>
                c.UserId == UserId &&
                (c.DueAt == null || c.DueAt <= now) &&
                (!c.DeckCards.Any() || c.DeckCards.Any(dc => dc.Deck.IsActive)));

        dueCount.Should().Be(0);
    }

    [Fact]
    public async Task LastNotifiedAt_UpdatesAfterNotification()
    {
        await using var db = _db.CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        var user = await db.Users.FindAsync(UserId);
        user!.LastNotifiedAt = now;
        await db.SaveChangesAsync();

        await using var db2 = _db.CreateDbContext();
        var reloaded = await db2.Users.FindAsync(UserId);
        reloaded!.LastNotifiedAt.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task InvalidToken_DeviceTokenIsRemoved()
    {
        await using var db = _db.CreateDbContext();
        var now = DateTimeOffset.UtcNow;

        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = UserId,
            Token = "invalid-token",
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();

        // Simulate what the background service does on 410 Gone
        var token = await db.DeviceTokens.FirstOrDefaultAsync(d => d.UserId == UserId);
        token.Should().NotBeNull();
        db.DeviceTokens.Remove(token!);
        await db.SaveChangesAsync();

        var count = await db.DeviceTokens.CountAsync(d => d.UserId == UserId);
        count.Should().Be(0);
    }
}
