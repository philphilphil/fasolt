using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SpacedMd.Server.Domain.Entities;

namespace SpacedMd.Server.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<DeckCard> DeckCards => Set<DeckCard>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Card>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Front).IsRequired();
            entity.Property(e => e.Back).IsRequired();
            entity.Property(e => e.SourceFile).HasMaxLength(255);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.SourceFile });
            entity.HasQueryFilter(e => e.DeletedAt == null);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.EaseFactor).HasDefaultValue(2.5);
            entity.Property(e => e.State).HasMaxLength(20).HasDefaultValue("new").IsRequired();
            entity.HasIndex(e => new { e.UserId, e.DueAt });
            entity.Property(e => e.SearchVector)
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    """to_tsvector('english', coalesce("Front",'') || ' ' || coalesce("Back",''))""",
                    stored: true);
            entity.HasIndex(e => e.SearchVector).HasMethod("gin");
        });

        builder.Entity<Deck>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.SearchVector)
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    """to_tsvector('english', coalesce("Name",'') || ' ' || coalesce("Description",''))""",
                    stored: true);
            entity.HasIndex(e => e.SearchVector).HasMethod("gin");
        });

        builder.Entity<DeckCard>(entity =>
        {
            entity.HasKey(e => new { e.DeckId, e.CardId });
            entity.HasIndex(e => e.CardId);
            entity.HasOne(e => e.Deck).WithMany(d => d.Cards).HasForeignKey(e => e.DeckId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Card).WithMany(c => c.DeckCards).HasForeignKey(e => e.CardId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ApiToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.TokenHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.TokenPrefix).HasMaxLength(8).IsRequired();
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
