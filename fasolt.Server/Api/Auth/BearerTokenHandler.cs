using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Server.Api.Auth;

public class BearerTokenDefaults
{
    public const string AuthenticationScheme = "BearerToken";
}

public class BearerTokenOptions : AuthenticationSchemeOptions { }

public class BearerTokenHandler(
    IOptionsMonitor<BearerTokenOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IServiceScopeFactory scopeFactory)
    : AuthenticationHandler<BearerTokenOptions>(options, logger, encoder)
{
    private static readonly TimeSpan LastUsedThrottle = TimeSpan.FromMinutes(5);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var token = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token) || !token.StartsWith("sm_"))
            return AuthenticateResult.NoResult();

        var hash = ComputeHash(token);

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var apiToken = await db.ApiTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null);

        if (apiToken is null)
            return AuthenticateResult.Fail("Invalid token");

        if (apiToken.ExpiresAt.HasValue && apiToken.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return AuthenticateResult.Fail("Token expired");

        // Throttle last-used timestamp updates to reduce DB writes
        if (apiToken.LastUsedAt is null ||
            DateTimeOffset.UtcNow - apiToken.LastUsedAt.Value > LastUsedThrottle)
        {
            apiToken.LastUsedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, apiToken.UserId),
            new Claim(ClaimTypes.Name, apiToken.User.UserName ?? apiToken.User.Email ?? ""),
            new Claim(ClaimTypes.Email, apiToken.User.Email ?? ""),
            new Claim("auth_method", "api_token"),
            new Claim("token_id", apiToken.Id.ToString()),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    public static string ComputeHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(bytes);
    }
}
