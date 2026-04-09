namespace Fasolt.Server.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", (IConfiguration configuration) => Results.Ok(new
        {
            status = "healthy",
            version = "0.1.1",
            features = new
            {
                githubLogin = !string.IsNullOrEmpty(configuration["GITHUB_CLIENT_ID"]),
                appleLogin = !string.IsNullOrEmpty(configuration["APPLE_BUNDLE_ID"]),
            },
        })).AllowAnonymous();
    }
}
