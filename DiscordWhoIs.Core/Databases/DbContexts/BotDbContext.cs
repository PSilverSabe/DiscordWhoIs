using DiscordWhoIs.Core.Configuration.Models;
using DiscordWhoIs.Core.Databases.DbModels;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace DiscordWhoIs.Core.Databases.DbContexts
{
    [method: DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(Alias))]
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
    public class BotDbContext(DbContextOptions<BotDbContext> options) : DbContext(options)
#pragma warning restore IL2026 
    {
        public DbSet<Alias> Aliases { get; set; } = null!;

        public DbSet<Fanfic> Fanfics { get; set; } = null!;

        public DbSet<Author> Authors { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Alias>(entity =>
            {
                entity.HasKey(e => e.AliasUserName);
                entity.Property(e => e.AliasUserName).IsRequired().HasMaxLength(256);
                entity.HasOne(e => e.Author).WithMany(a => a.Aliases).HasForeignKey(e => e.AuthorId);
            });

            modelBuilder.Entity<Fanfic>(entity =>
            {
                entity.HasKey(e => e.FanficId);
                entity.Property(e => e.Link).IsRequired();
                entity.HasMany(e => e.Authors).WithMany(a => a.Fanfics);
            });

            modelBuilder.Entity<Author>(entity =>
            {
                entity.HasKey(e => e.AuthorId);
                entity.Property(e => e.Ao3ProfileName).IsRequired().HasMaxLength(256);
                entity.Property(e => e.FanficNetProfileName).HasMaxLength(256);
                entity.HasMany(e => e.Fanfics).WithMany(f => f.Authors);
            });
        }
    }
}
