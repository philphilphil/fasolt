using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;
using Fasolt.Tests.Helpers;

namespace Fasolt.Tests.Auth;

public class AppleAuthServiceTests : IAsyncLifetime
{
    private const string BundleId = "com.fasolt.app";
    private readonly TestDb _db = new();
    private RSA _signingKey = null!;
    private const string Kid = "test-kid";

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
        _signingKey = RSA.Create(2048);
    }

    public async Task DisposeAsync()
    {
        _signingKey.Dispose();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task ResolveUserAsync_CreatesNewUser_WhenNotFound()
    {
        var token = CreateAppleToken(sub: "001", email: "user@icloud.com", emailVerified: true);
        var (service, db) = CreateService();
        await using var _ = db;

        var user = await service.ResolveUserAsync(token);

        user.Should().NotBeNull();
        user.ExternalProvider.Should().Be("Apple");
        user.ExternalProviderId.Should().Be("001");
        user.Email.Should().Be("user@icloud.com");
        user.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveUserAsync_ReusesExistingAppleUser()
    {
        var existingId = Guid.NewGuid().ToString();
        await using (var db = _db.CreateDbContext())
        {
            db.Users.Add(new AppUser
            {
                Id = existingId,
                UserName = "apple-001",
                NormalizedUserName = "APPLE-001",
                Email = "user@icloud.com",
                NormalizedEmail = "USER@ICLOUD.COM",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ExternalProvider = "Apple",
                ExternalProviderId = "001",
            });
            await db.SaveChangesAsync();
        }

        var token = CreateAppleToken(sub: "001", email: null, emailVerified: false);
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        var user = await service.ResolveUserAsync(token);
        user.Id.Should().Be(existingId);
    }

    [Fact]
    public async Task ResolveUserAsync_LinksByVerifiedEmail()
    {
        var existingId = Guid.NewGuid().ToString();
        await using (var db = _db.CreateDbContext())
        {
            db.Users.Add(new AppUser
            {
                Id = existingId,
                UserName = "user@example.com",
                NormalizedUserName = "USER@EXAMPLE.COM",
                Email = "user@example.com",
                NormalizedEmail = "USER@EXAMPLE.COM",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            });
            await db.SaveChangesAsync();
        }

        var token = CreateAppleToken(sub: "002", email: "user@example.com", emailVerified: true);
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        var user = await service.ResolveUserAsync(token);

        user.Id.Should().Be(existingId);
        user.ExternalProvider.Should().Be("Apple");
        user.ExternalProviderId.Should().Be("002");
    }

    [Fact]
    public async Task ResolveUserAsync_RefusesLinkOnUnverifiedEmail()
    {
        await using (var db = _db.CreateDbContext())
        {
            db.Users.Add(new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = "user@example.com",
                NormalizedUserName = "USER@EXAMPLE.COM",
                Email = "user@example.com",
                NormalizedEmail = "USER@EXAMPLE.COM",
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            });
            await db.SaveChangesAsync();
        }

        var token = CreateAppleToken(sub: "003", email: "user@example.com", emailVerified: false);
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        var act = async () => await service.ResolveUserAsync(token);
        await act.Should().ThrowAsync<AppleAuthException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task ResolveUserAsync_RejectsExpiredToken()
    {
        var token = CreateAppleToken(sub: "004", email: "x@x.com", emailVerified: true,
            issuedAt: DateTime.UtcNow.AddHours(-2), expires: DateTime.UtcNow.AddHours(-1));
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        var act = async () => await service.ResolveUserAsync(token);
        await act.Should().ThrowAsync<AppleAuthException>();
    }

    [Fact]
    public async Task ResolveUserAsync_RejectsWrongIssuer()
    {
        var token = CreateAppleToken(sub: "005", email: "x@x.com", emailVerified: true,
            issuer: "https://evil.example.com");
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        var act = async () => await service.ResolveUserAsync(token);
        await act.Should().ThrowAsync<AppleAuthException>();
    }

    [Fact]
    public async Task ResolveUserAsync_RejectsWrongAudience()
    {
        var token = CreateAppleToken(sub: "006", email: "x@x.com", emailVerified: true,
            audience: "com.somebody.else");
        var (service, ctx) = CreateService();
        await using var _ = ctx;

        var act = async () => await service.ResolveUserAsync(token);
        await act.Should().ThrowAsync<AppleAuthException>();
    }

    // -- helpers --

    private (AppleAuthService service, AppDbContext db) CreateService()
    {
        var db = _db.CreateDbContext();
        var userManager = TestIdentity.CreateUserManager(db);
        var jwks = new StubJwksCache(_signingKey, Kid);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Apple:BundleId"] = BundleId })
            .Build();
        var logger = NullLogger<AppleAuthService>.Instance;
        return (new AppleAuthService(jwks, userManager, config, logger), db);
    }

    private string CreateAppleToken(
        string sub,
        string? email,
        bool emailVerified,
        string issuer = "https://appleid.apple.com",
        string audience = BundleId,
        DateTime? issuedAt = null,
        DateTime? expires = null)
    {
        var iat = issuedAt ?? DateTime.UtcNow;
        var exp = expires ?? DateTime.UtcNow.AddMinutes(10);
        var creds = new SigningCredentials(new RsaSecurityKey(_signingKey) { KeyId = Kid }, SecurityAlgorithms.RsaSha256);
        var claims = new List<Claim> { new("sub", sub) };
        if (email is not null) claims.Add(new("email", email));
        claims.Add(new("email_verified", emailVerified ? "true" : "false"));
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: iat,
            expires: exp,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class StubJwksCache : AppleJwksCache
    {
        private readonly JsonWebKeySet _keys;
        public StubJwksCache(RSA rsa, string kid)
            : base(new HttpClient(), TimeProvider.System)
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
}
