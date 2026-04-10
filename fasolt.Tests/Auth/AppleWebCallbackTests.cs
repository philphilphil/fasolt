using System.Net;
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
public class AppleWebCallbackTests
{
    private const string WebClientId = "app.fasolt.web";
    private const string Kid = "test-kid";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly RSA _signingKey;

    public AppleWebCallbackTests(WebApplicationFactory<Program> factory)
    {
        _signingKey = RSA.Create(2048);
        var key = _signingKey;
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["APPLE_WEB_CLIENT_ID"] = WebClientId,
                    ["APPLE_BUNDLE_ID"] = "com.fasolt.app",
                });
            });
            builder.ConfigureServices(services =>
            {
                var existing = services.SingleOrDefault(d => d.ServiceType == typeof(AppleJwksCache));
                if (existing is not null) services.Remove(existing);
                services.AddSingleton<AppleJwksCache>(_ => new StubJwksCache(key, Kid));
            });
        });
    }

    [Fact]
    public async Task AppleLogin_RedirectsToApple_WhenConfigured()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/account/apple-login?returnUrl=%2Fstudy");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.Should().StartWith("https://appleid.apple.com/auth/authorize");
        location.Should().Contain($"client_id={WebClientId}");
        location.Should().Contain("response_mode=form_post");
        location.Should().Contain("redirect_uri=");
    }

    [Fact]
    public async Task AppleLogin_Returns404_WhenNotConfigured()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["APPLE_WEB_CLIENT_ID"] = "",
                });
            });
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/account/apple-login");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AppleCallback_WithValidToken_SignsInAndRedirects()
    {
        var idToken = MakeAppleIdToken("apple-web-001", "webuser@icloud.com", true);
        var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("/study"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id_token"] = idToken,
            ["state"] = state,
        });

        var response = await client.PostAsync("/api/account/apple-callback", content);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/study");

        // Should set the Identity application cookie
        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        setCookies.Should().Contain(c => c.Contains(".AspNetCore.Identity.Application"),
            "successful Apple callback must issue the Identity cookie");
    }

    [Fact]
    public async Task AppleCallback_WithInvalidToken_RedirectsToLoginWithError()
    {
        var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("/"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id_token"] = "not-a-valid-jwt",
            ["state"] = state,
        });

        var response = await client.PostAsync("/api/account/apple-callback", content);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("/login");
        response.Headers.Location!.OriginalString.Should().Contain("error=apple_auth_failed");
    }

    [Fact]
    public async Task AppleCallback_WithMissingToken_RedirectsToLoginWithError()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["error"] = "user_cancelled_authorize",
        });

        var response = await client.PostAsync("/api/account/apple-callback", content);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Contain("/login");
        response.Headers.Location!.OriginalString.Should().Contain("error=apple_auth_failed");
    }

    [Fact]
    public async Task AppleCallback_WithValidToken_CreatesNewUser()
    {
        var uniqueSub = $"apple-web-new-{Guid.NewGuid():N}";
        var uniqueEmail = $"new-{Guid.NewGuid():N}@icloud.com";
        var idToken = MakeAppleIdToken(uniqueSub, uniqueEmail, true);
        var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("/"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id_token"] = idToken,
            ["state"] = state,
        });

        var response = await client.PostAsync("/api/account/apple-callback", content);
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Verify user was created in the database
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Fasolt.Server.Domain.Entities.AppUser>>();
        var user = await userManager.FindByEmailAsync(uniqueEmail);
        user.Should().NotBeNull();
        user!.ExternalProvider.Should().Be("Apple");
        user.ExternalProviderId.Should().Be(uniqueSub);
        user.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task AppleCallback_PreservesReturnUrl_FromState()
    {
        var idToken = MakeAppleIdToken($"apple-web-ret-{Guid.NewGuid():N}", $"ret-{Guid.NewGuid():N}@icloud.com", true);
        var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("/decks"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id_token"] = idToken,
            ["state"] = state,
        });

        var response = await client.PostAsync("/api/account/apple-callback", content);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be("/decks");
    }

    [Fact]
    public async Task AppleCallback_RejectsNonLocalReturnUrl()
    {
        var idToken = MakeAppleIdToken($"apple-web-xss-{Guid.NewGuid():N}", $"xss-{Guid.NewGuid():N}@icloud.com", true);
        var state = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("https://evil.com"));

        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["id_token"] = idToken,
            ["state"] = state,
        });

        var response = await client.PostAsync("/api/account/apple-callback", content);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        // Should fall back to "/" not redirect to external URL
        response.Headers.Location!.OriginalString.Should().Be("/");
    }

    private string MakeAppleIdToken(string sub, string email, bool emailVerified)
    {
        var creds = new SigningCredentials(new RsaSecurityKey(_signingKey) { KeyId = Kid }, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(
            issuer: "https://appleid.apple.com",
            audience: WebClientId,
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
