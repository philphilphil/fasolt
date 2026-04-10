using System.Reflection;

namespace Fasolt.Server.Api.Endpoints;

public static class HealthEndpoints
{
    private static readonly string AppVersion =
        typeof(HealthEndpoints).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            .Split('+')[0] // strip git hash suffix if present
        ?? typeof(HealthEndpoints).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", (IConfiguration configuration) => Results.Ok(new
        {
            status = "healthy",
            version = AppVersion,
            features = new
            {
                githubLogin = !string.IsNullOrEmpty(configuration["GITHUB_CLIENT_ID"]),
                appleLogin = !string.IsNullOrEmpty(configuration["APPLE_BUNDLE_ID"]) || !string.IsNullOrEmpty(configuration["APPLE_WEB_CLIENT_ID"]),
            },
        })).AllowAnonymous();
    }
}
