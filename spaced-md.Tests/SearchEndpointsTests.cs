using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SpacedMd.Server.Application.Dtos;
using SpacedMd.Server.Api.Endpoints;
using SpacedMd.Tests.Helpers;

namespace SpacedMd.Tests;

/// <summary>
/// Search endpoint integration tests. Requires Docker Postgres running.
/// </summary>
public class SearchEndpointsTests : IAsyncLifetime
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
    public async Task Search_RequiresMinLength()
    {
        // Single-character query is below the 2-char minimum — returns empty result
        var response = await _client.GetAsync("/api/search?q=a");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        result.Should().NotBeNull();
        result!.Cards.Should().BeEmpty();
        result.Decks.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_DoesNotReturnFileResults()
    {
        // The search response type has only Cards and Decks — no "Files" property.
        // Use a single-char query so the endpoint returns early (before raw SQL)
        // yet still produces a valid SearchResponse with the correct shape.
        var response = await _client.GetAsync("/api/search?q=x");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Deserialize as a dynamic JSON object to confirm no "files" property exists
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync());
        var root = doc.RootElement;

        root.TryGetProperty("files", out _).Should().BeFalse(
            "the search response should not contain a 'files' property");
        root.TryGetProperty("Files", out _).Should().BeFalse(
            "the search response should not contain a 'Files' property");
    }

    [Fact]
    public async Task Search_FindsCards_ByFrontText()
    {
        await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest(null, null, "Photosynthesis question", "It makes food from light."));

        var response = await _client.GetAsync("/api/search?q=Photosynthesis");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        result!.Cards.Should().Contain(c => c.Headline.Contains("Photosynthesis"));
    }

    [Fact]
    public async Task Search_FindsDecks_ByName()
    {
        await _client.PostAsJsonAsync("/api/decks",
            new CreateDeckRequest("Biology Fundamentals", "Core biology concepts"));

        var response = await _client.GetAsync("/api/search?q=Biology");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SearchResponse>();
        result!.Decks.Should().Contain(d => d.Headline.Contains("Biology"));
    }
}
