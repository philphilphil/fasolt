using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SpacedMd.Server.Api.Auth;
using SpacedMd.Server.Domain.Entities;
using SpacedMd.Server.Infrastructure.Data;

namespace SpacedMd.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory that uses a real Postgres test database
/// (unique per instance) and seeds a test user with a known API token.
/// Requires Docker Postgres running on localhost:5432.
/// </summary>
public class SpacedMdFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"spacedmd_test_{Guid.NewGuid():N}";

    public const string TestToken = "sm_test_token_integration_tests_only_0000000000000000";
    public const string TestUserEmail = "test@spaced-md.test";
    public const string TestUserPassword = "Test1234!";

    private string ConnectionString =>
        $"Host=localhost;Port=5432;Database={_dbName};Username=spaced;Password=spaced_dev";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registrations
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    d.ServiceType.FullName?.Contains("DbContextOptions") == true)
                .ToList();

            foreach (var d in toRemove)
                services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(ConnectionString));
        });
    }

    /// <summary>
    /// Creates the schema and seeds a test user + API token.
    /// </summary>
    public async Task SeedTestUserAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

        await db.Database.EnsureCreatedAsync();

        var existing = await userManager.FindByEmailAsync(TestUserEmail);
        if (existing is not null)
            return;

        var user = new AppUser
        {
            UserName = TestUserEmail,
            Email = TestUserEmail,
            DisplayName = "Test User",
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, TestUserPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        var hash = BearerTokenHandler.ComputeHash(TestToken);
        db.ApiTokens.Add(new ApiToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Name = "Test Token",
            TokenHash = hash,
            TokenPrefix = TestToken[..8],
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Creates an HttpClient pre-configured with the test Bearer token.
    /// </summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Drop the test database using a direct connection (host may already be disposed)
            using var conn = new Npgsql.NpgsqlConnection(
                "Host=localhost;Port=5432;Database=postgres;Username=spaced;Password=spaced_dev");
            conn.Open();
            using (var terminate = conn.CreateCommand())
            {
                terminate.CommandText = $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_dbName}'";
                terminate.ExecuteNonQuery();
            }
            using (var drop = conn.CreateCommand())
            {
                drop.CommandText = $"DROP DATABASE IF EXISTS \"{_dbName}\"";
                drop.ExecuteNonQuery();
            }
        }
        base.Dispose(disposing);
    }
}
