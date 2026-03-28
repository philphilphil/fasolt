using FluentAssertions;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class SchedulingSettingsServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GetSettings_ReturnsDefaults_WhenNotCustomized()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.GetSettings(UserId);

        result.DesiredRetention.Should().Be(0.9);
        result.MaximumInterval.Should().Be(36500);
    }

    [Fact]
    public async Task UpdateSettings_SavesAndReturnsValues()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.85, 365);

        result.Should().NotBeNull();
        result!.DesiredRetention.Should().Be(0.85);
        result.MaximumInterval.Should().Be(365);
    }

    [Fact]
    public async Task UpdateSettings_PersistsValues()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);
        await svc.UpdateSettings(UserId, 0.85, 365);

        await using var db2 = _db.CreateDbContext();
        var svc2 = new SchedulingSettingsService(db2);
        var result = await svc2.GetSettings(UserId);

        result.DesiredRetention.Should().Be(0.85);
        result.MaximumInterval.Should().Be(365);
    }

    [Fact]
    public async Task UpdateSettings_RejectsRetentionTooLow()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.5, 365);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsRetentionTooHigh()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.99, 365);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsIntervalTooLow()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.9, 0);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsIntervalTooHigh()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.9, 40000);

        result.Should().BeNull();
    }
}
