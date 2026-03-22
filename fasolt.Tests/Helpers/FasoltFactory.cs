using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory that uses a real Postgres test database
/// (unique per instance) and seeds a test user.
/// Requires Docker Postgres running on localhost:5432.
/// </summary>
public class FasoltFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"fasolt_test_{Guid.NewGuid():N}";

    public const string TestUserEmail = "test@fasolt.test";
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
    /// Creates the schema and seeds a test user.
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
    }

    /// <summary>
    /// Creates an HttpClient authenticated via cookie login.
    /// </summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });

        var loginResponse = await client.PostAsJsonAsync("/api/identity/login", new
        {
            email = TestUserEmail,
            password = TestUserPassword,
        });

        loginResponse.EnsureSuccessStatusCode();
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
