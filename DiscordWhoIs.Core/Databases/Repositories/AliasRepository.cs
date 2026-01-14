using DiscordWhoIs.Core.Databases.DbContexts;
using DiscordWhoIs.Core.Databases.DbModels;
using DiscordWhoIs.Core.Databases.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;

namespace DiscordWhoIs.Core.Databases.Repositories
{
    public class AliasRepository : RepositoryBase<BotDbContext, AliasRepository>, IAliasRepository
    {
        private readonly ConcurrentDictionary<string, Alias> _store = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _dbLock = new(1, 1);

        public AliasRepository(IDbContextFactory<BotDbContext> dbContextFactory, ILogger<AliasRepository> logger)
            : base(dbContextFactory, logger)
        {
            // Load existing aliases from DB into in-memory store
            using var context = _dbContextFactory.CreateDbContext();
            foreach (var entry in context.Aliases.Include(x => x.Author).AsNoTracking())
            {
                _store[entry.AliasUserName] = entry;
            }
            _logger.LogInformation("Loaded {Count} aliases into memory.", _store.Count);
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
                real = entry.Author.Ao3ProfileName;
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
                var authorEntity = await context.Authors.FirstOrDefaultAsync(a => a.Ao3ProfileName == real) 
                    ?? throw new InvalidOperationException($"No author found with AO3 profile name '{real}'. Cannot create alias.");

                if (existing != null)
                {
                    existing.AliasUserName = alias;
                    context.Aliases.Update(existing);
                }
                else
                {
                    var newEntry = new Alias(alias, authorEntity.AuthorId);
                    await context.Aliases.AddAsync(newEntry);
                }

                await SaveChangesAsync(context);

                _store[alias] = new Alias(alias, authorEntity.AuthorId);
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
                    await SaveChangesAsync(context);

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
