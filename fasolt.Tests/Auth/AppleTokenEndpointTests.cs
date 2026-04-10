using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Fasolt.Server.Application.Auth;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests.Auth;

[Collection(WebAppCollection.Name)]
public class AppleTokenEndpointTests
{
    private const string BundleId = "com.fasolt.app";
    private const string Kid = "test-kid";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly RSA _signingKey;

    public AppleTokenEndpointTests(WebApplicationFactory<Program> factory)
    {
        _signingKey = RSA.Create(2048);
        var key = _signingKey;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["APPLE_BUNDLE_ID"] = BundleId,
                });
            });
            builder.ConfigureServices(services =>
            {
                // Replace the real JWKS cache with a stub signed by our local key
                var existing = services.SingleOrDefault(d => d.ServiceType == typeof(AppleJwksCache));
                if (existing is not null) services.Remove(existing);
                services.AddSingleton<AppleJwksCache>(_ => new StubJwksCache(key, Kid));
            });
        });
    }

    [Fact]
    public async Task PostToken_WithValidAppleIdentityToken_ReturnsOAuthTokens()
    {
        var idToken = MakeAppleIdToken("apple-sub-001", "user@icloud.com", true);
        var client = _factory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = AppleAuthService.GrantType,
            ["client_id"] = "fasolt-ios",
            ["identity_token"] = idToken,
        });

        var response = await client.PostAsync("/oauth/token", content);
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"body was: {body}");

        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        payload.Should().NotBeNull();
        payload!.AccessToken.Should().NotBeNullOrEmpty();
        payload.RefreshToken.Should().NotBeNullOrEmpty();
        payload.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task PostToken_WithInvalidAppleToken_ReturnsInvalidGrant()
    {
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = AppleAuthService.GrantType,
            ["client_id"] = "fasolt-ios",
            ["identity_token"] = "not-a-real-jwt",
        });

        var response = await client.PostAsync("/oauth/token", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private string MakeAppleIdToken(string sub, string email, bool emailVerified)
    {
        var creds = new SigningCredentials(new RsaSecurityKey(_signingKey) { KeyId = Kid }, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: "https://appleid.apple.com",
            audience: BundleId,
            claims: new[]
            {
                new Claim("sub", sub),
                new Claim("email", email),
                new Claim("email_verified", emailVerified ? "true" : "false"),
            },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: creds);
        var handler = new JwtSecurityTokenHandler();
        handler.InboundClaimTypeMap.Clear();
        return handler.WriteToken(token);
    }

    private record TokenResponse(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string TokenType,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn,
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken);

    private sealed class StubJwksCache : AppleJwksCache
    {
        private readonly JsonWebKeySet _keys;
        public StubJwksCache(RSA rsa, string kid)
            : base(new StubHttpClientFactory(), TimeProvider.System)
        {
            var p = rsa.ExportParameters(false);
            var jwk = new JsonWebKey
            {
                Kty = "RSA", Kid = kid, Use = "sig", Alg = "RS256",
                N = Base64UrlEncoder.Encode(p.Modulus!),
                E = Base64UrlEncoder.Encode(p.Exponent!),
            };
            _keys = new JsonWebKeySet();
            _keys.Keys.Add(jwk);
        }
        public override Task<JsonWebKeySet> GetKeysAsync(CancellationToken ct = default) => Task.FromResult(_keys);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
