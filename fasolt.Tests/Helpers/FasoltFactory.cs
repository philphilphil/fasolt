using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;
using Fasolt.Server.Infrastructure.Data;

namespace Fasolt.Tests.Helpers;

/// <summary>
/// Creates an isolated Postgres test database per instance.
/// Provides an AppDbContext and a seeded test user for service-level tests.
/// Requires Docker Postgres running on localhost:5432.
/// </summary>
public class TestDb : IAsyncDisposable
{
    private readonly string _dbName = $"fasolt_test_{Guid.NewGuid():N}";

    public string UserId { get; private set; } = null!;

    private string ConnectionString =>
        $"Host=localhost;Port=5432;Database={_dbName};Username=fasolt;Password=fasolt_dev";

    private DbContextOptions<AppDbContext> Options =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

    public AppDbContext CreateDbContext() => new(Options);

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();

        // Seed a minimal test user (just the row, no Identity overhead)
        var userId = Guid.NewGuid().ToString();
        db.Users.Add(new AppUser
        {
            Id = userId,
            UserName = "test@fasolt.test",
            NormalizedUserName = "TEST@FASOLT.TEST",
            Email = "test@fasolt.test",
            NormalizedEmail = "TEST@FASOLT.TEST",
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString(),
        });
        await db.SaveChangesAsync();
        UserId = userId;
    }

    public async ValueTask DisposeAsync()
    {
        await using var conn = new Npgsql.NpgsqlConnection(
            "Host=localhost;Port=5432;Database=postgres;Username=fasolt;Password=fasolt_dev");
        await conn.OpenAsync();

        await using (var terminate = conn.CreateCommand())
        {
            terminate.CommandText = $"SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{_dbName}'";
            await terminate.ExecuteNonQueryAsync();
        }
        await using (var drop = conn.CreateCommand())
        {
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{_dbName}\"";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
