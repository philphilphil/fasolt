using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests.Auth;

[Collection(WebAppCollection.Name)]
public class OAuthAuthorizeScreenHintTests
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthAuthorizeScreenHintTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                    ["OTP_PEPPER"] = "test-pepper",
                });
            });
        });
    }

    [Fact]
    public async Task Authorize_WithSignupHint_RedirectsToRegister_WhenUnauthenticated()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            "/oauth/authorize?response_type=code&client_id=fasolt-ios&redirect_uri=fasolt://oauth/callback&code_challenge=abc&code_challenge_method=S256&screen_hint=signup");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/oauth/register");
    }

    [Fact]
    public async Task Authorize_WithoutHint_StillRedirectsToLogin_WhenUnauthenticated()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.GetAsync(
            "/oauth/authorize?response_type=code&client_id=fasolt-ios&redirect_uri=fasolt://oauth/callback&code_challenge=abc&code_challenge_method=S256");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().StartWith("/oauth/login");
    }
}
