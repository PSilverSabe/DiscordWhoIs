using System.Diagnostics.CodeAnalysis;
using DiscordWhoIs.Core.Databases.DbModels;
using Microsoft.EntityFrameworkCore;

namespace DiscordWhoIs.Core.Databases.DbContexts;

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
            entity.HasKey(a => a.Id);

            entity.Property(a => a.AliasUserName)
                  .UseCollation("NOCASE");

            entity.HasIndex(a => a.AliasUserName)
                  .IsUnique();

            entity.Property(a => a.AliasUserName)
                  .IsRequired()
                  .HasMaxLength(200);

            entity.HasOne(a => a.Author)
                  .WithMany(a => a.Aliases)
                  .HasForeignKey(a => a.AuthorId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Fanfic>(entity =>
        {
            entity.HasKey(e => e.FanficId);

            entity.Property(e => e.Link)
                  .IsRequired();

            entity.HasMany(e => e.Authors)
                  .WithMany(a => a.Fanfics);
        });

        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(e => e.AuthorId);

            entity.Property(e => e.Ao3ProfileName)
                  .IsRequired()
                  .HasMaxLength(256);

            entity.HasIndex(a => a.Ao3ProfileName)
                  .IsUnique();

            entity.HasIndex(e => e.DiscordId)
                  .IsUnique();

            entity.Property(a => a.Ao3ProfileName)
                  .UseCollation("NOCASE");

            entity.Property(e => e.FanficNetProfileName)
                  .HasMaxLength(256);

            entity.HasMany(e => e.Fanfics).WithMany(f => f.Authors);
        });
    }
}
