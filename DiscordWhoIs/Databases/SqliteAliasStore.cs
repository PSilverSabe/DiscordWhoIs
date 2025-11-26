using DiscordWhoIs.Databases.DbContexts;
using DiscordWhoIs.Databases.Interfaces;
using DiscordWhoIs.Databases.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace DiscordWhoIs.Databases
{
    public class SqliteAliasStore : IAliasStore
    {
        private readonly IDbContextFactory<AliasDbContext> _dbContextFactory;
        private readonly ILogger<SqliteAliasStore> _logger;
        private readonly ConcurrentDictionary<string, AliasEntry> _store = new(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _dbLock = new(1, 1);

        public SqliteAliasStore(IDbContextFactory<AliasDbContext> dbContextFactory, ILogger<SqliteAliasStore> logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;

            using var context = _dbContextFactory.CreateDbContext();
            context.Database.EnsureCreated(); // Creates DB + AliasEntries table if missing

            // Load existing aliases
            foreach (var entry in context.AliasEntries.AsNoTracking())
            {
                _store[entry.Alias] = entry;
            }
        }

        public IReadOnlyList<AliasEntry> GetAllAliases() => _store.Values.ToList();

        public bool TryResolve(string alias, out string real)
        {
            real = default!;
            if (string.IsNullOrWhiteSpace(alias)) return false;
            return _store.TryGetValue(alias.Trim(), out var entry) && (real = entry.Real) != null;
        }

        public bool TryGet(string alias, out AliasEntry? entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(alias)) return false;
            return _store.TryGetValue(alias.Trim(), out entry);
        }

        public async Task AddOrUpdateAsync(string alias, string real, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(alias)) throw new ArgumentException("Alias cannot be empty.", nameof(alias));
            if (string.IsNullOrWhiteSpace(real)) throw new ArgumentException("Real username cannot be empty.", nameof(real));

            alias = alias.Trim();
            real = real.Trim();
            description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            await _dbLock.WaitAsync();
            try
            {
                await using var context = await _dbContextFactory.CreateDbContextAsync();

                var existing = await context.AliasEntries.FindAsync(alias);
                if (existing != null)
                {
                    existing.Real = real;
                    existing.Description = description;
                    context.AliasEntries.Update(existing);
                }
                else
                {
                    var newEntry = new AliasEntry(alias, real, description);
                    await context.AliasEntries.AddAsync(newEntry);
                }

                await context.SaveChangesAsync();

                _store[alias] = new AliasEntry(alias, real, description);
                _logger.LogInformation("Added/updated alias {Alias} -> {Real} (desc: {Desc})", alias, real, description ?? "<none>");
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
                var entity = await context.AliasEntries.FindAsync(alias);
                if (entity != null)
                {
                    context.AliasEntries.Remove(entity);
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
