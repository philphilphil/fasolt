using Microsoft.IdentityModel.Tokens;

namespace Fasolt.Server.Application.Auth;

public class AppleJwksCache
{
    public const string HttpClientName = "AppleJwks";

    private const string AppleJwksUrl = "https://appleid.apple.com/auth/keys";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private JsonWebKeySet? _cached;
    private DateTimeOffset _expiresAt;

    public AppleJwksCache(IHttpClientFactory httpClientFactory, TimeProvider? time = null)
    {
        _httpClientFactory = httpClientFactory;
        _time = time ?? TimeProvider.System;
    }

    public virtual async Task<JsonWebKeySet> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        if (_cached is not null && now < _expiresAt)
            return _cached;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            now = _time.GetUtcNow();
            if (_cached is not null && now < _expiresAt)
                return _cached;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var json = await client.GetStringAsync(AppleJwksUrl, cancellationToken);
            var keys = new JsonWebKeySet(json);
            _cached = keys;
            _expiresAt = now.Add(CacheLifetime);
            return keys;
        }
        finally
        {
            _lock.Release();
        }
    }
}
