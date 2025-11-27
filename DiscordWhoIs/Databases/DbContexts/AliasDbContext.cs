using DiscordWhoIs.Databases.DbModels;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace DiscordWhoIs.Databases.DbContexts
{
    [method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(AliasEntry))]
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
    public class AliasDbContext(DbContextOptions<AliasDbContext> options) : DbContext(options)
#pragma warning restore IL2026 
    {
        public DbSet<AliasEntry> AliasEntries { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AliasEntry>(entity =>
            {
                entity.HasKey(e => e.Alias);
                entity.Property(e => e.Alias).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Real).IsRequired().HasMaxLength(256);
            });
        }
    }
}
