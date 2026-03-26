using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Fasolt.Server.Infrastructure.Services;

public class ApnsSettings
{
    public string KeyId { get; set; } = default!;
    public string TeamId { get; set; } = default!;
    public string BundleId { get; set; } = default!;
    public string? KeyPath { get; set; }
    public string? KeyBase64 { get; set; }
    public bool UseSandbox { get; set; } = true;
}

public class ApnsService
{
    private readonly HttpClient _httpClient;
    private readonly ApnsSettings _settings;
    private readonly ECDsa _key;
    private readonly ILogger<ApnsService> _logger;
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiry;

    public ApnsService(HttpClient httpClient, ApnsSettings settings, ILogger<ApnsService> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
        _key = LoadKey(settings);
    }

    private static ECDsa LoadKey(ApnsSettings settings)
    {
        var key = ECDsa.Create();
        if (!string.IsNullOrEmpty(settings.KeyBase64))
        {
            var pem = Encoding.UTF8.GetString(Convert.FromBase64String(settings.KeyBase64));
            key.ImportFromPem(pem);
        }
        else if (!string.IsNullOrEmpty(settings.KeyPath))
        {
            var pem = File.ReadAllText(settings.KeyPath);
            key.ImportFromPem(pem);
        }
        else
        {
            throw new InvalidOperationException("APNs key must be configured via Apns:KeyPath or Apns:KeyBase64.");
        }
        return key;
    }

    private string GetToken()
    {
        if (_cachedToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _cachedToken;

        var securityKey = new ECDsaSecurityKey(_key) { KeyId = _settings.KeyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.TeamId,
            IssuedAt = DateTime.UtcNow,
            SigningCredentials = credentials,
        };

        var handler = new JsonWebTokenHandler();
        _cachedToken = handler.CreateToken(descriptor);
        _tokenExpiry = DateTimeOffset.UtcNow.AddMinutes(50);

        return _cachedToken;
    }

    /// <summary>
    /// Sends a push notification. Returns true if successful, false if the token is invalid (should be deleted).
    /// </summary>
    public async Task<bool> SendNotification(string deviceToken, string title, string body, int badgeCount)
    {
        var token = GetToken();

        var payload = new
        {
            aps = new
            {
                alert = new { title, body },
                sound = "default",
                badge = badgeCount,
            }
        };

        var host = _settings.UseSandbox
            ? "api.sandbox.push.apple.com"
            : "api.push.apple.com";
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://{host}/3/device/{deviceToken}");
        request.Version = new Version(2, 0);
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        request.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
        request.Headers.TryAddWithoutValidation("apns-topic", _settings.BundleId);
        request.Headers.TryAddWithoutValidation("apns-push-type", "alert");
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
                return true;

            if ((int)response.StatusCode == 410) // Gone — token no longer valid
            {
                _logger.LogInformation("APNs token {Token} is no longer valid (410 Gone)", deviceToken[..8]);
                return false;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("APNs FAILED {StatusCode}: {Body} | KeyId={KeyId} TeamId={TeamId} Topic={Topic}",
                (int)response.StatusCode, responseBody, _settings.KeyId, _settings.TeamId, _settings.BundleId);
            return true; // Don't delete the token on transient errors
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send APNs notification to {Token}", deviceToken[..8]);
            return true; // Don't delete on network errors
        }
    }
}
