using Microsoft.EntityFrameworkCore;
using SpacedMd.Server.Infrastructure.Data;

namespace SpacedMd.Tests.Helpers;

/// <summary>
/// AppDbContext subclass for integration tests.
/// Strips Postgres-specific features (NpgsqlTsVector computed columns, tsvector column type)
/// so the InMemory provider can build the model without errors.
/// </summary>
public class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Ignore the tsvector search vector properties — InMemory doesn't support NpgsqlTsVector
        builder.Entity<SpacedMd.Server.Domain.Entities.Card>()
            .Ignore(e => e.SearchVector);

        builder.Entity<SpacedMd.Server.Domain.Entities.Deck>()
            .Ignore(e => e.SearchVector);
    }
}
