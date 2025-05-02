using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace spaced_md.Infrastructure.Database
{
    public class MarkdownFile
    {
        public Guid Id { get; set; }
        public required string FileName { get; set; }
        public required string Content { get; set; }
        public required string ApplicationUserId { get; set; }
        public IdentityUser? ApplicationUser { get; set; }
        public ICollection<Card>? Cards { get; set; }
        public DateTime UploadedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsDeleted => DeletedAt.HasValue;
    }

    public class Card
    {
        public Guid Id { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
        public Guid MarkdownFileId { get; set; }
        public MarkdownFile? MarkdownFile { get; set; }
        public required string ApplicationUserId { get; set; }
        public IdentityUser? ApplicationUser { get; set; }
        public ICollection<GroupCard>? Groups { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public bool IsDeleted => DeletedAt.HasValue;
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
