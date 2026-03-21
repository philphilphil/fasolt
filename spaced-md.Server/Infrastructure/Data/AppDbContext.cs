using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SpacedMd.Server.Domain.Entities;

namespace SpacedMd.Server.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<MarkdownFile> MarkdownFiles => Set<MarkdownFile>();
    public DbSet<FileHeading> FileHeadings => Set<FileHeading>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<MarkdownFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.Content).IsRequired();
            entity.HasIndex(e => new { e.UserId, e.FileName }).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<FileHeading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.File).WithMany(f => f.Headings).HasForeignKey(e => e.FileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.FileId);
        });
    }
}
