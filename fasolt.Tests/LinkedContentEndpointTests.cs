using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Application.Services;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests;

/// <summary>
/// The service layer refuses every write to linked content; this pins the other half
/// — that <c>AddLinkedContentGuard()</c> is actually wired onto the card and deck
/// groups, so the refusal reaches a client as a 403 with the shared error code rather
/// than an unhandled 500.
/// </summary>
[Collection(WebAppCollection.Name)]
public class LinkedContentEndpointTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public LinkedContentEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["OTP_PEPPER"] = "test-pepper",
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                });
            });
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => TestUserCleanup.DeleteTestUsersAsync(_factory);

    [Fact]
    public async Task RenamingALinkedDeck_Returns403WithTheLinkedContentCode()
    {
        var subscriberEmail = TestEmail.Create();
        const string password = "Abcdefg1";
        string deckPublicId;

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var subscriber = new AppUser { UserName = subscriberEmail, Email = subscriberEmail, EmailConfirmed = true };
            (await userManager.CreateAsync(subscriber, password)).Succeeded.Should().BeTrue();

            var authorEmail = TestEmail.Create();
            var author = new AppUser { UserName = authorEmail, Email = authorEmail, EmailConfirmed = true };
            (await userManager.CreateAsync(author, password)).Succeeded.Should().BeTrue();

            var deck = new Deck
            {
                Id = Guid.NewGuid(),
                PublicId = NanoIdGenerator.New(),
                UserId = author.Id,
                Name = "Author's Deck",
                CreatedAt = DateTimeOffset.UtcNow,
                Visibility = DeckVisibility.Public,
                PublishedAt = DateTimeOffset.UtcNow,
            };
            db.Decks.Add(deck);
            db.DeckSubscriptions.Add(new DeckSubscription
            {
                UserId = subscriber.Id,
                DeckId = deck.Id,
                SubscribedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
            deckPublicId = deck.PublicId;
        }

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var authCookie = await LoginAndGetAuthCookie(client, subscriberEmail, password);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/decks/{deckPublicId}")
        {
            Content = JsonContent.Create(new { name = "Mine now", description = (string?)null }),
        };
        request.Headers.Add("Cookie", authCookie);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("error").GetString().Should().Be(LinkedContentException.ErrorCode);
        body.GetProperty("message").GetString().Should().Contain("linked");

        using var verifyScope = _factory.Services.CreateScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await verify.Decks.AsNoTracking().FirstAsync(d => d.PublicId == deckPublicId))
            .Name.Should().Be("Author's Deck");
    }

    private static async Task<string> LoginAndGetAuthCookie(HttpClient client, string email, string password)
    {
        var getResponse = await client.GetAsync("/login?returnUrl=%2F");
        var html = await getResponse.Content.ReadAsStringAsync();
        var match = Regex.Match(html,
            @"name=""__RequestVerificationToken""[^>]*value=""([^""]+)""|value=""([^""]+)""[^>]*name=""__RequestVerificationToken""");
        match.Success.Should().BeTrue("login page should include an antiforgery token");
        var csrfToken = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = csrfToken,
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["ReturnUrl"] = "/",
        });

        var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/login") { Content = loginContent };
        loginRequest.Headers.Add("Cookie", getResponse.Headers.GetValues("Set-Cookie").FirstOrDefault() ?? "");
        var loginResponse = await client.SendAsync(loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        return loginResponse.Headers.GetValues("Set-Cookie")
            .First(c => c.Contains(".AspNetCore.Identity.Application", StringComparison.Ordinal));
    }
}
