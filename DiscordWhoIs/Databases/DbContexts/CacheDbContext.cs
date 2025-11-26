using DiscordWhoIs.Databases.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace DiscordWhoIs.Databases.DbContexts
{
    public class CacheDbContext : DbContext
    {
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
        public CacheDbContext(DbContextOptions<CacheDbContext> options) : base(options) { }

        public DbSet<CacheEntry> CacheEntries { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CacheEntry>(entity =>
            {
                entity.HasKey(e => e.Key);
                entity.Property(e => e.TypeName).IsRequired();
                entity.Property(e => e.Json).IsRequired();
                entity.Property(e => e.ExpiresAt);
            });
        }
    }
}
