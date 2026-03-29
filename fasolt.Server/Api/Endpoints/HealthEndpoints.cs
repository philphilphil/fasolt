namespace Fasolt.Server.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", (IConfiguration configuration) => Results.Ok(new
        {
            status = "healthy",
            version = "0.1.0",
            features = new
            {
                githubLogin = !string.IsNullOrEmpty(configuration["GitHub:ClientId"]),
            },
        })).AllowAnonymous();
    }
}
