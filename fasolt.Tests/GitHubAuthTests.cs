using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class GitHubAuthTests : IAsyncLifetime
{
    private readonly TestDb _db = new();

    public async Task InitializeAsync() => await _db.InitializeAsync();
    public async Task DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task CanCreateUserWithExternalProvider()
    {
        await using var db = _db.CreateDbContext();
        var user = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "github@test.com",
            NormalizedUserName = "GITHUB@TEST.COM",
            Email = "github@test.com",
            NormalizedEmail = "GITHUB@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ExternalProvider = "GitHub",
            ExternalProviderId = "12345",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var found = await db.Users.FirstOrDefaultAsync(u => u.ExternalProvider == "GitHub" && u.ExternalProviderId == "12345");
        found.Should().NotBeNull();
        found!.Email.Should().Be("github@test.com");
    }

    [Fact]
    public async Task LookupByExternalProvider_FindsGitHubUser()
    {
        await using var db = _db.CreateDbContext();
        db.Users.Add(new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "ghuser@test.com",
            NormalizedUserName = "GHUSER@TEST.COM",
            Email = "ghuser@test.com",
            NormalizedEmail = "GHUSER@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ExternalProvider = "GitHub",
            ExternalProviderId = "67890",
        });
        await db.SaveChangesAsync();

        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalProvider == "GitHub" && u.ExternalProviderId == "67890");
        user.Should().NotBeNull();
        user!.Email.Should().Be("ghuser@test.com");
    }

    [Fact]
    public async Task LookupByExternalProvider_DoesNotFindPasswordUser()
    {
        // The seeded test user has no ExternalProvider
        await using var db = _db.CreateDbContext();

        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalProvider == "GitHub" && u.ExternalProviderId == "99999");
        user.Should().BeNull();
    }

    [Fact]
    public async Task EmailCollisionCheck_FindsExistingPasswordUser()
    {
        // The seeded test user (test@fasolt.test) has no ExternalProvider
        await using var db = _db.CreateDbContext();

        var existing = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == "TEST@FASOLT.TEST");
        existing.Should().NotBeNull();
        existing!.ExternalProvider.Should().BeNull();
    }

    [Fact]
    public async Task UniqueIndex_PreventsDuplicateExternalProvider()
    {
        await using var db = _db.CreateDbContext();
        db.Users.Add(new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "first@test.com",
            NormalizedUserName = "FIRST@TEST.COM",
            Email = "first@test.com",
            NormalizedEmail = "FIRST@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ExternalProvider = "GitHub",
            ExternalProviderId = "same-id",
        });
        await db.SaveChangesAsync();

        await using var db2 = _db.CreateDbContext();
        db2.Users.Add(new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "second@test.com",
            NormalizedUserName = "SECOND@TEST.COM",
            Email = "second@test.com",
            NormalizedEmail = "SECOND@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            ExternalProvider = "GitHub",
            ExternalProviderId = "same-id",
        });

        var act = () => db2.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
