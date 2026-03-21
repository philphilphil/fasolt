using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SpacedMd.Server.Application.Dtos;
using SpacedMd.Server.Domain.Entities;
using SpacedMd.Tests.Helpers;

namespace SpacedMd.Tests;

public class CardEndpointsTests : IAsyncLifetime
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

    // ── CreateCard ────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCard_WithSourceFile_ReturnsCard()
    {
        var request = new CreateCardRequest("notes.md", "Introduction", "What is X?", "X is Y.");

        var response = await _client.PostAsJsonAsync("/api/cards", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var card = await response.Content.ReadFromJsonAsync<CardDto>();
        card.Should().NotBeNull();
        card!.SourceFile.Should().Be("notes.md");
        card.SourceHeading.Should().Be("Introduction");
        card.Front.Should().Be("What is X?");
        card.Back.Should().Be("X is Y.");
    }

    [Fact]
    public async Task CreateCard_WithoutSourceFile_ReturnsCard()
    {
        var request = new CreateCardRequest(null, null, "Capital of France?", "Paris");

        var response = await _client.PostAsJsonAsync("/api/cards", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var card = await response.Content.ReadFromJsonAsync<CardDto>();
        card.Should().NotBeNull();
        card!.SourceFile.Should().BeNull();
        card.Front.Should().Be("Capital of France?");
    }

    // ── ListCards ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListCards_FilterBySourceFile()
    {
        // Arrange — create cards for two different source files
        await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest("file-a.md", null, "Front A", "Back A"));
        await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest("file-b.md", null, "Front B", "Back B"));

        // Act
        var response = await _client.GetAsync("/api/cards?sourceFile=file-a.md");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<CardDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().AllSatisfy(c => c.SourceFile.Should().Be("file-a.md"));
        result.Items.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ListCards_FilterByDeckId()
    {
        // Arrange — create a deck, create a card, add the card to the deck
        var deckResponse = await _client.PostAsJsonAsync("/api/decks",
            new CreateDeckRequest("Test Deck for Filter", null));
        deckResponse.IsSuccessStatusCode.Should().BeTrue();
        var deck = await deckResponse.Content.ReadFromJsonAsync<DeckDto>();

        var cardResponse = await _client.PostAsJsonAsync("/api/cards",
            new CreateCardRequest(null, null, "Deck Filter Q?", "Deck Filter A."));
        cardResponse.IsSuccessStatusCode.Should().BeTrue();
        var card = await cardResponse.Content.ReadFromJsonAsync<CardDto>();

        // Add card to deck
        await _client.PostAsJsonAsync($"/api/decks/{deck!.Id}/cards",
            new AddCardsToDeckRequest([card!.Id]));

        // Act — filter by deckId
        var response = await _client.GetAsync($"/api/cards?deckId={deck.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<CardDto>>();
        result.Should().NotBeNull();
        result!.Items.Should().Contain(c => c.Id == card.Id);
    }

    // ── BulkCreate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task BulkCreate_CreatesCards_WithSourceFileOverride()
    {
        // Per-card sourceFile should override request-level default
        var request = new BulkCreateCardsRequest(
            SourceFile: "default-source.md",
            DeckId: null,
            Cards:
            [
                new BulkCardItem("Q from default", "A from default"),
                new BulkCardItem("Q from override", "A from override", SourceFile: "override-source.md"),
            ]);

        var response = await _client.PostAsJsonAsync("/api/cards/bulk", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<BulkCreateCardsResponse>();
        result.Should().NotBeNull();
        result!.Created.Should().HaveCount(2);
        result.Skipped.Should().BeEmpty();

        var defaultCard = result.Created.Single(c => c.Front == "Q from default");
        defaultCard.SourceFile.Should().Be("default-source.md");

        var overrideCard = result.Created.Single(c => c.Front == "Q from override");
        overrideCard.SourceFile.Should().Be("override-source.md");
    }

    [Fact]
    public async Task BulkCreate_SkipsDuplicates_SameSourceFile()
    {
        const string sourceFile = "dup-test.md";

        // First batch — creates the card
        var first = await _client.PostAsJsonAsync("/api/cards/bulk",
            new BulkCreateCardsRequest(sourceFile, null,
            [
                new BulkCardItem("Duplicate Q", "Duplicate A"),
            ]));
        first.IsSuccessStatusCode.Should().BeTrue();

        // Second batch — same front + same sourceFile = duplicate
        var second = await _client.PostAsJsonAsync("/api/cards/bulk",
            new BulkCreateCardsRequest(sourceFile, null,
            [
                new BulkCardItem("Duplicate Q", "Different A"),
            ]));

        second.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await second.Content.ReadFromJsonAsync<BulkCreateCardsResponse>();
        result.Should().NotBeNull();
        result!.Created.Should().BeEmpty();
        result.Skipped.Should().HaveCount(1);
        result.Skipped[0].Front.Should().Be("Duplicate Q");
    }

    [Fact]
    public async Task BulkCreate_AllowsSameFront_DifferentSourceFile()
    {
        // Same front text but different sourceFile = NOT a duplicate
        var request = new BulkCreateCardsRequest(
            SourceFile: null,
            DeckId: null,
            Cards:
            [
                new BulkCardItem("Shared Front Text", "Back A", SourceFile: "source-one.md"),
                new BulkCardItem("Shared Front Text", "Back B", SourceFile: "source-two.md"),
            ]);

        var response = await _client.PostAsJsonAsync("/api/cards/bulk", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<BulkCreateCardsResponse>();
        result.Should().NotBeNull();
        result!.Created.Should().HaveCount(2);
        result.Skipped.Should().BeEmpty();
    }
}
