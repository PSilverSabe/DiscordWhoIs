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

    public DbSet<EmbedPosterConfiguration> EmbedPosterConfiguration { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Alias configuration
        modelBuilder.Entity<Alias>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.AliasUserName)
                  .UseCollation("NOCASE")
                  .IsRequired()
                  .HasMaxLength(256);

            entity.HasIndex(e => e.AliasUserName)
                  .IsUnique();

            entity.HasOne(e => e.Author)
                  .WithMany(e => e.Aliases)
                  .HasForeignKey(e => e.AuthorId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Fanfic configuration
        modelBuilder.Entity<Fanfic>(entity =>
        {
            entity.HasKey(e => e.FanficId);

            entity.Property(e => e.Link)
                  .IsRequired();

            entity.HasIndex(e => e.Link)
                  .IsUnique();

            // Configure many-to-many with Author explicitly
            entity.HasMany(e => e.Authors)
                  .WithMany(e => e.Fanfics)
                  .UsingEntity<Dictionary<string, object>>(
                      "AuthorFanfic", // shadow table name
                      j => j.HasOne<Author>()
                            .WithMany()
                            .HasForeignKey("AuthorsAuthorId")
                            .OnDelete(DeleteBehavior.Cascade),
                      j => j.HasOne<Fanfic>()
                            .WithMany()
                            .HasForeignKey("FanficsFanficId")
                            .OnDelete(DeleteBehavior.Cascade),
                      j =>
                      {
                          j.HasKey("AuthorsAuthorId", "FanficsFanficId");

                          // Named unique index to appear in snapshot
                          j.HasIndex(new[] { "FanficsFanficId", "AuthorsAuthorId" })
                           .IsUnique()
                           .HasDatabaseName("IX_FanficAuthors_FanficId_AuthorId");

                          j.ToTable("AuthorFanfic");
                      });
        });

        // Author configuration
        modelBuilder.Entity<Author>(entity =>
        {
            entity.HasKey(e => e.AuthorId);

            // Ao3ProfileName
            entity.Property(e => e.Ao3ProfileName)
                  .UseCollation("NOCASE")
                  .IsRequired()
                  .HasMaxLength(256);

            entity.HasIndex(e => e.Ao3ProfileName)
                  .IsUnique();

            // FanficNetId
            entity.Property(e => e.FanficNetId)
                  .IsRequired(false);

            entity.HasIndex(e => e.FanficNetId)
                  .IsUnique();

            // FanficNetProfileName
            entity.Property(e => e.FanficNetProfileName)
                  .UseCollation("NOCASE")
                  .IsRequired(false)
                  .HasMaxLength(256);

            entity.HasIndex(e => e.FanficNetProfileName)
                  .IsUnique();

            // DiscordId
            entity.Property(e => e.DiscordId)
                  .IsRequired(false);

            entity.HasIndex(e => e.DiscordId)
                  .IsUnique();

            // DiscordUserName
            entity.Property(e => e.DiscordUserName)
                  .UseCollation("NOCASE")
                  .IsRequired(false)
                  .HasMaxLength(256);

            // Configure many-to-many with Fanfic
            entity.HasMany(e => e.Fanfics)
                  .WithMany(f => f.Authors)
                  .UsingEntity<Dictionary<string, object>>(
                      "AuthorFanfic", // ensures consistent shadow table
                      j => j.HasOne<Fanfic>().WithMany().HasForeignKey("FanficsFanficId").OnDelete(DeleteBehavior.Cascade),
                      j => j.HasOne<Author>().WithMany().HasForeignKey("AuthorsAuthorId").OnDelete(DeleteBehavior.Cascade)
                  );
        });
    }
}
