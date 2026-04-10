using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Application.Auth;

public sealed class AppleAuthException : Exception
{
    public AppleAuthException(string message) : base(message) { }
}

public sealed class AppleAuthService
{
    public const string GrantType = "urn:fasolt:apple";

    private const string AppleIssuer = "https://appleid.apple.com";
    private const string ProviderName = "Apple";

    private readonly AppleJwksCache _jwksCache;
    private readonly UserManager<AppUser> _userManager;
    private readonly List<string> _audiences;
    private readonly ILogger<AppleAuthService> _logger;

    public AppleAuthService(AppleJwksCache jwksCache, UserManager<AppUser> userManager, IConfiguration configuration, ILogger<AppleAuthService> logger)
    {
        _jwksCache = jwksCache;
        _userManager = userManager;
        _logger = logger;

        // Accept tokens issued for either the iOS bundle ID or the web Services ID
        _audiences = new List<string>();
        var bundleId = configuration["APPLE_BUNDLE_ID"];
        var webClientId = configuration["APPLE_WEB_CLIENT_ID"];
        if (!string.IsNullOrEmpty(bundleId)) _audiences.Add(bundleId);
        if (!string.IsNullOrEmpty(webClientId)) _audiences.Add(webClientId);
        if (_audiences.Count == 0)
            throw new InvalidOperationException("At least one of APPLE_BUNDLE_ID or APPLE_WEB_CLIENT_ID must be configured");
    }

    public async Task<AppUser> ResolveUserAsync(string identityToken, CancellationToken cancellationToken = default)
    {
        var principal = await ValidateTokenAsync(identityToken, cancellationToken);

        var sub = principal.FindFirstValue("sub")
            ?? throw new AppleAuthException("Apple token is missing the 'sub' claim.");
        var email = principal.FindFirstValue("email");
        var emailVerifiedRaw = principal.FindFirstValue("email_verified");
        var emailVerified = bool.TryParse(emailVerifiedRaw, out var parsed) && parsed;

        // 1. Existing Apple user?
        var existing = await _userManager.Users
            .FirstOrDefaultAsync(u => u.ExternalProvider == ProviderName && u.ExternalProviderId == sub, cancellationToken);
        if (existing is not null)
            return existing;

        // 2. Link to existing local account if email matches AND Apple verified it
        if (!string.IsNullOrEmpty(email))
        {
            var byEmail = await _userManager.FindByEmailAsync(email);
            if (byEmail is not null)
            {
                if (!emailVerified)
                    throw new AppleAuthException(
                        "An account with this email already exists. Sign in with your password and link Apple from settings.");

                // Refuse to overwrite an existing external provider link — the user
                // must sign in with their original provider and link Apple from Settings
                // (which doesn't exist yet; forward-compatible error message).
                if (byEmail.ExternalProvider is not null && byEmail.ExternalProvider != ProviderName)
                    throw new AppleAuthException(
                        $"This email is already linked to {byEmail.ExternalProvider}. Sign in with {byEmail.ExternalProvider} and link Apple from settings.");

                byEmail.ExternalProvider = ProviderName;
                byEmail.ExternalProviderId = sub;
                var update = await _userManager.UpdateAsync(byEmail);
                if (!update.Succeeded)
                    throw new AppleAuthException("Failed to link Apple account to existing user.");
                return byEmail;
            }
        }

        // 3. Create a new user
        var newUser = new AppUser
        {
            UserName = $"apple-{sub}",
            Email = email,
            EmailConfirmed = true,
            ExternalProvider = ProviderName,
            ExternalProviderId = sub,
        };
        var create = await _userManager.CreateAsync(newUser);
        if (!create.Succeeded)
        {
            _logger.LogWarning(
                "Failed to create user from Apple sign-in: {Errors}",
                string.Join(", ", create.Errors.Select(e => e.Description)));
            throw new AppleAuthException("Failed to create account. Please try again.");
        }
        return newUser;
    }

    private async Task<ClaimsPrincipal> ValidateTokenAsync(string identityToken, CancellationToken cancellationToken)
    {
        JsonWebKeySet jwks;
        try
        {
            jwks = await _jwksCache.GetKeysAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new AppleAuthException("Could not fetch Apple signing keys: " + ex.Message);
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = AppleIssuer,
            ValidateAudience = true,
            ValidAudiences = _audiences,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = jwks.Keys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };

        var handler = new JwtSecurityTokenHandler
        {
            // Prevent claim type remapping so we get "sub" not the long URI name
            MapInboundClaims = false,
        };
        try
        {
            return handler.ValidateToken(identityToken, parameters, out _);
        }
        catch (Exception ex) when (ex is SecurityTokenException or SecurityTokenArgumentException)
        {
            throw new AppleAuthException("Apple identity token is invalid: " + ex.Message);
        }
    }
}
