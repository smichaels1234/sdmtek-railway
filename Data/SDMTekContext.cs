using Microsoft.EntityFrameworkCore;
using backend.Models;

namespace backend.Data
{
    public class SDMTekContext : DbContext
    {
        public SDMTekContext(DbContextOptions<SDMTekContext> options) : base(options)
        {
        }

        public DbSet<Contact> Contacts { get; set; }
        public DbSet<NewsletterSubscriber> NewsletterSubscribers { get; set; }
        public DbSet<NewsletterIssue> NewsletterIssues { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasDefaultSchema("sdmtek_website");

            modelBuilder.Entity<NewsletterSubscriber>(entity =>
            {
                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(320);

                entity.HasIndex(e => e.Email)
                    .IsUnique();

                entity.HasIndex(e => e.UnsubscribeToken)
                    .IsUnique();
            });

            modelBuilder.Entity<NewsletterIssue>(entity =>
            {
                entity.Property(e => e.Subject)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.HtmlBody)
                    .IsRequired();
            });

            // Configure your entities here
            // modelBuilder.Entity<User>(entity =>
            // {
            //     entity.HasKey(e => e.Id);
            //     entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            // });
        }
    }
}