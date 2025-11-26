using DiscordWhoIs.Databases.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace DiscordWhoIs.Databases.DbContexts
{
    public class AliasDbContext : DbContext
    {
        [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "<Pending>")]
        public AliasDbContext(DbContextOptions<AliasDbContext> options) : base(options) { }

        public DbSet<AliasEntry> AliasEntries { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AliasEntry>(entity =>
            {
                entity.HasKey(e => e.Alias);
                entity.Property(e => e.Alias).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Real).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Description).HasMaxLength(1024);
            });
            modelBuilder.Entity<AliasEntry>().ToTable("aliases");
        }
    }
}
