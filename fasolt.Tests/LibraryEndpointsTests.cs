using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Application.Dtos;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

/// <summary>
/// The library read endpoints are the anonymous funnel, so their auth behaviour is
/// worth pinning at the HTTP level rather than only in the service tests.
/// </summary>
[Collection(WebAppCollection.Name)]
public class LibraryEndpointsTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public LibraryEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private static async Task<(string UserId, Deck Public, Deck Unlisted, Deck Private)> Seed(AppDbContext db)
    {
        var userId = $"lib-test-{Guid.NewGuid():N}";
        var email = $"{userId}@test.local";
        db.Users.Add(new AppUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
            Handle = $"h{Guid.NewGuid().ToString("N")[..10]}",
        });

        Deck MakeDeck(DeckVisibility visibility, string name) => new()
        {
            Id = Guid.NewGuid(),
            PublicId = NanoIdGenerator.New(),
            UserId = userId,
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
            Visibility = visibility,
            PublishedAt = visibility == DeckVisibility.Private ? null : DateTimeOffset.UtcNow,
        };

        var publicDeck = MakeDeck(DeckVisibility.Public, "Endpoint Public Deck");
        var unlistedDeck = MakeDeck(DeckVisibility.Unlisted, "Endpoint Unlisted Deck");
        var privateDeck = MakeDeck(DeckVisibility.Private, "Endpoint Private Deck");
        db.Decks.AddRange(publicDeck, unlistedDeck, privateDeck);

        await db.SaveChangesAsync();
        return (userId, publicDeck, unlistedDeck, privateDeck);
    }

    [Fact]
    public async Task AnonymousVisitorSeesPublicDecksOnlyInTheListing()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, publicDeck, unlistedDeck, privateDeck) = await Seed(db);

        try
        {
            var client = _factory.CreateClient();

            var listing = await client.GetFromJsonAsync<LibraryListResponse>("/api/library?pageSize=50");
            listing.Should().NotBeNull();
            listing!.Items.Should().Contain(d => d.Id == publicDeck.PublicId);
            listing.Items.Should().NotContain(d => d.Id == unlistedDeck.PublicId);
            listing.Items.Should().NotContain(d => d.Id == privateDeck.PublicId);

            // Unlisted resolves by direct id; private never does.
            (await client.GetAsync($"/api/library/decks/{unlistedDeck.PublicId}"))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync($"/api/library/decks/{publicDeck.PublicId}"))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync($"/api/library/decks/{privateDeck.PublicId}"))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);

            // Importing still requires an account.
            (await client.PostAsync($"/api/library/decks/{publicDeck.PublicId}/copy", null))
                .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task LibraryPagesGetInjectedMetaTags()
    {
        // The middleware rewrites the built index.html; point the web root at a
        // throwaway copy so the test does not depend on a client build being present.
        var webRoot = Directory.CreateTempSubdirectory("fasolt-seo-").FullName;
        await File.WriteAllTextAsync(Path.Combine(webRoot, "index.html"), """
            <!doctype html>
            <html><head>
            <meta name="description" content="original description" />
            <title>fasolt</title>
            </head><body><div id="app"></div></body></html>
            """);

        var seoFactory = _factory.WithWebHostBuilder(builder => builder.UseWebRoot(webRoot));

        using var scope = seoFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, publicDeck, unlistedDeck, privateDeck) = await Seed(db);
        var handle = await db.Users.Where(u => u.Id == userId).Select(u => u.Handle).FirstAsync();

        try
        {
            var client = seoFactory.CreateClient();

            var listing = await client.GetStringAsync("/library");
            listing.Should().Contain("<title>Deck library — fasolt</title>");
            listing.Should().Contain("""<meta property="og:title" content="Deck library — fasolt" />""");
            listing.Should().NotContain("original description");

            var deckPage = await client.GetStringAsync($"/library/{publicDeck.PublicId}");
            deckPage.Should().Contain($"Endpoint Public Deck by @{handle} — fasolt");
            deckPage.Should().Contain("""<meta property="og:type" content="website" />""");
            deckPage.Should().NotContain("noindex");

            // Unlisted decks unfurl but must not be indexed.
            var unlistedPage = await client.GetStringAsync($"/library/{unlistedDeck.PublicId}");
            unlistedPage.Should().Contain("Endpoint Unlisted Deck");
            unlistedPage.Should().Contain("""<meta name="robots" content="noindex" />""");

            // A private deck gets no injected metadata at all.
            var privatePage = await client.GetStringAsync($"/library/{privateDeck.PublicId}");
            privatePage.Should().NotContain("Endpoint Private Deck");
            privatePage.Should().Contain("<title>fasolt</title>");
        }
        finally
        {
            await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SitemapListsPublicDecks()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (userId, publicDeck, unlistedDeck, _) = await Seed(db);

        try
        {
            var client = _factory.CreateClient();
            var response = await client.GetAsync("/sitemap.xml");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var xml = await response.Content.ReadAsStringAsync();

            xml.Should().Contain("/library</loc>");
            xml.Should().Contain($"/library/{publicDeck.PublicId}</loc>");
            xml.Should().NotContain(unlistedDeck.PublicId);
        }
        finally
        {
            await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();
        }
    }
}
