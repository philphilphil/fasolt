using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests.Auth;

[Collection(WebAppCollection.Name)]
public class OAuthProviderHintTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthProviderHintTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GITHUB_CLIENT_ID"] = "test-github-id",
                });
            });
        });
    }

    [Fact]
    public async Task OAuthLogin_WithGithubProviderHint_RedirectsToGithubLogin()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/oauth/login?provider_hint=github&returnUrl=/");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/api/account/github-login");
    }

    [Fact]
    public async Task OAuthLogin_WithoutHint_StillRendersHtml()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/oauth/login?returnUrl=/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("<form");
    }

    [Fact]
    public async Task OAuthLogin_WithUnknownHint_IgnoresHint()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/oauth/login?provider_hint=evilcorp&returnUrl=/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OAuthLogin_ContainsCreateAccountLink()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync("/oauth/login?returnUrl=/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("/oauth/register");
        body.Should().Contain("Create an account");
    }
}
