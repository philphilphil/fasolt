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
        // Seed test data including all entity types
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

            db.DeckSnapshots.Add(new DeckSnapshot
            {
                Id = Guid.NewGuid(),
                PublicId = "snap00000001",
                DeckId = deck.Id,
                UserId = UserId,
                Version = 1,
                CardCount = 1,
                Data = """{"cards":[]}""",
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
                Token = "export-token",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });

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
        export.Snapshots.Should().HaveCount(1);
        export.Snapshots[0].DeckName.Should().Be("Test Deck");
        export.Snapshots[0].CardCount.Should().Be(1);
        export.ConsentGrants.Should().HaveCount(1);
        export.ConsentGrants[0].ClientId.Should().Be("test-client");
        export.DeviceToken.Should().NotBeNull();
        export.DeviceToken!.Token.Should().Be("export-token");
        export.ExportedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExportUserData_WithNoData_ReturnsEmptyExport()
    {
        await using var ctx = _db.CreateDbContext();
        var svc = new AccountDataService(ctx);
        var user = await ctx.Users.FirstAsync(u => u.Id == UserId);

        var export = await svc.ExportUserData(user);

        export.Account.Email.Should().Be("test@fasolt.test");
        export.Cards.Should().BeEmpty();
        export.Decks.Should().BeEmpty();
        export.Sources.Should().BeEmpty();
        export.Snapshots.Should().BeEmpty();
        export.ConsentGrants.Should().BeEmpty();
        export.DeviceToken.Should().BeNull();
    }

    [Fact]
    public async Task DeleteUserData_WithNonExistentUser_CompletesWithoutError()
    {
        await using var ctx = _db.CreateDbContext();
        var svc = new AccountDataService(ctx);

        var act = () => svc.DeleteUserData("nonexistent-user-id");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteUserData_DoesNotAffectOtherUsers()
    {
        // Create a second user with data
        string otherUserId;
        await using (var db = _db.CreateDbContext())
        {
            var otherUser = new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "other@fasolt.test",
                NormalizedUserName = "OTHER@FASOLT.TEST",
                Email = "other@fasolt.test",
                NormalizedEmail = "OTHER@FASOLT.TEST",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            };
            db.Users.Add(otherUser);
            otherUserId = otherUser.Id;

            var otherCard = new Card
            {
                Id = Guid.NewGuid(),
                PublicId = "cardother001",
                UserId = otherUserId,
                Front = "Other Q",
                Back = "Other A",
                State = "new",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Cards.Add(otherCard);

            // Also add a card for the user being deleted
            db.Cards.Add(new Card
            {
                Id = Guid.NewGuid(),
                PublicId = "carddel00001",
                UserId = UserId,
                Front = "Delete me",
                Back = "Gone",
                State = "new",
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
        }

        await using var ctx = _db.CreateDbContext();
        var svc = new AccountDataService(ctx);

        await svc.DeleteUserData(UserId);

        await using var verify = _db.CreateDbContext();
        (await verify.Users.AnyAsync(u => u.Id == otherUserId)).Should().BeTrue();
        (await verify.Cards.CountAsync(c => c.UserId == otherUserId)).Should().Be(1);
        (await verify.Users.AnyAsync(u => u.Id == UserId)).Should().BeFalse();
    }

    [Fact]
    public async Task ExportUserData_DoesNotIncludeOtherUsersData()
    {
        // Create a second user with data
        await using (var db = _db.CreateDbContext())
        {
            var otherUser = new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "other@fasolt.test",
                NormalizedUserName = "OTHER@FASOLT.TEST",
                Email = "other@fasolt.test",
                NormalizedEmail = "OTHER@FASOLT.TEST",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            };
            db.Users.Add(otherUser);

            db.Cards.Add(new Card
            {
                Id = Guid.NewGuid(),
                PublicId = "cardother001",
                UserId = otherUser.Id,
                Front = "Other user's card",
                Back = "Should not appear",
                State = "new",
                CreatedAt = DateTimeOffset.UtcNow,
            });

            // Add a card for the target user
            db.Cards.Add(new Card
            {
                Id = Guid.NewGuid(),
                PublicId = "cardmine0001",
                UserId = UserId,
                Front = "My card",
                Back = "Should appear",
                State = "new",
                CreatedAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();
        }

        await using var ctx = _db.CreateDbContext();
        var svc = new AccountDataService(ctx);
        var user = await ctx.Users.FirstAsync(u => u.Id == UserId);

        var export = await svc.ExportUserData(user);

        export.Cards.Should().HaveCount(1);
        export.Cards[0].Front.Should().Be("My card");
    }
}
