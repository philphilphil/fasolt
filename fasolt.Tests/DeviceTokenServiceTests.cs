using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class DeviceTokenServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private DeviceTokenService CreateService(AppDbContext db)
    {
        var store = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<AppUser>(db);
        var userManager = new UserManager<AppUser>(
            store, Options.Create(new IdentityOptions()), null!, null!, null!, null!, null!, null!,
            NullLogger<UserManager<AppUser>>.Instance);
        return new DeviceTokenService(db, userManager);
    }

    [Fact]
    public async Task UpsertDeviceToken_CreatesNew()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        await svc.UpsertDeviceToken(UserId, "token-abc");

        var stored = await db.DeviceTokens.SingleAsync(t => t.UserId == UserId);
        stored.Token.Should().Be("token-abc");
    }

    [Fact]
    public async Task UpsertDeviceToken_UpdatesExisting()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        await svc.UpsertDeviceToken(UserId, "old-token");
        await svc.UpsertDeviceToken(UserId, "new-token");

        var stored = await db.DeviceTokens.SingleAsync(t => t.UserId == UserId);
        stored.Token.Should().Be("new-token");
    }

    [Fact]
    public async Task DeleteDeviceToken_RemovesToken()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        await svc.UpsertDeviceToken(UserId, "to-delete");
        await svc.DeleteDeviceToken(UserId);

        var count = await db.DeviceTokens.CountAsync(t => t.UserId == UserId);
        count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteDeviceToken_WhenNoneExists_IsIdempotent()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        // Should not throw
        await svc.DeleteDeviceToken(UserId);

        var count = await db.DeviceTokens.CountAsync(t => t.UserId == UserId);
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetSettings_ReturnsDefaultInterval()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var settings = await svc.GetSettings(UserId);

        settings.IntervalHours.Should().Be(8);
        settings.HasDeviceToken.Should().BeFalse();
    }

    [Fact]
    public async Task GetSettings_ReflectsDeviceToken()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        await svc.UpsertDeviceToken(UserId, "test-token");
        var settings = await svc.GetSettings(UserId);

        settings.HasDeviceToken.Should().BeTrue();
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(6, true)]
    [InlineData(8, true)]
    [InlineData(10, true)]
    [InlineData(12, true)]
    [InlineData(24, true)]
    [InlineData(5, false)]
    [InlineData(0, false)]
    [InlineData(48, false)]
    public async Task UpdateSettings_ValidatesInterval(int interval, bool expected)
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var result = await svc.UpdateSettings(UserId, interval);

        result.Should().Be(expected);
    }
}
