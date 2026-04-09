using System.Net;
using FluentAssertions;
using Fasolt.Server.Application.Auth;
using Microsoft.IdentityModel.Tokens;

namespace Fasolt.Tests.Auth;

public class AppleJwksCacheTests
{
    private const string SampleJwks = """
    {
      "keys": [
        {
          "kty": "RSA",
          "kid": "abc123",
          "use": "sig",
          "alg": "RS256",
          "n": "xLzYzLN6sampleIDcw",
          "e": "AQAB"
        }
      ]
    }
    """;

    [Fact]
    public async Task GetKeysAsync_FetchesAndCachesJwks()
    {
        var handler = new StubHandler(SampleJwks);
        var factory = new StubHttpClientFactory(handler);
        var cache = new AppleJwksCache(factory, TimeProvider.System);

        var first = await cache.GetKeysAsync();
        var second = await cache.GetKeysAsync();

        first.Should().NotBeNull();
        first.Keys.Should().HaveCount(1);
        handler.CallCount.Should().Be(1, "the second call should be served from cache");
        second.Should().BeSameAs(first);
    }

    [Fact]
    public async Task GetKeysAsync_RefetchesAfterExpiry()
    {
        var handler = new StubHandler(SampleJwks);
        var factory = new StubHttpClientFactory(handler);
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var cache = new AppleJwksCache(factory, time);

        await cache.GetKeysAsync();
        time.Advance(TimeSpan.FromHours(2));
        await cache.GetKeysAsync();

        handler.CallCount.Should().Be(2);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        private readonly string _body;
        public StubHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now = _now.Add(amount);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler);
    }
}
