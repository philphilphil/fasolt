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
/// Custom WebApplicationFactory that replaces Postgres with the EF Core InMemory
/// provider and seeds a test user with a known API token.
/// </summary>
public class SpacedMdFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    public const string TestToken = "sm_test_token_integration_tests_only_0000000000000000";
    public const string TestUserEmail = "test@spaced-md.test";
    public const string TestUserPassword = "Test1234!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext-related registrations so Npgsql internals don't leak through
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(AppDbContext) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition().FullName?.Contains("IDbContextOptionsConfiguration") == true))
                .ToList();

            foreach (var d in toRemove)
                services.Remove(d);

            // Build InMemory options manually so we control exactly what goes in
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(_dbName)
                .ConfigureWarnings(w =>
                    w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            // Register the options singleton
            services.AddSingleton(dbOptions);

            // Register TestAppDbContext as the concrete type but resolved as AppDbContext
            services.AddScoped<AppDbContext>(sp =>
                new TestAppDbContext(sp.GetRequiredService<DbContextOptions<AppDbContext>>()));
        });
    }

    /// <summary>
    /// Creates the schema and seeds a test user + API token.
    /// Idempotent — safe to call multiple times.
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
}
