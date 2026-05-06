using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>, IDataProtectionKeyContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Deck> Decks => Set<Deck>();
    public DbSet<DeckCard> DeckCards => Set<DeckCard>();
    public DbSet<ConsentGrant> ConsentGrants => Set<ConsentGrant>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<DeckSnapshot> DeckSnapshots => Set<DeckSnapshot>();
    public DbSet<AppLog> Logs => Set<AppLog>();
    public DbSet<EmailVerificationCode> EmailVerificationCodes => Set<EmailVerificationCode>();
    public DbSet<PasswordResetCode> PasswordResetCodes => Set<PasswordResetCode>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Card>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PublicId).HasMaxLength(12).IsRequired();
            entity.HasIndex(e => e.PublicId).IsUnique();
            entity.Property(e => e.Front).HasMaxLength(10_000).IsRequired();
            entity.Property(e => e.Back).HasMaxLength(50_000).IsRequired();
            entity.Property(e => e.SourceFile).HasMaxLength(255);
            entity.Property(e => e.SourceHeading).HasMaxLength(255);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.UserId, e.SourceFile });

            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.PublicId).HasMaxLength(12).IsRequired();
            entity.HasIndex(e => e.PublicId).IsUnique();
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

        builder.Entity<DeckSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PublicId).HasMaxLength(12).IsRequired();
            entity.HasIndex(e => e.PublicId).IsUnique();
            entity.Property(e => e.Data).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(e => new { e.UserId, e.DeckId, e.CreatedAt });
            entity.HasOne(e => e.Deck).WithMany().HasForeignKey(e => e.DeckId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DeckCard>(entity =>
        {
            entity.HasKey(e => new { e.DeckId, e.CardId });
            entity.HasIndex(e => e.CardId);
            entity.HasOne(e => e.Deck).WithMany(d => d.Cards).HasForeignKey(e => e.DeckId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Card).WithMany(c => c.DeckCards).HasForeignKey(e => e.CardId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ConsentGrant>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClientId).HasMaxLength(255).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.ClientId }).IsUnique();
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<DeviceToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Token).HasMaxLength(200).IsRequired();
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AppUser>(entity =>
        {
            entity.Property(e => e.NotificationIntervalHours).HasDefaultValue(8);
            entity.Property(e => e.DesiredRetention).HasDefaultValue(null);
            entity.Property(e => e.MaximumInterval).HasDefaultValue(null);
            entity.Property(e => e.DayStartHour).HasDefaultValue(null);
            entity.Property(e => e.TimeZone).HasMaxLength(64);
            entity.Property(e => e.ExternalProvider).HasMaxLength(50);
            entity.Property(e => e.ExternalProviderId).HasMaxLength(255);
            entity.HasIndex(e => new { e.ExternalProvider, e.ExternalProviderId })
                .IsUnique()
                .HasFilter("\"ExternalProvider\" IS NOT NULL");
        });

        builder.Entity<AppLog>(entity =>
        {
            entity.ToTable("Logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(50).IsRequired();
            entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Detail).HasMaxLength(1000);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Type);
        });

        builder.Entity<EmailVerificationCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CodeHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PasswordResetCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CodeHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

    }
}
