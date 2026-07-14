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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<NewsletterSubscriber>(entity =>
            {
                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(320);

                entity.HasIndex(e => e.Email)
                    .IsUnique();
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