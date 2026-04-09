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

    // Single-reference snapshot so the fast path reads keys + expiry
    // atomically. Before this was two separate fields (JsonWebKeySet? +
    // DateTimeOffset), and as a singleton the uncontended read could see a
    // torn DateTimeOffset (two 64-bit words) paired with a stale keyset.
    // A reference read of _snapshot is atomic per the .NET memory model;
    // marking it volatile additionally prevents reordering across threads.
    private sealed record Snapshot(JsonWebKeySet Keys, DateTimeOffset ExpiresAt);
    private volatile Snapshot? _snapshot;

    public AppleJwksCache(IHttpClientFactory httpClientFactory, TimeProvider? time = null)
    {
        _httpClientFactory = httpClientFactory;
        _time = time ?? TimeProvider.System;
    }

    public virtual async Task<JsonWebKeySet> GetKeysAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var snapshot = _snapshot;
        if (snapshot is not null && now < snapshot.ExpiresAt)
            return snapshot.Keys;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            now = _time.GetUtcNow();
            snapshot = _snapshot;
            if (snapshot is not null && now < snapshot.ExpiresAt)
                return snapshot.Keys;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var json = await client.GetStringAsync(AppleJwksUrl, cancellationToken);
            var keys = new JsonWebKeySet(json);
            _snapshot = new Snapshot(keys, now.Add(CacheLifetime));
            return keys;
        }
        finally
        {
            _lock.Release();
        }
    }
}
