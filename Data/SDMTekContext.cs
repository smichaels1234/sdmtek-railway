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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure your entities here
            // modelBuilder.Entity<User>(entity =>
            // {
            //     entity.HasKey(e => e.Id);
            //     entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            // });
        }
    }
}