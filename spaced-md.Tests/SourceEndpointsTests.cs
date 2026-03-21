using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SpacedMd.Server.Application.Dtos;
using SpacedMd.Tests.Helpers;

namespace SpacedMd.Tests;

/// <summary>
/// Source endpoint tests.
/// The /api/sources endpoint uses Postgres-specific raw SQL (FILTER clause, ::int casts).
/// Tests that exercise that SQL are skipped for the InMemory provider.
/// Auth behaviour (401 without token) is provider-agnostic and tested without skipping.
/// </summary>
public class SourceEndpointsTests : IAsyncLifetime
{
    private readonly SpacedMdFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _factory.SeedTestUserAsync();
        _client = _factory.CreateAuthenticatedClient();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ListSources_RequiresAuth()
    {
        // Unauthenticated request should return 401
        using var unauthClient = _factory.CreateClient();

        var response = await unauthClient.GetAsync("/api/sources");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(Skip = "Requires Postgres — raw SQL uses FILTER clause and ::int casts not supported by the InMemory provider")]
    public async Task ListSources_ReturnsEmpty_WhenNoCards()
    {
        var response = await _client.GetAsync("/api/sources");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SourceListResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
    }

    [Fact(Skip = "Requires Postgres — raw SQL uses FILTER clause and ::int casts not supported by the InMemory provider")]
    public async Task ListSources_ReturnsGroupedSources_WhenCardsExist()
    {
        // Create cards for two different source files
        await _client.PostAsJsonAsync("/api/cards/bulk",
            new BulkCreateCardsRequest("alpha.md", null,
            [
                new BulkCardItem("Q1", "A1"),
                new BulkCardItem("Q2", "A2"),
            ]));

        await _client.PostAsJsonAsync("/api/cards/bulk",
            new BulkCreateCardsRequest("beta.md", null,
            [
                new BulkCardItem("Q3", "A3"),
            ]));

        var response = await _client.GetAsync("/api/sources");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SourceListResponse>();
        result.Should().NotBeNull();

        var alpha = result!.Items.SingleOrDefault(i => i.SourceFile == "alpha.md");
        alpha.Should().NotBeNull();
        alpha!.CardCount.Should().Be(2);

        var beta = result.Items.SingleOrDefault(i => i.SourceFile == "beta.md");
        beta.Should().NotBeNull();
        beta!.CardCount.Should().Be(1);
    }

    [Fact(Skip = "Requires Postgres — raw SQL uses FILTER clause and ::int casts not supported by the InMemory provider")]
    public async Task ListSources_ExcludesNullSourceFile()
    {
        // Cards without a sourceFile should not appear in the sources list
        await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest(null, null, "No source Q", "No source A"));

        var response = await _client.GetAsync("/api/sources");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SourceListResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().NotContain(i => i.SourceFile == null);
    }
}
