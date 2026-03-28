using FluentAssertions;
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
        var reviewSvc = new ReviewService(db, TimeProvider.System);

        var card = await cardSvc.CreateCard(UserId, "Review Q?", "Review A.", "review-source.md", "## Section");

        var dueCards = await reviewSvc.GetDueCards(UserId);

        dueCards.Should().Contain(c => c.Id == card.Id);
        var target = dueCards.Single(c => c.Id == card.Id);
        target.SourceFile.Should().Be("review-source.md");
        target.SourceHeading.Should().Be("## Section");
    }
}
