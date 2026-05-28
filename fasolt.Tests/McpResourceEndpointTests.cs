using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

[Collection(WebAppCollection.Name)]
public class McpResourceEndpointTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public McpResourceEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                    ["GITHUB_CLIENT_ID"] = "test-github-id",
                });
            });
        });
    }

    [Fact]
    public async Task ListUserResources_ServiceReturnsActiveDecksAndStatics_ViaScopedDi()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userId = $"int-test-{Guid.NewGuid():N}";
        db.Users.Add(new AppUser
        {
            Id = userId,
            UserName = $"{userId}@test.local",
            NormalizedUserName = $"{userId}@test.local".ToUpperInvariant(),
            Email = $"{userId}@test.local",
            NormalizedEmail = $"{userId}@test.local".ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        });
        db.Decks.Add(new Deck
        {
            Id = Guid.NewGuid(),
            PublicId = "deck-int-" + Guid.NewGuid().ToString("N")[..3],
            UserId = userId,
            Name = "IntegrationDeck",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var svc = scope.ServiceProvider.GetRequiredService<McpResourceService>();
        var entries = await svc.ListUserResourcesAsync(userId);

        entries.Should().Contain(e => e.Name == "IntegrationDeck");
        entries.Should().Contain(e => e.Uri == "fasolt://due-today");
        entries.Should().Contain(e => e.Uri == "fasolt://recent");

        // cleanup
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"AspNetUsers\" WHERE \"Id\" = {userId}");
    }

    [Fact]
    public async Task McpEndpoint_RejectsUnauthenticatedRequest()
    {
        var client = _factory.CreateClient();
        var req = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "resources/list",
            @params = new { },
        };

        var response = await client.PostAsJsonAsync("/mcp", req);

        // Either 401 (auth challenge) or a JSON-RPC error response — accept multiple
        // since the SDK / auth pipeline may convert one to the other.
        response.StatusCode.Should().BeOneOf(
            System.Net.HttpStatusCode.Unauthorized,
            System.Net.HttpStatusCode.Forbidden,
            System.Net.HttpStatusCode.BadRequest);
    }
}
