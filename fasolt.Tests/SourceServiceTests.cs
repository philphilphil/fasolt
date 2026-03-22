using FluentAssertions;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class SourceServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task ListSources_ReturnsEmpty_WhenNoCards()
    {
        await using var db = _db.CreateDbContext();
        var svc = new SourceService(db);

        var result = await svc.ListSources(UserId);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListSources_ReturnsGroupedSources()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var sourceSvc = new SourceService(db);

        await cardSvc.BulkCreateCards(UserId,
            [new BulkCardItem("Q1", "A1"), new BulkCardItem("Q2", "A2")],
            sourceFile: "alpha.md", deckId: null);

        await cardSvc.BulkCreateCards(UserId,
            [new BulkCardItem("Q3", "A3")],
            sourceFile: "beta.md", deckId: null);

        var result = await sourceSvc.ListSources(UserId);

        var alpha = result.Items.SingleOrDefault(i => i.SourceFile == "alpha.md");
        alpha.Should().NotBeNull();
        alpha!.CardCount.Should().Be(2);

        var beta = result.Items.SingleOrDefault(i => i.SourceFile == "beta.md");
        beta.Should().NotBeNull();
        beta!.CardCount.Should().Be(1);
    }

    [Fact]
    public async Task ListSources_ExcludesNullSourceFile()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);
        var sourceSvc = new SourceService(db);

        await cardSvc.CreateCard(UserId, "No source Q", "No source A", null, null);

        var result = await sourceSvc.ListSources(UserId);

        result.Items.Should().NotContain(i => i.SourceFile == null);
    }
}
