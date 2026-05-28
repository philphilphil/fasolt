using FluentAssertions;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class McpResourceServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    private McpResourceService CreateService(AppDbContext db) =>
        new McpResourceService(
            db,
            new ReviewService(db, TimeProvider.System, new StudyStatsService(db, TimeProvider.System)),
            TimeProvider.System);

    [Fact]
    public async Task ListUserResourcesAsync_NoDecks_ReturnsTwoStatics()
    {
        await using var db = _db.CreateDbContext();
        var svc = CreateService(db);

        var entries = await svc.ListUserResourcesAsync(UserId);

        entries.Should().HaveCount(2);
        entries.Should().Contain(e => e.Uri == "fasolt://due-today");
        entries.Should().Contain(e => e.Uri == "fasolt://recent");
    }
}
