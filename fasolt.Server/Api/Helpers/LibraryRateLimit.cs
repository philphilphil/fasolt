using System.Threading.RateLimiting;

namespace Fasolt.Server.Api.Helpers;

/// <summary>
/// The per-IP ceiling for the anonymous library surface. Defined here rather than
/// inline in Program.cs because two places need the exact same numbers: the
/// <c>library</c> endpoint policy on <c>/api/library</c>, and
/// <see cref="Fasolt.Server.Api.Middleware.SeoMiddleware"/>, which short-circuits the
/// HTML routes before <c>UseRateLimiter</c> ever runs and therefore has to meter
/// itself.
/// </summary>
public static class LibraryRateLimit
{
    /// <summary>Roomier than the "api" policy — one visitor fires several reads per screen.</summary>
    public const int PermitLimit = 120;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static RateLimitPartition<string> Partition(HttpContext context) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitLimit,
                Window = Window,
                QueueLimit = 0,
            });
}
