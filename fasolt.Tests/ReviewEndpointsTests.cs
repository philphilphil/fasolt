using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Fasolt.Server.Application.Dtos;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class ReviewEndpointsTests : IAsyncLifetime
{
    private readonly FasoltFactory _factory = new();
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
    public async Task GetDueCards_IncludesSourceFile()
    {
        // Arrange — create a card with sourceFile and sourceHeading
        var createResp = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest("review-source.md", "## Section", "Review Q?", "Review A."));
        createResp.IsSuccessStatusCode.Should().BeTrue();
        var created = await createResp.Content.ReadFromJsonAsync<CardDto>();

        // Act — new cards have DueAt = null so they are immediately due
        var response = await _client.GetAsync("/api/review/due");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dueCards = await response.Content.ReadFromJsonAsync<List<DueCardDto>>();
        dueCards.Should().NotBeNull();

        var target = dueCards!.FirstOrDefault(c => c.Id == created!.Id);
        target.Should().NotBeNull("the newly created card should appear in due cards");
        target!.SourceFile.Should().Be("review-source.md");
        target.SourceHeading.Should().Be("## Section");
    }
}
