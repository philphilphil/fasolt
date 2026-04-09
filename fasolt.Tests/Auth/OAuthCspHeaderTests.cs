using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fasolt.Tests.Auth;

public class OAuthCspHeaderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OAuthCspHeaderTests(WebApplicationFactory<Program> factory)
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

    [Theory]
    [InlineData("/oauth/login")]
    [InlineData("/oauth/register")]
    [InlineData("/oauth/forgot-password")]
    [InlineData("/oauth/reset-password?email=foo@example.com")]
    public async Task OauthPages_ReturnCspHeader(string path)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Content-Security-Policy").Should().BeTrue(
            $"{path} should set a CSP header");

        var csp = string.Join(";", response.Headers.GetValues("Content-Security-Policy"));
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("style-src 'self'");
        csp.Should().Contain("script-src 'self'");
        csp.Should().Contain("form-action");
        csp.Should().Contain("frame-ancestors 'none'");
        csp.Should().NotContain("'unsafe-inline'",
            "the whole point of the middleware is to avoid unsafe-inline");
    }

    [Fact]
    public async Task NonOauthPaths_StillReturnPermissiveCsp()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Content-Security-Policy").Should().BeTrue(
            "non-oauth paths still need CSP — the SPA bundle has XSS surface area too");

        var csp = string.Join(";", response.Headers.GetValues("Content-Security-Policy"));
        // The permissive policy allows unsafe-inline for styles (SPA uses runtime
        // style injection via shadcn/Vue). The oauth-scoped policy is stricter
        // and does not permit unsafe-inline — see OauthPages_ReturnCspHeader.
        csp.Should().Contain("default-src 'self'");
        csp.Should().Contain("'unsafe-inline'", "SPA style-src needs unsafe-inline");
    }
}
