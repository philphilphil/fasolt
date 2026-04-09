using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Fasolt.Server.Application.Auth;

namespace Fasolt.Tests.Auth;

public class AppleJwksCacheDiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AppleJwksCacheDiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                });
            });
        });
    }

    [Fact]
    public void AppleJwksCache_IsRegisteredAsSingleton()
    {
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        var first = scope1.ServiceProvider.GetRequiredService<AppleJwksCache>();
        var second = scope2.ServiceProvider.GetRequiredService<AppleJwksCache>();

        first.Should().BeSameAs(second,
            "AppleJwksCache must be a singleton so its in-memory JWKS cache survives across requests");
    }
}
