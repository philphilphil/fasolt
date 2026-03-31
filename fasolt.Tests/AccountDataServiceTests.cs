using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class AccountDataServiceTests : IAsyncLifetime
{
    private readonly TestDb _db = new();
    private string UserId => _db.UserId;

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task DeleteUserData_RemovesAllUserEntities()
    {
        // Seed test data
        await using (var db = _db.CreateDbContext())
        {
            var deck = new Deck
            {
                Id = Guid.NewGuid(),
                PublicId = "deck00000002",
                UserId = UserId,
                Name = "Doomed Deck",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Decks.Add(deck);

            var card = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = "card00000002",
                UserId = UserId,
                Front = "Gone?",
                Back = "Gone.",
                State = "new",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Cards.Add(card);
            db.DeckCards.Add(new DeckCard { DeckId = deck.Id, CardId = card.Id });

            db.DeckSnapshots.Add(new DeckSnapshot
            {
                Id = Guid.NewGuid(),
                PublicId = "snap00000001",
                DeckId = deck.Id,
                UserId = UserId,
                Version = 1,
                CardCount = 1,
                Data = "{}",
                CreatedAt = DateTimeOffset.UtcNow,
            });

            db.ConsentGrants.Add(new ConsentGrant
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                ClientId = "test-client",
                GrantedAt = DateTimeOffset.UtcNow,
            });

            db.DeviceTokens.Add(new DeviceToken
            {
                UserId = UserId,
                Token = "test-token",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
        }

        await using var ctx = _db.CreateDbContext();
        var svc = new AccountDataService(ctx);

        await svc.DeleteUserData(UserId);

        await using var verify = _db.CreateDbContext();
        (await verify.Users.AnyAsync(u => u.Id == UserId)).Should().BeFalse();
        (await verify.Cards.AnyAsync(c => c.UserId == UserId)).Should().BeFalse();
        (await verify.Decks.AnyAsync(d => d.UserId == UserId)).Should().BeFalse();
        (await verify.DeckSnapshots.AnyAsync(s => s.UserId == UserId)).Should().BeFalse();
        (await verify.ConsentGrants.AnyAsync(c => c.UserId == UserId)).Should().BeFalse();
        (await verify.DeviceTokens.AnyAsync(d => d.UserId == UserId)).Should().BeFalse();
    }

    [Fact]
    public async Task ExportUserData_ReturnsAllUserData()
    {
        // Seed test data
        await using (var db = _db.CreateDbContext())
        {
            var deck = new Deck
            {
                Id = Guid.NewGuid(),
                PublicId = "deck00000001",
                UserId = UserId,
                Name = "Test Deck",
                Description = "A test deck",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Decks.Add(deck);

            var card = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = "card00000001",
                UserId = UserId,
                Front = "What is X?",
                Back = "X is Y.",
                SourceFile = "notes.md",
                SourceHeading = "Intro",
                State = "new",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Cards.Add(card);
            db.DeckCards.Add(new DeckCard { DeckId = deck.Id, CardId = card.Id });
            await db.SaveChangesAsync();
        }

        await using var ctx = _db.CreateDbContext();
        var svc = new AccountDataService(ctx);
        var user = await ctx.Users.FirstAsync(u => u.Id == UserId);

        var export = await svc.ExportUserData(user);

        export.Account.Email.Should().Be("test@fasolt.test");
        export.Cards.Should().HaveCount(1);
        export.Cards[0].Front.Should().Be("What is X?");
        export.Cards[0].PublicId.Should().Be("card00000001");
        export.Decks.Should().HaveCount(1);
        export.Decks[0].Name.Should().Be("Test Deck");
        export.Decks[0].Cards.Should().Contain("card00000001");
        export.Sources.Should().Contain("notes.md");
        export.ExportedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}
