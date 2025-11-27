using DiscordWhoIs.Databases.DbModels;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace DiscordWhoIs.Databases.DbContexts
{
    [method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(AliasEntry))]
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
    public class CacheDbContext(DbContextOptions<CacheDbContext> options) : DbContext(options)
#pragma warning restore IL2026 
    {
        public DbSet<CacheEntry> CacheEntries { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CacheEntry>(entity =>
            {
                entity.HasKey(e => e.Key);
                entity.Property(e => e.Json).IsRequired();
                entity.Property(e => e.ExpiresAt);
            });
        }
    }
}
