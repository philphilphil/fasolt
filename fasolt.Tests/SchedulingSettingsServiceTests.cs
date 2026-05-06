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
        result.DayStartHour.Should().Be(4);
        result.TimeZone.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_SavesAndReturnsValues()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.85, 365, 6, "Europe/Berlin");

        result.Should().NotBeNull();
        result!.DesiredRetention.Should().Be(0.85);
        result.MaximumInterval.Should().Be(365);
        result.DayStartHour.Should().Be(6);
        result.TimeZone.Should().Be("Europe/Berlin");
    }

    [Fact]
    public async Task UpdateSettings_PersistsValues()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);
        await svc.UpdateSettings(UserId, 0.85, 365, 6, "Europe/Berlin");

        await using var db2 = _db.CreateDbContext();
        var svc2 = new SchedulingSettingsService(db2);
        var result = await svc2.GetSettings(UserId);

        result.DesiredRetention.Should().Be(0.85);
        result.MaximumInterval.Should().Be(365);
        result.DayStartHour.Should().Be(6);
        result.TimeZone.Should().Be("Europe/Berlin");
    }

    [Fact]
    public async Task UpdateSettings_RejectsRetentionTooLow()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.5, 365, 4, "UTC");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsRetentionTooHigh()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.99, 365, 4, "UTC");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsIntervalTooLow()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.9, 0, 4, "UTC");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsIntervalTooHigh()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.9, 40000, 4, "UTC");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsDayStartHourTooLow()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.9, 365, -1, "UTC");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsDayStartHourTooHigh()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.9, 365, 24, "UTC");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateSettings_RejectsInvalidTimeZone()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SchedulingSettingsService(db);

        var result = await svc.UpdateSettings(UserId, 0.9, 365, 4, "Not/A/RealZone");

        result.Should().BeNull();
    }
}
