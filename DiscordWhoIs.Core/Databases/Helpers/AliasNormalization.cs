using System.Text.RegularExpressions;
using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordWhoIs.Core.Databases.Helpers;

public static partial class AliasNormalization
{
    private static readonly Regex s_aliasedAuthorRegex =
        AliasedAuthorFixer();

    public static void SplitAliasedAuthors(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        IDbContextFactory<BotDbContext> factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<BotDbContext>>();
        using BotDbContext context = factory.CreateDbContext();

        // Load all authors, aliases, and fanfic relationships
        var authors = context.Authors
            .Include(a => a.Aliases)
            .Include(a => a.Fanfics)
            .ToList();

        // Map current canonical names for collision detection
        var nameLookup = authors.ToDictionary(a => a.Ao3ProfileName, StringComparer.OrdinalIgnoreCase);

        foreach (Author? author in authors.ToList()) // ToList so we can safely remove
        {
            Match match = s_aliasedAuthorRegex.Match(author.Ao3ProfileName);
            if (!match.Success)
            {
                continue;
            }

            string canonical = match.Groups["canonical"].Value.Trim();
            string alias = match.Groups["alias"].Value.Trim();

            if (nameLookup.TryGetValue(canonical, out Author? existingCanonical) &&
                existingCanonical.AuthorId != author.AuthorId)
            {
                // Merge aliases into canonical
                if (!existingCanonical.Aliases.Any(a => a.AliasUserName.Equals(alias, StringComparison.OrdinalIgnoreCase)))
                {
                    existingCanonical.Aliases.Add(new Alias(alias, existingCanonical.AuthorId));
                }

                // Move fanfics to canonical author
                foreach (Fanfic? fanfic in author.Fanfics.ToList())
                {
                    if (!existingCanonical.Fanfics.Any(f => f.FanficId == fanfic.FanficId))
                    {
                        existingCanonical.Fanfics.Add(fanfic);
                    }

                    // Remove fanfic from duplicate author
                    author.Fanfics.Remove(fanfic);
                }

                // Remove duplicate author
                context.Authors.Remove(author);
            }
            else
            {
                // Safe to update canonical name
                author.Ao3ProfileName = canonical;

                // Add alias
                if (!author.Aliases.Any(a => a.AliasUserName.Equals(alias, StringComparison.OrdinalIgnoreCase)))
                {
                    author.Aliases.Add(new Alias(alias, author.AuthorId));
                }

                // Update lookup
                nameLookup[canonical] = author;
            }
        }

        context.SaveChanges();
    }

    [GeneratedRegex(@"^(?<alias>.*?)\s*\(\s*(?<canonical>[^()]+)\s*\)$", RegexOptions.Compiled)]
    private static partial Regex AliasedAuthorFixer();
}
