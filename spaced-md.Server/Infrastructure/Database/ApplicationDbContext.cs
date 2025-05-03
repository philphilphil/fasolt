using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using spaced_md.Server;

namespace spaced_md.Infrastructure.Database
{
    public class MarkdownFile : AuditableEntity
    {
        public Guid Id { get; set; }
        public required string FileName { get; set; }
        public required string Content { get; set; }
        public required string Md5 { get; set; }
        public required string ApplicationUserId { get; set; }
        public IdentityUser? ApplicationUser { get; set; }
        public ICollection<Card>? Cards { get; set; }
    }



    public class Card : AuditableEntity
    {
        public Guid Id { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
        public Guid MarkdownFileId { get; set; }
        public MarkdownFile? MarkdownFile { get; set; }
        public required string ApplicationUserId { get; set; }
        public IdentityUser? ApplicationUser { get; set; }
        public ICollection<GroupCard>? Groups { get; set; }
        public CardUsageType UsageType { get; set; }
        public string? Heading { get; set; }
        public string? HeadingLineNr { get; set; }

    }

    public class Group
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public required string ApplicationUserId { get; set; }
        public IdentityUser? ApplicationUser { get; set; }
        public ICollection<GroupCard>? Cards { get; set; }
    }

    public class GroupCard
    {
        public Guid CardId { get; set; }
        public required Card Card { get; set; }
        public Guid GroupId { get; set; }
        public required Group Group { get; set; }
    }

    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<MarkdownFile> MarkdownFiles { get; set; }
        public DbSet<Card> Cards { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupCard> GroupCards { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<GroupCard>()
                .HasKey(gc => new { gc.GroupId, gc.CardId });

            builder.Entity<GroupCard>()
                .HasOne(gc => gc.Group)
                .WithMany(g => g.Cards)
                .HasForeignKey(gc => gc.GroupId);

            builder.Entity<GroupCard>()
                .HasOne(gc => gc.Card)
                .WithMany(c => c.Groups)
                .HasForeignKey(gc => gc.CardId);
        }
    }
}
