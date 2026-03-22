using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Fasolt.Server.Application.Dtos;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

public class DeckEndpointsTests : IAsyncLifetime
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
    public async Task CreateDeck_ReturnsDeck()
    {
        var request = new CreateDeckRequest("My Test Deck", "A deck for testing");

        var response = await _client.PostAsJsonAsync("/api/decks", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var deck = await response.Content.ReadFromJsonAsync<DeckDto>();
        deck.Should().NotBeNull();
        deck!.Name.Should().Be("My Test Deck");
        deck.Description.Should().Be("A deck for testing");
        deck.CardCount.Should().Be(0);
    }

    [Fact]
    public async Task ListDecks_ReturnsDecks()
    {
        // Arrange — create two decks
        await _client.PostAsJsonAsync("/api/decks", new CreateDeckRequest("Deck Alpha", null));
        await _client.PostAsJsonAsync("/api/decks", new CreateDeckRequest("Deck Beta", null));

        // Act
        var response = await _client.GetAsync("/api/decks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var decks = await response.Content.ReadFromJsonAsync<List<DeckDto>>();
        decks.Should().NotBeNull();
        decks!.Count.Should().BeGreaterThanOrEqualTo(2);
        decks.Should().Contain(d => d.Name == "Deck Alpha");
        decks.Should().Contain(d => d.Name == "Deck Beta");
    }

    [Fact]
    public async Task GetDeckDetail_IncludesCardSourceFile()
    {
        // Arrange — create a deck and a card with sourceFile/sourceHeading, then link them
        var deckResp = await _client.PostAsJsonAsync("/api/decks",
            new CreateDeckRequest("Detail Deck", null));
        deckResp.IsSuccessStatusCode.Should().BeTrue();
        var deck = await deckResp.Content.ReadFromJsonAsync<DeckDto>();

        var cardResp = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest("source.md", "## Section", "Card Q?", "Card A."));
        cardResp.IsSuccessStatusCode.Should().BeTrue();
        var card = await cardResp.Content.ReadFromJsonAsync<CardDto>();

        var addResp = await _client.PostAsJsonAsync($"/api/decks/{deck!.Id}/cards",
            new AddCardsToDeckRequest([card!.Id]));
        addResp.IsSuccessStatusCode.Should().BeTrue();

        // Act
        var response = await _client.GetAsync($"/api/decks/{deck.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await response.Content.ReadFromJsonAsync<DeckDetailDto>();
        detail.Should().NotBeNull();
        detail!.Cards.Should().HaveCount(1);

        var deckCard = detail.Cards[0];
        deckCard.SourceFile.Should().Be("source.md");
        deckCard.SourceHeading.Should().Be("## Section");
    }
}
