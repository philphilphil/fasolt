using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace spaced_md.Infrastructure.Database
{
    public class ApplicationUser : IdentityUser
    {
        public ICollection<MarkdownFile> MarkdownFiles { get; set; }
        public ICollection<Card> Cards { get; set; }
        public ICollection<Group> Groups { get; set; }
    }

    public class MarkdownFile
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string Content { get; set; }
        public DateTime UploadedAt { get; set; }
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
        public ICollection<Card> Cards { get; set; }
    }

    public class Card
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        // Optional property to support spaced repetition
        public DateTime? NextReviewDate { get; set; }
        public int MarkdownFileId { get; set; }
        public MarkdownFile MarkdownFile { get; set; }
        // Card directly assigned to a user
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
        public ICollection<GroupCard> GroupCards { get; set; }
    }

    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ApplicationUserId { get; set; }
        public ApplicationUser ApplicationUser { get; set; }
        public ICollection<GroupCard> GroupCards { get; set; }
    }

    public class GroupCard
    {
        public int CardId { get; set; }
        public Card Card { get; set; }
        public int GroupId { get; set; }
        public Group Group { get; set; }
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
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
                .WithMany(g => g.GroupCards)
                .HasForeignKey(gc => gc.GroupId);

            builder.Entity<GroupCard>()
                .HasOne(gc => gc.Card)
                .WithMany(c => c.GroupCards)
                .HasForeignKey(gc => gc.CardId);
        }
    }
}
