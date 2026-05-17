using FluentAssertions;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class AdminServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string SeededUserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private static AppUser MakeUser(string email, string? externalProvider = null, DateTimeOffset? lockoutEnd = null)
    {
        var normalized = email.ToUpperInvariant();
        return new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = email,
            NormalizedUserName = normalized,
            Email = email,
            NormalizedEmail = normalized,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ExternalProvider = externalProvider,
            LockoutEnabled = lockoutEnd.HasValue,
            LockoutEnd = lockoutEnd,
        };
    }

    // ---------- ListUsers filters ----------

    [Fact]
    public async Task ListUsers_FiltersByEmailSubstring_CaseInsensitive()
    {
        await using var db = _db.CreateDbContext();
        db.Users.AddRange(
            MakeUser("alice@example.com"),
            MakeUser("bob@example.com"),
            MakeUser("carol@other.com"));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.ListUsers(1, 50, q: "EXAMPLE", provider: null, lockedOnly: null, hasPushOnly: null);

        result.Users.Select(u => u.Email).Should().BeEquivalentTo(["alice@example.com", "bob@example.com"]);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ListUsers_FiltersByUsernameSubstring()
    {
        await using var db = _db.CreateDbContext();
        var user = MakeUser("alice@example.com", externalProvider: "GitHub");
        user.UserName = "octocat";
        user.NormalizedUserName = "OCTOCAT";
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.ListUsers(1, 50, q: "octo", provider: null, lockedOnly: null, hasPushOnly: null);

        result.Users.Should().ContainSingle(u => u.Email == "alice@example.com");
    }

    [Fact]
    public async Task ListUsers_QTrimsWhitespace()
    {
        await using var db = _db.CreateDbContext();
        db.Users.Add(MakeUser("alice@example.com"));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.ListUsers(1, 50, q: "  alice  ", provider: null, lockedOnly: null, hasPushOnly: null);

        result.Users.Should().ContainSingle(u => u.Email == "alice@example.com");
    }

    [Fact]
    public async Task ListUsers_ProviderEmailSentinel_ReturnsOnlyLocalUsers()
    {
        await using var db = _db.CreateDbContext();
        db.Users.AddRange(
            MakeUser("local@example.com"),
            MakeUser("github@example.com", externalProvider: "GitHub"),
            MakeUser("apple@example.com", externalProvider: "Apple"));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.ListUsers(1, 50, q: null, provider: "Email", lockedOnly: null, hasPushOnly: null);

        // The seeded user from TestDb has ExternalProvider=null so it also counts.
        result.Users.Select(u => u.Email).Should().Contain("local@example.com")
            .And.NotContain("github@example.com")
            .And.NotContain("apple@example.com");
    }

    [Fact]
    public async Task ListUsers_ProviderGitHub_ReturnsOnlyGitHubUsers()
    {
        await using var db = _db.CreateDbContext();
        db.Users.AddRange(
            MakeUser("local@example.com"),
            MakeUser("gh@example.com", externalProvider: "GitHub"),
            MakeUser("apple@example.com", externalProvider: "Apple"));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.ListUsers(1, 50, q: null, provider: "GitHub", lockedOnly: null, hasPushOnly: null);

        result.Users.Should().ContainSingle()
            .Which.Email.Should().Be("gh@example.com");
    }

    [Fact]
    public async Task ListUsers_LockedOnly_ReturnsActivelyLockedUsers()
    {
        await using var db = _db.CreateDbContext();
        var future = DateTimeOffset.UtcNow.AddDays(1);
        var past = DateTimeOffset.UtcNow.AddDays(-1);
        db.Users.AddRange(
            MakeUser("locked@example.com", lockoutEnd: future),
            MakeUser("expired@example.com", lockoutEnd: past),
            MakeUser("active@example.com"));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.ListUsers(1, 50, q: null, provider: null, lockedOnly: true, hasPushOnly: null);

        result.Users.Should().ContainSingle()
            .Which.Email.Should().Be("locked@example.com");
    }

    [Fact]
    public async Task ListUsers_HasPushOnly_ReturnsOnlyUsersWithDeviceTokens()
    {
        await using var db = _db.CreateDbContext();
        var withPush = MakeUser("push@example.com");
        var withoutPush = MakeUser("nopush@example.com");
        db.Users.AddRange(withPush, withoutPush);
        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = withPush.Id,
            Token = "device-token",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.ListUsers(1, 50, q: null, provider: null, lockedOnly: null, hasPushOnly: true);

        result.Users.Should().ContainSingle()
            .Which.Email.Should().Be("push@example.com");
    }

    [Fact]
    public async Task ListUsers_CombinesFilters()
    {
        await using var db = _db.CreateDbContext();
        var future = DateTimeOffset.UtcNow.AddDays(1);
        db.Users.AddRange(
            MakeUser("match@example.com", externalProvider: "GitHub", lockoutEnd: future),
            MakeUser("other@example.com", externalProvider: "GitHub"), // not locked
            MakeUser("local-locked@example.com", lockoutEnd: future)); // not GitHub
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.ListUsers(1, 50, q: "example", provider: "GitHub", lockedOnly: true, hasPushOnly: null);

        result.Users.Should().ContainSingle()
            .Which.Email.Should().Be("match@example.com");
    }

    [Fact]
    public async Task ListUsers_AppliesPagination()
    {
        await using var db = _db.CreateDbContext();
        for (var i = 0; i < 5; i++)
            db.Users.Add(MakeUser($"u{i}@page.test"));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var page1 = await svc.ListUsers(1, 2, q: "page.test", provider: null, lockedOnly: null, hasPushOnly: null);
        var page2 = await svc.ListUsers(2, 2, q: "page.test", provider: null, lockedOnly: null, hasPushOnly: null);

        page1.TotalCount.Should().Be(5);
        page1.Users.Should().HaveCount(2);
        page2.Users.Should().HaveCount(2);
        page1.Users.Select(u => u.Email).Should().NotIntersectWith(page2.Users.Select(u => u.Email));
    }

    // ---------- GetLogs filters ----------

    private static AppLog MakeLog(LogType type, string message, bool success = true, string? detail = null, DateTimeOffset? createdAt = null)
        => new()
        {
            Type = type,
            Message = message,
            Detail = detail,
            Success = success,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        };

    [Fact]
    public async Task GetLogs_FiltersByType()
    {
        await using var db = _db.CreateDbContext();
        db.Logs.AddRange(
            MakeLog(LogType.UserRegistered, "signup 1"),
            MakeLog(LogType.UserRegistered, "signup 2"),
            MakeLog(LogType.Admin, "admin action"),
            MakeLog(LogType.Notification, "push sent"));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.GetLogs(1, 50, type: "UserRegistered", q: null, success: null);

        result.TotalCount.Should().Be(2);
        result.Logs.Should().OnlyContain(l => l.Type == "UserRegistered");
    }

    [Fact]
    public async Task GetLogs_TypeFilterIsCaseInsensitive()
    {
        await using var db = _db.CreateDbContext();
        db.Logs.Add(MakeLog(LogType.UserRegistered, "signup"));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.GetLogs(1, 50, type: "userregistered", q: null, success: null);

        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetLogs_FiltersBySuccessFalse()
    {
        await using var db = _db.CreateDbContext();
        db.Logs.AddRange(
            MakeLog(LogType.Notification, "ok 1", success: true),
            MakeLog(LogType.Notification, "ok 2", success: true),
            MakeLog(LogType.Notification, "failed", success: false));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.GetLogs(1, 50, type: null, q: null, success: false);

        result.TotalCount.Should().Be(1);
        result.Logs.Should().ContainSingle().Which.Message.Should().Be("failed");
    }

    [Fact]
    public async Task GetLogs_FiltersByMessageSubstring_CaseInsensitive()
    {
        await using var db = _db.CreateDbContext();
        db.Logs.AddRange(
            MakeLog(LogType.UserRegistered, "New user registered: alice@example.com (Email)"),
            MakeLog(LogType.UserRegistered, "New user registered: bob@example.com (GitHub)"),
            MakeLog(LogType.Admin, "deleted user carol"));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.GetLogs(1, 50, type: null, q: "ALICE", success: null);

        result.TotalCount.Should().Be(1);
        result.Logs.Single().Message.Should().Contain("alice");
    }

    [Fact]
    public async Task GetLogs_QMatchesDetailField()
    {
        await using var db = _db.CreateDbContext();
        db.Logs.AddRange(
            MakeLog(LogType.Notification, "push sent", detail: "Token returned 410 Gone"),
            MakeLog(LogType.Notification, "push sent", detail: "Delivered"));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.GetLogs(1, 50, type: null, q: "410", success: null);

        result.TotalCount.Should().Be(1);
        result.Logs.Single().Detail.Should().Contain("410");
    }

    [Fact]
    public async Task GetLogs_OrdersByCreatedAtDescending()
    {
        await using var db = _db.CreateDbContext();
        var t0 = DateTimeOffset.UtcNow;
        db.Logs.AddRange(
            MakeLog(LogType.Admin, "oldest", createdAt: t0.AddMinutes(-30)),
            MakeLog(LogType.Admin, "middle", createdAt: t0.AddMinutes(-15)),
            MakeLog(LogType.Admin, "newest", createdAt: t0));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var result = await svc.GetLogs(1, 50, type: null, q: null, success: null);

        result.Logs.Select(l => l.Message).Should().ContainInOrder("newest", "middle", "oldest");
    }

    // ---------- GetStats ----------

    [Fact]
    public async Task GetStats_CountsUsersCardsDecksAndPush()
    {
        await using var db = _db.CreateDbContext();
        var locked = MakeUser("locked@example.com", lockoutEnd: DateTimeOffset.UtcNow.AddDays(1));
        var withPush = MakeUser("push@example.com");
        var plain = MakeUser("plain@example.com");
        db.Users.AddRange(locked, withPush, plain);
        db.DeviceTokens.Add(new DeviceToken
        {
            UserId = withPush.Id,
            Token = "tok",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        var now = DateTimeOffset.UtcNow;
        db.Cards.AddRange(
            new Card
            {
                Id = Guid.NewGuid(),
                PublicId = "due1",
                UserId = SeededUserId,
                Front = "f1",
                Back = "b1",
                DueAt = now.AddMinutes(-5),
                CreatedAt = now,
            },
            new Card
            {
                Id = Guid.NewGuid(),
                PublicId = "future1",
                UserId = SeededUserId,
                Front = "f2",
                Back = "b2",
                DueAt = now.AddDays(2),
                CreatedAt = now,
            },
            new Card
            {
                Id = Guid.NewGuid(),
                PublicId = "suspdue1",
                UserId = SeededUserId,
                Front = "f3",
                Back = "b3",
                DueAt = now.AddMinutes(-5),
                IsSuspended = true,
                CreatedAt = now,
            });
        db.Decks.Add(new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = "deck1",
            UserId = SeededUserId,
            Name = "Deck A",
            CreatedAt = now,
        });
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var stats = await svc.GetStats();

        // 3 explicitly added users + 1 seeded by TestDb.InitializeAsync
        stats.TotalUsers.Should().Be(4);
        stats.LockedUsers.Should().Be(1);
        stats.UsersWithPush.Should().Be(1);
        stats.TotalCards.Should().Be(3);
        stats.TotalDecks.Should().Be(1);
        // suspended cards are excluded even if due
        stats.DueCards.Should().Be(1);
    }

    [Fact]
    public async Task GetStats_RegistrationsCountedByWindow()
    {
        await using var db = _db.CreateDbContext();
        var now = DateTimeOffset.UtcNow;
        db.Logs.AddRange(
            MakeLog(LogType.UserRegistered, "today", createdAt: now.AddHours(-1)),
            MakeLog(LogType.UserRegistered, "6 days ago", createdAt: now.AddDays(-6)),
            MakeLog(LogType.UserRegistered, "10 days ago", createdAt: now.AddDays(-10)),
            MakeLog(LogType.UserRegistered, "40 days ago", createdAt: now.AddDays(-40)),
            // Non-signup logs in the window should not count
            MakeLog(LogType.Notification, "push", createdAt: now.AddHours(-2)),
            MakeLog(LogType.Admin, "admin", createdAt: now.AddDays(-2)));
        await db.SaveChangesAsync();

        var svc = new AdminService(db);

        var stats = await svc.GetStats();

        stats.RegistrationsLast7d.Should().Be(2);   // today + 6 days ago
        stats.RegistrationsLast30d.Should().Be(3);  // + 10 days ago, excludes 40 days
    }
}
