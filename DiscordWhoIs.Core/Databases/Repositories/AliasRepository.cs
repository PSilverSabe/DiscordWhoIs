using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DiscordWhoIs.Core.Databases.Repositories
{
    public class AliasRepository : IAliasRepository
    {
        private readonly IDbContextFactory<BotDbContext> _dbContextFactory;
        private readonly ILogger<AliasRepository> _logger;
        private readonly ConcurrentDictionary<string, Alias> _store = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _dbLock = new(1, 1);

        public AliasRepository(IDbContextFactory<BotDbContext> dbContextFactory, ILogger<AliasRepository> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;

            using var context = _dbContextFactory.CreateDbContext();
            try
            {
                context.Database.Migrate(); // Creates DB + Aliases table if missing

                // Load existing aliases
                foreach (var entry in context.Aliases.AsNoTracking())
                {
                    _store[entry.AliasUserName] = entry;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("DB ERROR PATH = " + context.Database.GetConnectionString());
                Console.WriteLine(ex);
            }
        }

        public Task<IReadOnlyList<Alias>> GetAllAsync()
        {
            return Task.FromResult((IReadOnlyList<Alias>)[.. _store.Values]);
        }

        public Task<bool> TryResolveAsync(string alias, out string real)
        {
            real = default!;
            if (string.IsNullOrWhiteSpace(alias))
            {
                return Task.FromResult(false);
            }

            var hasAlias = _store.TryGetValue(alias.Trim(), out var entry);
            if (hasAlias && entry != null)
            {
                real = entry.RealUserName;
            }


            return Task.FromResult(hasAlias);
        }

        public Task<bool> TryGetAsync(string alias, out Alias? entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(alias))
            {
                return Task.FromResult(false);
            }
            return Task.FromResult(_store.TryGetValue(alias.Trim(), out entry));
        }

        public async Task AddOrUpdateAsync(string alias, string real)
        {
            if (string.IsNullOrWhiteSpace(alias)) throw new ArgumentException("Alias cannot be empty.", nameof(alias));
            if (string.IsNullOrWhiteSpace(real)) throw new ArgumentException("RealUserName username cannot be empty.", nameof(real));

            alias = alias.Trim();
            real = real.Trim();

            await _dbLock.WaitAsync();
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();

                var existing = await context.Aliases.FindAsync(alias);
                if (existing != null)
                {
                    existing.RealUserName = real;
                    context.Aliases.Update(existing);
                }
                else
                {
                    var newEntry = new Alias(alias, real);
                    await context.Aliases.AddAsync(newEntry);
                }

                await context.SaveChangesAsync();

                _store[alias] = new Alias(alias, real);
                _logger.LogInformation("Added/updated alias {Alias} -> {RealUserName}", alias, real ?? "<none>");
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task<bool> RemoveAsync(string alias)
        {
            if (string.IsNullOrWhiteSpace(alias)) return false;
            alias = alias.Trim();

            await _dbLock.WaitAsync();
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();
                var entity = await context.Aliases.FindAsync(alias);
                if (entity != null)
                {
                    context.Aliases.Remove(entity);
                    await context.SaveChangesAsync();

                    _store.TryRemove(alias, out _);
                    _logger.LogInformation("Removed alias {Alias} from DB", alias);
                    return true;
                }

                return false;
            }
            finally
            {
                _dbLock.Release();
            }
        }
    }
}
