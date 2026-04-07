using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fasolt.Tests;

public class HealthEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Apple:BundleId"] = "com.fasolt.app",
                    ["GITHUB_CLIENT_ID"] = "test-github-id",
                });
            });
        });
    }

    [Fact]
    public async Task Health_ReportsAppleAndGithubFeatureFlags()
    {
        var client = _factory.CreateClient();
        var response = await client.GetFromJsonAsync<HealthResponse>("/api/health");
        response.Should().NotBeNull();
        response!.Features.GithubLogin.Should().BeTrue();
        response.Features.AppleLogin.Should().BeTrue();
    }

    private record HealthResponse(string Status, string Version, FeaturesResponse Features);
    private record FeaturesResponse(bool GithubLogin, bool AppleLogin);
}
