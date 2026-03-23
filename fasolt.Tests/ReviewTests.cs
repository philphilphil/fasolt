using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Services;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class ReviewTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task NewCard_AppearsDue()
    {
        await using var db = _db.CreateDbContext();
        var cardSvc = new CardService(db);

        var card = await cardSvc.CreateCard(UserId, "Review Q?", "Review A.", "review-source.md", "## Section");

        // New cards have DueAt = null, so they're immediately due
        var dueCards = await db.Cards
            .Where(c => c.UserId == UserId && (c.DueAt == null || c.DueAt <= DateTimeOffset.UtcNow))
            .ToListAsync();

        dueCards.Should().Contain(c => c.PublicId == card.Id);
        var target = dueCards.Single(c => c.PublicId == card.Id);
        target.SourceFile.Should().Be("review-source.md");
        target.SourceHeading.Should().Be("## Section");
    }
}
