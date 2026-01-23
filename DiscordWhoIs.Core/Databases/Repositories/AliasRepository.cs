using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordWhoIs.Core.Databases.Repositories;

public class AliasRepository(IDbContextFactory<BotDbContext> dbContextFactory, ILogger<AliasRepository> logger)
    : RepositoryBase<BotDbContext, AliasRepository>(dbContextFactory, logger), IAliasRepository
{
    public async Task<IReadOnlyList<Alias>> GetAllAsync()
    {
        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        var aliases = context.Aliases
            .Include(a => a.Author)
            .AsNoTracking()
            .ToList();

        return aliases;
    }

    public async Task AddOrUpdateAsync(string alias, string real)
    {
        alias = alias.Trim();
        real = real.Trim();

        if (string.IsNullOrWhiteSpace(alias))
        {
            _logger.LogInformation("Alias cannot be empty.");
            throw new ArgumentException("Alias cannot be empty.", nameof(alias));
        }

        if (string.IsNullOrWhiteSpace(real))
        {
            _logger.LogInformation($"Real Name cannot be empty.");
            throw new ArgumentException("RealUserName username cannot be empty.", nameof(real));
        }

        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();

        // Find the author entity
        _logger.LogInformation("Finding Author Information.");
        Author authorEntity = await context.Authors.FirstOrDefaultAsync(a => a.Ao3ProfileName == real)
            ?? throw new InvalidOperationException($"No author found with AO3 profile name '{real}'.");

        // Find existing alias
        _logger.LogInformation($"Finding Aliases for Author: {authorEntity.Ao3ProfileName}");
        Alias? existing = await context.Aliases.FindAsync(alias);

        _logger.LogInformation($"Updating or Adding new alias.");
        if (existing != null)
        {
            existing.AuthorId = authorEntity.AuthorId;
            context.Aliases.Update(existing);
        }
        else
        {
            var newEntry = new Alias(alias, authorEntity.AuthorId);
            await context.Aliases.AddAsync(newEntry);
        }

        await SaveChangesAsync(context);

        _logger.LogInformation("Added/updated alias {Alias} -> {RealUserName}", alias, real);
    }

    public async Task<bool> RemoveAsync(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }

        alias = alias.Trim();

        await using BotDbContext context = await _dbContextFactory.CreateDbContextAsync();
        Alias? entity = await context.Aliases.FindAsync(alias);

        if (entity == null)
        {
            return false;
        }

        context.Aliases.Remove(entity);
        await SaveChangesAsync(context);

        _logger.LogInformation("Removed alias {Alias} from DB", alias);
        return true;
    }
}
