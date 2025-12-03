using DiscordWhoIs.Databases.DbModels;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace DiscordWhoIs.Databases.DbContexts
{
    [method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(Alias))]
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
    public class BotDbContext(DbContextOptions<BotDbContext> options) : DbContext(options)
#pragma warning restore IL2026 
    {
        public DbSet<Alias> Aliases { get; set; } = null!;

        public DbSet<Fanfic> Fanfics { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Alias>(entity =>
            {
                entity.HasKey(e => e.AliasUserName);
                entity.Property(e => e.AliasUserName).IsRequired().HasMaxLength(256);
                entity.Property(e => e.RealUserName).IsRequired().HasMaxLength(256);
            });

            modelBuilder.Entity<Fanfic>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).IsRequired().HasMaxLength(64);
                entity.Property(e => e.Link).IsRequired();
            });
        }
    }
}
